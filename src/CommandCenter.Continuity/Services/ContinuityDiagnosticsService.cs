using System.Text;
using CommandCenter.Continuity.Abstractions;
using CommandCenter.Continuity.Models;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Continuity.Services;

public sealed class ContinuityDiagnosticsService(
    IRepositoryService repositoryService,
    IArtifactService artifactService,
    IArtifactStore artifactStore,
    IOperationalContextParser parser,
    IUnderstandingDiffService diffService,
    IOperationalContextProposalStore proposalStore) : IContinuityDiagnosticsService
{
    public async Task<ContinuityDiagnostics> GetDiagnosticsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        IReadOnlyList<RevisionEntry> revisions = await LoadRevisionEntriesAsync(repository);
        var ledger = new UnderstandingEvolutionLedger
        {
            Revisions = revisions
                .Select(entry => entry.Snapshot)
                .ToArray()
        };
        UnderstandingRevisionSnapshot? first = ledger.Revisions.FirstOrDefault();
        UnderstandingRevisionSnapshot? current = ledger.CurrentRevision;
        IReadOnlyList<OperationalContextProposal> proposals = await proposalStore.ListAsync(repository);
        var warnings = new List<string>();
        warnings.AddRange(proposals.SelectMany(proposal => proposal.CompressionSummary.Warnings));
        warnings.AddRange(proposals.SelectMany(proposal => proposal.CompressionSummary.StableUnderstandingRetentionWarnings));
        OperationalEvolutionSummary operationalEvolution = BuildOperationalEvolution(revisions);
        IReadOnlyList<ContinuityDiagnosticGroup> diagnosticGroups = BuildDiagnosticGroups(operationalEvolution, proposals);

        return new ContinuityDiagnostics
        {
            RepositoryId = repository.Id,
            GeneratedAt = DateTimeOffset.UtcNow,
            RevisionCount = ledger.Revisions.Count,
            CurrentContextByteCount = current?.ByteCount ?? 0,
            CurrentContextCharacterCount = current?.CharacterCount ?? 0,
            ContextByteGrowth = current is null || first is null ? 0 : current.ByteCount - first.ByteCount,
            AverageBytesPerRevision = ledger.Revisions.Count == 0 ? 0 : ledger.Revisions.Average(revision => revision.ByteCount),
            RevisionFrequency = CalculateRevisionFrequency(ledger.Revisions),
            EvolutionLedger = ledger,
            OperationalEvolution = operationalEvolution,
            ArchitectureTrend = BuildTrend(operationalEvolution.SemanticChanges, "Architecture"),
            ConstraintTrend = BuildTrend(operationalEvolution.SemanticChanges, "Constraints"),
            DecisionTrend = BuildTrend(operationalEvolution.SemanticChanges, "Stable Decisions"),
            RationaleTrend = BuildTrend(operationalEvolution.SemanticChanges, "Decision Rationale"),
            OpenQuestionTrend = BuildActiveTrend(
                operationalEvolution.SemanticChanges,
                revisions,
                "Open Questions",
                document => document.OpenQuestions,
                "resolved question"),
            ActiveRiskTrend = BuildActiveTrend(
                operationalEvolution.SemanticChanges,
                revisions,
                "Active Risks",
                document => document.ActiveRisks,
                "retired risk"),
            CompressionTrend = BuildCompressionTrend(proposals),
            RepeatedInvestigationIndicators = FindRepeatedIndicators(
                revisions,
                document => document.RecentUnderstandingChanges,
                ["investigat"]),
            RepeatedQuestionIndicators = FindRepeatedQuestions(revisions),
            DecisionReworkIndicators = FindRepeatedIndicators(
                revisions,
                document => document.StableDecisions.Concat(document.RecentUnderstandingChanges),
                ["rework", "revisit", "replace decision", "changed decision"]),
            ContinuityWarnings = warnings
                .Where(warning => !string.IsNullOrWhiteSpace(warning))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            DiagnosticGroups = diagnosticGroups
        };
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync()).FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<IReadOnlyList<RevisionEntry>> LoadRevisionEntriesAsync(Repository repository)
    {
        var artifacts = (await artifactService.DiscoverAsync(repository))
            .Where(artifact => artifact.Family == ArtifactFamily.OperationalContext)
            .Select(artifact => new
            {
                Artifact = artifact,
                RevisionNumber = artifact.VersionKind == ArtifactVersionKind.Current
                    ? int.MaxValue
                    : TryParseHistoricalRevisionNumber(artifact.Name)
            })
            .Where(entry => entry.RevisionNumber > 0)
            .OrderBy(entry => entry.RevisionNumber)
            .ToArray();

        int highestHistoricalRevision = artifacts
            .Where(entry => entry.Artifact.VersionKind == ArtifactVersionKind.Historical)
            .Select(entry => entry.RevisionNumber)
            .DefaultIfEmpty(0)
            .Max();
        var entries = new List<RevisionEntry>();
        foreach (var entry in artifacts)
        {
            string content = await artifactStore.ReadAsync(ArtifactPath.ResolveRepositoryPath(repository, entry.Artifact.RelativePath)) ?? string.Empty;
            int bytes = Encoding.UTF8.GetByteCount(content);
            int revisionNumber = entry.Artifact.VersionKind == ArtifactVersionKind.Current
                ? highestHistoricalRevision + 1
                : entry.RevisionNumber;
            OperationalContextDocument document = parser.Parse(content);
            entries.Add(new RevisionEntry(
                document,
                new UnderstandingRevisionSnapshot
                {
                    RevisionNumber = revisionNumber,
                    RelativePath = entry.Artifact.RelativePath,
                    LastUpdatedAt = GetLastWriteTime(ArtifactPath.ResolveRepositoryPath(repository, entry.Artifact.RelativePath)),
                    ByteCount = bytes,
                    CharacterCount = content.Length,
                    ArchitectureItemCount = document.Architecture.Count,
                    ConstraintCount = document.Constraints.Count,
                    StableDecisionCount = document.StableDecisions.Count,
                    DecisionRationaleCount = document.DecisionRationale.Count,
                    OpenQuestionCount = document.OpenQuestions.Count,
                    ActiveRiskCount = document.ActiveRisks.Count
                }));
        }

        return entries;
    }

    private OperationalEvolutionSummary BuildOperationalEvolution(IReadOnlyList<RevisionEntry> revisions)
    {
        if (revisions.Count < 2)
        {
            return new OperationalEvolutionSummary
            {
                DiagnosticGroups =
                [
                    new ContinuityDiagnosticGroup
                    {
                        Category = "evolution",
                        Title = "Operational evolution",
                        Diagnostics = ["At least two operational-context revisions are required to compare evolution."]
                    }
                ]
            };
        }

        IReadOnlyList<OperationalContextSemanticChange> changes = diffService.Compare(revisions[^2].Document, revisions[^1].Document);
        int resolvedCount = CountResolved(changes) + CountResolutionEvidence(revisions, "resolved question") + CountResolutionEvidence(revisions, "retired risk");
        int removedCount = changes.Count(IsRemovedChange);
        OperationalEvolutionSummary summary = new()
        {
            AddedCount = changes.Count(IsAddedChange),
            ModifiedCount = changes.Count(IsModifiedChange),
            RemovedCount = removedCount,
            PreservedCount = CountPreservedItems(revisions[^2].Document, revisions[^1].Document),
            LostCount = Math.Max(0, removedCount - resolvedCount),
            ResolvedCount = resolvedCount,
            SemanticChanges = changes,
            DiagnosticGroups = BuildOperationalEvolutionDiagnosticGroups(changes, removedCount, resolvedCount)
        };
        return summary;
    }

    private static ContinuityTrend BuildTrend(
        IReadOnlyList<OperationalContextSemanticChange> changes,
        string section)
    {
        OperationalContextSemanticChange[] sectionChanges = changes
            .Where(change => string.Equals(change.Section, section, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        int removedCount = sectionChanges.Count(IsRemovedChange);
        return new ContinuityTrend
        {
            AddedCount = sectionChanges.Count(IsAddedChange),
            ModifiedCount = sectionChanges.Count(IsModifiedChange),
            RemovedCount = removedCount,
            LostCount = removedCount
        };
    }

    private static ContinuityTrend BuildActiveTrend(
        IReadOnlyList<OperationalContextSemanticChange> changes,
        IReadOnlyList<RevisionEntry> revisions,
        string section,
        Func<OperationalContextDocument, IEnumerable<OperationalContextItem>> getItems,
        string resolutionPrefix)
    {
        ContinuityTrend trend = BuildTrend(changes, section);
        if (revisions.Count < 2)
        {
            return trend;
        }

        HashSet<string> previous = ToNormalizedSet(getItems(revisions[^2].Document));
        HashSet<string> current = ToNormalizedSet(getItems(revisions[^1].Document));
        string[] removed = previous.Except(current, StringComparer.OrdinalIgnoreCase).ToArray();
        HashSet<string> resolutionEvidence = ToNormalizedSet(revisions[^1].Document.RecentUnderstandingChanges
            .Where(item => item.Text.StartsWith(resolutionPrefix, StringComparison.OrdinalIgnoreCase)));
        int resolved = removed.Count(item => resolutionEvidence.Any(evidence => evidence.Contains(item, StringComparison.OrdinalIgnoreCase)));
        return new ContinuityTrend
        {
            AddedCount = trend.AddedCount,
            ModifiedCount = trend.ModifiedCount,
            RemovedCount = trend.RemovedCount,
            ResolvedCount = resolved,
            LostCount = Math.Max(0, trend.RemovedCount - resolved)
        };
    }

    private static CompressionTrend BuildCompressionTrend(IReadOnlyList<OperationalContextProposal> proposals)
    {
        string[] warnings = proposals.SelectMany(proposal => proposal.CompressionSummary.Warnings)
            .Concat(proposals.SelectMany(proposal => proposal.CompressionSummary.StableUnderstandingRetentionWarnings))
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new CompressionTrend
        {
            ProposalCount = proposals.Count,
            CompressedItemCount = proposals.Sum(proposal => proposal.CompressionSummary.CompressedItemCount),
            RemovedItemCount = proposals.Sum(proposal => proposal.CompressionSummary.RemovedItemCount),
            ResolvedQuestionCount = proposals.Sum(proposal => proposal.CompressionSummary.ResolvedQuestionCount),
            RetiredRiskCount = proposals.Sum(proposal => proposal.CompressionSummary.RetiredRiskCount),
            WarningCount = warnings.Length,
            Warnings = warnings,
            NoiseRemovedIndicators = proposals
                .SelectMany(proposal => proposal.CompressionSummary.NoiseRemovedIndicators)
                .Where(indicator => !string.IsNullOrWhiteSpace(indicator))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    private static IReadOnlyList<string> FindRepeatedQuestions(IReadOnlyList<RevisionEntry> revisions)
    {
        return revisions
            .SelectMany(revision => revision.Document.OpenQuestions)
            .Select(item => item.Text)
            .GroupBy(Normalize, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.First())
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> FindRepeatedIndicators(
        IReadOnlyList<RevisionEntry> revisions,
        Func<OperationalContextDocument, IEnumerable<OperationalContextItem>> getItems,
        IReadOnlyList<string> markers)
    {
        return revisions
            .SelectMany(revision => getItems(revision.Document))
            .Select(item => item.Text)
            .Where(text => markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(Normalize, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.First())
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static HashSet<string> ToNormalizedSet(IEnumerable<OperationalContextItem> items)
    {
        return items
            .Select(item => Normalize(item.Text))
            .Where(text => text.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        string normalized = value.Trim().ToLowerInvariant();
        foreach (string prefix in new[] { "resolved question:", "retired risk:", "decision:", "rationale for" })
        {
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                normalized = normalized[prefix.Length..].Trim();
            }
        }

        return string.Join(' ', normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static IReadOnlyList<ContinuityDiagnosticGroup> BuildDiagnosticGroups(
        OperationalEvolutionSummary operationalEvolution,
        IReadOnlyList<OperationalContextProposal> proposals)
    {
        var groups = new List<ContinuityDiagnosticGroup>();
        groups.AddRange(operationalEvolution.DiagnosticGroups);

        string[] compressionDiagnostics =
        [
            $"Proposal count: {proposals.Count}.",
            $"Compressed item count: {proposals.Sum(proposal => proposal.CompressionSummary.CompressedItemCount)}.",
            $"Removed item count: {proposals.Sum(proposal => proposal.CompressionSummary.RemovedItemCount)}."
        ];
        groups.Add(new ContinuityDiagnosticGroup
        {
            Category = "compression",
            Title = "Compression diagnostics",
            Diagnostics = compressionDiagnostics
        });

        return groups;
    }

    private static IReadOnlyList<ContinuityDiagnosticGroup> BuildOperationalEvolutionDiagnosticGroups(
        IReadOnlyList<OperationalContextSemanticChange> changes,
        int removedCount,
        int resolvedCount)
    {
        var groups = new List<ContinuityDiagnosticGroup>
        {
            new()
            {
                Category = "evolution",
                Title = "Operational evolution",
                Diagnostics =
                [
                    $"Added item count: {changes.Count(IsAddedChange)}.",
                    $"Modified item count: {changes.Count(IsModifiedChange)}.",
                    $"Removed item count: {removedCount}.",
                    $"Resolved item count: {resolvedCount}."
                ]
            },
            new()
            {
                Category = "diff",
                Title = "Semantic diff",
                Diagnostics = changes.Count == 0
                    ? ["No semantic changes detected between the latest operational-context revisions."]
                    : changes.Select(change => $"{change.Type} in {change.Section}: {change.Description}").ToArray()
            }
        };

        foreach (OperationalContextSemanticChange change in changes.Where(IsModifiedChange))
        {
            var diagnostics = new List<string>
            {
                $"Section: {change.Section}.",
                $"Item id: {change.ItemId}.",
                $"Identity basis: {change.IdentityBasis}.",
                $"Previous state: {change.PreviousState}.",
                $"Current state: {change.CurrentState}.",
                $"Modification reason: {change.ModificationReason}."
            };
            diagnostics.AddRange(change.SupportingEvidence.Select(evidence => $"Supporting evidence: {evidence}"));
            groups.Add(new ContinuityDiagnosticGroup
            {
                Category = "evolution",
                Title = "Modified operational-context item",
                Diagnostics = diagnostics
            });
        }

        return groups;
    }

    private static int CountResolutionEvidence(IReadOnlyList<RevisionEntry> revisions, string resolutionPrefix)
    {
        if (revisions.Count < 2)
        {
            return 0;
        }

        return revisions[^1].Document.RecentUnderstandingChanges.Count(item =>
            item.Text.StartsWith(resolutionPrefix, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountResolved(IReadOnlyList<OperationalContextSemanticChange> changes)
    {
        return changes.Count(change => change.Type == Primitives.OperationalContextSemanticChangeType.OpenDecisionResolved);
    }

    private static int CountPreservedItems(OperationalContextDocument previous, OperationalContextDocument current)
    {
        HashSet<string> previousItems = ToNormalizedSet(EnumerateKnownItems(previous));
        HashSet<string> currentItems = ToNormalizedSet(EnumerateKnownItems(current));
        return previousItems.Intersect(currentItems, StringComparer.OrdinalIgnoreCase).Count();
    }

    private static IEnumerable<OperationalContextItem> EnumerateKnownItems(OperationalContextDocument document)
    {
        return document.CurrentMentalModel
            .Concat(document.Architecture)
            .Concat(document.AuthorityBoundaries)
            .Concat(document.Constraints)
            .Concat(document.StableDecisions)
            .Concat(document.DecisionRationale)
            .Concat(document.OpenQuestions)
            .Concat(document.ActiveRisks)
            .Concat(document.RecentUnderstandingChanges);
    }

    private static bool IsAddedChange(OperationalContextSemanticChange change)
    {
        return change.Type is
            Primitives.OperationalContextSemanticChangeType.ItemAdded or
            Primitives.OperationalContextSemanticChangeType.ConstraintAdded or
            Primitives.OperationalContextSemanticChangeType.QuestionAdded or
            Primitives.OperationalContextSemanticChangeType.RiskAdded or
            Primitives.OperationalContextSemanticChangeType.DecisionAdded or
            Primitives.OperationalContextSemanticChangeType.ImportantDecisionIntroduced;
    }

    private static bool IsModifiedChange(OperationalContextSemanticChange change)
    {
        return change.Type is
            Primitives.OperationalContextSemanticChangeType.ItemChanged or
            Primitives.OperationalContextSemanticChangeType.ModifiedArchitecture or
            Primitives.OperationalContextSemanticChangeType.ModifiedConstraint or
            Primitives.OperationalContextSemanticChangeType.ModifiedWorkflow or
            Primitives.OperationalContextSemanticChangeType.ModifiedDecision or
            Primitives.OperationalContextSemanticChangeType.ModifiedUnderstanding or
            Primitives.OperationalContextSemanticChangeType.SectionChanged;
    }

    private static bool IsRemovedChange(OperationalContextSemanticChange change)
    {
        return change.Type is
            Primitives.OperationalContextSemanticChangeType.ItemRemoved or
            Primitives.OperationalContextSemanticChangeType.ConstraintRemoved or
            Primitives.OperationalContextSemanticChangeType.QuestionRemoved or
            Primitives.OperationalContextSemanticChangeType.RiskRemoved or
            Primitives.OperationalContextSemanticChangeType.DecisionRemoved or
            Primitives.OperationalContextSemanticChangeType.DecisionRetired or
            Primitives.OperationalContextSemanticChangeType.RationaleLostWarning or
            Primitives.OperationalContextSemanticChangeType.OpenDecisionResolved or
            Primitives.OperationalContextSemanticChangeType.LostUnderstanding or
            Primitives.OperationalContextSemanticChangeType.ResolvedUnderstanding or
            Primitives.OperationalContextSemanticChangeType.DuplicateRemoved or
            Primitives.OperationalContextSemanticChangeType.TransientRemoved;
    }

    private static TimeSpan? CalculateRevisionFrequency(IReadOnlyList<UnderstandingRevisionSnapshot> revisions)
    {
        DateTimeOffset[] timestamps = revisions
            .Select(revision => revision.LastUpdatedAt)
            .Where(timestamp => timestamp.HasValue)
            .Select(timestamp => timestamp!.Value)
            .Order()
            .ToArray();
        if (timestamps.Length < 2)
        {
            return null;
        }

        return TimeSpan.FromTicks((long)timestamps.Zip(timestamps.Skip(1), (left, right) => right - left).Average(span => span.Ticks));
    }

    private static DateTimeOffset? GetLastWriteTime(string path)
    {
        return File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) : null;
    }

    private static int TryParseHistoricalRevisionNumber(string fileName)
    {
        const string prefix = "operational_context.";
        const string suffix = ".md";
        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(fileName[prefix.Length..^suffix.Length], out int revision) ? revision : 0;
    }

    private sealed record RevisionEntry(
        OperationalContextDocument Document,
        UnderstandingRevisionSnapshot Snapshot);
}
