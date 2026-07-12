using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Resolution;

public sealed class WorkflowResolver
{
    public WorkflowResolutionResult Resolve(
        WorkflowInvocation invocation,
        RepositoryObservation observation,
        IReadOnlyList<WorkflowDefinition> definitions)
    {
        WorkflowSelectionResult selection = InvocationModeResolver.Resolve(invocation, observation);
        WorkflowDefinition? definition = definitions.FirstOrDefault(item => item.Identity == selection.SelectedWorkflow);
        if (definition is null)
        {
            return StoppedResult(
                RepositoryClassification.Unsupported,
                selection,
                WorkflowResolutionState.Invalid,
                null,
                observation,
                [new ResolutionWarning(
                    WarningCategory.Workflow,
                    $"Workflow definition '{selection.SelectedWorkflow}' is not registered.",
                    "workflow resolver",
                    "Register the workflow definition before resolving execution.",
                    [])],
                []);
        }

        if (observation.StorageVerification.IsUnusable)
        {
            RepositoryClassification storageClassification = observation.StorageVerification.Authority switch
            {
                StorageAuthorityKind.Corrupt => RepositoryClassification.Corrupt,
                StorageAuthorityKind.Unsupported => RepositoryClassification.Unsupported,
                StorageAuthorityKind.Ambiguous => RepositoryClassification.Ambiguous,
                _ => RepositoryClassification.StorageUnusable,
            };
            return StoppedResult(
                storageClassification,
                selection,
                WorkflowResolutionState.Invalid,
                null,
                observation,
                observation.StorageVerification.BlockingConditions,
                []);
        }

        IReadOnlyList<ObservedWorkflowState> observedWorkflowStates = observation.WorkflowStates
            .Where(state => state.Workflow == selection.SelectedWorkflow)
            .ToArray();
        if (observedWorkflowStates.Count > 1)
        {
            var ambiguity = new ResolutionAmbiguity(
                AmbiguityCategory.Workflow,
                $"Multiple states were observed for workflow '{selection.SelectedWorkflow}'.",
                observedWorkflowStates.SelectMany(state => state.Evidence).ToArray(),
                observedWorkflowStates.SelectMany(state => state.Evidence).ToArray());
            return StoppedResult(
                RepositoryClassification.Ambiguous,
                selection,
                WorkflowResolutionState.Ambiguous,
                null,
                observation,
                [],
                [ambiguity]);
        }

        ObservedWorkflowState? observed = observedWorkflowStates.SingleOrDefault();
        WorkflowResolutionState state = observed?.State ?? ResolveAbsentWorkflowState(definition, observation.Products);
        WorkflowStageIdentity? selectedStage = SelectStage(definition, observed, state);
        IReadOnlyList<TransitionEligibility> transitionEligibility =
            selectedStage is null || IsTerminal(state)
                ? []
                : ResolveTransitions(definition, selectedStage.Value, observation);
        RepositoryClassification classification = Classify(state, transitionEligibility, observation.StorageAuthority.Authority);
        IReadOnlyList<ResolutionWarning> warnings = observed?.Warnings
            .Concat(transitionEligibility.SelectMany(transition => transition.Warnings))
            .ToArray()
            ?? transitionEligibility.SelectMany(transition => transition.Warnings).ToArray();
        IReadOnlyList<string> satisfied = transitionEligibility.SelectMany(transition => transition.SatisfiedGates).Distinct(StringComparer.Ordinal).ToArray();
        IReadOnlyList<string> unsatisfied = transitionEligibility.SelectMany(transition => transition.UnsatisfiedGates).Distinct(StringComparer.Ordinal).ToArray();
        IReadOnlyList<string> evidence = selection.Evidence
            .Concat(observed?.Evidence ?? [])
            .Concat(transitionEligibility.SelectMany(transition => transition.Evidence))
            .Concat(observation.StorageAuthority.Evidence)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new WorkflowResolutionResult(
            classification,
            selection,
            state,
            selectedStage,
            transitionEligibility,
            new ResolutionExplanation(
                $"Selected {selection.SelectedWorkflow} in {selection.InvocationMode} mode.",
                selection.SelectedWorkflow,
                selectedStage,
                transitionEligibility
                    .Where(transition => transition.State == TransitionEligibilityState.Eligible)
                    .Select(transition => transition.Transition)
                    .ToArray(),
                satisfied,
                unsatisfied,
                warnings,
                evidence,
                observation.StorageAuthority,
                observation.Evidence.Where(item => item.Ignored).Select(item => item.Location).ToArray(),
                observation.StorageVerification.Conflicts,
                [],
                observation.StorageAuthority.ConfidenceQualifier,
                transitionEligibility.Any(transition => transition.State == TransitionEligibilityState.Ambiguous)
                    ? "One or more transitions are ambiguous."
                    : "None."));
    }

    private static WorkflowResolutionState ResolveAbsentWorkflowState(
        WorkflowDefinition definition,
        IReadOnlyList<ObservedProduct> products)
    {
        if (definition.EntryProducts.Count == 0 || RequirementsSatisfied(definition.EntryProducts, products))
        {
            return WorkflowResolutionState.EligibleToStart;
        }

        // Unsatisfied entry requirements are derived facts; the transition input gates report the
        // specific missing products on the next invocation instead of a latched cannot-proceed state.
        return WorkflowResolutionState.Absent;
    }

    private static WorkflowStageIdentity? SelectStage(
        WorkflowDefinition definition,
        ObservedWorkflowState? observed,
        WorkflowResolutionState state)
    {
        if (IsTerminal(state))
        {
            return null;
        }

        if (observed?.CurrentStage is { IsEmpty: false } currentStage)
        {
            return currentStage;
        }

        return definition.Stages
            .FirstOrDefault(stage => observed is null || !observed.CompletedStages.Contains(stage.Identity))
            ?.Identity;
    }

    private static IReadOnlyList<TransitionEligibility> ResolveTransitions(
        WorkflowDefinition definition,
        WorkflowStageIdentity selectedStage,
        RepositoryObservation observation)
    {
        WorkflowStageDefinition? stage = definition.Stages.FirstOrDefault(item => item.Identity == selectedStage);
        if (stage is null)
        {
            return [];
        }

        var results = new List<TransitionEligibility>();
        foreach (WorkflowTransitionIdentity transitionIdentity in stage.Transitions)
        {
            WorkflowTransitionDefinition? transition =
                definition.Transitions.FirstOrDefault(item => item.Identity == transitionIdentity);
            if (transition is null)
            {
                results.Add(new TransitionEligibility(
                    transitionIdentity,
                    TransitionEligibilityState.Invalid,
                    [],
                    [$"{selectedStage}.TransitionReference"],
                    [new ResolutionWarning(
                        WarningCategory.Stage,
                        $"Stage '{selectedStage}' references missing transition '{transitionIdentity}'.",
                        "workflow definition",
                        "Fix workflow definition before execution.",
                        [])],
                    []));
                continue;
            }

            ObservedTransitionRun? completedRun = observation.TransitionRuns.FirstOrDefault(
                run => run.Workflow == definition.Identity &&
                    run.Transition == transition.Identity &&
                    run.State == TransitionEligibilityState.Completed);
            if (completedRun is not null)
            {
                results.Add(new TransitionEligibility(
                    transition.Identity,
                    TransitionEligibilityState.Completed,
                    [$"{transition.Identity}.Completed"],
                    [],
                    [],
                    completedRun.Evidence));
                continue;
            }

            IReadOnlyList<ProductRequirement> missing = transition.RequiredInputProducts
                .Where(requirement => !ProductUsable(requirement, observation.Products))
                .ToArray();
            if (missing.Count == 0)
            {
                results.Add(new TransitionEligibility(
                    transition.Identity,
                    TransitionEligibilityState.Eligible,
                    transition.RequiredInputProducts.Select(requirement => $"{transition.Identity}.{requirement.Product}.Input").ToArray(),
                    [],
                    [],
                    ProductEvidence(transition.RequiredInputProducts, observation.Products)));
            }
            else
            {
                results.Add(new TransitionEligibility(
                    transition.Identity,
                    TransitionEligibilityState.MissingRequiredInput,
                    transition.RequiredInputProducts
                        .Except(missing)
                        .Select(requirement => $"{transition.Identity}.{requirement.Product}.Input")
                        .ToArray(),
                    missing.Select(requirement => $"{transition.Identity}.{requirement.Product}.Input").ToArray(),
                    missing.Select(requirement => new ResolutionWarning(
                        WarningCategory.Validation,
                        $"Required product '{requirement.Product}' is missing, stale, invalid, ambiguous, or not gate usable.",
                        "workflow resolver",
                        $"Produce and validate '{requirement.Product}' before running '{transition.Identity}'.",
                        [])).ToArray(),
                    ProductEvidence(transition.RequiredInputProducts, observation.Products)));
            }
        }

        return results;
    }

    private static bool RequirementsSatisfied(
        IReadOnlyList<ProductRequirement> requirements,
        IReadOnlyList<ObservedProduct> products) =>
        requirements.All(requirement => ProductUsable(requirement, products));

    private static bool ProductUsable(ProductRequirement requirement, IReadOnlyList<ObservedProduct> products)
    {
        ObservedProduct? observed = products.FirstOrDefault(product => product.Product.Identity == requirement.Product);
        if (observed is null || !observed.GateUsable)
        {
            return false;
        }

        if (requirement.RequiresFreshness && observed.Product.Freshness == ProductFreshness.Stale)
        {
            return false;
        }

        return observed.Product.ValidationState is not ProductValidationState.Invalid
            and not ProductValidationState.Stale
            and not ProductValidationState.Ambiguous;
    }

    private static IReadOnlyList<string> ProductEvidence(
        IReadOnlyList<ProductRequirement> requirements,
        IReadOnlyList<ObservedProduct> products) =>
        requirements
            .Select(requirement => products.FirstOrDefault(product => product.Product.Identity == requirement.Product))
            .Where(product => product is not null)
            .SelectMany(product => product!.Evidence)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static bool IsTerminal(WorkflowResolutionState state) =>
        state is WorkflowResolutionState.Completed
            or WorkflowResolutionState.Cancelled
            or WorkflowResolutionState.Failed
            or WorkflowResolutionState.Invalid
            or WorkflowResolutionState.Ambiguous;

    private static RepositoryClassification Classify(
        WorkflowResolutionState state,
        IReadOnlyList<TransitionEligibility> transitions,
        StorageAuthorityKind authority)
    {
        if (authority == StorageAuthorityKind.Corrupt)
        {
            return RepositoryClassification.Corrupt;
        }

        if (authority == StorageAuthorityKind.Unsupported)
        {
            return RepositoryClassification.Unsupported;
        }

        return state switch
        {
            WorkflowResolutionState.Absent or WorkflowResolutionState.EligibleToStart => RepositoryClassification.Fresh,
            WorkflowResolutionState.Active or WorkflowResolutionState.Resumable => RepositoryClassification.InProgress,
            WorkflowResolutionState.Completed => RepositoryClassification.Completed,
            WorkflowResolutionState.Waiting => RepositoryClassification.Waiting,
            WorkflowResolutionState.Cancelled => RepositoryClassification.Cancelled,
            WorkflowResolutionState.Failed => RepositoryClassification.Failed,
            WorkflowResolutionState.Invalid => RepositoryClassification.Failed,
            WorkflowResolutionState.Ambiguous => RepositoryClassification.Ambiguous,
            _ => RepositoryClassification.InProgress,
        };
    }

    private static WorkflowResolutionResult StoppedResult(
        RepositoryClassification classification,
        WorkflowSelectionResult selection,
        WorkflowResolutionState workflowState,
        WorkflowStageIdentity? selectedStage,
        RepositoryObservation observation,
        IReadOnlyList<ResolutionWarning> warnings,
        IReadOnlyList<ResolutionAmbiguity> ambiguities) =>
        new(
            classification,
            selection,
            workflowState,
            selectedStage,
            [],
            new ResolutionExplanation(
                $"Resolution stopped for {selection.SelectedWorkflow}.",
                selection.SelectedWorkflow,
                selectedStage,
                [],
                [],
                [],
                warnings,
                selection.Evidence.Concat(observation.StorageAuthority.Evidence).ToArray(),
                observation.StorageAuthority,
                observation.Evidence.Where(item => item.Ignored).Select(item => item.Location).ToArray(),
                observation.StorageVerification.Conflicts,
                ambiguities,
                observation.StorageAuthority.ConfidenceQualifier,
                ambiguities.Count > 0 ? "Ambiguity requires explicit resolution." : "Resolution stops until the remediation in the warnings is complete."));
}

public static class InvocationModeResolver
{
    public static WorkflowSelectionResult Resolve(
        WorkflowInvocation invocation,
        RepositoryObservation observation)
    {
        bool hasEvalIntent = observation.EvaluationIntentPaths.Count > 0;
        WorkflowIdentity selected = invocation.Mode switch
        {
            InvocationModeKind.ForcedEvalChain or InvocationModeKind.BoundedEval => WorkflowIdentity.EvalRoadmap,
            InvocationModeKind.ForcedTraditionalChain or InvocationModeKind.BoundedTraditional => WorkflowIdentity.TraditionalRoadmap,
            InvocationModeKind.BoundedPlan => WorkflowIdentity.Plan,
            InvocationModeKind.BoundedExecute => WorkflowIdentity.Execute,
            _ => hasEvalIntent ? WorkflowIdentity.EvalRoadmap : WorkflowIdentity.TraditionalRoadmap,
        };
        string chain = selected switch
        {
            var identity when identity == WorkflowIdentity.EvalRoadmap => "EvalRoadmapChain",
            var identity when identity == WorkflowIdentity.TraditionalRoadmap => "TraditionalRoadmapChain",
            var identity when identity == WorkflowIdentity.Plan => "BoundedPlan",
            var identity when identity == WorkflowIdentity.Execute => "BoundedExecute",
            _ => "Unknown",
        };
        string explanation = invocation.Mode == InvocationModeKind.DefaultChained
            ? hasEvalIntent
                ? "Default chained invocation selected EvalRoadmap because evaluation intent files were observed."
                : "Default chained invocation selected TraditionalRoadmap because no evaluation intent files were observed."
            : $"Explicit invocation mode selected {selected}.";

        return new WorkflowSelectionResult(
            invocation.Mode,
            selected,
            chain,
            invocation.IsBounded,
            hasEvalIntent ? observation.EvaluationIntentPaths : [],
            explanation);
    }
}
