using System.Security.Cryptography;
using System.Text;
using CommandCenter.Core.Artifacts;
using CommandCenter.Continuity;
using CommandCenter.Continuity.Abstractions;
using CommandCenter.Continuity.Models;
using CommandCenter.Continuity.Primitives;
using CommandCenter.Core.Planning;
using CommandCenter.Core.Repositories;
using CommandCenter.Execution;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;

namespace CommandCenter.Middle.Continuity;

public sealed class OperationalContextGenerationService(
    IRepositoryService repositoryService,
    IArtifactService artifactService,
    IPlanningService planningService,
    IExecutionSessionService executionSessionService,
    IOperationalContextParser parser,
    IUnderstandingDiffService diffService,
    IUnderstandingCompressionService compressionService,
    IDecisionAnalysisService decisionAnalysisService,
    IOperationalContextProposalStore proposalStore) : IOperationalContextGenerationService
{
    private const string CurrentOperationalContextPath = ".agents/operational_context.md";
    private const string CurrentHandoffPath = ".agents/handoffs/handoff.md";
    private const string CurrentDecisionsPath = ".agents/decisions/decisions.md";
    private const int DecisionAssimilationLimitCount = 8;
    private const string DecisionAssimilationLimitReason =
        "Operational context proposal generation includes at most eight qualifying durable decision signals to keep the proposed context reviewable.";

    public async Task<OperationalContextProposal> GenerateAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        OperationalContextInputSet inputSet = await BuildInputSetAsync(repository);
        OperationalContextDocument currentDocument = parser.Parse(inputSet.CurrentOperationalContext ?? string.Empty);
        DecisionAnalysisResult decisionAnalysis = decisionAnalysisService.Analyze(inputSet.DecisionArtifacts);
        DecisionAssimilationProjection decisionAssimilation = BuildDecisionAssimilationProjection(decisionAnalysis);
        OperationalContextDocument proposedDocument = BuildProposedDocument(inputSet, currentDocument, decisionAssimilation);
        OperationalContextCompressionResult compression = compressionService.Compress(currentDocument, proposedDocument);
        string generatedContent = parser.Render(compression.Document);
        OperationalContextDocument generatedDocument = parser.Parse(generatedContent);
        string generatedContentHash = HashContent(generatedContent);
        OperationalContextCompressionSummary compressionSummary = AppendDecisionWarnings(compression.Summary, decisionAnalysis.Warnings);

        await proposalStore.SupersedePendingAsync(repository);

        var proposal = new OperationalContextProposal
        {
            ProposalId = CreateProposalId(),
            RepositoryId = repository.Id,
            GeneratedAt = DateTimeOffset.UtcNow,
            Status = OperationalContextProposalStatus.Pending,
            InputFingerprints = BuildFingerprints(inputSet, generatedContent),
            BaselineCurrentContextHash = HashOptionalContent(inputSet.CurrentOperationalContext),
            GeneratedContentHash = generatedContentHash,
            SemanticChanges = diffService.Compare(currentDocument, generatedDocument),
            DecisionAssimilation = decisionAssimilation,
            CompressionSummary = compressionSummary
        };

        return await proposalStore.SaveAsync(repository, proposal, generatedContent);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync()).FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<OperationalContextInputSet> BuildInputSetAsync(Repository repository)
    {
        IReadOnlyList<Artifact> artifacts = await artifactService.DiscoverAsync(repository);
        IReadOnlyList<DecisionArtifactInput> decisionArtifacts = await ReadDecisionArtifactsAsync(repository, artifacts);
        return new OperationalContextInputSet
        {
            Repository = repository,
            CurrentOperationalContext = await ReadOptionalAsync(repository, CurrentOperationalContextPath),
            CurrentHandoff = await ReadOptionalAsync(repository, CurrentHandoffPath),
            CurrentDecisions = await ReadOptionalAsync(repository, CurrentDecisionsPath),
            DecisionArtifacts = decisionArtifacts,
            ExecutionHistory = await executionSessionService.GetRepositorySessionHistoryAsync(repository.Id, 5),
            MilestonePaths = artifacts
                .Where(artifact => artifact.Type == ArtifactType.Milestone)
                .Select(artifact => artifact.RelativePath)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            HasPlan = artifacts.Any(artifact => artifact.Type == ArtifactType.Plan),
            PlanningReadiness = (await planningService.DetermineReadinessAsync(repository)).ToString()
        };
    }

    private async Task<string?> ReadOptionalAsync(Repository repository, string relativePath)
    {
        return await artifactService.ExistsAsync(repository, relativePath)
            ? await artifactService.LoadAsync(repository, relativePath)
            : null;
    }

    private async Task<IReadOnlyList<DecisionArtifactInput>> ReadDecisionArtifactsAsync(
        Repository repository,
        IReadOnlyList<Artifact> artifacts)
    {
        Artifact[] selectedArtifacts = artifacts
            .Where(artifact => artifact.Type == ArtifactType.Decision)
            .OrderByDescending(artifact => artifact.VersionKind == ArtifactVersionKind.Current)
            .ThenByDescending(artifact => artifact.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();

        var decisionArtifacts = new List<DecisionArtifactInput>();
        foreach (Artifact artifact in selectedArtifacts)
        {
            decisionArtifacts.Add(new DecisionArtifactInput
            {
                RelativePath = artifact.RelativePath,
                Content = await artifactService.LoadAsync(repository, artifact.RelativePath),
                IsCurrent = artifact.VersionKind == ArtifactVersionKind.Current
            });
        }

        return decisionArtifacts;
    }

    private static OperationalContextDocument BuildProposedDocument(
        OperationalContextInputSet inputSet,
        OperationalContextDocument current,
        DecisionAssimilationProjection decisionAssimilation)
    {
        List<OperationalContextItem> currentMentalModel = current.CurrentMentalModel.ToList();
        AddUnique(
            currentMentalModel,
            OperationalContextItemKind.MentalModel,
            $"Repository `{inputSet.Repository.Name}` uses repository-owned `.agents` artifacts as continuity inputs.",
            ".agents");
        AddUnique(
            currentMentalModel,
            OperationalContextItemKind.MentalModel,
            inputSet.HasPlan
                ? "Planning state is available from `.agents/plan.md` and milestone artifacts."
                : "Planning state is partial because `.agents/plan.md` is not present.",
            ".agents/plan.md");

        List<OperationalContextItem> stableDecisions = current.StableDecisions.ToList();
        List<OperationalContextItem> decisionRationale = current.DecisionRationale.ToList();
        List<OperationalContextItem> constraints = current.Constraints.ToList();
        List<OperationalContextItem> openQuestions = current.OpenQuestions.ToList();

        foreach (DecisionAssimilationRecord decision in decisionAssimilation.Decisions.Where(decision => decision.IsAssimilated))
        {
            AddUnique(
                stableDecisions,
                OperationalContextItemKind.StableDecision,
                decision.OperationalStatement ?? $"Decision: {decision.Statement}",
                decision.SourceRelativePath);

            if (!string.IsNullOrWhiteSpace(decision.Rationale))
            {
                AddUnique(
                    decisionRationale,
                    OperationalContextItemKind.DecisionRationale,
                    $"Rationale for `{decision.Statement}`: {decision.Rationale}",
                    decision.SourceRelativePath);
            }

            foreach (string constraint in decision.ConstraintsIntroduced.Take(3))
            {
                AddUnique(constraints, OperationalContextItemKind.Constraint, constraint, decision.SourceRelativePath);
            }

            foreach (string openQuestion in decision.OpenQuestions.Take(3))
            {
                AddUnique(openQuestions, OperationalContextItemKind.OpenQuestion, $"Open decision: {openQuestion}", decision.SourceRelativePath);
            }
        }

        List<OperationalContextItem> recentChanges = current.RecentUnderstandingChanges.ToList();
        foreach (string change in ExtractHandoffSignals(inputSet.CurrentHandoff).Take(8))
        {
            AddUnique(recentChanges, OperationalContextItemKind.RecentChange, change, CurrentHandoffPath);
        }

        foreach (ExecutionSessionSummary session in inputSet.ExecutionHistory.Take(5))
        {
            string status = session.State.ToString();
            AddUnique(
                recentChanges,
                OperationalContextItemKind.RecentChange,
                $"Recent execution `{session.SessionId:D}` is recorded with state `{status}`.",
                null);
        }

        return new OperationalContextDocument
        {
            Title = string.IsNullOrWhiteSpace(current.Title) ? "Operational Context" : current.Title,
            CurrentMentalModel = currentMentalModel,
            Architecture = current.Architecture,
            AuthorityBoundaries = current.AuthorityBoundaries,
            Constraints = constraints,
            StableDecisions = stableDecisions,
            DecisionRationale = decisionRationale,
            OpenQuestions = openQuestions,
            ActiveRisks = current.ActiveRisks,
            RecentUnderstandingChanges = recentChanges,
            AdditionalSections = current.AdditionalSections
        };
    }

    private static DecisionAssimilationProjection BuildDecisionAssimilationProjection(DecisionAnalysisResult decisionAnalysis)
    {
        DecisionSignal[] qualifyingSignals = decisionAnalysis.Signals
            .Where(QualifiesForAssimilation)
            .ToArray();
        HashSet<DecisionSignal> assimilatedSignals = qualifyingSignals
            .Take(DecisionAssimilationLimitCount)
            .ToHashSet();
        HashSet<DecisionSignal> omittedSignals = qualifyingSignals
            .Skip(DecisionAssimilationLimitCount)
            .ToHashSet();

        DecisionAssimilationRecord[] records = decisionAnalysis.Signals
            .Select(signal => CreateAssimilationRecord(signal, assimilatedSignals, omittedSignals))
            .ToArray();

        return new DecisionAssimilationProjection
        {
            Decisions = records,
            Consequences = decisionAnalysis.Consequences,
            Contradictions = decisionAnalysis.Contradictions,
            Limit = new DecisionAssimilationLimit
            {
                Limit = DecisionAssimilationLimitCount,
                Reason = DecisionAssimilationLimitReason,
                TotalAnalyzedItemCount = decisionAnalysis.Signals.Count,
                TotalQualifyingItemCount = qualifyingSignals.Length,
                AssimilatedItemCount = assimilatedSignals.Count,
                OmittedItemCount = omittedSignals.Count
            }
        };
    }

    private static DecisionAssimilationRecord CreateAssimilationRecord(
        DecisionSignal signal,
        HashSet<DecisionSignal> assimilatedSignals,
        HashSet<DecisionSignal> omittedSignals)
    {
        bool qualifies = QualifiesForAssimilation(signal);
        bool assimilated = assimilatedSignals.Contains(signal);
        bool omitted = omittedSignals.Contains(signal);

        return new DecisionAssimilationRecord
        {
            DecisionId = signal.DecisionId,
            SourceRelativePath = signal.SourceRelativePath,
            Statement = signal.Statement,
            Taxonomy = signal.Taxonomy,
            TaxonomyBasis = signal.TaxonomyBasis,
            Status = assimilated
                ? DecisionAssimilationStatus.Assimilated
                : omitted
                    ? DecisionAssimilationStatus.OmittedByLimit
                    : DecisionAssimilationStatus.Excluded,
            IsDurable = IsDurable(signal),
            QualifiesForAssimilation = qualifies,
            IsAssimilated = assimilated,
            IsOmittedByLimit = omitted,
            ExclusionReason = qualifies ? null : GetExclusionReason(signal),
            OmissionReason = omitted ? DecisionAssimilationLimitReason : null,
            OperationalStatement = qualifies ? $"Decision: {signal.Statement}" : null,
            Rationale = signal.Rationale,
            ConstraintsIntroduced = signal.ConstraintsIntroduced,
            ConsequencesIntroduced = signal.Consequences,
            OpenQuestions = signal.OpenQuestions,
            SourceEvidence = BuildSourceEvidence(signal)
        };
    }

    private static IReadOnlyList<string> BuildSourceEvidence(DecisionSignal signal)
    {
        var evidence = new List<string>
        {
            $"Source artifact: {signal.SourceRelativePath}",
            $"Decision statement: {signal.Statement}"
        };

        if (!string.IsNullOrWhiteSpace(signal.Rationale))
        {
            evidence.Add($"Rationale: {signal.Rationale}");
        }

        foreach (string constraint in signal.ConstraintsIntroduced.Take(3))
        {
            evidence.Add($"Constraint: {constraint}");
        }

        foreach (string consequence in signal.Consequences.Take(3))
        {
            evidence.Add($"Consequence: {consequence}");
        }

        return evidence;
    }

    private static string GetExclusionReason(DecisionSignal signal)
    {
        if (signal.IsSupersededOrRetired)
        {
            return "Decision signal is superseded or retired.";
        }

        return signal.Taxonomy switch
        {
            DecisionTaxonomy.HistoricalDecision => "Historical decision signals are not assimilated into current operational context.",
            DecisionTaxonomy.TacticalDecision => "Tactical decision signals are execution detail and are not assimilated as durable operational context.",
            _ => "Decision signal does not satisfy durable assimilation criteria."
        };
    }

    private static bool QualifiesForAssimilation(DecisionSignal signal)
    {
        return !signal.IsSupersededOrRetired &&
            signal.Taxonomy is DecisionTaxonomy.ArchitecturalDecision or DecisionTaxonomy.StrategicDecision;
    }

    private static bool IsDurable(DecisionSignal signal)
    {
        return signal.Taxonomy is DecisionTaxonomy.ArchitecturalDecision or DecisionTaxonomy.StrategicDecision;
    }

    private static IEnumerable<string> ExtractHandoffSignals(string? handoffMarkdown)
    {
        return ExtractBullets(handoffMarkdown)
            .Where(line => !line.StartsWith("`", StringComparison.Ordinal))
            .Select(line => $"Latest handoff signal: {line}");
    }

    private static IEnumerable<string> ExtractBullets(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            yield break;
        }

        foreach (string rawLine in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length > 2 &&
                (line[0] == '-' || line[0] == '*' || line[0] == '+') &&
                char.IsWhiteSpace(line[1]))
            {
                string text = line[2..].Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return text;
                }
            }
        }
    }

    private static void AddUnique(
        List<OperationalContextItem> items,
        OperationalContextItemKind kind,
        string text,
        string? sourceRelativePath)
    {
        if (items.Any(item => string.Equals(Normalize(item.Text), Normalize(text), StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        items.Add(new OperationalContextItem
        {
            Id = CreateItemId(kind.ToString(), text),
            Kind = kind,
            Text = text,
            SourceRelativePath = sourceRelativePath
        });
    }

    private static IReadOnlyList<OperationalContextInputFingerprint> BuildFingerprints(
        OperationalContextInputSet inputSet,
        string generatedContent)
    {
        string executionHistoryContent = string.Join(
            Environment.NewLine,
            inputSet.ExecutionHistory.Select(session => $"{session.SessionId}|{session.State}|{session.CompletedAt:O}"));
        string planningContent = string.Join(
            Environment.NewLine,
            inputSet.MilestonePaths.Prepend($"PlanningReadiness={inputSet.PlanningReadiness}").Prepend($"HasPlan={inputSet.HasPlan}"));

        return
        [
            Fingerprint("CurrentOperationalContext", CurrentOperationalContextPath, inputSet.CurrentOperationalContext),
            Fingerprint("CurrentHandoff", CurrentHandoffPath, inputSet.CurrentHandoff),
            Fingerprint("CurrentDecisions", CurrentDecisionsPath, inputSet.CurrentDecisions),
            Fingerprint("ExecutionHistory", ".agents/execution-sessions", executionHistoryContent),
            Fingerprint("PlanningState", ".agents", planningContent),
            Fingerprint("GeneratedProposal", ".agents/operational_context/proposals", generatedContent)
        ];
    }

    private static OperationalContextInputFingerprint Fingerprint(string name, string relativePath, string? content)
    {
        bool present = content is not null;
        string normalizedContent = content ?? "<absent>";
        int bytes = Encoding.UTF8.GetByteCount(normalizedContent);
        return new OperationalContextInputFingerprint
        {
            Name = name,
            RelativePath = relativePath,
            Present = present,
            Hash = HashContent(normalizedContent),
            CharacterCount = normalizedContent.Length,
            ByteCount = bytes
        };
    }

    private static OperationalContextCompressionSummary AppendDecisionWarnings(
        OperationalContextCompressionSummary summary,
        IReadOnlyList<string> decisionWarnings)
    {
        if (decisionWarnings.Count == 0)
        {
            return summary;
        }

        string[] warnings = summary.Warnings.Concat(decisionWarnings.Select(warning => $"Decision analysis warning: {warning}")).ToArray();
        string[] stableWarnings = summary.StableUnderstandingRetentionWarnings
            .Concat(decisionWarnings
                .Where(warning => warning.Contains("rationale", StringComparison.OrdinalIgnoreCase) ||
                    warning.Contains("Contradictory", StringComparison.OrdinalIgnoreCase))
                .Select(warning => $"Decision analysis warning: {warning}"))
            .ToArray();
        string[] revisionSummary = summary.RevisionSummary
            .Concat([$"{decisionWarnings.Count} decision-continuity warning(s) require review."])
            .ToArray();

        return new OperationalContextCompressionSummary
        {
            PreservedItemCount = summary.PreservedItemCount,
            AddedItemCount = summary.AddedItemCount,
            ModifiedItemCount = summary.ModifiedItemCount,
            RemovedItemCount = summary.RemovedItemCount,
            CompressedItemCount = summary.CompressedItemCount,
            PermanentUnderstandingItemCount = summary.PermanentUnderstandingItemCount,
            ActiveUnderstandingItemCount = summary.ActiveUnderstandingItemCount,
            HistoricalUnderstandingItemCount = summary.HistoricalUnderstandingItemCount,
            HistoricalNoiseItemCount = summary.HistoricalNoiseItemCount,
            ResolvedQuestionCount = summary.ResolvedQuestionCount,
            RetiredRiskCount = summary.RetiredRiskCount,
            WarningCount = warnings.Length,
            Warnings = warnings,
            RevisionSummary = revisionSummary,
            NoiseRemovedIndicators = summary.NoiseRemovedIndicators,
            StableUnderstandingRetentionWarnings = stableWarnings,
            ItemOutcomes = summary.ItemOutcomes
        };
    }

    private static string HashOptionalContent(string? content)
    {
        return HashContent(content ?? "<absent>");
    }

    private static string HashContent(string content)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    private static string CreateProposalId()
    {
        return $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
    }

    private static string CreateItemId(string section, string text)
    {
        string normalized = Normalize($"{section}:{text}");
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"{Normalize(section).Replace(' ', '-')}-{Convert.ToHexString(bytes)[..12].ToLowerInvariant()}";
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
