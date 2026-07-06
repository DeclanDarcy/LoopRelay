using LoopRelay.Continuity.Abstractions;
using LoopRelay.Continuity.Models;
using LoopRelay.Continuity.Primitives;

namespace LoopRelay.Continuity.Services;

public sealed class UnderstandingCompressionService : IUnderstandingCompressionService
{
    private const int RecentUnderstandingChangeLimit = 12;

    public OperationalContextCompressionResult Compress(
        OperationalContextDocument current,
        OperationalContextDocument proposed)
    {
        var noiseRemovedIndicators = new List<string>();
        var compressionOutcomes = new List<OperationalContextCompressionOutcome>();
        IReadOnlyList<OperationalContextItem> compressedRecentChanges = CompressRecentChanges(
            proposed.RecentUnderstandingChanges,
            noiseRemovedIndicators,
            compressionOutcomes);
        OperationalContextItem[] resolvedQuestions = SelectItemsWithExplicitOutcome(
            proposed.OpenQuestions,
            compressedRecentChanges,
            IsQuestionResolutionEvidence).ToArray();
        OperationalContextItem[] retiredRisks = SelectItemsWithExplicitOutcome(
            proposed.ActiveRisks,
            compressedRecentChanges,
            IsRiskRetirementEvidence).ToArray();
        var compressedDocument = new OperationalContextDocument
        {
            Title = proposed.Title,
            CurrentMentalModel = proposed.CurrentMentalModel,
            Architecture = proposed.Architecture,
            AuthorityBoundaries = proposed.AuthorityBoundaries,
            Constraints = proposed.Constraints,
            StableDecisions = proposed.StableDecisions,
            DecisionRationale = proposed.DecisionRationale,
            OpenQuestions = proposed.OpenQuestions
                .Where(item => !ContainsEquivalent(resolvedQuestions, item))
                .ToArray(),
            ActiveRisks = proposed.ActiveRisks
                .Where(item => !ContainsEquivalent(retiredRisks, item))
                .ToArray(),
            AdditionalSections = proposed.AdditionalSections,
            RecentUnderstandingChanges = compressedRecentChanges
        };

        List<string> warnings = BuildRetentionWarnings(current, compressedDocument, resolvedQuestions, retiredRisks).ToList();
        string[] stableWarnings = warnings
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

        HashSet<string> currentTexts = AllItems(current)
            .Select(item => Normalize(item.Text))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> proposedTexts = AllItems(compressedDocument)
            .Select(item => Normalize(item.Text))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        compressionOutcomes.AddRange(BuildItemOutcomes(current, compressedDocument, resolvedQuestions, retiredRisks));
        int compressedNoiseCount = proposed.RecentUnderstandingChanges.Count - compressedDocument.RecentUnderstandingChanges.Count;
        string[] revisionSummary = BuildRevisionSummary(
            compressedDocument,
            currentTexts,
            resolvedQuestions,
            retiredRisks,
            compressedNoiseCount,
            warnings).ToArray();

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
            ResolvedQuestionCount = resolvedQuestions.Length,
            RetiredRiskCount = retiredRisks.Length,
            WarningCount = warnings.Count,
            Warnings = warnings,
            RevisionSummary = revisionSummary,
            NoiseRemovedIndicators = noiseRemovedIndicators,
            StableUnderstandingRetentionWarnings = stableWarnings,
            ItemOutcomes = compressionOutcomes
        };

        return new OperationalContextCompressionResult
        {
            Document = compressedDocument,
            Summary = summary
        };
    }

    private static IReadOnlyList<OperationalContextItem> CompressRecentChanges(
        IReadOnlyList<OperationalContextItem> recentChanges,
        List<string> noiseRemovedIndicators,
        List<OperationalContextCompressionOutcome> compressionOutcomes)
    {
        var retained = new List<OperationalContextItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (OperationalContextItem item in recentChanges.AsEnumerable().Reverse())
        {
            string normalized = Normalize(item.Text);
            if (!seen.Add(normalized))
            {
                noiseRemovedIndicators.Add($"Repeated recent-change detail removed: {item.Text}");
                compressionOutcomes.Add(CreateOutcome(
                    "DuplicateRemoved",
                    item,
                    "recent-change-duplicate-removal",
                    "Normalized recent-change text must be unique within the retained proposal window.",
                    "Repeated recent-change detail was removed.",
                    [$"Normalized text: {normalized}"]));
                continue;
            }

            if (IsTransientExecutionNoise(item.Text) && retained.Count >= RecentUnderstandingChangeLimit / 2)
            {
                noiseRemovedIndicators.Add($"Transient execution detail compressed: {item.Text}");
                compressionOutcomes.Add(CreateOutcome(
                    "TransientRemoved",
                    item,
                    "transient-execution-noise-removal",
                    $"Transient execution detail is removed after {RecentUnderstandingChangeLimit / 2} retained recent-change item(s).",
                    "Transient execution status is historical noise after enough recent context is retained.",
                    [$"Retained recent-change count before removal: {retained.Count}"]));
                continue;
            }

            retained.Add(item);
        }

        retained.Reverse();
        if (retained.Count <= RecentUnderstandingChangeLimit)
        {
            return retained;
        }

        foreach (OperationalContextItem removed in retained.Take(retained.Count - RecentUnderstandingChangeLimit))
        {
            noiseRemovedIndicators.Add($"Older recent-change detail compressed: {removed.Text}");
            compressionOutcomes.Add(CreateOutcome(
                "Compressed",
                removed,
                "recent-change-window-limit",
                $"Recent understanding changes retain at most {RecentUnderstandingChangeLimit} item(s).",
                "Older recent-change detail was compressed to keep operational context reviewable.",
                [$"Recent-change count before limit: {retained.Count}"]));
        }

        return retained
            .Skip(retained.Count - RecentUnderstandingChangeLimit)
            .ToArray();
    }

    private static IEnumerable<OperationalContextCompressionOutcome> BuildItemOutcomes(
        OperationalContextDocument current,
        OperationalContextDocument compressedDocument,
        IReadOnlyList<OperationalContextItem> resolvedQuestions,
        IReadOnlyList<OperationalContextItem> retiredRisks)
    {
        HashSet<string> currentTexts = AllItems(current)
            .Select(item => Normalize(item.Text))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> compressedTexts = AllItems(compressedDocument)
            .Select(item => Normalize(item.Text))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (OperationalContextItem item in AllItems(compressedDocument))
        {
            bool retained = currentTexts.Contains(Normalize(item.Text));
            yield return CreateOutcome(
                retained ? "Retained" : "Added",
                item,
                retained ? "normalized-text-retention" : "proposal-addition",
                "Normalized item text is compared across current and compressed proposed operational context.",
                retained
                    ? "Item remains present after compression."
                    : "Item is present in the compressed proposal and was not in current context.",
                [$"Normalized text: {Normalize(item.Text)}"]);
        }

        foreach (OperationalContextItem item in AllItems(current).Where(item => !compressedTexts.Contains(Normalize(item.Text))))
        {
            OperationalContextCompressionOutcome? explicitOutcome = BuildExplicitRemovalOutcome(item, resolvedQuestions, retiredRisks);
            if (explicitOutcome is not null)
            {
                yield return explicitOutcome;
                continue;
            }

            yield return CreateOutcome(
                "Removed",
                item,
                "retention-warning-check",
                "Current stable or active understanding should remain unless the proposal contains matching text or explicit resolution evidence.",
                "Item from current context is absent from compressed proposal.",
                [$"Current normalized text: {Normalize(item.Text)}"]);
        }
    }

    private static OperationalContextCompressionOutcome? BuildExplicitRemovalOutcome(
        OperationalContextItem item,
        IReadOnlyList<OperationalContextItem> resolvedQuestions,
        IReadOnlyList<OperationalContextItem> retiredRisks)
    {
        if (ContainsEquivalent(resolvedQuestions, item))
        {
            return CreateOutcome(
                "ResolvedQuestion",
                item,
                "explicit-question-resolution",
                "Open questions are removed only when recent understanding contains explicit resolution evidence with matching meaningful tokens.",
                "Open question was explicitly resolved by proposal evidence.",
                [$"Resolved question: {item.Text}"]);
        }

        if (ContainsEquivalent(retiredRisks, item))
        {
            return CreateOutcome(
                "RetiredRisk",
                item,
                "explicit-risk-retirement",
                "Active risks are removed only when recent understanding contains explicit retirement evidence with matching meaningful tokens.",
                "Active risk was explicitly retired by proposal evidence.",
                [$"Retired risk: {item.Text}"]);
        }

        return null;
    }

    private static OperationalContextCompressionOutcome CreateOutcome(
        string outcome,
        OperationalContextItem item,
        string rule,
        string threshold,
        string rationale,
        IReadOnlyList<string> evidence)
    {
        return new OperationalContextCompressionOutcome
        {
            Outcome = outcome,
            ItemKind = item.Kind.ToString(),
            ItemText = item.Text,
            Rule = rule,
            Threshold = threshold,
            Rationale = rationale,
            Evidence = evidence
        };
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
        OperationalContextDocument proposed,
        IReadOnlyList<OperationalContextItem> explicitlyResolvedQuestions,
        IReadOnlyList<OperationalContextItem> explicitlyRetiredRisks)
    {
        foreach (string warning in MissingWarnings("Architecture", current.Architecture, proposed.Architecture))
        {
            yield return warning;
        }

        foreach (string warning in MissingWarnings("Constraint", current.Constraints, proposed.Constraints))
        {
            yield return warning;
        }

        foreach (string warning in MissingWarnings(
            "Open question",
            current.OpenQuestions.Where(item => !ContainsEquivalent(explicitlyResolvedQuestions, item)).ToArray(),
            proposed.OpenQuestions))
        {
            yield return warning;
        }

        foreach (string warning in MissingWarnings(
            "Active risk",
            current.ActiveRisks.Where(item => !ContainsEquivalent(explicitlyRetiredRisks, item)).ToArray(),
            proposed.ActiveRisks))
        {
            yield return warning;
        }

        foreach (string warning in MissingWarnings("Stable decision", current.StableDecisions, proposed.StableDecisions))
        {
            yield return warning;
        }

        foreach (string warning in MissingWarnings("Decision rationale", current.DecisionRationale, proposed.DecisionRationale))
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
        HashSet<string> proposedTexts = proposed
            .Select(item => Normalize(item.Text))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (OperationalContextItem item in current.Where(item => !proposedTexts.Contains(Normalize(item.Text))))
        {
            yield return $"{label} disappeared without explicit resolution: {item.Text}";
        }
    }

    private static IEnumerable<OperationalContextItem> SelectItemsWithExplicitOutcome(
        IReadOnlyList<OperationalContextItem> candidates,
        IReadOnlyList<OperationalContextItem> evidenceItems,
        Func<string, bool> isOutcomeEvidence)
    {
        foreach (OperationalContextItem candidate in candidates)
        {
            string[] candidateTokens = MeaningfulTokens(candidate.Text).ToArray();
            if (candidateTokens.Length == 0)
            {
                continue;
            }

            if (evidenceItems.Any(evidence =>
                    isOutcomeEvidence(evidence.Text) &&
                    candidateTokens.Count(token => evidence.Text.Contains(token, StringComparison.OrdinalIgnoreCase)) >=
                    Math.Min(3, candidateTokens.Length)))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<string> BuildRevisionSummary(
        OperationalContextDocument compressedDocument,
        HashSet<string> currentTexts,
        IReadOnlyList<OperationalContextItem> resolvedQuestions,
        IReadOnlyList<OperationalContextItem> retiredRisks,
        int compressedNoiseCount,
        IReadOnlyList<string> warnings)
    {
        int addedPermanent = AllItems(compressedDocument)
            .Count(item =>
                !currentTexts.Contains(Normalize(item.Text)) &&
                Classify(item) == OperationalContextInformationTier.PermanentUnderstanding);
        int addedActive = AllItems(compressedDocument)
            .Count(item =>
                !currentTexts.Contains(Normalize(item.Text)) &&
                Classify(item) == OperationalContextInformationTier.ActiveUnderstanding);

        if (addedPermanent > 0)
        {
            yield return $"{addedPermanent} durable understanding item(s) added.";
        }

        if (addedActive > 0)
        {
            yield return $"{addedActive} active understanding item(s) added.";
        }

        if (resolvedQuestions.Count > 0)
        {
            yield return $"{resolvedQuestions.Count} open question(s) explicitly resolved.";
        }

        if (retiredRisks.Count > 0)
        {
            yield return $"{retiredRisks.Count} active risk(s) explicitly retired.";
        }

        if (compressedNoiseCount > 0)
        {
            yield return $"{compressedNoiseCount} historical-noise item(s) compressed.";
        }

        if (warnings.Count > 0)
        {
            yield return $"{warnings.Count} retention warning(s) require review.";
        }

        if (addedPermanent == 0 &&
            addedActive == 0 &&
            resolvedQuestions.Count == 0 &&
            retiredRisks.Count == 0 &&
            compressedNoiseCount == 0 &&
            warnings.Count == 0)
        {
            yield return "No material compression changes detected.";
        }
    }

    private static bool IsQuestionResolutionEvidence(string text)
    {
        return text.Contains("resolved question", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("question resolved", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("resolved open question", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("open question resolved", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRiskRetirementEvidence(string text)
    {
        return text.Contains("retired risk", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("risk retired", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("resolved risk", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("risk resolved", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsEquivalent(
        IReadOnlyList<OperationalContextItem> items,
        OperationalContextItem item)
    {
        return items.Any(candidate => string.Equals(Normalize(candidate.Text), Normalize(item.Text), StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> MeaningfulTokens(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a",
            "an",
            "and",
            "are",
            "be",
            "before",
            "can",
            "do",
            "does",
            "for",
            "how",
            "in",
            "is",
            "of",
            "or",
            "should",
            "the",
            "to",
            "when",
            "while",
            "with"
        };

        foreach (string token in Normalize(text)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim('`', '.', ',', ':', ';', '?', '!', '"', '\'')))
        {
            if (token.Length >= 4 && !stopWords.Contains(token))
            {
                yield return token;
            }
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
