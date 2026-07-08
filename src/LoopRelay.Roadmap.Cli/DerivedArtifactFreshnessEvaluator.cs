namespace LoopRelay.Roadmap.Cli;

internal static class DerivedArtifactFreshnessEvaluator
{
    public static DerivedArtifactFreshness Evaluate(
        DerivedArtifactProvenance current,
        DerivedArtifactManifestEntry? persisted,
        string? currentArtifactHash)
    {
        if (persisted is null)
        {
            return DerivedArtifactFreshness.Unknown(DerivedArtifactStaleReason.MissingManifest);
        }

        if (persisted.ProvenanceStatus == DerivedArtifactProvenanceStatus.Superseded)
        {
            return DerivedArtifactFreshness.Stale(DerivedArtifactStaleReason.Superseded);
        }

        if (persisted.ProvenanceStatus != DerivedArtifactProvenanceStatus.Trusted)
        {
            return DerivedArtifactFreshness.Unknown(DerivedArtifactStaleReason.UnknownProvenance);
        }

        var reasons = new List<DerivedArtifactStaleReason>();
        if (!string.Equals(persisted.ArtifactKind, current.ArtifactKind, StringComparison.Ordinal))
        {
            reasons.Add(DerivedArtifactStaleReason.ArtifactKindDrift);
        }

        if (!string.Equals(persisted.ArtifactIdentity, current.ArtifactIdentity, StringComparison.Ordinal))
        {
            reasons.Add(DerivedArtifactStaleReason.ArtifactIdentityDrift);
        }

        if (!string.Equals(persisted.Generator, current.Generator, StringComparison.Ordinal))
        {
            reasons.Add(DerivedArtifactStaleReason.GeneratorDrift);
        }

        if (currentArtifactHash is null)
        {
            reasons.Add(DerivedArtifactStaleReason.ArtifactMissing);
        }
        else if (!string.Equals(persisted.ArtifactHash, currentArtifactHash, StringComparison.Ordinal))
        {
            reasons.Add(DerivedArtifactStaleReason.ArtifactHashDrift);
        }

        AddInputReasons(current, persisted, reasons);

        if (reasons.Count == 0)
        {
            return DerivedArtifactFreshness.Fresh;
        }

        return reasons.Contains(DerivedArtifactStaleReason.UnknownProvenance)
            ? DerivedArtifactFreshness.Unknown(reasons.ToArray())
            : DerivedArtifactFreshness.Stale(reasons.ToArray());
    }

    private static void AddInputReasons(
        DerivedArtifactProvenance current,
        DerivedArtifactManifestEntry persisted,
        List<DerivedArtifactStaleReason> reasons)
    {
        if (persisted.CausalInputs.Count == 0)
        {
            reasons.Add(DerivedArtifactStaleReason.UnknownProvenance);
            return;
        }

        Dictionary<string, DerivedArtifactCausalInput> previous = persisted.CausalInputs.ToDictionary(InputKey, StringComparer.Ordinal);
        Dictionary<string, DerivedArtifactCausalInput> next = current.CausalInputs.ToDictionary(InputKey, StringComparer.Ordinal);

        foreach (DerivedArtifactCausalInput currentInput in current.CausalInputs)
        {
            if (!previous.TryGetValue(InputKey(currentInput), out DerivedArtifactCausalInput? persistedInput))
            {
                reasons.Add(ReasonForInput(currentInput));
                continue;
            }

            if (!string.Equals(persistedInput.Version, currentInput.Version, StringComparison.Ordinal))
            {
                reasons.Add(ReasonForInput(currentInput));
            }
        }

        foreach (DerivedArtifactCausalInput persistedInput in persisted.CausalInputs)
        {
            if (!next.ContainsKey(InputKey(persistedInput)))
            {
                reasons.Add(ReasonForInput(persistedInput));
            }
        }
    }

    private static DerivedArtifactStaleReason ReasonForInput(DerivedArtifactCausalInput input) =>
        input.Kind switch
        {
            ExecutionPreparationProvenanceService.ActiveEpicInputKind => DerivedArtifactStaleReason.ActiveEpicDrift,
            ExecutionPreparationProvenanceService.MilestoneSpecInputKind => DerivedArtifactStaleReason.MilestoneSpecDrift,
            ExecutionPreparationProvenanceService.DecisionLedgerInputKind => DerivedArtifactStaleReason.DecisionLedgerDrift,
            ExecutionPreparationProvenanceService.OperationalContextInputKind => DerivedArtifactStaleReason.OperationalContextDrift,
            ExecutionPreparationProvenanceService.ExecutionPromptInputKind => DerivedArtifactStaleReason.ExecutionPromptDrift,
            SelectionProvenanceService.SelectionCycleInputKind => DerivedArtifactStaleReason.SelectionCycleDrift,
            SelectionProvenanceService.SelectionProjectionInputKind => DerivedArtifactStaleReason.SelectionProjectionDrift,
            SelectionProvenanceService.SelectionPromptContextInputKind => DerivedArtifactStaleReason.SelectionPromptContextDrift,
            SelectionProvenanceService.SelectionSecondaryInputKind => DerivedArtifactStaleReason.SelectionSecondaryInputDrift,
            SelectionProvenanceService.RoadmapCompletionContextInputKind => DerivedArtifactStaleReason.RoadmapCompletionContextDrift,
            SelectionProvenanceService.RoadmapSourceInputKind => DerivedArtifactStaleReason.RoadmapSourceDrift,
            SelectionProvenanceService.RetiredEpicStateInputKind => DerivedArtifactStaleReason.RetiredEpicStateDrift,
            _ => DerivedArtifactStaleReason.CausalInputDrift,
        };

    private static string InputKey(DerivedArtifactCausalInput input) => $"{input.Kind}:{input.Identity}";
}
