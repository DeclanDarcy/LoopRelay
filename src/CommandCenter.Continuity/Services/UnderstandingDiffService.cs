using CommandCenter.Continuity.Abstractions;
using CommandCenter.Continuity.Models;
using CommandCenter.Continuity.Primitives;

namespace CommandCenter.Continuity.Services;

public sealed class UnderstandingDiffService : IUnderstandingDiffService
{
    public IReadOnlyList<OperationalContextSemanticChange> Compare(
        OperationalContextDocument current,
        OperationalContextDocument proposed)
    {
        var changes = new List<OperationalContextSemanticChange>();
        CompareSection(changes, "Current Mental Model", current.CurrentMentalModel, proposed.CurrentMentalModel);
        CompareSection(changes, "Architecture", current.Architecture, proposed.Architecture);
        CompareSection(changes, "Authority Boundaries", current.AuthorityBoundaries, proposed.AuthorityBoundaries);
        CompareSection(changes, "Constraints", current.Constraints, proposed.Constraints);
        CompareSection(changes, "Stable Decisions", current.StableDecisions, proposed.StableDecisions);
        CompareSection(changes, "Decision Rationale", current.DecisionRationale, proposed.DecisionRationale);
        CompareSection(changes, "Open Questions", current.OpenQuestions, proposed.OpenQuestions);
        CompareSection(changes, "Active Risks", current.ActiveRisks, proposed.ActiveRisks);
        CompareSection(changes, "Recent Understanding Changes", current.RecentUnderstandingChanges, proposed.RecentUnderstandingChanges);

        HashSet<string> currentAdditional = current.AdditionalSections.Select(section => section.Heading).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> proposedAdditional = proposed.AdditionalSections.Select(section => section.Heading).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string heading in proposedAdditional.Except(currentAdditional, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(new OperationalContextSemanticChange
            {
                Type = OperationalContextSemanticChangeType.SectionAdded,
                Section = heading,
                Description = $"Additional section added: {heading}."
            });
        }

        foreach (string heading in currentAdditional.Except(proposedAdditional, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(new OperationalContextSemanticChange
            {
                Type = OperationalContextSemanticChangeType.SectionRemoved,
                Section = heading,
                Description = $"Additional section removed: {heading}."
            });
        }

        return changes;
    }

    private static void CompareSection(
        List<OperationalContextSemanticChange> changes,
        string section,
        IReadOnlyList<OperationalContextItem> current,
        IReadOnlyList<OperationalContextItem> proposed)
    {
        if (current.Count == 0 && proposed.Count > 0)
        {
            changes.Add(new OperationalContextSemanticChange
            {
                Type = OperationalContextSemanticChangeType.SectionAdded,
                Section = section,
                Description = $"Section populated: {section}."
            });
        }
        else if (current.Count > 0 && proposed.Count == 0)
        {
            changes.Add(new OperationalContextSemanticChange
            {
                Type = OperationalContextSemanticChangeType.SectionRemoved,
                Section = section,
                Description = $"Section emptied: {section}."
            });
        }

        Dictionary<string, OperationalContextItem> currentByText = FirstByNormalizedText(current);
        Dictionary<string, OperationalContextItem> proposedByText = FirstByNormalizedText(proposed);

        List<OperationalContextItem> currentUnmatched = currentByText.Values
            .Where(item => !proposedByText.ContainsKey(Normalize(item.Text)))
            .OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();
        List<OperationalContextItem> proposedUnmatched = proposedByText.Values
            .Where(item => !currentByText.ContainsKey(Normalize(item.Text)))
            .OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();

        IReadOnlyList<OperationalContextModification> modifications = MatchModifications(section, currentUnmatched, proposedUnmatched);
        var modifiedCurrent = modifications.Select(modification => modification.Previous).ToHashSet();
        var modifiedProposed = modifications.Select(modification => modification.Current).ToHashSet();

        foreach (OperationalContextModification modification in modifications)
        {
            changes.Add(new OperationalContextSemanticChange
            {
                Type = ModifiedTypeFor(section, modification.Current.Kind),
                Section = section,
                Description = $"Item modified in {section}: {modification.Previous.Text} -> {modification.Current.Text}",
                ItemId = string.IsNullOrWhiteSpace(modification.Current.Id) ? modification.Previous.Id : modification.Current.Id,
                PreviousState = modification.Previous.Text,
                CurrentState = modification.Current.Text,
                ModificationReason = modification.Reason,
                IdentityBasis = modification.IdentityBasis,
                SupportingEvidence = modification.SupportingEvidence
            });
        }

        foreach (OperationalContextItem item in proposedUnmatched.Where(item => !modifiedProposed.Contains(item)).OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(new OperationalContextSemanticChange
            {
                Type = AddedTypeFor(item),
                Section = section,
                Description = $"Item added to {section}: {item.Text}",
                ItemId = item.Id
            });
        }

        foreach (OperationalContextItem item in currentUnmatched.Where(item => !modifiedCurrent.Contains(item)).OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(new OperationalContextSemanticChange
            {
                Type = RemovedTypeFor(item),
                Section = section,
                Description = $"Item removed from {section}: {item.Text}",
                ItemId = item.Id
            });
        }

        if (current.Count > 0 && proposed.Count > 0 && current.Any(item => item.Kind == OperationalContextItemKind.DecisionRationale))
        {
            CompareDecisionRationale(changes, current, proposed);
        }
    }

    private static void CompareDecisionRationale(
        List<OperationalContextSemanticChange> changes,
        IReadOnlyList<OperationalContextItem> current,
        IReadOnlyList<OperationalContextItem> proposed)
    {
        Dictionary<string, OperationalContextItem> proposedByDecision = proposed
            .Select(item => (Item: item, DecisionKey: DecisionRationaleKey(item.Text)))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.DecisionKey))
            .GroupBy(entry => entry.DecisionKey!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Item, StringComparer.OrdinalIgnoreCase);

        foreach (OperationalContextItem item in current.OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase))
        {
            string? decisionKey = DecisionRationaleKey(item.Text);
            if (string.IsNullOrWhiteSpace(decisionKey) ||
                !proposedByDecision.TryGetValue(decisionKey, out OperationalContextItem? proposedItem) ||
                string.Equals(Normalize(item.Text), Normalize(proposedItem.Text), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            changes.Add(new OperationalContextSemanticChange
            {
                Type = OperationalContextSemanticChangeType.RationaleChanged,
                Section = "Decision Rationale",
                Description = $"Decision rationale changed for `{decisionKey}`.",
                ItemId = proposedItem.Id
            });
        }
    }

    private static OperationalContextSemanticChangeType AddedTypeFor(OperationalContextItemKind kind)
    {
        return kind switch
        {
            OperationalContextItemKind.Constraint => OperationalContextSemanticChangeType.ConstraintAdded,
            OperationalContextItemKind.OpenQuestion => OperationalContextSemanticChangeType.QuestionAdded,
            OperationalContextItemKind.ActiveRisk => OperationalContextSemanticChangeType.RiskAdded,
            OperationalContextItemKind.StableDecision => OperationalContextSemanticChangeType.ImportantDecisionIntroduced,
            OperationalContextItemKind.DecisionRationale => OperationalContextSemanticChangeType.RationaleChanged,
            _ => OperationalContextSemanticChangeType.ItemAdded
        };
    }

    private static OperationalContextSemanticChangeType RemovedTypeFor(OperationalContextItemKind kind)
    {
        return kind switch
        {
            OperationalContextItemKind.Constraint => OperationalContextSemanticChangeType.ConstraintRemoved,
            OperationalContextItemKind.OpenQuestion => OperationalContextSemanticChangeType.QuestionRemoved,
            OperationalContextItemKind.ActiveRisk => OperationalContextSemanticChangeType.RiskRemoved,
            OperationalContextItemKind.StableDecision => OperationalContextSemanticChangeType.DecisionRetired,
            OperationalContextItemKind.DecisionRationale => OperationalContextSemanticChangeType.RationaleLostWarning,
            _ => OperationalContextSemanticChangeType.ItemRemoved
        };
    }

    private static OperationalContextSemanticChangeType ModifiedTypeFor(
        string section,
        OperationalContextItemKind kind)
    {
        return kind switch
        {
            OperationalContextItemKind.Architecture or OperationalContextItemKind.AuthorityBoundary =>
                OperationalContextSemanticChangeType.ModifiedArchitecture,
            OperationalContextItemKind.Constraint =>
                OperationalContextSemanticChangeType.ModifiedConstraint,
            OperationalContextItemKind.StableDecision or OperationalContextItemKind.DecisionRationale =>
                OperationalContextSemanticChangeType.ModifiedDecision,
            OperationalContextItemKind.OpenQuestion or OperationalContextItemKind.ActiveRisk =>
                OperationalContextSemanticChangeType.ModifiedWorkflow,
            OperationalContextItemKind.MentalModel or OperationalContextItemKind.RecentChange =>
                OperationalContextSemanticChangeType.ModifiedUnderstanding,
            _ when section.Contains("workflow", StringComparison.OrdinalIgnoreCase) =>
                OperationalContextSemanticChangeType.ModifiedWorkflow,
            _ => OperationalContextSemanticChangeType.ItemChanged
        };
    }

    private static OperationalContextSemanticChangeType AddedTypeFor(OperationalContextItem item)
    {
        if (item.Kind == OperationalContextItemKind.OpenQuestion &&
            item.Text.StartsWith("Open decision:", StringComparison.OrdinalIgnoreCase))
        {
            return OperationalContextSemanticChangeType.OpenDecisionPreserved;
        }

        return AddedTypeFor(item.Kind);
    }

    private static OperationalContextSemanticChangeType RemovedTypeFor(OperationalContextItem item)
    {
        if (item.Kind == OperationalContextItemKind.OpenQuestion &&
            item.Text.StartsWith("Open decision:", StringComparison.OrdinalIgnoreCase))
        {
            return OperationalContextSemanticChangeType.OpenDecisionResolved;
        }

        return RemovedTypeFor(item.Kind);
    }

    private static Dictionary<string, OperationalContextItem> FirstByNormalizedText(IReadOnlyList<OperationalContextItem> items)
    {
        return items
            .GroupBy(item => Normalize(item.Text), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<OperationalContextModification> MatchModifications(
        string section,
        IReadOnlyList<OperationalContextItem> current,
        IReadOnlyList<OperationalContextItem> proposed)
    {
        var modifications = new List<OperationalContextModification>();
        var matchedProposed = new HashSet<OperationalContextItem>();

        foreach (OperationalContextItem previous in current)
        {
            OperationalContextModification? match = proposed
                .Where(item => !matchedProposed.Contains(item))
                .Select(item => TryCreateModification(section, previous, item, current, proposed))
                .Where(modification => modification is not null)
                .OrderBy(modification => modification!.Precedence)
                .ThenBy(modification => modification!.Current.Text, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (match is null)
            {
                continue;
            }

            matchedProposed.Add(match.Current);
            modifications.Add(match);
        }

        return modifications
            .OrderBy(modification => modification.Previous.Text, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static OperationalContextModification? TryCreateModification(
        string section,
        OperationalContextItem previous,
        OperationalContextItem current,
        IReadOnlyList<OperationalContextItem> previousCandidates,
        IReadOnlyList<OperationalContextItem> currentCandidates)
    {
        if (previous.Kind != current.Kind)
        {
            return null;
        }

        string? identityBasis = null;
        string? reason = null;
        int precedence = int.MaxValue;
        var evidence = new List<string>
        {
            $"Section: {section}",
            $"Previous item id: {previous.Id}",
            $"Current item id: {current.Id}",
            $"Previous state: {previous.Text}",
            $"Current state: {current.Text}"
        };

        if (!string.IsNullOrWhiteSpace(previous.Id) &&
            string.Equals(previous.Id, current.Id, StringComparison.OrdinalIgnoreCase))
        {
            identityBasis = "persistent-item-id";
            reason = "The operational-context item kept the same backend item id while its text changed.";
            precedence = 0;
        }
        else if (!string.IsNullOrWhiteSpace(previous.SourceRelativePath) &&
            string.Equals(previous.SourceRelativePath, current.SourceRelativePath, StringComparison.OrdinalIgnoreCase) &&
            IsUniqueSourceReference(previous.SourceRelativePath, previous.Kind, previousCandidates) &&
            IsUniqueSourceReference(current.SourceRelativePath, current.Kind, currentCandidates))
        {
            identityBasis = "source-reference";
            reason = "The operational-context item kept the same unique source artifact reference while its text changed.";
            precedence = 1;
            evidence.Add($"Source reference: {current.SourceRelativePath}");
        }
        else
        {
            string? previousSemanticKey = SemanticLineageKey(previous);
            string? currentSemanticKey = SemanticLineageKey(current);
            if (!string.IsNullOrWhiteSpace(previousSemanticKey) &&
                string.Equals(previousSemanticKey, currentSemanticKey, StringComparison.OrdinalIgnoreCase))
            {
                identityBasis = "section-semantic-lineage";
                reason = "The operational-context item kept the same section, kind, and backend-owned semantic lineage key while its text changed.";
                precedence = 2;
                evidence.Add($"Semantic lineage key: {currentSemanticKey}");
            }
        }

        return identityBasis is null
            ? null
            : new OperationalContextModification(previous, current, identityBasis, reason!, evidence, precedence);
    }

    private static bool IsUniqueSourceReference(
        string? sourceRelativePath,
        OperationalContextItemKind kind,
        IReadOnlyList<OperationalContextItem> items)
    {
        if (string.IsNullOrWhiteSpace(sourceRelativePath))
        {
            return false;
        }

        return items.Count(item =>
            item.Kind == kind &&
            string.Equals(item.SourceRelativePath, sourceRelativePath, StringComparison.OrdinalIgnoreCase)) == 1;
    }

    private static string? SemanticLineageKey(OperationalContextItem item)
    {
        string? decisionKey = DecisionRationaleKey(item.Text);
        if (!string.IsNullOrWhiteSpace(decisionKey))
        {
            return $"rationale:{Normalize(decisionKey)}";
        }

        string withoutPrefix = StripKnownPrefix(item.Text);
        string normalized = Normalize(RemoveRationaleSuffix(withoutPrefix));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        foreach (string marker in new[] { " must ", " should ", " owns ", " own ", " is ", " are ", " remains ", " remain " })
        {
            int index = $" {normalized} ".IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                string candidate = normalized[..index].Trim();
                return candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 3
                    ? $"{item.Kind}:{candidate}"
                    : null;
            }
        }

        string[] words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length >= 5
            ? $"{item.Kind}:{string.Join(' ', words.Take(5))}"
            : null;
    }

    private static string StripKnownPrefix(string value)
    {
        foreach (string prefix in new[] { "Decision:", "Open decision:", "Open question:", "Risk:", "Constraint:" })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return value[prefix.Length..].Trim();
            }
        }

        return value.Trim();
    }

    private static string RemoveRationaleSuffix(string value)
    {
        foreach (string marker in new[] { " because ", " since ", " so that " })
        {
            int index = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                return value[..index].Trim();
            }
        }

        return value;
    }

    private static string? DecisionRationaleKey(string text)
    {
        const string prefix = "Rationale for `";
        if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        int end = text.IndexOf("`:", prefix.Length, StringComparison.Ordinal);
        return end <= prefix.Length ? null : text[prefix.Length..end].Trim();
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record OperationalContextModification(
        OperationalContextItem Previous,
        OperationalContextItem Current,
        string IdentityBasis,
        string Reason,
        IReadOnlyList<string> SupportingEvidence,
        int Precedence);
}
