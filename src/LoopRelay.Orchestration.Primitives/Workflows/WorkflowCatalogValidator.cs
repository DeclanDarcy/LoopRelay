using LoopRelay.Orchestration.Services;

namespace LoopRelay.Orchestration.Workflows;

public sealed record WorkflowCatalogValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public static class WorkflowCatalogValidator
{
    public static WorkflowCatalogValidationResult Validate(CanonicalWorkflowCatalogSnapshot catalog)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(catalog.Identity)) errors.Add("catalog/identity: identity is required.");
        if (string.IsNullOrWhiteSpace(catalog.SemanticVersion)) errors.Add("catalog/version: semantic version is required.");
        if (catalog.Workflows.Select(item => item.Identity).Distinct().Count() != catalog.Workflows.Count)
            errors.Add("catalog/workflows: workflow identities must be unique.");
        if (catalog.Chains.Select(item => item.Identity).Distinct(StringComparer.Ordinal).Count() != catalog.Chains.Count)
            errors.Add("catalog/chains: chain identities must be unique.");

        HashSet<WorkflowIdentity> workflowIds = catalog.Workflows.Select(item => item.Identity).ToHashSet();
        foreach (WorkflowDefinition workflow in catalog.Workflows.OrderBy(item => item.Identity.Value, StringComparer.Ordinal))
        {
            string root = $"workflow/{workflow.Identity}";
            foreach (string error in WorkflowDefinitionValidator.Validate(workflow).Errors)
                errors.Add($"{root}: {error}");
            ValidateReachability(workflow, root, errors);
            foreach (WorkflowTransitionDefinition transition in workflow.Transitions
                         .OrderBy(item => item.Identity.Value, StringComparer.Ordinal))
                ValidateTransition(root, transition, errors);
        }

        foreach (WorkflowChainDefinition chain in catalog.Chains.OrderBy(item => item.Identity, StringComparer.Ordinal))
        {
            if (!workflowIds.Contains(chain.InitialWorkflow))
                errors.Add($"chain/{chain.Identity}: initial workflow '{chain.InitialWorkflow}' is unknown.");
            if (chain.Workflows.Count == 0 || chain.Workflows.Any(item => !workflowIds.Contains(item.Identity)))
                errors.Add($"chain/{chain.Identity}: chain contains an unknown or empty workflow sequence.");
            if (chain.Workflows.Select(item => item.Identity).Distinct().Count() != chain.Workflows.Count)
                errors.Add($"chain/{chain.Identity}: workflow sequence is cyclic or duplicated.");
        }
        return new WorkflowCatalogValidationResult(errors.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray());
    }

    private static void ValidateTransition(string workflowPath, WorkflowTransitionDefinition transition,
        List<string> errors)
    {
        string path = $"{workflowPath}/transition/{transition.Identity}";
        if (transition.ValidatorReferences is not { Count: > 0 })
            errors.Add($"{path}: at least one typed validator owner reference is required.");
        else
        {
            foreach (ValidatorReference validator in transition.ValidatorReferences)
            {
                if (!WorkflowCatalogOwnerRegistry.Owns(validator))
                    errors.Add($"{path}/validator/{validator.Identity}: validator owner is unregistered.");
            }
        }
        if (transition.PromptContract is null ||
            !string.Equals(transition.PromptContract.TemplateIdentity, transition.PromptIdentity, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(transition.PromptContract.TemplateVersion) ||
            !WorkflowCatalogOwnerRegistry.OwnsPrompt(transition.PromptContract.TemplateIdentity,
                transition.PromptContract.TemplateVersion))
            errors.Add($"{path}/prompt: prompt asset identity/version is missing or mismatched.");
        if (transition.PromptContract is not null &&
            (string.IsNullOrWhiteSpace(transition.PromptContract.RequiredPolicyProfile) ||
             transition.PromptContract.ResolvedPolicyRequirements.Count == 0))
            errors.Add($"{path}/policy: prompt policy profile and resolved requirements are required.");
        foreach (RuntimeCapabilityIdentity capability in transition.PromptContract?.RuntimeCapabilities ?? [])
            if (!WorkflowCatalogOwnerRegistry.Supports(capability))
                errors.Add($"{path}/capability/{capability}: runtime capability is unsupported.");
        foreach (string category in transition.InteractionCategories ?? [])
            if (!WorkflowCatalogOwnerRegistry.OwnsInteraction(category))
                errors.Add($"{path}/interaction/{category}: interaction category owner is unregistered.");
        foreach (string strategy in transition.RecoveryStrategies ?? [])
            if (!WorkflowCatalogOwnerRegistry.OwnsRecoveryStrategy(strategy))
                errors.Add($"{path}/recovery/{strategy}: recovery strategy owner is unregistered.");
        foreach (EffectDefinition effect in transition.Effects)
            if (!WorkflowCatalogOwnerRegistry.OwnsEffect(effect.Category))
                errors.Add($"{path}/effect/{effect.Identity}: effect category owner is unregistered.");

        OutputSurfaceDefinition[] surfaces = (transition.OutputSurfaces ?? []).ToArray();
        if (transition.EligibleSuccessors.Distinct().Count() != transition.EligibleSuccessors.Count)
            errors.Add($"{path}/successors: transition successors are ambiguous or duplicated.");
        if (transition.ProducedProducts.Count > 0 && surfaces.Length == 0)
            errors.Add($"{path}/outputs: produced products require declared output surfaces.");
        foreach (OutputSurfaceDefinition surface in surfaces)
        {
            ValidateSurface(path, surface, errors);
            if (surface.CommitPolicy == CommitPolicy.BlockingLocal && !transition.Effects.Any(effect =>
                    effect.Category == EffectCategory.Git &&
                    effect.Identity.Value.StartsWith("derived-git-commit:", StringComparison.Ordinal) &&
                    effect.Identity.Value.EndsWith(surface.Path, StringComparison.Ordinal)))
                errors.Add($"{path}/output/{surface.Path}: blocking commit effect was not structurally derived.");
            if (surface.PushPolicy == PushPolicy.RequiredAsync && !transition.Effects.Any(effect =>
                    effect.Category == EffectCategory.Git &&
                    effect.Identity.Value.StartsWith("derived-git-push:", StringComparison.Ordinal) &&
                    effect.Identity.Value.EndsWith(surface.Path, StringComparison.Ordinal)))
                errors.Add($"{path}/output/{surface.Path}: required push effect was not structurally derived.");
        }
        if (transition.Effects.Where(item => item.Category == EffectCategory.Git)
            .Any(item => !item.Identity.Value.StartsWith("derived-git-", StringComparison.Ordinal)))
            errors.Add($"{path}/effects: workflow-authored Git mechanics are forbidden.");

        foreach (GateRequirementDefinition requirement in transition.InputGate.Requirements
                     .Where(item => item.InputSurface is not null))
            ValidatePath(requirement.InputSurface!, $"{path}/input/{requirement.Identity}", errors);
        InputSurfaceDefinition[] inputs = (transition.InputSurfaces ?? []).ToArray();
        if (transition.RequiredInputProducts.Count > 0 && inputs.Length == 0)
            errors.Add($"{path}/inputs: required products need complete filesystem input surfaces.");
        foreach (InputSurfaceDefinition input in inputs)
        {
            ValidatePath(input.Path, $"{path}/input/{input.Path}", errors);
            if (!WorkflowCatalogOwnerRegistry.Owns(input.Validator))
                errors.Add($"{path}/input/{input.Path}: input validator owner is unregistered.");
        }
    }

    private static void ValidateSurface(string transitionPath, OutputSurfaceDefinition surface, List<string> errors)
    {
        string path = $"{transitionPath}/output/{surface.Path}";
        ValidatePath(surface.Path, path, errors);
        bool agents = surface.Path == OrchestrationArtifactPaths.AgentsDirectory ||
            OrchestrationArtifactPaths.IsAgentsPath(surface.Path);
        if (agents && surface.RepositoryTarget == RepositoryTarget.Workspace)
            errors.Add($"{path}: .agents output must name nested Agents or parent gitlink topology.");
        if (string.IsNullOrWhiteSpace(surface.Ownership)) errors.Add($"{path}: ownership is required.");
        if (!WorkflowCatalogOwnerRegistry.Owns(surface.Validator))
            errors.Add($"{path}: output validator owner is unregistered.");
    }

    private static void ValidatePath(string value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\\') || Path.IsPathRooted(value) ||
            value.Split('/').Any(segment => segment == ".."))
            errors.Add($"{label}: surface must be normalized repository-relative and cannot escape root.");
    }

    private static void ValidateReachability(WorkflowDefinition workflow, string path, List<string> errors)
    {
        if (workflow.Stages.Count == 0)
        {
            errors.Add($"{path}/graph: workflow has no stages.");
            return;
        }
        var byId = workflow.Stages.ToDictionary(item => item.Identity);
        var reached = new HashSet<WorkflowStageIdentity>();
        var queue = new Queue<WorkflowStageIdentity>();
        queue.Enqueue(workflow.Stages[0].Identity);
        foreach (WorkflowStageDefinition stage in workflow.Stages)
            if (stage.AllowedSuccessors.Distinct().Count() != stage.AllowedSuccessors.Count)
                errors.Add($"{path}/stage/{stage.Identity}: stage successors are ambiguous or duplicated.");
        while (queue.TryDequeue(out WorkflowStageIdentity identity))
        {
            if (!reached.Add(identity)) continue;
            foreach (WorkflowStageIdentity successor in byId[identity].AllowedSuccessors)
                if (byId.ContainsKey(successor)) queue.Enqueue(successor);
        }
        foreach (WorkflowStageDefinition stage in workflow.Stages.Where(item => !reached.Contains(item.Identity)))
            errors.Add($"{path}/stage/{stage.Identity}: stage is unreachable from workflow entry.");
        if (!workflow.Stages.Any(stage => reached.Contains(stage.Identity) && stage.AllowedSuccessors.Count == 0))
            errors.Add($"{path}/graph: no reachable terminal stage exists.");
    }
}
