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
            ArchitectureTrend = CompareItems(revisions, document => document.Architecture),
            ConstraintTrend = CompareItems(revisions, document => document.Constraints),
            DecisionTrend = CompareItems(revisions, document => document.StableDecisions),
            RationaleTrend = CompareItems(revisions, document => document.DecisionRationale),
            OpenQuestionTrend = CompareQuestions(revisions),
            ActiveRiskTrend = CompareRisks(revisions),
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
                .ToArray()
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

    private static ContinuityTrend CompareItems(
        IReadOnlyList<RevisionEntry> revisions,
        Func<OperationalContextDocument, IEnumerable<OperationalContextItem>> getItems)
    {
        if (revisions.Count < 2)
        {
            return new ContinuityTrend();
        }

        HashSet<string> previous = ToNormalizedSet(getItems(revisions[^2].Document));
        HashSet<string> current = ToNormalizedSet(getItems(revisions[^1].Document));
        return new ContinuityTrend
        {
            AddedCount = current.Except(previous, StringComparer.OrdinalIgnoreCase).Count(),
            RemovedCount = previous.Except(current, StringComparer.OrdinalIgnoreCase).Count(),
            LostCount = previous.Except(current, StringComparer.OrdinalIgnoreCase).Count()
        };
    }

    private static ContinuityTrend CompareQuestions(IReadOnlyList<RevisionEntry> revisions)
    {
        return CompareActiveItemsWithResolutionEvidence(
            revisions,
            document => document.OpenQuestions,
            "resolved question");
    }

    private static ContinuityTrend CompareRisks(IReadOnlyList<RevisionEntry> revisions)
    {
        return CompareActiveItemsWithResolutionEvidence(
            revisions,
            document => document.ActiveRisks,
            "retired risk");
    }

    private static ContinuityTrend CompareActiveItemsWithResolutionEvidence(
        IReadOnlyList<RevisionEntry> revisions,
        Func<OperationalContextDocument, IEnumerable<OperationalContextItem>> getItems,
        string resolutionPrefix)
    {
        if (revisions.Count < 2)
        {
            return new ContinuityTrend();
        }

        HashSet<string> previous = ToNormalizedSet(getItems(revisions[^2].Document));
        HashSet<string> current = ToNormalizedSet(getItems(revisions[^1].Document));
        string[] removed = previous.Except(current, StringComparer.OrdinalIgnoreCase).ToArray();
        HashSet<string> resolutionEvidence = ToNormalizedSet(revisions[^1].Document.RecentUnderstandingChanges
            .Where(item => item.Text.StartsWith(resolutionPrefix, StringComparison.OrdinalIgnoreCase)));
        int resolved = removed.Count(item => resolutionEvidence.Any(evidence => evidence.Contains(item, StringComparison.OrdinalIgnoreCase)));
        return new ContinuityTrend
        {
            AddedCount = current.Except(previous, StringComparer.OrdinalIgnoreCase).Count(),
            RemovedCount = removed.Length,
            ResolvedCount = resolved,
            LostCount = removed.Length - resolved
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
