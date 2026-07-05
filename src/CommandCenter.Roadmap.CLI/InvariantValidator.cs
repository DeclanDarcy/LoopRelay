namespace CommandCenter.Roadmap.Cli;

internal sealed class InvariantValidator(
    RoadmapArtifacts artifacts,
    NorthStarContextLoader northStarLoader,
    ProjectionRegistry projectionRegistry,
    PromptContractRegistry contractRegistry,
    ProjectionManifestStore manifestStore,
    ArtifactLifecycleStore lifecycleStore,
    SplitFamilyStore splitFamilyStore)
{
    public async Task<InvariantValidationResult> ValidateAsync(
        RoadmapState state,
        string expectedNorthStarHash,
        CancellationToken cancellationToken = default)
    {
        try
        {
            NorthStarContext northStar = await northStarLoader.LoadAsync(cancellationToken);
            if (!string.Equals(northStar.Hash, expectedNorthStarHash, StringComparison.Ordinal))
            {
                return await FailAsync(state, RoadmapState.Failed, "Core North-Star hash changed during the run.");
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
                    (await artifacts.ListAsync(RoadmapArtifactPaths.SpecsDirectory, "*.md")).Count == 0)
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
        if (!childEpicPath.StartsWith(".agents/epic-", StringComparison.OrdinalIgnoreCase))
        {
            return InvariantValidationResult.Valid();
        }

        bool exists = await splitFamilyStore.ExistsForChildAsync(childEpicPath);
        return exists
            ? InvariantValidationResult.Valid()
            : await FailAsync(RoadmapState.SplitChildSelection, RoadmapState.EvidenceBlocked, $"Split child {childEpicPath} has no split-family artifact.");
    }

    private async Task<InvariantValidationResult> ValidateSpecsBelongToActiveEpicAsync(RoadmapState state)
    {
        IReadOnlyList<string> specs = await artifacts.ListAsync(RoadmapArtifactPaths.SpecsDirectory, "*.md");
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
