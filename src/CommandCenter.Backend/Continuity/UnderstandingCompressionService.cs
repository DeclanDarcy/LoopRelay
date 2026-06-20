namespace CommandCenter.Backend.Continuity;

public sealed class UnderstandingCompressionService : IUnderstandingCompressionService
{
    private const int RecentUnderstandingChangeLimit = 12;

    public OperationalContextCompressionResult Compress(
        OperationalContextDocument current,
        OperationalContextDocument proposed)
    {
        var noiseRemovedIndicators = new List<string>();
        var compressedRecentChanges = CompressRecentChanges(proposed.RecentUnderstandingChanges, noiseRemovedIndicators);
        var compressedDocument = new OperationalContextDocument
        {
            Title = proposed.Title,
            CurrentMentalModel = proposed.CurrentMentalModel,
            Architecture = proposed.Architecture,
            AuthorityBoundaries = proposed.AuthorityBoundaries,
            Constraints = proposed.Constraints,
            StableDecisions = proposed.StableDecisions,
            DecisionRationale = proposed.DecisionRationale,
            OpenQuestions = proposed.OpenQuestions,
            ActiveRisks = proposed.ActiveRisks,
            AdditionalSections = proposed.AdditionalSections,
            RecentUnderstandingChanges = compressedRecentChanges
        };

        var warnings = BuildRetentionWarnings(current, compressedDocument).ToList();
        var stableWarnings = warnings
            .Where(warning =>
                warning.Contains("Architecture", StringComparison.OrdinalIgnoreCase) ||
                warning.Contains("Constraint", StringComparison.OrdinalIgnoreCase) ||
                warning.Contains("Open question", StringComparison.OrdinalIgnoreCase) ||
                warning.Contains("Active risk", StringComparison.OrdinalIgnoreCase) ||
                warning.Contains("Stable decision", StringComparison.OrdinalIgnoreCase) ||
                warning.Contains("Decision rationale", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (CountItems(compressedDocument) > CountItems(current) + 15 ||
            compressedDocument.RecentUnderstandingChanges.Count >= RecentUnderstandingChangeLimit)
        {
            warnings.Add("Proposal growth indicates possible historical replay; review recent understanding changes for transient execution detail.");
        }

        var currentTexts = AllItems(current)
            .Select(item => Normalize(item.Text))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var proposedTexts = AllItems(compressedDocument)
            .Select(item => Normalize(item.Text))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var compressedNoiseCount = proposed.RecentUnderstandingChanges.Count - compressedDocument.RecentUnderstandingChanges.Count;

        var summary = new OperationalContextCompressionSummary
        {
            PreservedItemCount = AllItems(compressedDocument).Count(item => currentTexts.Contains(Normalize(item.Text))),
            AddedItemCount = AllItems(compressedDocument).Count(item => !currentTexts.Contains(Normalize(item.Text))),
            ModifiedItemCount = 0,
            RemovedItemCount = AllItems(current).Count(item => !proposedTexts.Contains(Normalize(item.Text))),
            CompressedItemCount = Math.Max(0, compressedNoiseCount),
            PermanentUnderstandingItemCount = CountTier(compressedDocument, OperationalContextInformationTier.PermanentUnderstanding),
            ActiveUnderstandingItemCount = CountTier(compressedDocument, OperationalContextInformationTier.ActiveUnderstanding),
            HistoricalUnderstandingItemCount = CountTier(compressedDocument, OperationalContextInformationTier.HistoricalUnderstanding),
            HistoricalNoiseItemCount = Math.Max(0, compressedNoiseCount),
            WarningCount = warnings.Count,
            Warnings = warnings,
            NoiseRemovedIndicators = noiseRemovedIndicators,
            StableUnderstandingRetentionWarnings = stableWarnings
        };

        return new OperationalContextCompressionResult
        {
            Document = compressedDocument,
            Summary = summary
        };
    }

    private static IReadOnlyList<OperationalContextItem> CompressRecentChanges(
        IReadOnlyList<OperationalContextItem> recentChanges,
        List<string> noiseRemovedIndicators)
    {
        var retained = new List<OperationalContextItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in recentChanges.AsEnumerable().Reverse())
        {
            var normalized = Normalize(item.Text);
            if (!seen.Add(normalized))
            {
                noiseRemovedIndicators.Add($"Repeated recent-change detail removed: {item.Text}");
                continue;
            }

            if (IsTransientExecutionNoise(item.Text) && retained.Count >= RecentUnderstandingChangeLimit / 2)
            {
                noiseRemovedIndicators.Add($"Transient execution detail compressed: {item.Text}");
                continue;
            }

            retained.Add(item);
        }

        retained.Reverse();
        if (retained.Count <= RecentUnderstandingChangeLimit)
        {
            return retained;
        }

        foreach (var removed in retained.Take(retained.Count - RecentUnderstandingChangeLimit))
        {
            noiseRemovedIndicators.Add($"Older recent-change detail compressed: {removed.Text}");
        }

        return retained
            .Skip(retained.Count - RecentUnderstandingChangeLimit)
            .ToArray();
    }

    private static bool IsTransientExecutionNoise(string text)
    {
        return text.Contains("Recent execution for", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("recorded with state", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("passed:", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("build passed", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> BuildRetentionWarnings(
        OperationalContextDocument current,
        OperationalContextDocument proposed)
    {
        foreach (var warning in MissingWarnings("Architecture", current.Architecture, proposed.Architecture))
        {
            yield return warning;
        }

        foreach (var warning in MissingWarnings("Constraint", current.Constraints, proposed.Constraints))
        {
            yield return warning;
        }

        foreach (var warning in MissingWarnings("Open question", current.OpenQuestions, proposed.OpenQuestions))
        {
            yield return warning;
        }

        foreach (var warning in MissingWarnings("Active risk", current.ActiveRisks, proposed.ActiveRisks))
        {
            yield return warning;
        }

        foreach (var warning in MissingWarnings("Stable decision", current.StableDecisions, proposed.StableDecisions))
        {
            yield return warning;
        }

        foreach (var warning in MissingWarnings("Decision rationale", current.DecisionRationale, proposed.DecisionRationale))
        {
            yield return warning;
        }

        if (current.AdditionalSections.Count > 0 && proposed.AdditionalSections.Count == 0)
        {
            yield return "Unknown operational-context sections disappeared; reviewer should confirm no hand-written context was lost.";
        }
    }

    private static IEnumerable<string> MissingWarnings(
        string label,
        IReadOnlyList<OperationalContextItem> current,
        IReadOnlyList<OperationalContextItem> proposed)
    {
        var proposedTexts = proposed
            .Select(item => Normalize(item.Text))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in current.Where(item => !proposedTexts.Contains(Normalize(item.Text))))
        {
            yield return $"{label} disappeared without explicit resolution: {item.Text}";
        }
    }

    private static int CountTier(
        OperationalContextDocument document,
        OperationalContextInformationTier tier)
    {
        return AllItems(document).Count(item => Classify(item) == tier);
    }

    private static OperationalContextInformationTier Classify(OperationalContextItem item)
    {
        return item.Kind switch
        {
            OperationalContextItemKind.MentalModel or
            OperationalContextItemKind.Architecture or
            OperationalContextItemKind.AuthorityBoundary or
            OperationalContextItemKind.Constraint or
            OperationalContextItemKind.StableDecision or
            OperationalContextItemKind.DecisionRationale => OperationalContextInformationTier.PermanentUnderstanding,
            OperationalContextItemKind.OpenQuestion or
            OperationalContextItemKind.ActiveRisk => OperationalContextInformationTier.ActiveUnderstanding,
            OperationalContextItemKind.RecentChange when IsTransientExecutionNoise(item.Text) => OperationalContextInformationTier.HistoricalNoise,
            OperationalContextItemKind.RecentChange => OperationalContextInformationTier.HistoricalUnderstanding,
            _ => OperationalContextInformationTier.HistoricalUnderstanding
        };
    }

    private static IReadOnlyList<OperationalContextItem> AllItems(OperationalContextDocument document)
    {
        return document.CurrentMentalModel
            .Concat(document.Architecture)
            .Concat(document.AuthorityBoundaries)
            .Concat(document.Constraints)
            .Concat(document.StableDecisions)
            .Concat(document.DecisionRationale)
            .Concat(document.OpenQuestions)
            .Concat(document.ActiveRisks)
            .Concat(document.RecentUnderstandingChanges)
            .Concat(document.AdditionalSections.SelectMany(section => section.Items))
            .ToArray();
    }

    private static int CountItems(OperationalContextDocument document)
    {
        return AllItems(document).Count;
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
