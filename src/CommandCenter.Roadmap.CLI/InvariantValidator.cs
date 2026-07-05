namespace CommandCenter.Roadmap.Cli;

internal sealed class InvariantValidator(
    RoadmapArtifacts artifacts,
    ProjectContextLoader projectContextLoader,
    ProjectionRegistry projectionRegistry,
    PromptContractRegistry contractRegistry,
    ProjectionManifestStore manifestStore,
    ArtifactLifecycleStore lifecycleStore,
    SplitFamilyStore splitFamilyStore)
{
    private readonly EpicArtifactValidator epicValidator = new();
    private readonly ProjectionProvenanceFactory provenanceFactory = new(projectionRegistry);

    public async Task<InvariantValidationResult> ValidateAsync(
        RoadmapState state,
        string expectedProjectContextHash,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ProjectContext projectContext = await projectContextLoader.LoadAsync(cancellationToken);
            if (!string.Equals(projectContext.Hash, expectedProjectContextHash, StringComparison.Ordinal))
            {
                return await FailAsync(state, RoadmapState.Failed, "Project Context hash changed during the run.");
            }

            foreach (ProjectionDefinition projection in projectionRegistry.All)
            {
                _ = contractRegistry.Get(projection.RuntimePromptName);
            }

            ProjectionManifest manifest = await manifestStore.LoadAsync();
            foreach (ProjectionDefinition projection in projectionRegistry.All)
            {
                if (!await artifacts.ExistsAsync(projection.ProjectionPath))
                {
                    continue;
                }

                ProjectionManifestEntry? entry = manifest.Find(projection.RuntimePromptName);
                if (entry is null)
                {
                    return await FailAsync(state, RoadmapState.Failed, $"Projection {projection.ProjectionPath} exists without a manifest entry.");
                }

                if (entry.ValidationStatus == ProjectionValidationStatus.Invalid)
                {
                    return await FailAsync(state, RoadmapState.EvidenceBlocked, $"Projection {projection.ProjectionPath} is marked invalid.");
                }

                ProjectionFreshness freshness = ProjectionFreshnessEvaluator.Evaluate(
                    provenanceFactory.Create(projection, projectContext),
                    entry);
                if (!freshness.IsFresh)
                {
                    return await FailAsync(
                        state,
                        RoadmapState.EvidenceBlocked,
                        $"Projection {projection.ProjectionPath} provenance is not fresh: {FormatReasons(freshness.Reasons)}.");
                }
            }

            IReadOnlyList<ArtifactLifecycleEntry> lifecycle = await lifecycleStore.LoadAsync();
            int activeEpics = lifecycle.Count(entry =>
                (entry.State is ArtifactLifecycleState.Ready or ArtifactLifecycleState.Executing) &&
                (string.Equals(entry.Path, RoadmapArtifactPaths.ActiveEpic, StringComparison.OrdinalIgnoreCase) ||
                 entry.Path.StartsWith(".agents/epic-", StringComparison.OrdinalIgnoreCase)));
            if (activeEpics > 1)
            {
                return await FailAsync(state, RoadmapState.Failed, "More than one epic is marked Ready or Executing.");
            }

            InvariantValidationResult activeEpicResult = await ValidateActiveEpicAsync(state);
            if (!activeEpicResult.IsValid)
            {
                return activeEpicResult;
            }

            InvariantValidationResult specResult = await ValidateSpecsBelongToActiveEpicAsync(state);
            if (!specResult.IsValid)
            {
                return specResult;
            }

            if (state is RoadmapState.ExecutionPromptReady or RoadmapState.ExecutionLoop)
            {
                if (!await artifacts.ExistsAsync(RoadmapArtifactPaths.ActiveEpic) ||
                    !await artifacts.ExistsAsync(RoadmapArtifactPaths.OperationalContext) ||
                    !await artifacts.ExistsAsync(RoadmapArtifactPaths.ExecutionPrompt) ||
                    (await MilestoneSpecPathsAsync()).Count == 0)
                {
                    return await FailAsync(state, RoadmapState.EvidenceBlocked, "Execution bridge prerequisites are incomplete.");
                }
            }

            return InvariantValidationResult.Valid();
        }
        catch (RoadmapStepException exception)
        {
            return await FailAsync(state, RoadmapState.EvidenceBlocked, exception.Message);
        }
    }

    public async Task<InvariantValidationResult> ValidateSplitChildPromotionAsync(string childEpicPath)
    {
        try
        {
            if (!SplitEpicBundleInterpreter.IsChildEpicPath(childEpicPath))
            {
                return await FailAsync(
                    RoadmapState.SplitChildSelection,
                    RoadmapState.EvidenceBlocked,
                    $"Split child promotion target is not a valid split child epic path: {childEpicPath}.");
            }

            bool exists = await splitFamilyStore.ExistsForChildAsync(childEpicPath);
            if (!exists)
            {
                return await FailAsync(RoadmapState.SplitChildSelection, RoadmapState.EvidenceBlocked, $"Split child {childEpicPath} has no split-family artifact.");
            }

            string content = await artifacts.ReadRequiredAsync(childEpicPath);
            ArtifactValidationResult validation = epicValidator.Validate(content);
            return validation.IsValid
                ? InvariantValidationResult.Valid()
                : await FailAsync(RoadmapState.SplitChildSelection, RoadmapState.EvidenceBlocked, validation.Error ?? $"Split child {childEpicPath} failed epic validation.");
        }
        catch (RoadmapStepException exception)
        {
            return await FailAsync(RoadmapState.SplitChildSelection, RoadmapState.EvidenceBlocked, exception.Message);
        }
    }

    private async Task<InvariantValidationResult> ValidateActiveEpicAsync(RoadmapState state)
    {
        if (!RequiresActiveEpic(state))
        {
            return InvariantValidationResult.Valid();
        }

        if (!await artifacts.ExistsAsync(RoadmapArtifactPaths.ActiveEpic))
        {
            return await FailAsync(state, RoadmapState.EvidenceBlocked, "Active epic is missing.");
        }

        string content = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.ActiveEpic);
        ArtifactValidationResult validation = epicValidator.Validate(content);
        return validation.IsValid
            ? InvariantValidationResult.Valid()
            : await FailAsync(state, RoadmapState.EvidenceBlocked, validation.Error ?? "Active epic failed validation.");
    }

    private static bool RequiresActiveEpic(RoadmapState state) =>
        state is RoadmapState.ActiveEpicReady
            or RoadmapState.GenerateMilestoneDeepDives
            or RoadmapState.MilestoneSpecsReady
            or RoadmapState.GenerateOperationalContext
            or RoadmapState.OperationalContextReady
            or RoadmapState.GenerateExecutionPrompt
            or RoadmapState.ExecutionPromptReady
            or RoadmapState.ExecutionLoop
            or RoadmapState.EpicCompletionDetected
            or RoadmapState.CompletionEvaluationAndContextUpdate;

    private async Task<InvariantValidationResult> ValidateSpecsBelongToActiveEpicAsync(RoadmapState state)
    {
        IReadOnlyList<string> specs = await MilestoneSpecPathsAsync();
        foreach (string spec in specs)
        {
            string content = await artifacts.ReadRequiredAsync(spec);
            string? declaredEpicPath = FindDeclaredEpicPath(content);
            if (declaredEpicPath is not null &&
                !string.Equals(declaredEpicPath, RoadmapArtifactPaths.ActiveEpic, StringComparison.OrdinalIgnoreCase))
            {
                return await FailAsync(state, RoadmapState.EvidenceBlocked, $"{spec} belongs to {declaredEpicPath}, not the active epic.");
            }
        }

        return InvariantValidationResult.Valid();
    }

    private static string? FindDeclaredEpicPath(string content)
    {
        foreach (string line in content.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("| Epic Path |", StringComparison.OrdinalIgnoreCase))
            {
                string[] cells = trimmed.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();
                return cells.Length >= 2 ? cells[1] : null;
            }

            if (trimmed.StartsWith("Epic Path:", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed["Epic Path:".Length..].Trim();
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<string>> MilestoneSpecPathsAsync() =>
        (await artifacts.ListAsync(RoadmapArtifactPaths.SpecsDirectory, "*.md"))
            .Where(RoadmapArtifactPaths.IsMilestoneSpecPath)
            .ToArray();

    private async Task<InvariantValidationResult> FailAsync(RoadmapState currentState, RoadmapState failureState, string message)
    {
        string content = RoadmapBlockedArtifact.Render(
            failureState,
            "InvariantValidator",
            message,
            "Repair the invariant violation before continuing the roadmap state machine.",
            "None",
            message,
            DateTimeOffset.UtcNow);
        string path = await artifacts.WriteNumberedEvidenceAsync(
            RoadmapArtifactPaths.OrchestrationEvidenceDirectory,
            "invariant-failure",
            content);
        return InvariantValidationResult.Invalid(failureState, message, path);
    }

    private static string FormatReasons(IReadOnlyList<ProjectionStaleReason> reasons) =>
        reasons.Count == 0 ? "UnknownProvenance" : string.Join(", ", reasons);
}

internal sealed record InvariantValidationResult(
    bool IsValid,
    RoadmapState FailureState,
    string? Error,
    string? EvidencePath)
{
    public static InvariantValidationResult Valid() => new(true, RoadmapState.CoreReady, null, null);

    public static InvariantValidationResult Invalid(RoadmapState failureState, string error, string evidencePath) =>
        new(false, failureState, error, evidencePath);
}
