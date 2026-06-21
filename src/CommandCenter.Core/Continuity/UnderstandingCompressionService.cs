namespace CommandCenter.Core.Continuity;

public sealed class UnderstandingCompressionService : IUnderstandingCompressionService
{
    private const int RecentUnderstandingChangeLimit = 12;

    public OperationalContextCompressionResult Compress(
        OperationalContextDocument current,
        OperationalContextDocument proposed)
    {
        var noiseRemovedIndicators = new List<string>();
        IReadOnlyList<OperationalContextItem> compressedRecentChanges = CompressRecentChanges(proposed.RecentUnderstandingChanges, noiseRemovedIndicators);
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

        foreach (OperationalContextItem item in recentChanges.AsEnumerable().Reverse())
        {
            string normalized = Normalize(item.Text);
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

        foreach (OperationalContextItem removed in retained.Take(retained.Count - RecentUnderstandingChangeLimit))
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
