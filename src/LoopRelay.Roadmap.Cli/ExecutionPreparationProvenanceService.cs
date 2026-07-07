namespace LoopRelay.Roadmap.Cli;

internal sealed class ExecutionPreparationProvenanceService(
    RoadmapArtifacts artifacts,
    ExecutionPreparationManifestStore manifestStore)
{
    public const string ActiveEpicInputKind = "ActiveEpic";
    public const string MilestoneSpecInputKind = "MilestoneSpec";
    public const string DecisionLedgerInputKind = "DecisionLedger";
    public const string OperationalContextInputKind = "OperationalContext";
    public const string ExecutionPromptInputKind = "ExecutionPrompt";

    public const string MilestoneSpecArtifactKind = "MilestoneSpec";
    public const string OperationalContextArtifactKind = "OperationalContext";
    public const string ExecutionPromptArtifactKind = "ExecutionPrompt";
    public const string ExecutionPlanArtifactKind = "ExecutionPlan";
    public const string ExecutionMilestoneArtifactKind = "ExecutionMilestone";

    private const string MilestoneSpecGenerator = "GenerateMilestoneDeepDivesForEpic:v1";
    private const string OperationalContextGeneratorName = "OperationalContextGenerator:v1";
    private const string ExecutionPromptGeneratorName = "ExecutionPromptGenerator:v1";
    private const string ExecutionCompatibilityGeneratorName = "ExecutionCompatibilityMaterializer:v1";
    private const string MissingInputVersion = "missing";

    public async Task RecordMilestoneSpecsAsync(
        IReadOnlyList<string> specPaths,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ExecutionPreparationInputSet inputSet = await CaptureInputSetFromSpecPathsAsync(specPaths, cancellationToken);
        ExecutionPreparationManifest manifest = (await manifestStore.LoadAsync())
            .WithAuthoritativeInputs(
                inputSet.ActiveEpic.Identity,
                inputSet.ActiveEpic.Version,
                inputSet.MilestoneSpecs);

        var activeIdentities = inputSet.MilestoneSpecs
            .Select(input => input.Identity)
            .ToHashSet(StringComparer.Ordinal);

        manifest = manifest.SupersedeActiveArtifacts(
            MilestoneSpecArtifactKind,
            activeIdentities,
            [DerivedArtifactStaleReason.Superseded]);

        foreach (ExecutionPreparationManifestInput spec in inputSet.MilestoneSpecs)
        {
            var provenance = new DerivedArtifactProvenance(
                MilestoneSpecArtifactKind,
                spec.Identity,
                MilestoneSpecGenerator,
                [ToCausalInput(inputSet.ActiveEpic)]);
            manifest = manifest.UpsertActive(DerivedArtifactManifestEntry.FromTrustedProvenance(
                provenance,
                spec.Identity,
                spec.Version,
                DateTimeOffset.UtcNow));
        }

        await manifestStore.SaveAsync(manifest);
    }

    public async Task RecordOperationalContextAsync(
        string content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DerivedArtifactFreshness specs = await EvaluateMilestoneSpecsFreshnessAsync(cancellationToken);
        if (!specs.IsFresh)
        {
            throw new RoadmapStepException($"Cannot record operational context because milestone specs are stale: {FormatReasons(specs.Reasons)}.");
        }

        ExecutionPreparationInputSet inputSet = await CaptureFreshInputSetAsync(cancellationToken);
        DerivedArtifactProvenance provenance = CreateOperationalContextProvenance(inputSet);
        await UpsertArtifactAsync(
            provenance,
            RoadmapArtifactPaths.OperationalContext,
            RoadmapHash.Sha256(content),
            inputSet,
            cancellationToken);
    }

    public async Task RecordExecutionPromptAsync(
        string content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ExecutionPreparationInputSet inputSet = await CaptureFreshInputSetAsync(cancellationToken);
        DerivedArtifactFreshness operationalContext = await EvaluateOperationalContextFreshnessAsync(cancellationToken);
        if (!operationalContext.IsFresh)
        {
            throw new RoadmapStepException($"Cannot record execution prompt because operational context is stale: {FormatReasons(operationalContext.Reasons)}.");
        }

        DerivedArtifactProvenance provenance = await CreateExecutionPromptProvenanceAsync(inputSet, cancellationToken);
        await UpsertArtifactAsync(
            provenance,
            RoadmapArtifactPaths.ExecutionPrompt,
            RoadmapHash.Sha256(content),
            inputSet,
            cancellationToken);
    }

    public async Task RecordCompatibilityArtifactsAsync(
        IReadOnlyList<string> milestonePaths,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ExecutionPreparationInputSet inputSet = await CaptureFreshInputSetAsync(cancellationToken);
        DerivedArtifactFreshness prompt = await EvaluateExecutionPromptFreshnessAsync(cancellationToken);
        if (!prompt.IsFresh)
        {
            throw new RoadmapStepException($"Cannot record compatibility artifacts because execution prompt is stale: {FormatReasons(prompt.Reasons)}.");
        }

        string? planHash = await HashIfPresentAsync(RoadmapArtifactPaths.ExecutionPlan);
        if (planHash is null)
        {
            throw new RoadmapStepException($"Cannot record compatibility artifact provenance because {RoadmapArtifactPaths.ExecutionPlan} is missing.");
        }

        if (milestonePaths.Count != inputSet.MilestoneSpecs.Count)
        {
            throw new RoadmapStepException("Cannot record compatibility artifact provenance because milestone output count does not match active spec count.");
        }

        ExecutionPreparationManifest manifest = (await manifestStore.LoadAsync())
            .WithAuthoritativeInputs(
                inputSet.ActiveEpic.Identity,
                inputSet.ActiveEpic.Version,
                inputSet.MilestoneSpecs);

        var activePlanIdentities = new HashSet<string>(StringComparer.Ordinal) { RoadmapArtifactPaths.ExecutionPlan };
        manifest = manifest.SupersedeActiveArtifacts(
            ExecutionPlanArtifactKind,
            activePlanIdentities,
            [DerivedArtifactStaleReason.Superseded]);

        DerivedArtifactProvenance planProvenance = await CreateExecutionPlanProvenanceAsync(inputSet, cancellationToken);
        manifest = manifest.UpsertActive(DerivedArtifactManifestEntry.FromTrustedProvenance(
            planProvenance,
            RoadmapArtifactPaths.ExecutionPlan,
            planHash,
            DateTimeOffset.UtcNow));

        var activeMilestoneIdentities = milestonePaths.ToHashSet(StringComparer.Ordinal);
        manifest = manifest.SupersedeActiveArtifacts(
            ExecutionMilestoneArtifactKind,
            activeMilestoneIdentities,
            [DerivedArtifactStaleReason.Superseded]);

        IReadOnlyList<ExecutionPreparationManifestInput> specs = inputSet.MilestoneSpecs;
        for (int i = 0; i < milestonePaths.Count; i++)
        {
            string milestonePath = milestonePaths[i];
            string? milestoneHash = await HashIfPresentAsync(milestonePath);
            if (milestoneHash is null)
            {
                throw new RoadmapStepException($"Cannot record compatibility artifact provenance because {milestonePath} is missing.");
            }

            IReadOnlyList<DerivedArtifactCausalInput> inputs =
            [
                ToCausalInput(inputSet.ActiveEpic),
                ToCausalInput(specs[i]),
                new(OperationalContextInputKind, RoadmapArtifactPaths.OperationalContext, (await RequireHashAsync(RoadmapArtifactPaths.OperationalContext))),
                new(ExecutionPromptInputKind, RoadmapArtifactPaths.ExecutionPrompt, (await RequireHashAsync(RoadmapArtifactPaths.ExecutionPrompt))),
            ];
            var milestoneProvenance = new DerivedArtifactProvenance(
                ExecutionMilestoneArtifactKind,
                milestonePath,
                ExecutionCompatibilityGeneratorName,
                inputs);
            manifest = manifest.UpsertActive(DerivedArtifactManifestEntry.FromTrustedProvenance(
                milestoneProvenance,
                milestonePath,
                milestoneHash,
                DateTimeOffset.UtcNow));
        }

        await manifestStore.SaveAsync(manifest);
    }

    public async Task<IReadOnlyList<string>> RequireFreshMilestoneSpecPathsAsync(
        CancellationToken cancellationToken = default)
    {
        DerivedArtifactFreshness freshness = await EvaluateMilestoneSpecsFreshnessAsync(cancellationToken);
        if (!freshness.IsFresh)
        {
            throw new RoadmapStepException($"Milestone spec provenance is not fresh: {FormatReasons(freshness.Reasons)}.");
        }

        ExecutionPreparationManifest manifest = await manifestStore.LoadAsync();
        return manifest.MilestoneSpecs.Select(input => input.Identity).ToArray();
    }

    public async Task<ExecutionPreparationReadiness> EvaluateReadinessAsync(
        bool requireSpecs,
        bool requireOperationalContext,
        bool requireExecutionPrompt,
        bool requireCompatibilityArtifacts,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var results = new List<ExecutionPreparationArtifactFreshness>();

        if (requireSpecs)
        {
            results.Add(new ExecutionPreparationArtifactFreshness(
                MilestoneSpecArtifactKind,
                RoadmapArtifactPaths.SpecsDirectory,
                await EvaluateMilestoneSpecsFreshnessAsync(cancellationToken)));
        }

        if (requireOperationalContext)
        {
            results.Add(new ExecutionPreparationArtifactFreshness(
                OperationalContextArtifactKind,
                RoadmapArtifactPaths.OperationalContext,
                await EvaluateOperationalContextFreshnessAsync(cancellationToken)));
        }

        if (requireExecutionPrompt)
        {
            results.Add(new ExecutionPreparationArtifactFreshness(
                ExecutionPromptArtifactKind,
                RoadmapArtifactPaths.ExecutionPrompt,
                await EvaluateExecutionPromptFreshnessAsync(cancellationToken)));
        }

        if (requireCompatibilityArtifacts)
        {
            results.Add(new ExecutionPreparationArtifactFreshness(
                "ExecutionCompatibilityArtifacts",
                RoadmapArtifactPaths.ExecutionPlan,
                await EvaluateCompatibilityFreshnessAsync(cancellationToken)));
        }

        if (results.All(result => result.Freshness.IsFresh))
        {
            return new ExecutionPreparationReadiness(true, "Execution preparation provenance is fresh.", results);
        }

        ExecutionPreparationArtifactFreshness firstStale = results.First(result => !result.Freshness.IsFresh);
        return new ExecutionPreparationReadiness(
            false,
            $"{firstStale.ArtifactKind} provenance is not fresh: {FormatReasons(firstStale.Freshness.Reasons)}.",
            results);
    }

    public async Task<DerivedArtifactFreshness> EvaluateMilestoneSpecsFreshnessAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ExecutionPreparationManifest manifest = await manifestStore.LoadAsync();
        if (manifest.MilestoneSpecs.Count == 0)
        {
            return DerivedArtifactFreshness.Unknown(DerivedArtifactStaleReason.MissingManifest);
        }

        string? activeEpicHash = await HashIfPresentAsync(RoadmapArtifactPaths.ActiveEpic);
        if (activeEpicHash is null)
        {
            return DerivedArtifactFreshness.Stale(DerivedArtifactStaleReason.ArtifactMissing);
        }

        var results = new List<DerivedArtifactFreshness>();
        foreach (ExecutionPreparationManifestInput spec in manifest.MilestoneSpecs)
        {
            var expected = new DerivedArtifactProvenance(
                MilestoneSpecArtifactKind,
                spec.Identity,
                MilestoneSpecGenerator,
                [new(ActiveEpicInputKind, RoadmapArtifactPaths.ActiveEpic, activeEpicHash)]);
            results.Add(DerivedArtifactFreshnessEvaluator.Evaluate(
                expected,
                manifest.FindActive(MilestoneSpecArtifactKind, spec.Identity),
                await HashIfPresentAsync(spec.Identity)));
        }

        IReadOnlySet<string> expectedSpecIdentities = manifest.MilestoneSpecs
            .Select(spec => spec.Identity)
            .ToHashSet(StringComparer.Ordinal);
        bool unexpectedActiveSpec = manifest.ActiveArtifacts.Any(entry =>
            string.Equals(entry.ArtifactKind, MilestoneSpecArtifactKind, StringComparison.Ordinal) &&
            !expectedSpecIdentities.Contains(entry.ArtifactIdentity));
        if (unexpectedActiveSpec)
        {
            results.Add(DerivedArtifactFreshness.Stale(DerivedArtifactStaleReason.UnexpectedActiveArtifact));
        }

        return DerivedArtifactFreshness.Combine(results.ToArray());
    }

    public async Task<DerivedArtifactFreshness> EvaluateOperationalContextFreshnessAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DerivedArtifactFreshness specs = await EvaluateMilestoneSpecsFreshnessAsync(cancellationToken);
        if (!specs.IsFresh)
        {
            return DerivedArtifactFreshness.Combine(specs, DerivedArtifactFreshness.Stale(DerivedArtifactStaleReason.MilestoneSpecDrift));
        }

        ExecutionPreparationInputSet inputSet = await CaptureFreshInputSetAsync(cancellationToken);
        ExecutionPreparationManifest manifest = await manifestStore.LoadAsync();
        return DerivedArtifactFreshnessEvaluator.Evaluate(
            CreateOperationalContextProvenance(inputSet),
            manifest.FindActive(OperationalContextArtifactKind, RoadmapArtifactPaths.OperationalContext),
            await HashIfPresentAsync(RoadmapArtifactPaths.OperationalContext));
    }

    public async Task<DerivedArtifactFreshness> EvaluateExecutionPromptFreshnessAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DerivedArtifactFreshness operationalContext = await EvaluateOperationalContextFreshnessAsync(cancellationToken);
        if (!operationalContext.IsFresh)
        {
            return DerivedArtifactFreshness.Combine(operationalContext, DerivedArtifactFreshness.Stale(DerivedArtifactStaleReason.OperationalContextDrift));
        }

        ExecutionPreparationInputSet inputSet = await CaptureFreshInputSetAsync(cancellationToken);
        ExecutionPreparationManifest manifest = await manifestStore.LoadAsync();
        return DerivedArtifactFreshnessEvaluator.Evaluate(
            await CreateExecutionPromptProvenanceAsync(inputSet, cancellationToken),
            manifest.FindActive(ExecutionPromptArtifactKind, RoadmapArtifactPaths.ExecutionPrompt),
            await HashIfPresentAsync(RoadmapArtifactPaths.ExecutionPrompt));
    }

    public async Task<DerivedArtifactFreshness> EvaluateCompatibilityFreshnessAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DerivedArtifactFreshness executionPrompt = await EvaluateExecutionPromptFreshnessAsync(cancellationToken);
        if (!executionPrompt.IsFresh)
        {
            return DerivedArtifactFreshness.Combine(executionPrompt, DerivedArtifactFreshness.Stale(DerivedArtifactStaleReason.ExecutionPromptDrift));
        }

        ExecutionPreparationInputSet inputSet = await CaptureFreshInputSetAsync(cancellationToken);
        ExecutionPreparationManifest manifest = await manifestStore.LoadAsync();
        var results = new List<DerivedArtifactFreshness>
        {
            DerivedArtifactFreshnessEvaluator.Evaluate(
                await CreateExecutionPlanProvenanceAsync(inputSet, cancellationToken),
                manifest.FindActive(ExecutionPlanArtifactKind, RoadmapArtifactPaths.ExecutionPlan),
                await HashIfPresentAsync(RoadmapArtifactPaths.ExecutionPlan)),
        };

        IReadOnlyList<string> expectedMilestonePaths = ExpectedCompatibilityMilestonePaths(inputSet.MilestoneSpecs.Count);
        for (int i = 0; i < expectedMilestonePaths.Count; i++)
        {
            string milestonePath = expectedMilestonePaths[i];
            IReadOnlyList<DerivedArtifactCausalInput> inputs =
            [
                ToCausalInput(inputSet.ActiveEpic),
                ToCausalInput(inputSet.MilestoneSpecs[i]),
                new(OperationalContextInputKind, RoadmapArtifactPaths.OperationalContext, await RequireHashAsync(RoadmapArtifactPaths.OperationalContext)),
                new(ExecutionPromptInputKind, RoadmapArtifactPaths.ExecutionPrompt, await RequireHashAsync(RoadmapArtifactPaths.ExecutionPrompt)),
            ];
            var expected = new DerivedArtifactProvenance(
                ExecutionMilestoneArtifactKind,
                milestonePath,
                ExecutionCompatibilityGeneratorName,
                inputs);
            results.Add(DerivedArtifactFreshnessEvaluator.Evaluate(
                expected,
                manifest.FindActive(ExecutionMilestoneArtifactKind, milestonePath),
                await HashIfPresentAsync(milestonePath)));
        }

        IReadOnlySet<string> expectedCompatibilityIdentities = expectedMilestonePaths
            .Append(RoadmapArtifactPaths.ExecutionPlan)
            .ToHashSet(StringComparer.Ordinal);
        bool unexpectedActiveCompatibility = manifest.ActiveArtifacts.Any(entry =>
            (string.Equals(entry.ArtifactKind, ExecutionPlanArtifactKind, StringComparison.Ordinal) ||
             string.Equals(entry.ArtifactKind, ExecutionMilestoneArtifactKind, StringComparison.Ordinal)) &&
            !expectedCompatibilityIdentities.Contains(entry.ArtifactIdentity));
        if (unexpectedActiveCompatibility)
        {
            results.Add(DerivedArtifactFreshness.Stale(DerivedArtifactStaleReason.UnexpectedActiveArtifact));
        }

        return DerivedArtifactFreshness.Combine(results.ToArray());
    }

    public static IReadOnlyList<string> ExpectedCompatibilityMilestonePaths(int milestoneCount) =>
        Enumerable.Range(1, milestoneCount)
            .Select(index => $"{RoadmapArtifactPaths.ExecutionMilestonesDirectory}/m{index:000}.md")
            .ToArray();

    private async Task UpsertArtifactAsync(
        DerivedArtifactProvenance provenance,
        string artifactPath,
        string artifactHash,
        ExecutionPreparationInputSet inputSet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ExecutionPreparationManifest manifest = (await manifestStore.LoadAsync())
            .WithAuthoritativeInputs(
                inputSet.ActiveEpic.Identity,
                inputSet.ActiveEpic.Version,
                inputSet.MilestoneSpecs);
        manifest = manifest.UpsertActive(DerivedArtifactManifestEntry.FromTrustedProvenance(
            provenance,
            artifactPath,
            artifactHash,
            DateTimeOffset.UtcNow));
        await manifestStore.SaveAsync(manifest);
    }

    private async Task<ExecutionPreparationInputSet> CaptureFreshInputSetAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ExecutionPreparationManifest manifest = await manifestStore.LoadAsync();
        if (manifest.MilestoneSpecs.Count == 0)
        {
            throw new RoadmapStepException("Cannot evaluate execution preparation without milestone spec provenance.");
        }

        string activeEpicHash = await RequireHashAsync(RoadmapArtifactPaths.ActiveEpic);
        return new ExecutionPreparationInputSet(
            new ExecutionPreparationManifestInput(ActiveEpicInputKind, RoadmapArtifactPaths.ActiveEpic, activeEpicHash),
            manifest.MilestoneSpecs,
            await CaptureDecisionLedgerInputAsync());
    }

    private async Task<ExecutionPreparationInputSet> CaptureInputSetFromSpecPathsAsync(
        IReadOnlyList<string> specPaths,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (specPaths.Count == 0)
        {
            throw new RoadmapStepException("Cannot record milestone spec provenance without milestone specs.");
        }

        var specs = new List<ExecutionPreparationManifestInput>();
        foreach (string specPath in specPaths.OrderBy(path => path, StringComparer.Ordinal))
        {
            specs.Add(new ExecutionPreparationManifestInput(
                MilestoneSpecInputKind,
                specPath,
                await RequireHashAsync(specPath)));
        }

        return new ExecutionPreparationInputSet(
            new ExecutionPreparationManifestInput(
                ActiveEpicInputKind,
                RoadmapArtifactPaths.ActiveEpic,
                await RequireHashAsync(RoadmapArtifactPaths.ActiveEpic)),
            specs,
            await CaptureDecisionLedgerInputAsync());
    }

    private async Task<ExecutionPreparationManifestInput> CaptureDecisionLedgerInputAsync()
    {
        string version = await HashIfPresentAsync(RoadmapArtifactPaths.DecisionLedgerJson) ?? MissingInputVersion;
        return new ExecutionPreparationManifestInput(
            DecisionLedgerInputKind,
            RoadmapArtifactPaths.DecisionLedgerJson,
            version);
    }

    private static DerivedArtifactProvenance CreateOperationalContextProvenance(ExecutionPreparationInputSet inputSet)
    {
        var inputs = new List<DerivedArtifactCausalInput> { ToCausalInput(inputSet.ActiveEpic) };
        inputs.AddRange(inputSet.MilestoneSpecs.Select(ToCausalInput));
        inputs.Add(ToCausalInput(inputSet.DecisionLedger));
        return new DerivedArtifactProvenance(
            OperationalContextArtifactKind,
            RoadmapArtifactPaths.OperationalContext,
            OperationalContextGeneratorName,
            inputs);
    }

    private async Task<DerivedArtifactProvenance> CreateExecutionPromptProvenanceAsync(
        ExecutionPreparationInputSet inputSet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var inputs = new List<DerivedArtifactCausalInput> { ToCausalInput(inputSet.ActiveEpic) };
        inputs.AddRange(inputSet.MilestoneSpecs.Select(ToCausalInput));
        inputs.Add(new DerivedArtifactCausalInput(
            OperationalContextInputKind,
            RoadmapArtifactPaths.OperationalContext,
            await RequireHashAsync(RoadmapArtifactPaths.OperationalContext)));
        return new DerivedArtifactProvenance(
            ExecutionPromptArtifactKind,
            RoadmapArtifactPaths.ExecutionPrompt,
            ExecutionPromptGeneratorName,
            inputs);
    }

    private async Task<DerivedArtifactProvenance> CreateExecutionPlanProvenanceAsync(
        ExecutionPreparationInputSet inputSet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var inputs = new List<DerivedArtifactCausalInput> { ToCausalInput(inputSet.ActiveEpic) };
        inputs.AddRange(inputSet.MilestoneSpecs.Select(ToCausalInput));
        inputs.Add(new DerivedArtifactCausalInput(
            OperationalContextInputKind,
            RoadmapArtifactPaths.OperationalContext,
            await RequireHashAsync(RoadmapArtifactPaths.OperationalContext)));
        inputs.Add(new DerivedArtifactCausalInput(
            ExecutionPromptInputKind,
            RoadmapArtifactPaths.ExecutionPrompt,
            await RequireHashAsync(RoadmapArtifactPaths.ExecutionPrompt)));
        return new DerivedArtifactProvenance(
            ExecutionPlanArtifactKind,
            RoadmapArtifactPaths.ExecutionPlan,
            ExecutionCompatibilityGeneratorName,
            inputs);
    }

    private async Task<string> RequireHashAsync(string path)
    {
        string? hash = await HashIfPresentAsync(path);
        if (hash is null)
        {
            throw new RoadmapStepException($"Required artifact is missing or empty: {path}");
        }

        return hash;
    }

    private async Task<string?> HashIfPresentAsync(string path)
    {
        string? content = await artifacts.ReadAsync(path);
        return string.IsNullOrWhiteSpace(content) ? null : RoadmapHash.Sha256(content);
    }

    private static DerivedArtifactCausalInput ToCausalInput(ExecutionPreparationManifestInput input) =>
        new(input.Kind, input.Identity, input.Version);

    private static string FormatReasons(IReadOnlyList<DerivedArtifactStaleReason> reasons) =>
        reasons.Count == 0 ? "UnknownProvenance" : string.Join(", ", reasons);
}

internal sealed record ExecutionPreparationInputSet(
    ExecutionPreparationManifestInput ActiveEpic,
    IReadOnlyList<ExecutionPreparationManifestInput> MilestoneSpecs,
    ExecutionPreparationManifestInput DecisionLedger);

internal sealed record ExecutionPreparationReadiness(
    bool IsFresh,
    string Reason,
    IReadOnlyList<ExecutionPreparationArtifactFreshness> Artifacts);

internal sealed record ExecutionPreparationArtifactFreshness(
    string ArtifactKind,
    string ArtifactIdentity,
    DerivedArtifactFreshness Freshness);
