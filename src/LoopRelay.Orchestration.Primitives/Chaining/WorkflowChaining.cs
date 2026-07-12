using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Chaining;

public enum WorkflowStopReason
{
    ChainCompleted,
    BoundedWorkflowCompleted,
    MissingRequiredInput,
    DirtyInputSurface,
    UnversionedInputSurface,
    StorageUnusable,
    RequiredEffectsPending,
    RecoveryRequired,
    WaitingForInteraction,
    CompatibilityImportRequired,
    UnsupportedProviderCapability,
    ConcurrentStateConflict,
    InputInvalidated,
    MissingRuntimePrerequisite,
    Waiting,
    Cancelled,
    Failed,
    Stalled,
    Ambiguous,
    NoEligibleTransition,
    TransitionCompleted,
}

public sealed record WorkflowRunContext(
    WorkspaceIdentity Workspace,
    RunIdentity Run,
    PolicyIdentity Policy,
    RuntimeProfileIdentity RuntimeProfile,
    PromptPolicyProfileIdentity PromptPolicyProfile);

public sealed record ProductTransferResult(
    WorkflowIdentity SourceWorkflow,
    WorkflowIdentity TargetWorkflow,
    IReadOnlyList<ProductRecord> Products,
    GateResult Gate,
    string Explanation);

public sealed record WorkflowBoundaryEvaluation(
    WorkflowIdentity SourceWorkflow,
    WorkflowIdentity? TargetWorkflow,
    GateResult ExitGate,
    GateResult? EntryGate,
    ProductTransferResult? ProductTransfer,
    bool CanAdvance,
    string Explanation);

public sealed record WorkflowBoundaryEvidenceRecord(
    WorkflowIdentity SourceWorkflow,
    WorkflowIdentity? TargetWorkflow,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record ChainBoundaryEvidenceCapture(
    RunIdentity Run,
    string ChainIdentity,
    WorkflowBoundaryEvaluation Evaluation,
    DateTimeOffset RecordedAt);

public interface IChainBoundaryEvidenceStore
{
    Task AppendAsync(ChainBoundaryEvidenceCapture capture, CancellationToken cancellationToken);
}

public interface ICanonicalRepositoryObservationSource
{
    Task<RepositoryObservation> ObserveAsync(CancellationToken cancellationToken = default);
}

public sealed record TransitionEffectCoordinationResult(
    bool RequiredEffectsPending,
    bool Failed,
    string Explanation,
    IReadOnlyList<string> Evidence);

public interface ITransitionEffectCoordinator
{
    Task<TransitionEffectCoordinationResult> CoordinateAsync(
        TransitionRuntimeResult attempt,
        CanonicalTransitionExecutionContext executionContext,
        CancellationToken cancellationToken);
}

public sealed record WorkflowControllerRequest(
    WorkflowInvocation Invocation,
    RepositoryObservation Observation,
    IReadOnlyList<WorkflowDefinition> Definitions,
    CanonicalTransitionExecutionContext ExecutionContext,
    AttemptAuthorization Authorization);

public sealed record WorkflowControllerResult(
    WorkflowResolutionResult Resolution,
    TransitionRuntimeResult? Transition,
    WorkflowStopReason StopReason,
    string Explanation,
    RepositoryObservation ObservationAfter,
    TransitionEffectCoordinationResult? EffectCoordination = null);

public sealed record WorkflowChainRunRequest(
    WorkflowInvocation Invocation,
    RepositoryObservation Observation,
    WorkflowChainDefinition Chain,
    IReadOnlyList<WorkflowDefinition> Definitions,
    WorkflowRunContext Context);

public interface IWorkflowInstanceRecorder
{
    Task<WorkflowInstanceIdentity> BeginInstanceAsync(
        RunIdentity run,
        WorkflowIdentity workflow,
        CancellationToken cancellationToken);

    Task CompleteInstanceAsync(
        WorkflowInstanceIdentity workflowInstance,
        string status,
        string? outcome,
        CancellationToken cancellationToken);
}

public sealed record WorkflowChainDecision(
    RunIdentity Run,
    WorkflowInstanceIdentity? CurrentWorkflowInstance,
    RuntimeOutcomeKind? TerminalOutcome,
    IReadOnlyList<ProductIdentity> ProductTransferManifest,
    bool RequiredEffectsPending,
    WorkflowIdentity? SelectedSuccessor,
    WorkflowStopReason StopReason,
    IReadOnlyList<string> Evidence);

public sealed record WorkflowChainRunResult(
    WorkflowIdentity LastWorkflow,
    WorkflowStopReason StopReason,
    IReadOnlyList<WorkflowBoundaryEvaluation> Boundaries,
    WorkflowControllerResult? ControllerResult,
    string Explanation,
    WorkflowChainDecision Decision);

public sealed class WorkflowEntryGateEvaluator
{
    public GateResult Evaluate(
        WorkflowDefinition workflow,
        IReadOnlyList<ProductRecord> transferredProducts,
        RepositoryObservation observation)
    {
        if (!observation.StorageAuthority.UsableAuthority)
        {
            return Gate(GateStatus.Unsatisfied, workflow.EntryGate,
                "Storage authority is not usable for workflow entry.", observation.StorageAuthority.Evidence);
        }

        IReadOnlyList<ProductRequirement> missing = workflow.EntryProducts
            .Where(requirement => !ProductUsable(requirement, transferredProducts))
            .ToArray();
        return missing.Count > 0
            ? Gate(GateStatus.Unsatisfied, workflow.EntryGate,
                $"Workflow '{workflow.Identity}' entry gate is missing required semantic products.",
                missing.Select(requirement => requirement.Product.Value).ToArray())
            : Gate(GateStatus.Satisfied, workflow.EntryGate,
                $"Workflow '{workflow.Identity}' entry gate is satisfied by transferred products.",
                transferredProducts.SelectMany(product => product.EvidenceLocations).ToArray());
    }

    private static bool ProductUsable(ProductRequirement requirement, IReadOnlyList<ProductRecord> products)
    {
        ProductRecord? product = products.FirstOrDefault(item => item.Identity == requirement.Product);
        return product is not null &&
            (!requirement.RequiresFreshness || product.Freshness != ProductFreshness.Stale) &&
            product.ValidationState is not ProductValidationState.Invalid
                and not ProductValidationState.Stale
                and not ProductValidationState.Ambiguous;
    }

    private static GateResult Gate(
        GateStatus status,
        GateDefinition definition,
        string explanation,
        IReadOnlyList<string> evidence) =>
        new(status, definition.Requirements.Select(requirement => new GateRequirementResult(
            requirement.Identity, status, explanation, evidence)).ToArray(), explanation, evidence);
}

public sealed class WorkflowExitGateEvaluator
{
    public GateResult Evaluate(WorkflowDefinition workflow, RepositoryObservation observation)
    {
        ObservedWorkflowState? state = observation.WorkflowStates
            .SingleOrDefault(item => item.Workflow == workflow.Identity);
        if (state?.State != WorkflowResolutionState.Completed)
        {
            return Gate(GateStatus.Unsatisfied, workflow.ExitGate,
                $"Workflow '{workflow.Identity}' is not completed.", state?.Evidence ?? []);
        }

        IReadOnlyList<ProductIdentity> missing = workflow.ExitProducts
            .Select(product => product.Identity)
            .Where(identity => !observation.Products.Any(product =>
                product.Product.Identity == identity && product.GateUsable))
            .ToArray();
        return missing.Count > 0
            ? Gate(GateStatus.Unsatisfied, workflow.ExitGate,
                $"Workflow '{workflow.Identity}' exit gate is missing required output products.",
                missing.Select(identity => identity.Value).ToArray())
            : Gate(GateStatus.Satisfied, workflow.ExitGate,
                $"Workflow '{workflow.Identity}' exit gate is satisfied.",
                state.Evidence.Concat(observation.Products.SelectMany(product => product.Evidence)).ToArray());
    }

    private static GateResult Gate(
        GateStatus status,
        GateDefinition definition,
        string explanation,
        IReadOnlyList<string> evidence) =>
        new(status, definition.Requirements.Select(requirement => new GateRequirementResult(
            requirement.Identity, status, explanation, evidence)).ToArray(), explanation, evidence);
}

public sealed class ProductTransferEvaluator
{
    public ProductTransferResult Evaluate(
        WorkflowDefinition source,
        WorkflowDefinition target,
        RepositoryObservation observation)
    {
        HashSet<ProductIdentity> required = target.EntryProducts.Select(requirement => requirement.Product).ToHashSet();
        IReadOnlyList<ProductRecord> products = observation.Products
            .Where(product => required.Contains(product.Product.Identity) && product.GateUsable)
            .Select(product => product.Product)
            .ToArray();
        IReadOnlyList<ProductIdentity> missing = required
            .Where(identity => products.All(product => product.Identity != identity))
            .ToArray();
        GateStatus status = missing.Count == 0 ? GateStatus.Satisfied : GateStatus.Unsatisfied;
        string explanation = missing.Count == 0
            ? $"Transferred semantic products from {source.Identity} to {target.Identity}."
            : $"Cannot transfer required semantic products from {source.Identity} to {target.Identity}.";
        return new ProductTransferResult(
            source.Identity,
            target.Identity,
            products,
            new GateResult(
                status,
                target.EntryGate.Requirements.Select(requirement => new GateRequirementResult(
                    requirement.Identity,
                    status,
                    explanation,
                    products.SelectMany(product => product.EvidenceLocations).ToArray())).ToArray(),
                explanation,
                products.SelectMany(product => product.EvidenceLocations)
                    .Concat(missing.Select(identity => identity.Value)).ToArray()),
            explanation);
    }
}

public sealed class WorkflowBoundaryEvidenceWriter(IChainBoundaryEvidenceStore _boundaryStore)
{
    private readonly List<WorkflowBoundaryEvidenceRecord> records = [];

    public IReadOnlyList<WorkflowBoundaryEvidenceRecord> Records => records;

    public async Task WriteAsync(
        WorkflowBoundaryEvaluation evaluation,
        RunIdentity run,
        string chainIdentity,
        CancellationToken cancellationToken = default)
    {
        string[] evidence = evaluation.ExitGate.Evidence
            .Concat(evaluation.EntryGate?.Evidence ?? [])
            .Concat(evaluation.ProductTransfer?.Gate.Evidence ?? [])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        records.Add(new WorkflowBoundaryEvidenceRecord(
            evaluation.SourceWorkflow,
            evaluation.TargetWorkflow,
            evaluation.Explanation,
            evidence));
        await _boundaryStore.AppendAsync(
            new ChainBoundaryEvidenceCapture(run, chainIdentity, evaluation, DateTimeOffset.UtcNow),
            cancellationToken);
    }
}

public sealed class WorkflowController(
    WorkflowResolver _resolver,
    ITransitionRuntime _transitionRuntime,
    ITransitionEffectCoordinator _effects,
    ICanonicalRepositoryObservationSource _observations)
{
    public async Task<WorkflowControllerResult> RunAsync(
        WorkflowControllerRequest request,
        CancellationToken cancellationToken = default)
    {
        WorkflowResolutionResult resolution = _resolver.Resolve(
            request.Invocation,
            request.Observation,
            request.Definitions);
        WorkflowStopReason? terminal = StopReasonFor(resolution);
        if (terminal is not null)
        {
            return new WorkflowControllerResult(
                resolution, null, terminal.Value, resolution.Explanation.Decision,
                request.Observation);
        }

        TransitionEligibility? selectedTransition = resolution.TransitionEligibility
            .FirstOrDefault(transition => transition.State == TransitionEligibilityState.Eligible);
        if (selectedTransition is null)
        {
            bool missingRequiredInput = resolution.TransitionEligibility
                .Any(transition => transition.State == TransitionEligibilityState.MissingRequiredInput);
            WorkflowStopReason reason = missingRequiredInput
                ? WorkflowStopReason.MissingRequiredInput
                : WorkflowStopReason.NoEligibleTransition;
            return new WorkflowControllerResult(
                resolution,
                null,
                reason,
                missingRequiredInput
                    ? "Every candidate transition is missing a required input product."
                    : "No eligible transition was available for the selected stage.",
                request.Observation);
        }

        TransitionRuntimeResult attempt = await _transitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                resolution.Selection.SelectedWorkflow,
                resolution.SelectedStage ?? new WorkflowStageIdentity("Unknown"),
                selectedTransition.Transition,
                request.ExecutionContext,
                request.Authorization),
            cancellationToken);
        TransitionEffectCoordinationResult? coordination = null;
        if (attempt.RequiredEffectsPending)
        {
            coordination = await _effects.CoordinateAsync(attempt, request.ExecutionContext, cancellationToken);
        }

        // Runtime results are evidence, not progression authority. Re-observe canonical state after
        // every attempt/effect cycle before selecting a stop or successor decision.
        RepositoryObservation observed = await _observations.ObserveAsync(cancellationToken);
        WorkflowStopReason stop = coordination is { Failed: true }
            ? WorkflowStopReason.Failed
            : coordination is { RequiredEffectsPending: true }
                ? WorkflowStopReason.RequiredEffectsPending
                : coordination is not null
                    ? WorkflowStopReason.TransitionCompleted
                : StopReasonFor(attempt.Outcome);
        return new WorkflowControllerResult(
            resolution,
            attempt,
            stop,
            coordination?.Explanation ?? attempt.Explanation,
            observed,
            coordination);
    }

    private static WorkflowStopReason? StopReasonFor(WorkflowResolutionResult resolution) =>
        resolution.Classification switch
        {
            RepositoryClassification.StorageUnusable or RepositoryClassification.Corrupt or RepositoryClassification.Unsupported =>
                WorkflowStopReason.StorageUnusable,
            RepositoryClassification.Waiting => WorkflowStopReason.Waiting,
            RepositoryClassification.Cancelled => WorkflowStopReason.Cancelled,
            RepositoryClassification.Failed => WorkflowStopReason.Failed,
            RepositoryClassification.Ambiguous => WorkflowStopReason.Ambiguous,
            RepositoryClassification.Completed => WorkflowStopReason.ChainCompleted,
            _ => null,
        };

    private static WorkflowStopReason StopReasonFor(RuntimeOutcomeKind outcome) => outcome switch
    {
        RuntimeOutcomeKind.Completed => WorkflowStopReason.TransitionCompleted,
        RuntimeOutcomeKind.EffectsPending => WorkflowStopReason.RequiredEffectsPending,
        RuntimeOutcomeKind.RecoveryRequired => WorkflowStopReason.RecoveryRequired,
        RuntimeOutcomeKind.HumanDecisionRequired => WorkflowStopReason.WaitingForInteraction,
        RuntimeOutcomeKind.CompatibilityImportRequired => WorkflowStopReason.CompatibilityImportRequired,
        RuntimeOutcomeKind.UnsupportedProviderCapability => WorkflowStopReason.UnsupportedProviderCapability,
        RuntimeOutcomeKind.ConcurrentStateConflict => WorkflowStopReason.ConcurrentStateConflict,
        RuntimeOutcomeKind.InputInvalidated => WorkflowStopReason.InputInvalidated,
        RuntimeOutcomeKind.MissingRequiredInput => WorkflowStopReason.MissingRequiredInput,
        RuntimeOutcomeKind.DirtyInputSurface => WorkflowStopReason.DirtyInputSurface,
        RuntimeOutcomeKind.UnversionedInputSurface => WorkflowStopReason.UnversionedInputSurface,
        RuntimeOutcomeKind.Waiting or RuntimeOutcomeKind.Paused => WorkflowStopReason.Waiting,
        RuntimeOutcomeKind.Cancelled => WorkflowStopReason.Cancelled,
        RuntimeOutcomeKind.Failed => WorkflowStopReason.Failed,
        RuntimeOutcomeKind.Stalled => WorkflowStopReason.Stalled,
        RuntimeOutcomeKind.Ambiguous => WorkflowStopReason.Ambiguous,
        _ => WorkflowStopReason.NoEligibleTransition,
    };
}

public sealed class WorkflowChainRunner(
    WorkflowResolver _resolver,
    WorkflowController _controller,
    WorkflowEntryGateEvaluator _entryGate,
    WorkflowExitGateEvaluator _exitGate,
    ProductTransferEvaluator _transfer,
    WorkflowBoundaryEvidenceWriter _evidenceWriter,
    IWorkflowInstanceRecorder _instances,
    ICanonicalRepositoryObservationSource _observations)
{
    public async Task<WorkflowChainRunResult> RunAsync(
        WorkflowChainRunRequest request,
        CancellationToken cancellationToken = default)
    {
        RepositoryObservation observation = request.Observation;
        WorkflowSelectionResult selection = InvocationModeResolver.Resolve(request.Invocation, observation);
        WorkflowIdentity current = selection.SelectedWorkflow;
        var boundaries = new List<WorkflowBoundaryEvaluation>();

        for (int guard = 0; guard < request.Chain.Workflows.Count + 1; guard++)
        {
            WorkflowDefinition definition = Definition(request.Definitions, current);
            WorkflowResolutionResult resolution = _resolver.Resolve(InvocationFor(current), observation, request.Definitions);
            if (resolution.WorkflowState != WorkflowResolutionState.Completed)
            {
                WorkflowInstanceIdentity instance = await _instances.BeginInstanceAsync(
                    request.Context.Run, current, cancellationToken);
                var execution = new CanonicalTransitionExecutionContext(
                    request.Invocation,
                    request.Context.Workspace,
                    request.Context.Run,
                    instance,
                    request.Context.Policy,
                    request.Context.RuntimeProfile,
                    request.Context.PromptPolicyProfile);
                WorkflowControllerResult controller = await _controller.RunAsync(
                    new WorkflowControllerRequest(
                        InvocationFor(current),
                        observation,
                        request.Definitions,
                        execution,
                        FreshAttemptAuthorization.Instance),
                    cancellationToken);
                await _instances.CompleteInstanceAsync(
                    instance,
                    controller.StopReason == WorkflowStopReason.TransitionCompleted ? "Active" : "Stopped",
                    controller.StopReason.ToString(),
                    CancellationToken.None);
                return Result(
                    request.Context.Run,
                    current,
                    controller.StopReason,
                    boundaries,
                    controller,
                    controller.Explanation,
                    instance,
                    controller.Transition?.Outcome,
                    controller.Transition?.RequiredEffectsPending ?? false,
                    null,
                    controller.Transition?.Evidence ?? []);
            }

            if (request.Invocation.IsBounded)
            {
                return Result(request.Context.Run, current, WorkflowStopReason.BoundedWorkflowCompleted,
                    boundaries, null, $"Bounded invocation stopped after {current} completed.",
                    null, RuntimeOutcomeKind.Completed, false, null, []);
            }

            if (definition.DownstreamWorkflow is not { IsEmpty: false } downstream)
            {
                return Result(request.Context.Run, current, WorkflowStopReason.ChainCompleted,
                    boundaries, null, $"Workflow chain completed at {current}.",
                    null, RuntimeOutcomeKind.Completed, false, null, []);
            }

            WorkflowDefinition target = Definition(request.Definitions, downstream);
            GateResult exit = _exitGate.Evaluate(definition, observation);
            ProductTransferResult transfer = _transfer.Evaluate(definition, target, observation);
            GateResult entry = _entryGate.Evaluate(target, transfer.Products, observation);
            bool canAdvance = exit.IsSatisfied && transfer.Gate.IsSatisfied && entry.IsSatisfied;
            var boundary = new WorkflowBoundaryEvaluation(
                definition.Identity,
                downstream,
                exit,
                entry,
                transfer,
                canAdvance,
                canAdvance
                    ? $"Advanced from {definition.Identity} to {downstream}."
                    : $"Stopped before {downstream} because a workflow boundary gate was unsatisfied.");
            boundaries.Add(boundary);
            await _evidenceWriter.WriteAsync(
                boundary, request.Context.Run, request.Chain.Identity, cancellationToken);
            if (!canAdvance)
            {
                WorkflowStopReason reason = observation.StorageAuthority.UsableAuthority
                    ? WorkflowStopReason.MissingRequiredInput
                    : WorkflowStopReason.StorageUnusable;
                return Result(request.Context.Run, current, reason, boundaries, null, boundary.Explanation,
                    null, null, false, null,
                    exit.Evidence.Concat(entry.Evidence).Concat(transfer.Gate.Evidence).ToArray());
            }

            current = downstream;
            observation = await _observations.ObserveAsync(cancellationToken);
        }

        return Result(request.Context.Run, current, WorkflowStopReason.Stalled, boundaries, null,
            "Workflow chain guard exhausted without terminal progress.", null,
            RuntimeOutcomeKind.Stalled, false, null, []);
    }

    private static WorkflowChainRunResult Result(
        RunIdentity run,
        WorkflowIdentity workflow,
        WorkflowStopReason stop,
        IReadOnlyList<WorkflowBoundaryEvaluation> boundaries,
        WorkflowControllerResult? controller,
        string explanation,
        WorkflowInstanceIdentity? instance,
        RuntimeOutcomeKind? terminalOutcome,
        bool pendingEffects,
        WorkflowIdentity? successor,
        IReadOnlyList<string> evidence)
    {
        ProductIdentity[] manifest = boundaries
            .SelectMany(boundary => boundary.ProductTransfer?.Products ?? [])
            .Select(product => product.Identity)
            .Distinct()
            .ToArray();
        return new WorkflowChainRunResult(
            workflow,
            stop,
            boundaries,
            controller,
            explanation,
            new WorkflowChainDecision(run, instance, terminalOutcome, manifest, pendingEffects,
                successor, stop, evidence));
    }

    private static WorkflowDefinition Definition(
        IReadOnlyList<WorkflowDefinition> definitions,
        WorkflowIdentity identity) =>
        definitions.Single(definition => definition.Identity == identity);

    private static WorkflowInvocation InvocationFor(WorkflowIdentity identity) => identity.Value switch
    {
        "EvalRoadmap" => new WorkflowInvocation(InvocationModeKind.BoundedEval),
        "TraditionalRoadmap" => new WorkflowInvocation(InvocationModeKind.BoundedTraditional),
        "Plan" => new WorkflowInvocation(InvocationModeKind.BoundedPlan),
        "Execute" => new WorkflowInvocation(InvocationModeKind.BoundedExecute),
        _ => throw new InvalidOperationException($"Unsupported workflow identity '{identity}'."),
    };
}
