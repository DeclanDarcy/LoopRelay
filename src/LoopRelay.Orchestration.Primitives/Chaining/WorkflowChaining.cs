using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Chaining;

public enum WorkflowStopReason
{
    ChainCompleted,
    BoundedWorkflowCompleted,
    Blocked,
    Waiting,
    Cancelled,
    Failed,
    Stalled,
    Ambiguous,
    NoEligibleTransition,
    TransitionCompleted,
}

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

public sealed record WorkflowControllerRequest(
    WorkflowInvocation Invocation,
    RepositoryObservation Observation,
    IReadOnlyList<WorkflowDefinition> Definitions,
    RunIdentity? Run = null,
    WorkflowInstanceIdentity? WorkflowInstance = null);

public sealed record WorkflowControllerResult(
    WorkflowResolutionResult Resolution,
    TransitionRuntimeResult? Transition,
    WorkflowStopReason StopReason,
    string Explanation);

public sealed record WorkflowChainRunRequest(
    WorkflowInvocation Invocation,
    RepositoryObservation Observation,
    WorkflowChainDefinition Chain,
    IReadOnlyList<WorkflowDefinition> Definitions,
    RunIdentity? Run = null);

public interface IWorkflowInstanceRecorder
{
    Task<string> BeginInstanceAsync(
        string runId,
        WorkflowIdentity workflow,
        CancellationToken cancellationToken);

    Task CompleteInstanceAsync(
        string workflowInstanceId,
        string status,
        string? outcome,
        CancellationToken cancellationToken);
}

public sealed record WorkflowChainRunResult(
    WorkflowIdentity LastWorkflow,
    WorkflowStopReason StopReason,
    IReadOnlyList<WorkflowBoundaryEvaluation> Boundaries,
    WorkflowControllerResult? ControllerResult,
    string Explanation);

public sealed class WorkflowEntryGateEvaluator
{
    public GateResult Evaluate(
        WorkflowDefinition workflow,
        IReadOnlyList<ProductRecord> transferredProducts,
        RepositoryObservation observation)
    {
        if (!observation.StorageAuthority.UsableAuthority)
        {
            return Gate(
                GateStatus.Blocked,
                workflow.EntryGate,
                "Storage authority is not usable for workflow entry.",
                observation.StorageAuthority.Evidence);
        }

        IReadOnlyList<ProductRequirement> missing = workflow.EntryProducts
            .Where(requirement => !ProductUsable(requirement, transferredProducts))
            .ToArray();
        if (missing.Count > 0)
        {
            return Gate(
                GateStatus.Blocked,
                workflow.EntryGate,
                $"Workflow '{workflow.Identity}' entry gate is missing required semantic products.",
                missing.Select(requirement => requirement.Product.Value).ToArray());
        }

        return Gate(
            GateStatus.Satisfied,
            workflow.EntryGate,
            $"Workflow '{workflow.Identity}' entry gate is satisfied by transferred products.",
            transferredProducts.SelectMany(product => product.EvidenceLocations).ToArray());
    }

    private static bool ProductUsable(ProductRequirement requirement, IReadOnlyList<ProductRecord> products)
    {
        ProductRecord? product = products.FirstOrDefault(item => item.Identity == requirement.Product);
        if (product is null)
        {
            return false;
        }

        if (requirement.RequiresFreshness && product.Freshness == ProductFreshness.Stale)
        {
            return false;
        }

        return product.ValidationState is not ProductValidationState.Invalid
            and not ProductValidationState.Stale
            and not ProductValidationState.Ambiguous;
    }

    private static GateResult Gate(
        GateStatus status,
        GateDefinition definition,
        string explanation,
        IReadOnlyList<string> evidence) =>
        new(
            status,
            definition.Requirements.Select(requirement => new GateRequirementResult(
                requirement.Identity,
                status,
                explanation,
                evidence)).ToArray(),
            explanation,
            evidence);
}

public sealed class WorkflowExitGateEvaluator
{
    public GateResult Evaluate(
        WorkflowDefinition workflow,
        RepositoryObservation observation)
    {
        ObservedWorkflowState? state = observation.WorkflowStates
            .SingleOrDefault(item => item.Workflow == workflow.Identity);
        if (state?.State != WorkflowResolutionState.Completed)
        {
            return Gate(
                GateStatus.Unsatisfied,
                workflow.ExitGate,
                $"Workflow '{workflow.Identity}' is not completed.",
                state?.Evidence ?? []);
        }

        IReadOnlyList<ProductIdentity> missing = workflow.ExitProducts
            .Select(product => product.Identity)
            .Where(identity => !observation.Products.Any(product => product.Product.Identity == identity && product.GateUsable))
            .ToArray();
        if (missing.Count > 0)
        {
            return Gate(
                GateStatus.Blocked,
                workflow.ExitGate,
                $"Workflow '{workflow.Identity}' exit gate is missing required output products.",
                missing.Select(identity => identity.Value).ToArray());
        }

        return Gate(
            GateStatus.Satisfied,
            workflow.ExitGate,
            $"Workflow '{workflow.Identity}' exit gate is satisfied.",
            state.Evidence.Concat(observation.Products.SelectMany(product => product.Evidence)).ToArray());
    }

    private static GateResult Gate(
        GateStatus status,
        GateDefinition definition,
        string explanation,
        IReadOnlyList<string> evidence) =>
        new(
            status,
            definition.Requirements.Select(requirement => new GateRequirementResult(
                requirement.Identity,
                status,
                explanation,
                evidence)).ToArray(),
            explanation,
            evidence);
}

public sealed class ProductTransferEvaluator
{
    public ProductTransferResult Evaluate(
        WorkflowDefinition source,
        WorkflowDefinition target,
        RepositoryObservation observation)
    {
        HashSet<ProductIdentity> required = target.EntryProducts
            .Select(requirement => requirement.Product)
            .ToHashSet();
        IReadOnlyList<ProductRecord> products = observation.Products
            .Where(product => required.Contains(product.Product.Identity))
            .Select(product => product.Product)
            .ToArray();
        IReadOnlyList<ProductIdentity> missing = required
            .Where(identity => products.All(product => product.Identity != identity))
            .ToArray();
        GateStatus status = missing.Count == 0 ? GateStatus.Satisfied : GateStatus.Blocked;
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
                products.SelectMany(product => product.EvidenceLocations).Concat(missing.Select(identity => identity.Value)).ToArray()),
            explanation);
    }
}

public sealed class WorkflowBoundaryEvidenceWriter
{
    private readonly List<WorkflowBoundaryEvidenceRecord> _records = [];

    public IReadOnlyList<WorkflowBoundaryEvidenceRecord> Records => _records;

    public Task WriteAsync(
        WorkflowBoundaryEvaluation evaluation,
        CancellationToken cancellationToken = default)
    {
        _records.Add(new WorkflowBoundaryEvidenceRecord(
            evaluation.SourceWorkflow,
            evaluation.TargetWorkflow,
            evaluation.Explanation,
            evaluation.ExitGate.Evidence
                .Concat(evaluation.EntryGate?.Evidence ?? [])
                .Concat(evaluation.ProductTransfer?.Gate.Evidence ?? [])
                .ToArray()));
        return Task.CompletedTask;
    }
}

public sealed class WorkflowController(
    WorkflowResolver _resolver,
    ITransitionRuntime _transitionRuntime)
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
                resolution,
                null,
                terminal.Value,
                resolution.Explanation.Decision);
        }

        TransitionEligibility? selectedTransition = resolution.TransitionEligibility
            .FirstOrDefault(transition => transition.State == TransitionEligibilityState.Eligible);
        if (selectedTransition is null)
        {
            return new WorkflowControllerResult(
                resolution,
                null,
                WorkflowStopReason.NoEligibleTransition,
                "No eligible transition was available for the selected stage.");
        }

        TransitionRuntimeResult transitionResult = await _transitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                resolution.Selection.SelectedWorkflow,
                resolution.SelectedStage ?? new WorkflowStageIdentity("Unknown"),
                selectedTransition.Transition,
                Run: request.Run,
                WorkflowInstance: request.WorkflowInstance),
            cancellationToken);
        return new WorkflowControllerResult(
            resolution,
            transitionResult,
            transitionResult.Outcome == RuntimeOutcomeKind.Completed
                ? WorkflowStopReason.TransitionCompleted
                : StopReasonFor(transitionResult.Outcome),
            transitionResult.Explanation);
    }

    private static WorkflowStopReason? StopReasonFor(WorkflowResolutionResult resolution) =>
        resolution.Classification switch
        {
            RepositoryClassification.Blocked or RepositoryClassification.Corrupt or RepositoryClassification.Unsupported => WorkflowStopReason.Blocked,
            RepositoryClassification.Waiting => WorkflowStopReason.Waiting,
            RepositoryClassification.Cancelled => WorkflowStopReason.Cancelled,
            RepositoryClassification.Failed => WorkflowStopReason.Failed,
            RepositoryClassification.Ambiguous => WorkflowStopReason.Ambiguous,
            RepositoryClassification.Completed => WorkflowStopReason.ChainCompleted,
            _ => null,
        };

    private static WorkflowStopReason StopReasonFor(RuntimeOutcomeKind outcome) =>
        outcome switch
        {
            RuntimeOutcomeKind.Blocked => WorkflowStopReason.Blocked,
            RuntimeOutcomeKind.Waiting => WorkflowStopReason.Waiting,
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
    IWorkflowInstanceRecorder? _instanceRecorder = null)
{
    public async Task<WorkflowChainRunResult> RunAsync(
        WorkflowChainRunRequest request,
        CancellationToken cancellationToken = default)
    {
        WorkflowSelectionResult selection = InvocationModeResolver.Resolve(
            request.Invocation,
            request.Observation);
        WorkflowIdentity current = selection.SelectedWorkflow;
        var boundaries = new List<WorkflowBoundaryEvaluation>();

        for (int guard = 0; guard < request.Chain.Workflows.Count + 1; guard++)
        {
            WorkflowDefinition definition = Definition(request.Definitions, current);
            string? instanceId = await TryBeginInstanceAsync(request.Run, current, cancellationToken);
            WorkflowResolutionResult resolution = _resolver.Resolve(
                InvocationFor(current),
                request.Observation,
                request.Definitions);

            if (resolution.WorkflowState != WorkflowResolutionState.Completed)
            {
                WorkflowControllerResult controller = await _controller.RunAsync(
                    new WorkflowControllerRequest(
                        InvocationFor(current),
                        request.Observation,
                        request.Definitions,
                        request.Run,
                        instanceId is null ? null : new WorkflowInstanceIdentity(instanceId)),
                    cancellationToken);
                await TryCompleteInstanceAsync(instanceId, "Stopped", controller.StopReason.ToString(), cancellationToken);
                return new WorkflowChainRunResult(
                    current,
                    controller.StopReason,
                    boundaries,
                    controller,
                    controller.Explanation);
            }

            if (request.Invocation.IsBounded)
            {
                await TryCompleteInstanceAsync(instanceId, "Completed", WorkflowStopReason.BoundedWorkflowCompleted.ToString(), cancellationToken);
                return new WorkflowChainRunResult(
                    current,
                    WorkflowStopReason.BoundedWorkflowCompleted,
                    boundaries,
                    null,
                    $"Bounded invocation stopped after {current} completed.");
            }

            if (definition.DownstreamWorkflow is not { IsEmpty: false } downstream)
            {
                await TryCompleteInstanceAsync(instanceId, "Completed", WorkflowStopReason.ChainCompleted.ToString(), cancellationToken);
                return new WorkflowChainRunResult(
                    current,
                    WorkflowStopReason.ChainCompleted,
                    boundaries,
                    null,
                    $"Workflow chain completed at {current}.");
            }

            WorkflowDefinition downstreamDefinition = Definition(request.Definitions, downstream);
            GateResult exit = _exitGate.Evaluate(definition, request.Observation);
            ProductTransferResult transfer = _transfer.Evaluate(definition, downstreamDefinition, request.Observation);
            GateResult entry = _entryGate.Evaluate(downstreamDefinition, transfer.Products, request.Observation);
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
            await _evidenceWriter.WriteAsync(boundary, cancellationToken);

            if (!canAdvance)
            {
                await TryCompleteInstanceAsync(instanceId, "Completed", WorkflowStopReason.Blocked.ToString(), cancellationToken);
                return new WorkflowChainRunResult(
                    current,
                    WorkflowStopReason.Blocked,
                    boundaries,
                    null,
                    boundary.Explanation);
            }

            await TryCompleteInstanceAsync(instanceId, "Completed", null, cancellationToken);
            current = downstream;
        }

        return new WorkflowChainRunResult(
            current,
            WorkflowStopReason.Stalled,
            boundaries,
            null,
            "Workflow chain guard exhausted without terminal progress.");
    }

    private async Task<string?> TryBeginInstanceAsync(
        RunIdentity? run,
        WorkflowIdentity workflow,
        CancellationToken cancellationToken)
    {
        if (_instanceRecorder is null || run is not { IsEmpty: false } runIdentity)
        {
            return null;
        }

        try
        {
            return await _instanceRecorder.BeginInstanceAsync(runIdentity.Value, workflow, cancellationToken);
        }
        catch
        {
            // Instance recording is supporting evidence; chain progression remains authoritative.
            return null;
        }
    }

    private async Task TryCompleteInstanceAsync(
        string? workflowInstanceId,
        string status,
        string? outcome,
        CancellationToken cancellationToken)
    {
        if (_instanceRecorder is null || workflowInstanceId is null)
        {
            return;
        }

        try
        {
            // Terminal instance evidence must survive a fired token; the store write always runs uncancelled.
            await _instanceRecorder.CompleteInstanceAsync(workflowInstanceId, status, outcome, CancellationToken.None);
        }
        catch
        {
            // Instance recording is supporting evidence; chain progression remains authoritative.
        }
    }

    private static WorkflowDefinition Definition(
        IReadOnlyList<WorkflowDefinition> definitions,
        WorkflowIdentity identity) =>
        definitions.Single(definition => definition.Identity == identity);

    private static WorkflowInvocation InvocationFor(WorkflowIdentity identity)
    {
        if (identity == WorkflowIdentity.EvalRoadmap)
        {
            return new WorkflowInvocation(InvocationModeKind.BoundedEval);
        }

        if (identity == WorkflowIdentity.TraditionalRoadmap)
        {
            return new WorkflowInvocation(InvocationModeKind.BoundedTraditional);
        }

        if (identity == WorkflowIdentity.Plan)
        {
            return new WorkflowInvocation(InvocationModeKind.BoundedPlan);
        }

        return new WorkflowInvocation(InvocationModeKind.BoundedExecute);
    }
}
