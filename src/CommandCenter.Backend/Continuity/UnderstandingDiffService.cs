namespace CommandCenter.Backend.Continuity;

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

        var currentAdditional = current.AdditionalSections.Select(section => section.Heading).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var proposedAdditional = proposed.AdditionalSections.Select(section => section.Heading).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var heading in proposedAdditional.Except(currentAdditional, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(new OperationalContextSemanticChange
            {
                Type = OperationalContextSemanticChangeType.SectionAdded,
                Section = heading,
                Description = $"Additional section added: {heading}."
            });
        }

        foreach (var heading in currentAdditional.Except(proposedAdditional, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
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

        var currentByText = current.ToDictionary(item => Normalize(item.Text), item => item, StringComparer.OrdinalIgnoreCase);
        var proposedByText = proposed.ToDictionary(item => Normalize(item.Text), item => item, StringComparer.OrdinalIgnoreCase);

        foreach (var item in proposedByText.Values.Where(item => !currentByText.ContainsKey(Normalize(item.Text))).OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(new OperationalContextSemanticChange
            {
                Type = AddedTypeFor(item.Kind),
                Section = section,
                Description = $"Item added to {section}: {item.Text}",
                ItemId = item.Id
            });
        }

        foreach (var item in currentByText.Values.Where(item => !proposedByText.ContainsKey(Normalize(item.Text))).OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(new OperationalContextSemanticChange
            {
                Type = RemovedTypeFor(item.Kind),
                Section = section,
                Description = $"Item removed from {section}: {item.Text}",
                ItemId = item.Id
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
            OperationalContextItemKind.StableDecision => OperationalContextSemanticChangeType.DecisionAdded,
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
            OperationalContextItemKind.StableDecision => OperationalContextSemanticChangeType.DecisionRemoved,
            _ => OperationalContextSemanticChangeType.ItemRemoved
        };
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
