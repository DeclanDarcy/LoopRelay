using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Primitives;
using LoopRelay.Decisions.Services;

namespace LoopRelay.Decisions.Tests;

[Collection("ProcessEnvironment")]
public sealed class DecisionRefinementServiceTests
{
    [Fact]
    public async Task RefinementAnalysisBuildsDirectivePlanWithoutMutatingProposal()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        RefinementAnalysisService analysisService = CreateAnalysisService(repository, decisionRepository);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        await generationService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        DecisionProposal needsRefinement = await generationService.MarkProposalNeedsRefinementAsync(
            repository.Id,
            proposal.Id,
            "Reviewer wants scoped regeneration.");
        string proposalJsonBefore = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/proposal.json");

        RefinementPlan plan = await analysisService.AnalyzeRefinementAsync(
            repository.Id,
            proposal.Id,
            new DecisionRefinementAnalysisRequest(
                "Must preserve package authority, explore another option, reevaluate risk, cost, and recommendation.",
                "reviewer",
                Fingerprint(needsRefinement)));

        string proposalJsonAfter = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/proposal.json");
        DecisionProposal? reloaded = await decisionRepository.GetProposalAsync(repository, proposal.Id);

        Assert.Equal(repository.Id, plan.RepositoryId);
        Assert.Equal(proposal.Id, plan.ProposalId);
        Assert.Equal(Fingerprint(needsRefinement), plan.BaseProposalFingerprint);
        Assert.Contains(plan.Directives, directive => directive.Type == RefinementDirectiveType.AddConstraint);
        Assert.Contains(plan.Directives, directive => directive.Type == RefinementDirectiveType.ExploreAlternative);
        Assert.Contains(plan.Directives, directive => directive.Type == RefinementDirectiveType.ReevaluateRisk);
        Assert.Contains(plan.Directives, directive => directive.Type == RefinementDirectiveType.ReevaluateCost);
        Assert.Contains(plan.Directives, directive => directive.Type == RefinementDirectiveType.ReevaluateRecommendation);
        Assert.True(plan.RegenerateOptions);
        Assert.True(plan.ReevaluateTradeoffs);
        Assert.True(plan.ReevaluateRecommendation);
        Assert.False(plan.FullRegeneration);
        Assert.NotEmpty(plan.AppliedConstraints);
        Assert.Contains(plan.Diagnostics, diagnostic => diagnostic.Contains("does not mutate", StringComparison.Ordinal));
        Assert.Equal(proposalJsonBefore, proposalJsonAfter);
        Assert.Equal(DecisionProposalState.NeedsRefinement, reloaded?.State);
        Assert.Empty(await decisionRepository.ListProposalRevisionsAsync(repository, proposal.Id));
    }

    [Fact]
    public async Task RefinementAnalysisRejectsStaleBaseProposalFingerprint()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        RefinementAnalysisService analysisService = CreateAnalysisService(repository, decisionRepository);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        await generationService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        await generationService.MarkProposalNeedsRefinementAsync(repository.Id, proposal.Id, "Needs analysis.");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            analysisService.AnalyzeRefinementAsync(
                repository.Id,
                proposal.Id,
                new DecisionRefinementAnalysisRequest("Reevaluate the recommendation.", BaseProposalFingerprint: "stale")));

        Assert.Equal("Refinement base proposal fingerprint is stale.", exception.Message);
        Assert.Empty(await decisionRepository.ListProposalRevisionsAsync(repository, proposal.Id));
    }

    [Fact]
    public async Task ScopedPackageRegenerationCreatesImmutablePackageVersionAndComparison()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        RefinementAnalysisService analysisService = CreateAnalysisService(repository, decisionRepository);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        var packageService = new DecisionPackageService(decisionRepository, projectionService);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        DecisionPackageVersion basePackage = Assert.Single(await decisionRepository.ListPackageVersionsAsync(repository, proposal.Id));
        string basePackageJsonBefore = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/versions/PKG-0001.json");
        await generationService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        DecisionProposal needsRefinement = await generationService.MarkProposalNeedsRefinementAsync(
            repository.Id,
            proposal.Id,
            "Reviewer wants scoped package regeneration.");
        RefinementPlan plan = await analysisService.AnalyzeRefinementAsync(
            repository.Id,
            proposal.Id,
            new DecisionRefinementAnalysisRequest(
                "Must preserve package authority, explore another option, reevaluate risk, cost, and recommendation.",
                "reviewer",
                Fingerprint(needsRefinement)));

        DecisionPackageRegenerationResult result = await packageService.RegeneratePackageAsync(
            repository,
            needsRefinement,
            basePackage,
            new DecisionPackageRegenerationRequest(plan, basePackage.Id, basePackage.PackageFingerprint, "reviewer"),
            DateTimeOffset.UtcNow);

        IReadOnlyList<DecisionPackageVersion> versions = await decisionRepository.ListPackageVersionsAsync(repository, proposal.Id);
        DecisionProposal? reloaded = await decisionRepository.GetProposalAsync(repository, proposal.Id);
        string basePackageJsonAfter = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/versions/PKG-0001.json");
        string comparisonMarkdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/versions/PKG-0001..PKG-0002.comparison.md");
        string refinementMarkdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/refinements/REF-0001.md");
        DecisionRefinementArtifact refinementArtifact = Assert.Single(await decisionRepository.ListRefinementArtifactsAsync(repository, proposal.Id));

        Assert.Equal("PKG-0001", result.BasePackageVersion.Id);
        Assert.Equal("PKG-0002", result.RegeneratedPackageVersion.Id);
        Assert.NotNull(result.RefinementArtifact);
        Assert.Equal("REF-0001", result.RefinementArtifact.Id);
        Assert.Equal("REF-0001", refinementArtifact.Id);
        Assert.Equal(plan.RepositoryId, refinementArtifact.Plan.RepositoryId);
        Assert.Equal(plan.ProposalId, refinementArtifact.Plan.ProposalId);
        Assert.Equal(plan.BaseProposalFingerprint, refinementArtifact.Plan.BaseProposalFingerprint);
        Assert.Equal(plan.RegenerateOptions, refinementArtifact.Plan.RegenerateOptions);
        Assert.Equal(plan.ReevaluateTradeoffs, refinementArtifact.Plan.ReevaluateTradeoffs);
        Assert.Equal(plan.ReevaluateRecommendation, refinementArtifact.Plan.ReevaluateRecommendation);
        Assert.Equal(plan.FullRegeneration, refinementArtifact.Plan.FullRegeneration);
        Assert.Equal(plan.Directives.Count, refinementArtifact.Directives.Count);
        Assert.Equal(plan.AppliedConstraints, refinementArtifact.Plan.AppliedConstraints);
        Assert.Equal(basePackage.Id, refinementArtifact.Request.BasePackageId);
        Assert.Equal(basePackage.PackageFingerprint, refinementArtifact.Request.BasePackageFingerprint);
        Assert.Equal("PKG-0002", refinementArtifact.RegeneratedPackageId);
        Assert.Equal(HumanAuthoringBurden.MajorRefinement, refinementArtifact.HumanAuthoringBurden);
        Assert.Equal(HumanAuthoringBurden.MajorRefinement, result.HumanAuthoringBurden);
        Assert.Equal(2, versions.Count);
        Assert.Equal(basePackageJsonBefore, basePackageJsonAfter);
        Assert.Equal(DecisionProposalState.NeedsRefinement, reloaded?.State);
        Assert.Empty(await decisionRepository.ListProposalRevisionsAsync(repository, proposal.Id));
        Assert.True(result.Comparison.OptionsChanged);
        Assert.True(result.Comparison.RecommendationChanged);
        Assert.Contains(result.RegeneratedPackageVersion.Package.Options, option => option.Id == "option-4");
        Assert.Equal(Fingerprint(needsRefinement), result.RegeneratedPackageVersion.Package.Metadata.SourceProposalFingerprint);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("prior package versions remain immutable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("Human authoring burden classified as MajorRefinement", StringComparison.Ordinal));
        Assert.Contains("Recommendation changed: True", comparisonMarkdown);
        Assert.Contains("PKG-0001..PKG-0002", comparisonMarkdown);
        Assert.Contains("REF-0001: PROP-0001 Refinement", refinementMarkdown);
        Assert.Contains("Human authoring burden: MajorRefinement", refinementMarkdown);
        Assert.Contains("## Directives", refinementMarkdown);
        Assert.Contains("## Plan", refinementMarkdown);
        Assert.Contains("## Comparison", refinementMarkdown);
        Assert.Contains("Regenerated package: PKG-0002", refinementMarkdown);
    }

    [Fact]
    public async Task VersionHistoryComparisonAndRefinementArtifactsPersistAfterRestart()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        RefinementAnalysisService analysisService = CreateAnalysisService(repository, decisionRepository);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        var packageService = new DecisionPackageService(decisionRepository, projectionService);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        DecisionPackageVersion basePackage = Assert.Single(await decisionRepository.ListPackageVersionsAsync(repository, proposal.Id));
        await generationService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        DecisionProposal needsRefinement = await generationService.MarkProposalNeedsRefinementAsync(
            repository.Id,
            proposal.Id,
            "Restart should preserve refinement trace.");
        RefinementPlan plan = await analysisService.AnalyzeRefinementAsync(
            repository.Id,
            proposal.Id,
            new DecisionRefinementAnalysisRequest(
                "Explore another option and reevaluate risk, cost, and recommendation.",
                "reviewer",
                Fingerprint(needsRefinement)));
        DecisionPackageRegenerationResult result = await packageService.RegeneratePackageAsync(
            repository,
            needsRefinement,
            basePackage,
            new DecisionPackageRegenerationRequest(plan, basePackage.Id, basePackage.PackageFingerprint, "reviewer"),
            DateTimeOffset.UtcNow);

        var restartedRepository = new FileSystemDecisionRepository(store);
        IReadOnlyList<DecisionPackageVersion> restartedVersions =
            await restartedRepository.ListPackageVersionsAsync(repository, proposal.Id);
        DecisionRefinementArtifact restartedRefinement =
            Assert.Single(await restartedRepository.ListRefinementArtifactsAsync(repository, proposal.Id));
        string refinementMarkdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/refinements/REF-0001.md");
        string comparisonMarkdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/versions/PKG-0001..PKG-0002.comparison.md");

        Assert.Equal(["PKG-0001", "PKG-0002"], restartedVersions.Select(version => version.Id).ToArray());
        Assert.NotNull(result.RefinementArtifact);
        Assert.Equal(result.RefinementArtifact.Id, restartedRefinement.Id);
        Assert.Equal(result.RefinementArtifact.RepositoryId, restartedRefinement.RepositoryId);
        Assert.Equal(result.RefinementArtifact.ProposalId, restartedRefinement.ProposalId);
        Assert.Equal(result.RefinementArtifact.BasePackageFingerprint, restartedRefinement.BasePackageFingerprint);
        Assert.Equal(result.RefinementArtifact.RegeneratedPackageFingerprint, restartedRefinement.RegeneratedPackageFingerprint);
        Assert.Equal(result.RefinementArtifact.HumanAuthoringBurden, restartedRefinement.HumanAuthoringBurden);
        Assert.Equal(plan.Directives.Select(directive => directive.Type), restartedRefinement.Directives.Select(directive => directive.Type));
        Assert.Equal("PKG-0001", restartedRefinement.BasePackageId);
        Assert.Equal("PKG-0002", restartedRefinement.RegeneratedPackageId);
        Assert.True(restartedRefinement.Comparison.OptionsChanged);
        Assert.True(restartedRefinement.Comparison.RecommendationChanged);
        Assert.Contains(restartedRefinement.Diagnostics, diagnostic =>
            diagnostic.Contains("using analyzed refinement plan", StringComparison.Ordinal));
        Assert.Contains("## Request", refinementMarkdown);
        Assert.Contains("## Directives", refinementMarkdown);
        Assert.Contains("## Plan", refinementMarkdown);
        Assert.Contains("## Comparison", refinementMarkdown);
        Assert.Contains("Human authoring burden: MajorRefinement", refinementMarkdown);
        Assert.Contains("Recommendation changed: True", comparisonMarkdown);
    }

    [Fact]
    public async Task ScopedPackageRegenerationRejectsStalePackageFingerprint()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        RefinementAnalysisService analysisService = CreateAnalysisService(repository, decisionRepository);
        var packageService = new DecisionPackageService(
            decisionRepository,
            new DecisionArtifactProjectionService(decisionRepository, store));
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        DecisionPackageVersion basePackage = Assert.Single(await decisionRepository.ListPackageVersionsAsync(repository, proposal.Id));
        await generationService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        DecisionProposal needsRefinement = await generationService.MarkProposalNeedsRefinementAsync(
            repository.Id,
            proposal.Id,
            "Reviewer wants scoped package regeneration.");
        RefinementPlan plan = await analysisService.AnalyzeRefinementAsync(
            repository.Id,
            proposal.Id,
            new DecisionRefinementAnalysisRequest(
                "Explore another option and reevaluate recommendation.",
                BaseProposalFingerprint: Fingerprint(needsRefinement)));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            packageService.RegeneratePackageAsync(
                repository,
                needsRefinement,
                basePackage,
                new DecisionPackageRegenerationRequest(plan, basePackage.Id, "stale-package-fingerprint"),
                DateTimeOffset.UtcNow));

        Assert.Equal("Package regeneration base package fingerprint is stale.", exception.Message);
        DecisionPackageVersion onlyPackage = Assert.Single(await decisionRepository.ListPackageVersionsAsync(repository, proposal.Id));
        Assert.Equal("PKG-0001", onlyPackage.Id);
    }

    [Fact]
    public async Task ConstraintDirectiveAffectsRecommendation()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        RefinementAnalysisService analysisService = CreateAnalysisService(repository, decisionRepository);
        var packageService = new DecisionPackageService(
            decisionRepository,
            new DecisionArtifactProjectionService(decisionRepository, store));
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        DecisionPackageVersion basePackage = Assert.Single(await decisionRepository.ListPackageVersionsAsync(repository, proposal.Id));
        await generationService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        DecisionProposal needsRefinement = await generationService.MarkProposalNeedsRefinementAsync(
            repository.Id,
            proposal.Id,
            "Constraint should influence generated recommendation.");
        RefinementPlan plan = await analysisService.AnalyzeRefinementAsync(
            repository.Id,
            proposal.Id,
            new DecisionRefinementAnalysisRequest(
                "Must preserve compatibility and must not replace the contested architecture; reevaluate recommendation.",
                "reviewer",
                Fingerprint(needsRefinement)));

        DecisionPackageRegenerationResult result = await packageService.RegeneratePackageAsync(
            repository,
            needsRefinement,
            basePackage,
            new DecisionPackageRegenerationRequest(plan, basePackage.Id, basePackage.PackageFingerprint, "reviewer"),
            DateTimeOffset.UtcNow);

        DecisionTradeoffComparison replaceComparison = result.RegeneratedPackageVersion.Package.TradeoffComparisons
            .Single(comparison => comparison.OptionId == "option-3");
        OptionEvaluation replaceEvaluation = result.RegeneratedPackageVersion.Package.Recommendation!.OptionEvaluations
            .Single(evaluation => evaluation.OptionId == "option-3");

        Assert.True(plan.ReevaluateTradeoffs);
        Assert.Contains(plan.AppliedConstraints, constraint => constraint.Contains("must not replace", StringComparison.OrdinalIgnoreCase));
        Assert.True(result.Comparison.RecommendationChanged);
        Assert.Contains(replaceComparison.DisqualifyingConstraints, constraint =>
            constraint.Contains("Constraint may be violated", StringComparison.Ordinal));
        Assert.Contains(replaceEvaluation.Constraints, constraint =>
            constraint.Contains("must not replace", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PriorityDirectiveChangesOptionEvaluation()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        RefinementAnalysisService analysisService = CreateAnalysisService(repository, decisionRepository);
        var packageService = new DecisionPackageService(
            decisionRepository,
            new DecisionArtifactProjectionService(decisionRepository, store));
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        DecisionPackageVersion basePackage = Assert.Single(await decisionRepository.ListPackageVersionsAsync(repository, proposal.Id));
        OptionEvaluation baseEvaluation = basePackage.Package.Recommendation!.OptionEvaluations
            .Single(evaluation => evaluation.OptionId == "option-1");
        await generationService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        DecisionProposal needsRefinement = await generationService.MarkProposalNeedsRefinementAsync(
            repository.Id,
            proposal.Id,
            "Priority should influence option evaluation.");
        RefinementPlan plan = await analysisService.AnalyzeRefinementAsync(
            repository.Id,
            proposal.Id,
            new DecisionRefinementAnalysisRequest(
                "Increase priority because execution is blocking; reevaluate recommendation.",
                "reviewer",
                Fingerprint(needsRefinement)));

        DecisionPackageRegenerationResult result = await packageService.RegeneratePackageAsync(
            repository,
            needsRefinement,
            basePackage,
            new DecisionPackageRegenerationRequest(plan, basePackage.Id, basePackage.PackageFingerprint, "reviewer"),
            DateTimeOffset.UtcNow);

        OptionEvaluation regeneratedEvaluation = result.RegeneratedPackageVersion.Package.Recommendation!.OptionEvaluations
            .Single(evaluation => evaluation.OptionId == "option-1");

        Assert.True(plan.ReevaluateTradeoffs);
        Assert.Contains(plan.Directives, directive => directive.Type == RefinementDirectiveType.IncreasePriority);
        Assert.NotEqual(baseEvaluation.Score, regeneratedEvaluation.Score);
        Assert.Contains("priority adjustment", regeneratedEvaluation.ScoreExplanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(regeneratedEvaluation.Strengths, strength =>
            strength.Contains("Priority directive favors timely progress", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RiskDirectiveUpdatesTradeoffAnalysis()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        RefinementAnalysisService analysisService = CreateAnalysisService(repository, decisionRepository);
        var packageService = new DecisionPackageService(
            decisionRepository,
            new DecisionArtifactProjectionService(decisionRepository, store));
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        DecisionPackageVersion basePackage = Assert.Single(await decisionRepository.ListPackageVersionsAsync(repository, proposal.Id));
        await generationService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        DecisionProposal needsRefinement = await generationService.MarkProposalNeedsRefinementAsync(
            repository.Id,
            proposal.Id,
            "Risk should influence tradeoff analysis.");
        RefinementPlan plan = await analysisService.AnalyzeRefinementAsync(
            repository.Id,
            proposal.Id,
            new DecisionRefinementAnalysisRequest(
                "Reevaluate risk: unsafe migration failure mode requires explicit review.",
                "reviewer",
                Fingerprint(needsRefinement)));

        DecisionPackageRegenerationResult result = await packageService.RegeneratePackageAsync(
            repository,
            needsRefinement,
            basePackage,
            new DecisionPackageRegenerationRequest(plan, basePackage.Id, basePackage.PackageFingerprint, "reviewer"),
            DateTimeOffset.UtcNow);

        DecisionRisk[] risks = result.RegeneratedPackageVersion.Package.AnalyzedOptions
            .SelectMany(option => option.Risks)
            .ToArray();

        Assert.True(plan.ReevaluateTradeoffs);
        Assert.True(result.Comparison.RisksChanged);
        Assert.Contains(risks, risk =>
            risk.Statement.Contains("Context risk remains relevant", StringComparison.Ordinal) &&
            risk.Statement.Contains("unsafe migration failure mode", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.RegeneratedPackageVersion.Package.TradeoffAnalysisDiagnostics!.Diagnostics, diagnostic =>
            diagnostic.Contains("Tradeoff analysis regenerated", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GoalClarificationTriggersFullRegeneration()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        RefinementAnalysisService analysisService = CreateAnalysisService(repository, decisionRepository);
        var packageService = new DecisionPackageService(
            decisionRepository,
            new DecisionArtifactProjectionService(decisionRepository, store));
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        DecisionPackageVersion basePackage = Assert.Single(await decisionRepository.ListPackageVersionsAsync(repository, proposal.Id));
        await generationService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        DecisionProposal needsRefinement = await generationService.MarkProposalNeedsRefinementAsync(
            repository.Id,
            proposal.Id,
            "Goal clarification should drive full regeneration.");
        RefinementPlan plan = await analysisService.AnalyzeRefinementAsync(
            repository.Id,
            proposal.Id,
            new DecisionRefinementAnalysisRequest(
                "Clarify the goal and scope around minimal repository persistence before recommendation.",
                "reviewer",
                Fingerprint(needsRefinement)));

        DecisionPackageRegenerationResult result = await packageService.RegeneratePackageAsync(
            repository,
            needsRefinement,
            basePackage,
            new DecisionPackageRegenerationRequest(plan, basePackage.Id, basePackage.PackageFingerprint, "reviewer"),
            DateTimeOffset.UtcNow);

        Assert.True(plan.FullRegeneration);
        Assert.True(plan.RegenerateOptions);
        Assert.True(result.Comparison.ContextFingerprintChanged);
        Assert.Contains(result.RegeneratedPackageVersion.Package.Options, option => option.Id == "option-4");
        Assert.Contains(result.RegeneratedPackageVersion.Package.GenerationDiagnostics!.Diagnostics, diagnostic =>
            diagnostic.Contains("Full regeneration: True", StringComparison.Ordinal));
        Assert.Equal(HumanAuthoringBurden.MajorRefinement, result.HumanAuthoringBurden);
    }

    [Fact]
    public async Task RefinementRejectsStaleBaseProposalFingerprint()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        DecisionRefinementService refinementService = CreateRefinementService(repository, store, decisionRepository);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        await generationService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        DecisionProposal needsRefinement = await generationService.MarkProposalNeedsRefinementAsync(repository.Id, proposal.Id, "Needs sharper rationale.");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            refinementService.RefineProposalAsync(
                repository.Id,
                proposal.Id,
                new DecisionRefinementRequest(
                    "Apply stale edit.",
                    Context: "Refined context.",
                    BaseProposalFingerprint: Fingerprint(proposal))));

        Assert.Equal("Refinement base proposal fingerprint is stale.", exception.Message);
        DecisionProposal? reloaded = await decisionRepository.GetProposalAsync(repository, proposal.Id);
        Assert.Equal(DecisionProposalState.NeedsRefinement, reloaded?.State);
        Assert.Equal(Fingerprint(needsRefinement), Fingerprint(reloaded!));
        Assert.Empty(await refinementService.ListProposalRevisionsAsync(repository.Id, proposal.Id));
    }

    [Fact]
    public async Task RefinementPreservesRetiredOptionsAssumptionsAttributionAndDiagnostics()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            signalKind: "Conflict",
            summary: "Conflict between backend API approaches.");
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        DecisionRefinementService refinementService = CreateRefinementService(repository, store, decisionRepository);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        await generationService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        DecisionProposal needsRefinement = await generationService.MarkProposalNeedsRefinementAsync(repository.Id, proposal.Id, "Needs narrower alternatives.");
        DecisionOption retainedOption = needsRefinement.Options.Single(option => option.Id == "option-1") with
        {
            Description = "Adopt the narrower repository-backed direction."
        };
        DecisionRecommendation recommendation = needsRefinement.Recommendation! with
        {
            Rationale = "Narrower rationale after reviewer challenge."
        };

        DecisionProposal refined = await refinementService.RefineProposalAsync(
            repository.Id,
            proposal.Id,
            new DecisionRefinementRequest(
                "Remove weak alternative and document constraint.",
                Options: [retainedOption],
                Assumptions: [],
                Recommendation: recommendation,
                RequestedBy: "reviewer",
                BaseProposalFingerprint: Fingerprint(needsRefinement),
                Constraints:
                [
                    new DecisionConstraint(
                        "constraint-1",
                        "Refinement must preserve repository artifact authority.",
                        needsRefinement.Evidence)
                ],
                OptionRevisions:
                [
                    new DecisionOptionRevision(
                        "option-2",
                        "Removed",
                        "Alternative was not supported strongly enough.",
                        needsRefinement.Options.Single(option => option.Id == "option-2"),
                        null)
                ],
                AssumptionRevisions:
                [
                    new DecisionAssumptionRevision(
                        "assumption-1",
                        "Retired",
                        "Reviewer challenged the source freshness assumption.",
                        PreviousStatement: needsRefinement.Assumptions.Single().Statement)
                ],
                RejectedChanges: ["Do not resolve the proposal during refinement."]));

        DecisionProposalRevision revision = Assert.Single(await refinementService.ListProposalRevisionsAsync(repository.Id, proposal.Id));
        Assert.Equal(DecisionProposalState.Refined, refined.State);
        Assert.Equal("reviewer", revision.RequestedBy);
        Assert.Contains("Options", revision.ChangedFields);
        Assert.Contains("Assumptions", revision.ChangedFields);
        Assert.Contains("Recommendation", revision.ChangedFields);
        Assert.Contains("Constraints", revision.ChangedFields);
        Assert.Contains("OptionRevisions", revision.ChangedFields);
        Assert.Contains("AssumptionRevisions", revision.ChangedFields);
        Assert.Contains("RejectedChanges", revision.ChangedFields);
        Assert.Contains(revision.RetiredOptions ?? [], option => option.Id == "option-2");
        Assert.Contains(revision.RetiredAssumptions ?? [], assumption => assumption.Id == "assumption-1");
        Assert.Contains(revision.Constraints ?? [], constraint => constraint.Id == "constraint-1");
        Assert.Contains(revision.RejectedChanges ?? [], change => change == "Do not resolve the proposal during refinement.");
        Assert.Contains(revision.Diagnostics ?? [], diagnostic => diagnostic.Contains("Retired 2 option", StringComparison.Ordinal));
        Assert.Equal("Narrower rationale after reviewer challenge.", revision.RevisedRecommendationRationale);
        Assert.Equal(HumanAuthoringBurden.FullRewrite, revision.HumanAuthoringBurden);

        string markdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/revisions/REV-0001.md");
        Assert.Contains("Requested by: reviewer", markdown);
        Assert.Contains("Human authoring burden: FullRewrite", markdown);
        Assert.Contains("## Retired Options", markdown);
        Assert.Contains("option-2", markdown);
        Assert.Contains("constraint-1: Refinement must preserve repository artifact authority.", markdown);
        Assert.Contains("Do not resolve the proposal during refinement.", markdown);
    }

    [Fact]
    public async Task RevisionComparisonCapturesTradeoffExpansionAndChainIntegrity()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(
            repository.Id,
            DecisionCandidateState.Promoted,
            signalKind: "Conflict",
            summary: "Conflict between provider bridge approaches.");
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        DecisionRefinementService refinementService = CreateRefinementService(repository, store, decisionRepository);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        await generationService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        DecisionProposal needsRefinement = await generationService.MarkProposalNeedsRefinementAsync(repository.Id, proposal.Id, "Needs expanded tradeoffs.");
        DecisionTradeoff previousTradeoff = needsRefinement.Tradeoffs.Single(tradeoff => tradeoff.OptionId == "option-1");
        DecisionTradeoff revisedTradeoff = previousTradeoff with
        {
            Benefit = "Creates explicit bridge direction and preserves repository authority.",
            Cost = "Requires one more certification pass before UI controls are exposed."
        };
        DecisionTradeoff addedTradeoff = new(
            "option-1",
            "Keeps refinement review evidence adjacent to the recommendation.",
            "Adds comparison projection maintenance.",
            needsRefinement.Evidence);

        DecisionProposal refined = await refinementService.RefineProposalAsync(
            repository.Id,
            proposal.Id,
            new DecisionRefinementRequest(
                "Expand tradeoffs before resolution.",
                Context: "Refined context with expanded tradeoff review.",
                Tradeoffs: [revisedTradeoff, addedTradeoff, .. needsRefinement.Tradeoffs.Where(tradeoff => tradeoff.OptionId != "option-1")],
                BaseProposalFingerprint: Fingerprint(needsRefinement),
                TradeoffRevisions:
                [
                    new DecisionTradeoffRevision(
                        "option-1",
                        "Expanded",
                        "Reviewer requested fuller benefit and cost traceability.",
                        previousTradeoff,
                        revisedTradeoff),
                    new DecisionTradeoffRevision(
                        "option-1",
                        "Added",
                        "Reviewer requested an additional comparison-maintenance cost.",
                        null,
                        addedTradeoff)
                ]));

        DecisionProposalRevision revision = Assert.Single(await refinementService.ListProposalRevisionsAsync(repository.Id, proposal.Id));
        DecisionProposalRevisionComparison comparison = await refinementService.GetProposalRevisionComparisonAsync(
            repository.Id,
            proposal.Id,
            revision.Id);

        Assert.Equal(DecisionProposalState.Refined, refined.State);
        Assert.Equal(Fingerprint(needsRefinement), comparison.SourceProposalFingerprint);
        Assert.False(comparison.SourceMatchesCurrentProposal);
        Assert.Equal(Fingerprint(refined), comparison.CurrentProposalFingerprint);
        Assert.Contains("Tradeoffs", comparison.ChangedFields);
        Assert.Contains("Context", comparison.ChangedFields);
        Assert.Equal(HumanAuthoringBurden.FullRewrite, comparison.HumanAuthoringBurden);
        Assert.Contains(comparison.FieldComparisons, field =>
            field.Field == "Tradeoffs" &&
            field.ChangeType == "Changed" &&
            field.PreviousValue!.Contains(previousTradeoff.Benefit, StringComparison.Ordinal) &&
            field.RevisedValue!.Contains(addedTradeoff.Benefit, StringComparison.Ordinal));
        Assert.Contains(comparison.TradeoffRevisions, tradeoffRevision => tradeoffRevision.ChangeType == "Expanded");
        Assert.Contains(comparison.TradeoffRevisions, tradeoffRevision => tradeoffRevision.ChangeType == "Added");
        Assert.Contains(comparison.PreviousTradeoffs, tradeoff => tradeoff.Benefit == previousTradeoff.Benefit);
        Assert.Contains(comparison.RevisedTradeoffs, tradeoff => tradeoff.Benefit == addedTradeoff.Benefit);

        string comparisonMarkdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/revisions/REV-0001.comparison.md");
        Assert.Contains("## Changed Fields", comparisonMarkdown);
        Assert.Contains("Tradeoffs: Changed", comparisonMarkdown);
        Assert.Contains("Keeps refinement review evidence adjacent to the recommendation.", comparisonMarkdown);
    }

    [Fact]
    public async Task RefinementRecordsExplicitPriorityAdjustmentWithoutProposalContentMutation()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        DecisionRefinementService refinementService = CreateRefinementService(repository, store, decisionRepository);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        await generationService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        DecisionProposal needsRefinement = await generationService.MarkProposalNeedsRefinementAsync(
            repository.Id,
            proposal.Id,
            "Priority needs explicit review metadata.");

        DecisionSourceReference source = new(
            "DecisionCandidate",
            ".agents/decisions/candidates/CAND-0001/candidate.json",
            CandidateId: candidate.Id);

        DecisionProposal refined = await refinementService.RefineProposalAsync(
            repository.Id,
            proposal.Id,
            new DecisionRefinementRequest(
                "Escalate priority because execution is blocked.",
                RequestedBy: "reviewer",
                BaseProposalFingerprint: Fingerprint(needsRefinement),
                PriorityAdjustments:
                [
                    new DecisionPriorityAdjustment(
                        DecisionCandidatePriority.High,
                        DecisionCandidatePriority.Blocking,
                        "Current milestone cannot proceed until this decision is resolved.",
                        source,
                        "reviewer")
                ]));

        DecisionProposalRevision revision = Assert.Single(await refinementService.ListProposalRevisionsAsync(repository.Id, proposal.Id));
        DecisionProposalRevisionComparison comparison = await refinementService.GetProposalRevisionComparisonAsync(
            repository.Id,
            proposal.Id,
            revision.Id);

        Assert.Equal(DecisionProposalState.Refined, refined.State);
        Assert.Equal(needsRefinement.Context, refined.Context);
        Assert.Equal(Fingerprint(needsRefinement), revision.SourceProposalFingerprint);
        Assert.Contains("PriorityAdjustments", revision.ChangedFields);
        Assert.Equal(HumanAuthoringBurden.MinorEdit, revision.HumanAuthoringBurden);
        DecisionPriorityAdjustment adjustment = Assert.Single(revision.PriorityAdjustments ?? []);
        Assert.Equal(DecisionCandidatePriority.High, adjustment.PreviousPriority);
        Assert.Equal(DecisionCandidatePriority.Blocking, adjustment.NewPriority);
        Assert.Equal("reviewer", adjustment.Attribution);
        Assert.Contains(revision.Diagnostics ?? [], diagnostic =>
            diagnostic.Contains("explicit priority adjustment", StringComparison.Ordinal));
        Assert.Contains(comparison.FieldComparisons, field =>
            field.Field == "PriorityAdjustments" &&
            field.ChangeType == "Metadata");
        Assert.Single(comparison.PriorityAdjustments);
        Assert.Equal(HumanAuthoringBurden.MinorEdit, comparison.HumanAuthoringBurden);

        string revisionMarkdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/revisions/REV-0001.md");
        string comparisonMarkdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/revisions/REV-0001.comparison.md");
        Assert.Contains("## Priority Adjustments", revisionMarkdown);
        Assert.Contains("Human authoring burden: MinorEdit", revisionMarkdown);
        Assert.Contains("High -> Blocking", revisionMarkdown);
        Assert.Contains("Current milestone cannot proceed until this decision is resolved.", comparisonMarkdown);
    }

    [Fact]
    public async Task ProposalLineageSeparatesCurrentProposalFromHistoricalRevisionsAndReviewNotes()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        DecisionRefinementService refinementService = CreateRefinementService(repository, store, decisionRepository);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        await generationService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        DecisionProposal needsRefinement = await generationService.MarkProposalNeedsRefinementAsync(
            repository.Id,
            proposal.Id,
            "Lineage should capture review state.");
        var reviewService = new DecisionReviewService(
            new StubRepositoryService(repository),
            decisionRepository,
            generationService);
        await reviewService.AddReviewNoteAsync(
            repository.Id,
            proposal.Id,
            new DecisionReviewNoteRequest("Historical revisions must remain explanatory.", "reviewer"));

        DecisionProposal refined = await refinementService.RefineProposalAsync(
            repository.Id,
            proposal.Id,
            new DecisionRefinementRequest(
                "Clarify lineage context.",
                Context: "Refined context for lineage.",
                BaseProposalFingerprint: Fingerprint(needsRefinement)));

        DecisionProposalLineage lineage = await refinementService.GetProposalLineageAsync(repository.Id, proposal.Id);

        Assert.Equal(refined.Id, lineage.CurrentProposal.Id);
        Assert.Equal(DecisionProposalState.Refined, lineage.CurrentState);
        Assert.Equal(Fingerprint(refined), lineage.CurrentProposalFingerprint);
        Assert.Contains(lineage.Diagnostics, diagnostic =>
            diagnostic.Contains("Current proposal is authoritative", StringComparison.Ordinal));
        DecisionProposalRevisionSnapshot snapshot = Assert.Single(lineage.Revisions);
        Assert.False(snapshot.IsCurrentProposal);
        Assert.Equal("REV-0001", snapshot.Revision.Id);
        Assert.Equal("Refined context for lineage.", lineage.CurrentProposal.Context);
        Assert.Equal(needsRefinement.Context, snapshot.Revision.PreviousContext);
        Assert.Equal("Refined context for lineage.", snapshot.Revision.RevisedContext);
        Assert.Equal("Historical revision is read-only explanatory history; currentProposal remains authoritative.", snapshot.AuthorityBoundary);
        Assert.Contains(lineage.ReviewNotes, note => note.Body == "Historical revisions must remain explanatory.");
        Assert.Contains(lineage.Events, item => item.Kind == "Revision" && item.ItemId == "REV-0001");
        Assert.Contains(lineage.Events, item => item.Kind == "ReviewNote" && item.ItemId == "NOTE-0001");
    }

    private static DecisionGenerationService CreateGenerationService(
        Repository repository,
        FileSystemArtifactStore store,
        FileSystemDecisionRepository decisionRepository)
    {
        var repositoryService = new StubRepositoryService(repository);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        return new DecisionGenerationService(
            repositoryService,
            decisionRepository,
            projectionService,
            new OptionGenerationService());
    }

    private static DecisionRefinementService CreateRefinementService(
        Repository repository,
        FileSystemArtifactStore store,
        FileSystemDecisionRepository decisionRepository)
    {
        var repositoryService = new StubRepositoryService(repository);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        return new DecisionRefinementService(repositoryService, decisionRepository, projectionService);
    }

    private static RefinementAnalysisService CreateAnalysisService(
        Repository repository,
        FileSystemDecisionRepository decisionRepository)
    {
        return new RefinementAnalysisService(new StubRepositoryService(repository), decisionRepository);
    }

    private static DecisionCandidate CreateCandidate(
        Guid repositoryId,
        DecisionCandidateState state,
        string signalKind = "MissingDirection",
        string summary = "Need to decide repository-backed persistence schema.")
    {
        return new DecisionCandidate(
            "CAND-0001",
            repositoryId,
            state,
            DecisionCandidatePriority.High,
            DecisionClassification.Architectural,
            "Decide persistence schema",
            summary,
            "source-fingerprint",
            [new DecisionSignal(
                signalKind,
                summary,
                DecisionClassification.Architectural,
                DecisionCandidatePriority.High,
                [new DecisionEvidence(
                    "Plan requires a persistence decision.",
                    [new DecisionSourceReference(
                        "Plan",
                        ".agents/plan.md",
                        Section: "Plan",
                        ItemId: "plan",
                        Excerpt: summary)])])],
            [new DecisionEvidence(
                "Plan requires a persistence decision.",
                [new DecisionSourceReference(
                    "Plan",
                    ".agents/plan.md",
                    Section: "Plan",
                    ItemId: "plan",
                    Excerpt: summary)])],
            [new DecisionSourceReference(
                "Plan",
                ".agents/plan.md",
                Section: "Plan",
                ItemId: "plan",
                Excerpt: summary)],
            ["Created by refinement test."],
            [new DecisionHistoryEntry(
                DateTimeOffset.UtcNow,
                state == DecisionCandidateState.Promoted ? "Promoted" : "Discovered",
                null,
                state.ToString(),
                "Seeded by refinement test.",
                [])]);
    }

    private static string Fingerprint(DecisionProposal proposal)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(proposal, CreateJsonOptions()));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static async Task<string> ReadAsync(Repository repository, string relativePath)
    {
        return await File.ReadAllTextAsync(Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static Repository CreateRepository()
    {
        string path = Path.Combine(Path.GetTempPath(), "LoopRelay.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, ".git"));
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path
        };
    }

    private sealed class StubRepositoryService(params Repository[] repositories) : IRepositoryService
    {
        public Task<IReadOnlyList<Repository>> GetAllAsync()
        {
            return Task.FromResult<IReadOnlyList<Repository>>(repositories);
        }

        public Task<Repository> RegisterAsync(string repositoryPath)
        {
            throw new NotSupportedException();
        }

        public Task RemoveAsync(Guid repositoryId)
        {
            throw new NotSupportedException();
        }
    }
}
