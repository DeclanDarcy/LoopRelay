namespace CommandCenter.Roadmap.Cli;

internal sealed record DerivedArtifactCausalInput(
    string Kind,
    string Identity,
    string Version);

internal sealed record DerivedArtifactProvenance(
    string ArtifactKind,
    string ArtifactIdentity,
    string Generator,
    IReadOnlyList<DerivedArtifactCausalInput> CausalInputs);

internal sealed record DerivedArtifactManifestEntry(
    string ArtifactKind,
    string ArtifactIdentity,
    string ArtifactPath,
    string Generator,
    string ArtifactHash,
    DateTimeOffset GeneratedAt,
    DerivedArtifactProvenanceStatus ProvenanceStatus,
    IReadOnlyList<DerivedArtifactCausalInput> CausalInputs,
    DerivedArtifactFreshnessStatus FreshnessStatus,
    IReadOnlyList<DerivedArtifactStaleReason> FreshnessReasons)
{
    public bool IsActiveTrusted => ProvenanceStatus == DerivedArtifactProvenanceStatus.Trusted;

    public static DerivedArtifactManifestEntry FromTrustedProvenance(
        DerivedArtifactProvenance provenance,
        string artifactPath,
        string artifactHash,
        DateTimeOffset generatedAt) =>
        new(
            provenance.ArtifactKind,
            provenance.ArtifactIdentity,
            artifactPath,
            provenance.Generator,
            artifactHash,
            generatedAt,
            DerivedArtifactProvenanceStatus.Trusted,
            provenance.CausalInputs,
            DerivedArtifactFreshnessStatus.Fresh,
            []);

    public DerivedArtifactManifestEntry Supersede(IReadOnlyList<DerivedArtifactStaleReason> reasons) =>
        this with
        {
            ProvenanceStatus = DerivedArtifactProvenanceStatus.Superseded,
            FreshnessStatus = DerivedArtifactFreshnessStatus.Stale,
            FreshnessReasons = reasons.Count == 0 ? [DerivedArtifactStaleReason.Superseded] : reasons,
        };

    public DerivedArtifactManifestEntry WithFreshness(DerivedArtifactFreshness freshness) =>
        this with
        {
            FreshnessStatus = freshness.Status,
            FreshnessReasons = freshness.Reasons,
        };
}

internal sealed record DerivedArtifactFreshness(
    DerivedArtifactFreshnessStatus Status,
    IReadOnlyList<DerivedArtifactStaleReason> Reasons)
{
    public bool IsFresh => Status == DerivedArtifactFreshnessStatus.Fresh;

    public static DerivedArtifactFreshness Fresh { get; } = new(DerivedArtifactFreshnessStatus.Fresh, []);

    public static DerivedArtifactFreshness Stale(params DerivedArtifactStaleReason[] reasons) =>
        new(DerivedArtifactFreshnessStatus.Stale, NormalizeReasons(reasons));

    public static DerivedArtifactFreshness Unknown(params DerivedArtifactStaleReason[] reasons) =>
        new(DerivedArtifactFreshnessStatus.UnknownProvenance, NormalizeReasons(reasons));

    public static DerivedArtifactFreshness Combine(params DerivedArtifactFreshness[] results)
    {
        if (results.All(result => result.IsFresh))
        {
            return Fresh;
        }

        DerivedArtifactStaleReason[] reasons = results
            .Where(result => !result.IsFresh)
            .SelectMany(result => result.Reasons)
            .ToArray();

        return results.Any(result => result.Status == DerivedArtifactFreshnessStatus.UnknownProvenance)
            ? Unknown(reasons)
            : Stale(reasons);
    }

    private static IReadOnlyList<DerivedArtifactStaleReason> NormalizeReasons(IReadOnlyList<DerivedArtifactStaleReason> reasons) =>
        reasons.Count == 0
            ? [DerivedArtifactStaleReason.UnknownProvenance]
            : reasons.Distinct().OrderBy(reason => reason.ToString(), StringComparer.Ordinal).ToArray();
}

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

internal enum DerivedArtifactProvenanceStatus
{
    Unknown,
    Trusted,
    Superseded,
}

internal enum DerivedArtifactFreshnessStatus
{
    Fresh,
    Stale,
    UnknownProvenance,
}

internal enum DerivedArtifactStaleReason
{
    MissingManifest,
    UnknownProvenance,
    Superseded,
    ArtifactKindDrift,
    ArtifactIdentityDrift,
    GeneratorDrift,
    ArtifactMissing,
    ArtifactHashDrift,
    ActiveEpicDrift,
    MilestoneSpecDrift,
    DecisionLedgerDrift,
    OperationalContextDrift,
    ExecutionPromptDrift,
    CausalInputDrift,
    UnexpectedActiveArtifact,
    SelectionCycleDrift,
    SelectionProjectionDrift,
    SelectionPromptContextDrift,
    SelectionSecondaryInputDrift,
    RoadmapCompletionContextDrift,
    RoadmapSourceDrift,
    RetiredEpicStateDrift,
}
