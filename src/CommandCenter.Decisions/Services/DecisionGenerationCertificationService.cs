using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Persistence;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class DecisionGenerationCertificationService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository,
    IDecisionProjectionService projectionService,
    IDecisionInfluenceService influenceService,
    IHumanAuthoringBurdenService humanAuthoringBurdenService) : IDecisionGenerationCertificationService
{
    public async Task<DecisionGenerationCertificationReport> GetCurrentCertificationAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await BuildReportAsync(repository);
    }

    public async Task<DecisionGenerationCertificationReport> RunCertificationAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionGenerationCertificationReport report = await BuildReportAsync(repository);
        return await decisionRepository.SaveGenerationCertificationReportAsync(repository, report);
    }

    public async Task<IReadOnlyList<DecisionGenerationCertificationReport>> ListReportsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await decisionRepository.ListGenerationCertificationReportsAsync(repository);
    }

    private async Task<DecisionGenerationCertificationReport> BuildReportAsync(Repository repository)
    {
        IReadOnlyList<DecisionCandidate> candidates = await decisionRepository.ListCandidatesAsync(repository);
        IReadOnlyList<DecisionProposal> proposals = await decisionRepository.ListProposalsAsync(repository);
        IReadOnlyList<Decision> decisions = await decisionRepository.ListDecisionsAsync(repository);
        IReadOnlyDictionary<string, IReadOnlyList<DecisionPackageVersion>> packageVersionsByProposal =
            await ListPackageVersionsAsync(repository, proposals);
        IReadOnlyList<DecisionPackageVersion> packageVersions =
            packageVersionsByProposal.SelectMany(pair => pair.Value).ToArray();
        IReadOnlyList<DecisionQualityAssessment> qualityAssessments =
            await decisionRepository.ListQualityAssessmentsAsync(repository);
        HumanAuthoringBurdenReport burdenReport =
            await humanAuthoringBurdenService.GenerateReportAsync(repository.Id);
        ExecutionDecisionProjection executionProjection =
            await projectionService.BuildExecutionProjectionAsync(repository.Id);

        Decision[] generatedResolvedDecisions = decisions
            .Where(IsGeneratedResolvedDecision)
            .OrderBy(decision => decision.Id.Value, StringComparer.Ordinal)
            .ToArray();
        var influenceTracesByDecision = new Dictionary<string, IReadOnlyList<DecisionInfluenceTrace>>(StringComparer.Ordinal);
        foreach (Decision decision in generatedResolvedDecisions)
        {
            influenceTracesByDecision[decision.Id.Value] =
                await influenceService.ListDecisionInfluenceAsync(repository.Id, decision.Id.Value);
        }

        var findings = new List<DecisionGenerationCertificationFinding>();
        AddFinding(
            findings,
            "GEN-001",
            "Generation",
            candidates.Any(candidate => candidate.Evidence.Count > 0 || candidate.Sources.Count > 0),
            "Candidates are evidence-backed.",
            $"Read {candidates.Count} candidate(s); at least one evidence-backed candidate is required.",
            candidates.Select(CandidateSource).ToArray(),
            [],
            candidates.Select(candidate => candidate.Id).ToArray(),
            []);
        AddFinding(
            findings,
            "GEN-002",
            "Generation",
            proposals.Any(proposal => proposal.Options.Count >= 2),
            "Generated proposals contain multiple options.",
            $"Read {proposals.Count} proposal(s); at least one generated proposal must contain two or more options.",
            proposals.Select(ProposalSource).ToArray(),
            [],
            proposals.Select(proposal => proposal.CandidateId).ToArray(),
            proposals.Select(proposal => proposal.Id).ToArray());
        AddFinding(
            findings,
            "GEN-003",
            "Generation",
            proposals.Any(proposal => proposal.Tradeoffs.Count >= proposal.Options.Count && proposal.Options.Count >= 2),
            "Generated proposals include tradeoff analysis for options.",
            "At least one generated proposal must provide tradeoff analysis for every generated option.",
            proposals.Select(ProposalSource).ToArray(),
            [],
            proposals.Select(proposal => proposal.CandidateId).ToArray(),
            proposals.Select(proposal => proposal.Id).ToArray());
        AddFinding(
            findings,
            "GEN-004",
            "Generation",
            proposals.Any(proposal => proposal.Recommendation is not null),
            "Generated proposals include a recommendation or explicit recommendation mode.",
            "At least one generated proposal must carry recommendation data for human review.",
            proposals.Select(ProposalSource).ToArray(),
            [],
            proposals.Select(proposal => proposal.CandidateId).ToArray(),
            proposals.Select(proposal => proposal.Id).ToArray());
        AddFinding(
            findings,
            "GEN-005",
            "Generation",
            packageVersions.Count > 0,
            "Generated package versions are persisted.",
            $"Read {packageVersions.Count} package version(s).",
            packageVersions.Select(PackageSource).ToArray(),
            [],
            packageVersions.Select(version => version.CandidateId).ToArray(),
            packageVersions.Select(version => version.ProposalId).ToArray());
        AddFinding(
            findings,
            "GOV-001",
            "Governance",
            generatedResolvedDecisions.All(decision =>
                decision.Resolution is { } resolution &&
                !IsSystemAuthority(resolution.ResolvedBy)),
            "Generated decisions reached authority only through human resolution.",
            $"Certified {generatedResolvedDecisions.Length} generated resolved decision(s).",
            generatedResolvedDecisions.Select(DecisionSource).ToArray(),
            generatedResolvedDecisions.Select(decision => decision.Id.Value).ToArray(),
            generatedResolvedDecisions.Select(decision => decision.Resolution!.SourceProposalSnapshot!.CandidateId).ToArray(),
            generatedResolvedDecisions.Select(decision => decision.Resolution!.SourceProposalSnapshot!.ProposalId).ToArray());
        AddFinding(
            findings,
            "THR-001",
            "Throughput",
            generatedResolvedDecisions.Length > 0,
            "Generated decisions reached resolution.",
            "At least one generated package must be resolved by a human before throughput replacement can be certified.",
            generatedResolvedDecisions.Select(DecisionSource).ToArray(),
            generatedResolvedDecisions.Select(decision => decision.Id.Value).ToArray(),
            generatedResolvedDecisions.Select(decision => decision.Resolution!.SourceProposalSnapshot!.CandidateId).ToArray(),
            generatedResolvedDecisions.Select(decision => decision.Resolution!.SourceProposalSnapshot!.ProposalId).ToArray());
        AddFinding(
            findings,
            "QLT-001",
            "Quality",
            generatedResolvedDecisions.All(decision =>
                qualityAssessments.Any(assessment => string.Equals(assessment.DecisionId, decision.Id.Value, StringComparison.Ordinal))) &&
            generatedResolvedDecisions.Length > 0,
            "Quality assessments exist for resolved generated decisions.",
            $"Read {qualityAssessments.Count} persisted quality assessment(s).",
            qualityAssessments.Select(AssessmentSource).ToArray(),
            qualityAssessments.Select(assessment => assessment.DecisionId).ToArray(),
            [],
            []);
        AddFinding(
            findings,
            "BUR-001",
            "WorkflowReplacement",
            burdenReport.FullRewriteCount == 0 &&
            burdenReport.GenerationBypassedCount == 0 &&
            (burdenReport.ReviewOnlyCount + burdenReport.MinorEditCount + burdenReport.MajorRefinementCount) > 0,
            "Human authoring burden remains review/refinement oriented.",
            $"Burden counts: review-only {burdenReport.ReviewOnlyCount}, minor edit {burdenReport.MinorEditCount}, major refinement {burdenReport.MajorRefinementCount}, full rewrite {burdenReport.FullRewriteCount}, bypassed {burdenReport.GenerationBypassedCount}.",
            burdenReport.Signals.SelectMany(signal => signal.Sources).ToArray(),
            burdenReport.Signals.Select(signal => signal.DecisionId).Distinct(StringComparer.Ordinal).ToArray(),
            [],
            []);
        AddFinding(
            findings,
            "CON-001",
            "Consumption",
            generatedResolvedDecisions.All(decision =>
                executionProjection.Constraints.Any(constraint => string.Equals(constraint.DecisionId, decision.Id.Value, StringComparison.Ordinal)) ||
                executionProjection.Directives.Any(directive => string.Equals(directive.DecisionId, decision.Id.Value, StringComparison.Ordinal)) ||
                executionProjection.Priorities.Any(priority => string.Equals(priority.DecisionId, decision.Id.Value, StringComparison.Ordinal)) ||
                executionProjection.ArchitectureRules.Any(rule => string.Equals(rule.DecisionId, decision.Id.Value, StringComparison.Ordinal))) &&
            generatedResolvedDecisions.Length > 0,
            "Accepted generated decisions project into execution consumption.",
            $"Execution projection returned {executionProjection.Constraints.Count} constraint(s), {executionProjection.Directives.Count} directive(s), {executionProjection.Priorities.Count} priority item(s), and {executionProjection.ArchitectureRules.Count} architecture rule(s).",
            [new DecisionSourceReference("ExecutionDecisionProjection", ".agents/decisions/projections")],
            generatedResolvedDecisions.Select(decision => decision.Id.Value).ToArray(),
            [],
            []);
        AddFinding(
            findings,
            "CON-002",
            "Consumption",
            influenceTracesByDecision.Count > 0 &&
            influenceTracesByDecision.All(pair => pair.Value.Any(trace =>
                trace.Statements.Any(statement => string.Equals(statement.DecisionId, pair.Key, StringComparison.Ordinal)))),
            "Execution influence is traceable for generated decisions.",
            $"Read {influenceTracesByDecision.Sum(pair => pair.Value.Count)} influence trace(s) for generated resolved decision(s).",
            influenceTracesByDecision.SelectMany(pair => pair.Value.Select(InfluenceSource)).ToArray(),
            influenceTracesByDecision.Keys.ToArray(),
            [],
            []);

        bool generationCertified = Passed(findings, "GEN-");
        bool governanceCertified = Passed(findings, "GOV-");
        bool throughputCertified = Passed(findings, "THR-");
        bool qualityCertified = Passed(findings, "QLT-");
        bool consumptionCertified = Passed(findings, "CON-");
        bool workflowReplacementCertified = Passed(findings, "BUR-");
        string[] failures = findings
            .Where(finding => !finding.Passed)
            .Select(finding => $"{finding.Id}: {finding.Summary}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        DecisionGenerationCertificationResult result = new(
            generationCertified,
            governanceCertified,
            throughputCertified,
            qualityCertified,
            consumptionCertified,
            workflowReplacementCertified,
            findings.OrderBy(finding => finding.Id, StringComparer.Ordinal).ToArray(),
            failures);

        return new DecisionGenerationCertificationReport(
            $"generation-certification.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfffffff}",
            repository.Id,
            DateTimeOffset.UtcNow,
            FingerprintInputs(candidates, proposals, decisions, packageVersions, qualityAssessments, burdenReport, executionProjection, influenceTracesByDecision),
            result,
            candidates.Count,
            proposals.Count,
            packageVersions.Count,
            generatedResolvedDecisions.Length,
            influenceTracesByDecision.Sum(pair => pair.Value.Count),
            burdenReport,
            qualityAssessments.OrderBy(assessment => assessment.Id, StringComparer.Ordinal).ToArray(),
            result.Certified
                ? ["Automated decision generation certification passed without mutating decision authority."]
                : ["Automated decision generation certification is advisory and does not mutate lifecycle authority."]);
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<DecisionPackageVersion>>> ListPackageVersionsAsync(
        Repository repository,
        IReadOnlyList<DecisionProposal> proposals)
    {
        var versions = new Dictionary<string, IReadOnlyList<DecisionPackageVersion>>(StringComparer.Ordinal);
        foreach (DecisionProposal proposal in proposals)
        {
            versions[proposal.Id] = await decisionRepository.ListPackageVersionsAsync(repository, proposal.Id);
        }

        return versions;
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static bool IsGeneratedResolvedDecision(Decision decision)
    {
        return decision.State == DecisionState.Resolved &&
            decision.Resolution?.Outcome == DecisionOutcome.Accepted &&
            decision.Resolution.SourceProposalSnapshot?.PackageId is not null;
    }

    private static bool IsSystemAuthority(string value)
    {
        return string.Equals(value, "system", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "governance", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "execution", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "certification", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Passed(IReadOnlyList<DecisionGenerationCertificationFinding> findings, string idPrefix)
    {
        return findings
            .Where(finding => finding.Id.StartsWith(idPrefix, StringComparison.Ordinal))
            .All(finding => finding.Passed);
    }

    private static void AddFinding(
        List<DecisionGenerationCertificationFinding> findings,
        string id,
        string category,
        bool passed,
        string summary,
        string detail,
        IReadOnlyList<DecisionSourceReference> sources,
        IReadOnlyList<string> relatedDecisionIds,
        IReadOnlyList<string> relatedCandidateIds,
        IReadOnlyList<string> relatedProposalIds)
    {
        findings.Add(new DecisionGenerationCertificationFinding(
            id,
            category,
            passed,
            summary,
            detail,
            sources,
            relatedDecisionIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            relatedCandidateIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            relatedProposalIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()));
    }

    private static string FingerprintInputs(
        IReadOnlyList<DecisionCandidate> candidates,
        IReadOnlyList<DecisionProposal> proposals,
        IReadOnlyList<Decision> decisions,
        IReadOnlyList<DecisionPackageVersion> packages,
        IReadOnlyList<DecisionQualityAssessment> qualityAssessments,
        HumanAuthoringBurdenReport burdenReport,
        ExecutionDecisionProjection executionProjection,
        IReadOnlyDictionary<string, IReadOnlyList<DecisionInfluenceTrace>> influenceTracesByDecision)
    {
        object input = new
        {
            Candidates = candidates.OrderBy(candidate => candidate.Id, StringComparer.Ordinal),
            Proposals = proposals.OrderBy(proposal => proposal.Id, StringComparer.Ordinal),
            Decisions = decisions.OrderBy(decision => decision.Id.Value, StringComparer.Ordinal),
            Packages = packages.OrderBy(package => package.Id, StringComparer.Ordinal),
            QualityAssessments = qualityAssessments.OrderBy(assessment => assessment.Id, StringComparer.Ordinal),
            BurdenReport = burdenReport,
            ExecutionProjection = executionProjection,
            InfluenceTraces = influenceTracesByDecision
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new
                {
                    DecisionId = pair.Key,
                    Traces = pair.Value.OrderBy(trace => trace.Id, StringComparer.Ordinal)
                })
        };
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input, DecisionJson.Options));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static DecisionSourceReference DecisionSource(Decision decision)
    {
        return new DecisionSourceReference(
            "DecisionRecord",
            $".agents/decisions/records/{decision.Id.Value}/decision.json",
            DecisionId: decision.Id);
    }

    private static DecisionSourceReference CandidateSource(DecisionCandidate candidate)
    {
        return new DecisionSourceReference(
            "DecisionCandidate",
            $".agents/decisions/candidates/{candidate.Id}/candidate.json",
            CandidateId: candidate.Id);
    }

    private static DecisionSourceReference ProposalSource(DecisionProposal proposal)
    {
        return new DecisionSourceReference(
            "DecisionProposal",
            $".agents/decisions/proposals/{proposal.Id}/proposal.json",
            ProposalId: proposal.Id,
            CandidateId: proposal.CandidateId);
    }

    private static DecisionSourceReference PackageSource(DecisionPackageVersion packageVersion)
    {
        return new DecisionSourceReference(
            "DecisionPackageVersion",
            $".agents/decisions/proposals/{packageVersion.ProposalId}/versions/{packageVersion.Id}.json",
            ProposalId: packageVersion.ProposalId,
            CandidateId: packageVersion.CandidateId);
    }

    private static DecisionSourceReference AssessmentSource(DecisionQualityAssessment assessment)
    {
        return new DecisionSourceReference(
            "DecisionQualityAssessment",
            $".agents/decisions/quality/assessments/{assessment.Id}.json",
            DecisionId: new DecisionId(assessment.DecisionId));
    }

    private static DecisionSourceReference InfluenceSource(DecisionInfluenceTrace trace)
    {
        return new DecisionSourceReference(
            "DecisionInfluenceTrace",
            $".agents/decisions/influence/{trace.Id}.json");
    }
}
