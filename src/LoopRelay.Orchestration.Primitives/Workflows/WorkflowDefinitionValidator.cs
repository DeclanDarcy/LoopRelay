namespace LoopRelay.Orchestration.Workflows;

public sealed record WorkflowDefinitionValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public static class WorkflowDefinitionValidator
{
    private static readonly string[] ImplementationDetailTerms =
    [
        "LoopRelay.Cli",
        "LoopRelay.Plan.Cli",
        "LoopRelay.Roadmap.Cli",
        "Program.cs",
        "sqlite",
        "SQLite",
        "IArtifactStore",
        "FileSystemArtifactStore",
        "file-backed",
    ];

    public static WorkflowDefinitionValidationResult Validate(WorkflowDefinition definition)
    {
        var errors = new List<string>();

        if (definition.Identity.IsEmpty)
        {
            errors.Add("Workflow identity is required.");
        }

        RequireExplanation(definition.Purpose, "Workflow purpose", errors);
        ValidateGate(definition.EntryGate, "entry gate", errors);
        ValidateGate(definition.ExitGate, "exit gate", errors);
        ValidateGate(definition.Completion.CompletionGate, "completion gate", errors);
        ValidateImplementationNeutralText(definition.Purpose, "workflow purpose", errors);
        ValidateImplementationNeutralText(definition.Blocker.Semantics, "blocker semantics", errors);
        ValidateImplementationNeutralText(definition.Recovery.Semantics, "recovery semantics", errors);

        var transitionIds = new HashSet<WorkflowTransitionIdentity>(
            definition.Transitions.Select(transition => transition.Identity));
        var stageIds = new HashSet<WorkflowStageIdentity>(
            definition.Stages.Select(stage => stage.Identity));
        var productIds = new HashSet<ProductIdentity>(
            definition.EntryProducts.Select(requirement => requirement.Product)
                .Concat(definition.ExitProducts.Select(product => product.Identity))
                .Concat(definition.Transitions.SelectMany(transition => transition.ProducedProducts.Select(product => product.Identity))));

        if (transitionIds.Count != definition.Transitions.Count)
        {
            errors.Add("Transition identities must be unique.");
        }

        if (stageIds.Count != definition.Stages.Count)
        {
            errors.Add("Stage identities must be unique.");
        }

        foreach (WorkflowStageDefinition stage in definition.Stages)
        {
            if (stage.Identity.IsEmpty)
            {
                errors.Add("Stage identity is required.");
            }

            RequireExplanation(stage.Purpose, $"Stage '{stage.Identity}' purpose", errors);
            ValidateImplementationNeutralText(stage.Purpose, $"stage '{stage.Identity}' purpose", errors);
            ValidateGate(stage.EntryGate, $"stage '{stage.Identity}' entry gate", errors);
            ValidateGate(stage.CompletionGate, $"stage '{stage.Identity}' completion gate", errors);

            foreach (WorkflowTransitionIdentity transition in stage.Transitions)
            {
                if (!transitionIds.Contains(transition))
                {
                    errors.Add($"Stage '{stage.Identity}' references unknown transition '{transition}'.");
                }
            }

            foreach (WorkflowStageIdentity successor in stage.AllowedSuccessors)
            {
                if (!stageIds.Contains(successor))
                {
                    errors.Add($"Stage '{stage.Identity}' references unknown successor stage '{successor}'.");
                }
            }

            ValidateDependencies(stage.Dependencies, productIds, transitionIds, stageIds, definition.Identity, errors);
        }

        foreach (WorkflowTransitionDefinition transition in definition.Transitions)
        {
            if (transition.Identity.IsEmpty)
            {
                errors.Add("Transition identity is required.");
            }

            RequireExplanation(transition.Purpose, $"Transition '{transition.Identity}' purpose", errors);
            RequireExplanation(transition.PromptIdentity, $"Transition '{transition.Identity}' prompt identity", errors);
            ValidateImplementationNeutralText(transition.Purpose, $"transition '{transition.Identity}' purpose", errors);
            ValidateGate(transition.InputGate, $"transition '{transition.Identity}' input gate", errors);
            ValidateGate(transition.OutputGate, $"transition '{transition.Identity}' output gate", errors);
            ValidateDependencies(transition.Dependencies, productIds, transitionIds, stageIds, definition.Identity, errors);

            foreach (WorkflowTransitionIdentity successor in transition.EligibleSuccessors)
            {
                if (!transitionIds.Contains(successor))
                {
                    errors.Add($"Transition '{transition.Identity}' references unknown successor transition '{successor}'.");
                }
            }

            foreach (ProductDefinition product in transition.ProducedProducts)
            {
                ValidateProduct(product, definition.Identity, transition.Identity, errors);
            }

            foreach (EffectDefinition effect in transition.Effects.OrderBy(effect => effect.Order))
            {
                if (effect.Identity.IsEmpty)
                {
                    errors.Add($"Transition '{transition.Identity}' contains an effect without an identity.");
                }

                RequireExplanation(effect.Trigger, $"Effect '{effect.Identity}' trigger", errors);
                RequireExplanation(effect.FailureSemantics, $"Effect '{effect.Identity}' failure semantics", errors);
                ValidateImplementationNeutralText(effect.Trigger, $"effect '{effect.Identity}' trigger", errors);
                ValidateImplementationNeutralText(effect.FailureSemantics, $"effect '{effect.Identity}' failure semantics", errors);
            }
        }

        foreach (ProductDefinition product in definition.ExitProducts)
        {
            ValidateProduct(product, definition.Identity, product.ProducerTransition, errors);
        }

        return new WorkflowDefinitionValidationResult(errors);
    }

    private static void ValidateProduct(
        ProductDefinition product,
        WorkflowIdentity workflow,
        WorkflowTransitionIdentity producerTransition,
        List<string> errors)
    {
        if (product.Identity.IsEmpty)
        {
            errors.Add("Product identity is required.");
        }

        if (product.ProducerWorkflow != workflow)
        {
            errors.Add($"Product '{product.Identity}' producer workflow must be '{workflow}'.");
        }

        if (product.ProducerTransition.IsEmpty || product.ProducerTransition != producerTransition)
        {
            errors.Add($"Product '{product.Identity}' producer transition is invalid.");
        }

        if (product.IntendedConsumers.Count == 0)
        {
            errors.Add($"Product '{product.Identity}' must declare at least one intended consumer.");
        }

        RequireExplanation(product.RepositoryOwnership, $"Product '{product.Identity}' repository ownership", errors);
        RequireExplanation(product.Authority, $"Product '{product.Identity}' authority", errors);

        if (product.StorageRepresentations.Count == 0)
        {
            errors.Add($"Product '{product.Identity}' must declare at least one storage representation.");
        }
    }

    private static void ValidateGate(GateDefinition gate, string label, List<string> errors)
    {
        if (gate.Identity.IsEmpty)
        {
            errors.Add($"{label} identity is required.");
        }

        RequireExplanation(gate.Purpose, $"{label} purpose", errors);
        RequireExplanation(gate.Authority, $"{label} authority", errors);
        RequireExplanation(gate.FailureSemantics, $"{label} failure semantics", errors);
        ValidateImplementationNeutralText(gate.Purpose, $"{label} purpose", errors);
        ValidateImplementationNeutralText(gate.FailureSemantics, $"{label} failure semantics", errors);

        if (gate.Requirements.Count == 0)
        {
            errors.Add($"{label} must declare explainable requirements.");
        }

        foreach (GateRequirementDefinition requirement in gate.Requirements)
        {
            RequireExplanation(requirement.Identity, $"{label} requirement identity", errors);
            RequireExplanation(requirement.Description, $"{label} requirement '{requirement.Identity}' description", errors);
        }
    }

    private static void ValidateDependencies(
        IReadOnlyList<TransitionDependency> dependencies,
        HashSet<ProductIdentity> productIds,
        HashSet<WorkflowTransitionIdentity> transitionIds,
        HashSet<WorkflowStageIdentity> stageIds,
        WorkflowIdentity workflowIdentity,
        List<string> errors)
    {
        foreach (TransitionDependency dependency in dependencies)
        {
            RequireExplanation(dependency.Identity, "Dependency identity", errors);
            RequireExplanation(dependency.Producer, $"Dependency '{dependency.Identity}' producer", errors);
            RequireExplanation(dependency.Consumer, $"Dependency '{dependency.Identity}' consumer", errors);

            switch (dependency.TargetKind)
            {
                case DependencyTargetKind.Product:
                    if (dependency.Product is not { IsEmpty: false } product || !productIds.Contains(product))
                    {
                        errors.Add($"Dependency '{dependency.Identity}' references an unknown product.");
                    }

                    break;
                case DependencyTargetKind.Transition:
                    if (dependency.Transition is not { IsEmpty: false } transition || !transitionIds.Contains(transition))
                    {
                        errors.Add($"Dependency '{dependency.Identity}' references an unknown transition.");
                    }

                    break;
                case DependencyTargetKind.Stage:
                    if (dependency.Stage is not { IsEmpty: false } stage || !stageIds.Contains(stage))
                    {
                        errors.Add($"Dependency '{dependency.Identity}' references an unknown stage.");
                    }

                    break;
                case DependencyTargetKind.Workflow:
                    if (dependency.Workflow is not { IsEmpty: false } workflow || workflow != workflowIdentity)
                    {
                        errors.Add($"Dependency '{dependency.Identity}' references an unknown workflow.");
                    }

                    break;
                default:
                    errors.Add($"Dependency '{dependency.Identity}' uses an unsupported target kind.");
                    break;
            }
        }
    }

    private static void RequireExplanation(string? value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{label} is required.");
        }
    }

    private static void ValidateImplementationNeutralText(string? value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (string term in ImplementationDetailTerms)
        {
            if (value.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label} must not embed implementation detail '{term}'.");
            }
        }
    }
}
