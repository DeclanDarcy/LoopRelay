using System.Diagnostics;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Runtime;

public sealed class TransitionRuntime(
    ITransitionDefinitionResolver _definitionResolver,
    IProductResolver _productResolver,
    IGateEvaluator _gateEvaluator,
    IPromptContextBuilder _promptContextBuilder,
    IPromptRenderer _promptRenderer,
    IPromptExecutor _promptExecutor,
    IOutputInterpreter _outputInterpreter,
    IProductValidator _productValidator,
    IEffectExecutor _effectExecutor,
    ITransitionRunStore _runStore,
    ITransitionEvidenceStore _evidenceStore,
    ITransitionBlockerStore? _blockerStore = null,
    ITransitionRecoveryStore? _recoveryStore = null,
    ITransitionGateEvaluationStore? _gateEvaluationStore = null,
    ITransitionEffectStore? _effectStore = null) : ITransitionRuntime
{
    public async Task<TransitionRuntimeResult> RunAsync(
        TransitionRuntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        string runId = Guid.NewGuid().ToString("N");
        WorkflowTransitionIdentity transitionIdentity = request.Transition;
        WorkflowTransitionDefinition? resolvedDefinition = null;
        GateResult? inputGate = null;
        GateResult? outputGate = null;
        ProductValidationResult? validation = null;
        EffectExecutionResult? effects = null;

        try
        {
            WorkflowTransitionDefinition definition =
                await _definitionResolver.ResolveAsync(request, cancellationToken);
            resolvedDefinition = definition;
            transitionIdentity = definition.Identity;

            ProductResolutionResult inputs =
                await _productResolver.ResolveAsync(definition.RequiredInputProducts, cancellationToken);
            inputGate = await _gateEvaluator.EvaluateInputGateAsync(definition.InputGate, inputs, cancellationToken);
            await TryRecordGateEvaluationAsync(runId, request, definition.Identity, definition.InputGate, inputGate, cancellationToken);
            if (!inputGate.IsSatisfied)
            {
                TransitionRuntimeResult result = Result(
                    MapGateStatus(inputGate.Status),
                    TransitionDurableState.Blocked,
                    definition.Identity,
                    inputGate,
                    null,
                    null,
                    null,
                    [],
                    inputGate.Explanation,
                    inputGate.Evidence);
                await TryPersistStateAsync(runId, definition.Identity, result.DurableState, result.Explanation, result.Evidence, cancellationToken);
                await TryRecordEventAsync(runId, definition.Identity, result.DurableState, "TransitionBlocked", result.Explanation, result.Evidence, cancellationToken);
                await TryRecordBlockerAsync(
                    runId,
                    request,
                    definition.Identity,
                    BlockerCategory.Validation,
                    result.Explanation,
                    $"Provide valid required input products before rerunning '{definition.Identity}'.",
                    result.Evidence,
                    cancellationToken);
                await TryRecordRecoveryMarkerAsync(runId, request, definition, definition.Identity, result, cancellationToken);
                return result;
            }

            PromptContext context = await _promptContextBuilder.BuildAsync(
                request,
                definition,
                inputs,
                cancellationToken);
            RenderedPrompt renderedPrompt = await _promptRenderer.RenderAsync(
                definition,
                context,
                cancellationToken);
            DateTimeOffset startedAt = DateTimeOffset.UtcNow;
            await _runStore.PersistStartedAsync(
                new TransitionRunStarted(runId, startedAt, request, definition, context.InputSnapshot, renderedPrompt),
                cancellationToken);
            await _evidenceStore.RecordEventAsync(
                new TransitionEvidenceEvent(
                    runId,
                    startedAt,
                    definition.Identity,
                    TransitionDurableState.Started,
                    "TransitionStarted",
                    "Transition input gate satisfied and prompt rendered.",
                    [renderedPrompt.EvidenceLocation, context.InputSnapshot.Hash]),
                cancellationToken);

            var stopwatch = Stopwatch.StartNew();
            PromptExecutionResult executionResult = await _promptExecutor.ExecuteAsync(
                definition,
                renderedPrompt,
                cancellationToken);
            stopwatch.Stop();

            if (executionResult.Status == PromptExecutionStatus.Cancelled)
            {
                return await CancelledAsync(
                    runId,
                    request,
                    definition,
                    definition.Identity,
                    inputGate,
                    "Prompt execution reported cancellation.",
                    cancellationToken);
            }

            await _evidenceStore.RecordRawOutputAsync(
                runId,
                definition.Identity,
                executionResult with { Duration = executionResult.Duration == TimeSpan.Zero ? stopwatch.Elapsed : executionResult.Duration },
                cancellationToken);
            await _runStore.PersistStateAsync(
                new TransitionRunStateUpdate(
                    runId,
                    DateTimeOffset.UtcNow,
                    definition.Identity,
                    TransitionDurableState.PromptCompleted,
                    "Prompt execution completed and raw output was captured.",
                    []),
                cancellationToken);

            if (executionResult.Status == PromptExecutionStatus.Failed)
            {
                string failure = executionResult.FailureMessage ?? "Prompt execution failed.";
                await _evidenceStore.RecordFailureAsync(runId, definition.Identity, failure, cancellationToken);
                TransitionRuntimeResult result = Result(
                    RuntimeOutcomeKind.Failed,
                    TransitionDurableState.Failed,
                    definition.Identity,
                    inputGate,
                    null,
                    null,
                    null,
                    [],
                    failure,
                    []);
                await TryPersistStateAsync(runId, definition.Identity, result.DurableState, result.Explanation, result.Evidence, cancellationToken);
                await TryRecordRecoveryMarkerAsync(runId, request, definition, definition.Identity, result, cancellationToken);
                return result;
            }

            InterpretedTransitionOutput interpreted = await _outputInterpreter.InterpretAsync(
                definition,
                executionResult,
                cancellationToken);
            await _runStore.PersistStateAsync(
                new TransitionRunStateUpdate(
                    runId,
                    DateTimeOffset.UtcNow,
                    definition.Identity,
                    TransitionDurableState.OutputInterpreted,
                    interpreted.Explanation,
                    interpreted.Evidence),
                cancellationToken);

            if (interpreted.Status != OutputInterpretationStatus.Valid)
            {
                TransitionRuntimeResult result = Result(
                    RuntimeOutcomeKind.Failed,
                    TransitionDurableState.Failed,
                    definition.Identity,
                    inputGate,
                    null,
                    null,
                    null,
                    [],
                    interpreted.Explanation,
                    interpreted.Evidence);
                await TryPersistStateAsync(runId, definition.Identity, result.DurableState, result.Explanation, result.Evidence, cancellationToken);
                await TryRecordEventAsync(runId, definition.Identity, result.DurableState, "TransitionOutputInvalid", result.Explanation, result.Evidence, cancellationToken);
                await TryRecordRecoveryMarkerAsync(runId, request, definition, definition.Identity, result, cancellationToken);
                return result;
            }

            validation = await _productValidator.ValidateAsync(definition, interpreted, cancellationToken);
            outputGate = await _gateEvaluator.EvaluateOutputGateAsync(definition.OutputGate, validation, cancellationToken);
            await TryRecordGateEvaluationAsync(runId, request, definition.Identity, definition.OutputGate, outputGate, cancellationToken);
            if (!validation.IsValid || !outputGate.IsSatisfied)
            {
                string explanation = validation.IsValid ? outputGate.Explanation : validation.Explanation;
                IReadOnlyList<string> evidence = validation.IsValid ? outputGate.Evidence : validation.Evidence;
                TransitionRuntimeResult result = Result(
                    RuntimeOutcomeKind.Failed,
                    TransitionDurableState.Failed,
                    definition.Identity,
                    inputGate,
                    outputGate,
                    validation,
                    null,
                    [],
                    explanation,
                    evidence);
                await TryPersistStateAsync(runId, definition.Identity, result.DurableState, result.Explanation, result.Evidence, cancellationToken);
                await TryRecordEventAsync(runId, definition.Identity, result.DurableState, "TransitionProductInvalid", result.Explanation, result.Evidence, cancellationToken);
                await TryRecordRecoveryMarkerAsync(runId, request, definition, definition.Identity, result, cancellationToken);
                return result;
            }

            await _runStore.PersistStateAsync(
                new TransitionRunStateUpdate(
                    runId,
                    DateTimeOffset.UtcNow,
                    definition.Identity,
                    TransitionDurableState.OutputValidated,
                    validation.Explanation,
                    validation.Evidence),
                cancellationToken);

            effects = await _effectExecutor.ExecuteAsync(definition, validation, cancellationToken);
            await TryRecordEffectsAsync(runId, request, definition, effects, cancellationToken);
            if (!effects.IsSuccess)
            {
                TransitionDurableState state = effects.Status switch
                {
                    EffectExecutionStatus.Stalled => TransitionDurableState.Stalled,
                    EffectExecutionStatus.PartiallyFailed => TransitionDurableState.EffectsPartiallyApplied,
                    _ => TransitionDurableState.Failed,
                };
                RuntimeOutcomeKind outcome = effects.Status == EffectExecutionStatus.Stalled
                    ? RuntimeOutcomeKind.Stalled
                    : RuntimeOutcomeKind.Failed;
                TransitionRuntimeResult result = Result(
                    outcome,
                    state,
                    definition.Identity,
                    inputGate,
                    outputGate,
                    validation,
                    effects,
                    [],
                    effects.Explanation,
                    effects.Evidence);
                await TryPersistStateAsync(runId, definition.Identity, result.DurableState, result.Explanation, result.Evidence, cancellationToken);
                await TryRecordEventAsync(
                    runId,
                    definition.Identity,
                    result.DurableState,
                    effects.Status == EffectExecutionStatus.Stalled ? "TransitionStalled" : "TransitionEffectFailure",
                    result.Explanation,
                    result.Evidence,
                    cancellationToken);
                await TryRecordRecoveryMarkerAsync(runId, request, definition, definition.Identity, result, cancellationToken);
                return result;
            }

            await _runStore.PersistStateAsync(
                new TransitionRunStateUpdate(
                    runId,
                    DateTimeOffset.UtcNow,
                    definition.Identity,
                    TransitionDurableState.EffectsApplied,
                    effects.Explanation,
                    effects.Evidence),
                cancellationToken);

            IReadOnlyList<WorkflowTransitionIdentity> successors =
                await _definitionResolver.ResolveEligibleSuccessorsAsync(
                    definition,
                    validation.Products,
                    cancellationToken);
            TransitionRuntimeResult completed = Result(
                RuntimeOutcomeKind.Completed,
                TransitionDurableState.Completed,
                definition.Identity,
                inputGate,
                outputGate,
                validation,
                effects,
                successors,
                "Transition completed through validated products and effects.",
                effects.Evidence);

            await _runStore.PersistCompletedAsync(
                new TransitionRunCompleted(runId, DateTimeOffset.UtcNow, definition.Identity, completed),
                cancellationToken);
            await _evidenceStore.RecordEventAsync(
                new TransitionEvidenceEvent(
                    runId,
                    DateTimeOffset.UtcNow,
                    definition.Identity,
                    TransitionDurableState.Completed,
                    "TransitionCompleted",
                    completed.Explanation,
                    completed.Evidence),
                cancellationToken);

            return completed;
        }
        catch (OperationCanceledException)
        {
            return await CancelledAsync(
                runId,
                request,
                resolvedDefinition,
                transitionIdentity,
                inputGate,
                "Transition execution was cancelled.",
                CancellationToken.None);
        }
        catch (PromptContextBlockedException exception)
        {
            TransitionRuntimeResult result = Result(
                RuntimeOutcomeKind.Blocked,
                TransitionDurableState.Blocked,
                transitionIdentity,
                inputGate,
                null,
                null,
                null,
                [],
                exception.Message,
                exception.Evidence);
            await TryPersistStateAsync(runId, transitionIdentity, result.DurableState, result.Explanation, result.Evidence, CancellationToken.None);
            await TryRecordEventAsync(runId, transitionIdentity, result.DurableState, "TransitionPromptContextBlocked", result.Explanation, result.Evidence, CancellationToken.None);
            await TryRecordBlockerAsync(
                runId,
                request,
                transitionIdentity,
                BlockerCategory.Transition,
                result.Explanation,
                $"Resolve blocked prompt context before rerunning '{transitionIdentity}'.",
                result.Evidence,
                CancellationToken.None);
            await TryRecordRecoveryMarkerAsync(runId, request, resolvedDefinition, transitionIdentity, result, CancellationToken.None);
            return result;
        }
        catch (Exception exception)
        {
            string explanation = $"Transition failed: {exception.Message}";
            await TryRecordFailureAsync(runId, transitionIdentity, explanation, CancellationToken.None);
            await TryPersistStateAsync(runId, transitionIdentity, TransitionDurableState.Failed, explanation, [], CancellationToken.None);
            TransitionRuntimeResult result = Result(
                RuntimeOutcomeKind.Failed,
                TransitionDurableState.Failed,
                transitionIdentity,
                inputGate,
                outputGate,
                validation,
                effects,
                [],
                explanation,
                []);
            await TryRecordRecoveryMarkerAsync(runId, request, resolvedDefinition, transitionIdentity, result, CancellationToken.None);
            return result;
        }
    }

    private async Task<TransitionRuntimeResult> CancelledAsync(
        string runId,
        TransitionRuntimeRequest request,
        WorkflowTransitionDefinition? definition,
        WorkflowTransitionIdentity transition,
        GateResult? inputGate,
        string explanation,
        CancellationToken cancellationToken)
    {
        await TryPersistStateAsync(runId, transition, TransitionDurableState.Cancelled, explanation, [], cancellationToken);
        await TryRecordEventAsync(runId, transition, TransitionDurableState.Cancelled, "TransitionCancelled", explanation, [], cancellationToken);
        TransitionRuntimeResult result = Result(
            RuntimeOutcomeKind.Cancelled,
            TransitionDurableState.Cancelled,
            transition,
            inputGate,
            null,
            null,
            null,
            [],
            explanation,
            []);
        await TryRecordRecoveryMarkerAsync(runId, request, definition, transition, result, cancellationToken);
        return result;
    }

    private async Task TryPersistStateAsync(
        string runId,
        WorkflowTransitionIdentity transition,
        TransitionDurableState state,
        string explanation,
        IReadOnlyList<string> evidence,
        CancellationToken cancellationToken)
    {
        try
        {
            await _runStore.PersistStateAsync(
                new TransitionRunStateUpdate(runId, DateTimeOffset.UtcNow, transition, state, explanation, evidence),
                cancellationToken);
        }
        catch
        {
            // Failure is reflected by the returned runtime outcome; callers decide recovery policy.
        }
    }

    private async Task TryRecordEventAsync(
        string runId,
        WorkflowTransitionIdentity transition,
        TransitionDurableState state,
        string eventName,
        string explanation,
        IReadOnlyList<string> evidence,
        CancellationToken cancellationToken)
    {
        try
        {
            await _evidenceStore.RecordEventAsync(
                new TransitionEvidenceEvent(runId, DateTimeOffset.UtcNow, transition, state, eventName, explanation, evidence),
                cancellationToken);
        }
        catch
        {
            // Failure is reflected by the returned runtime outcome; callers decide recovery policy.
        }
    }

    private async Task TryRecordFailureAsync(
        string runId,
        WorkflowTransitionIdentity transition,
        string failure,
        CancellationToken cancellationToken)
    {
        try
        {
            await _evidenceStore.RecordFailureAsync(runId, transition, failure, cancellationToken);
        }
        catch
        {
            // Failure is reflected by the returned runtime outcome; callers decide recovery policy.
        }
    }

    private async Task TryRecordBlockerAsync(
        string runId,
        TransitionRuntimeRequest request,
        WorkflowTransitionIdentity transition,
        BlockerCategory category,
        string reason,
        string requiredAction,
        IReadOnlyList<string> evidence,
        CancellationToken cancellationToken)
    {
        if (_blockerStore is null)
        {
            return;
        }

        try
        {
            await _blockerStore.RecordBlockerAsync(
                new TransitionBlockerCapture(
                    runId,
                    DateTimeOffset.UtcNow,
                    request,
                    transition,
                    category,
                    reason,
                    requiredAction,
                    Recoverable: true,
                    evidence),
                cancellationToken);
        }
        catch
        {
            // Blocker persistence is supporting evidence; the transition result already reports the block.
        }
    }

    private async Task TryRecordRecoveryMarkerAsync(
        string runId,
        TransitionRuntimeRequest request,
        WorkflowTransitionDefinition? definition,
        WorkflowTransitionIdentity transition,
        TransitionRuntimeResult result,
        CancellationToken cancellationToken)
    {
        if (_recoveryStore is null || !NeedsRecoveryMarker(result.DurableState))
        {
            return;
        }

        RecoveryDefinition recovery = definition?.Recovery ??
            new RecoveryDefinition(
                $"{transition}.Recovery",
                "Recover from transition evidence after the runtime ended before transition metadata could be fully resolved.",
                ["rerun"],
                ["silent repair"]);

        try
        {
            await _recoveryStore.RecordRecoveryMarkerAsync(
                new TransitionRecoveryMarkerCapture(
                    runId,
                    DateTimeOffset.UtcNow,
                    request,
                    transition,
                    result.DurableState,
                    result.Outcome,
                    recovery,
                    result.Explanation,
                    result.Evidence),
                cancellationToken);
        }
        catch
        {
            // Recovery marker persistence is supporting evidence; the transition result remains authoritative.
        }
    }

    private async Task TryRecordGateEvaluationAsync(
        string runId,
        TransitionRuntimeRequest request,
        WorkflowTransitionIdentity transition,
        GateDefinition gate,
        GateResult result,
        CancellationToken cancellationToken)
    {
        if (_gateEvaluationStore is null)
        {
            return;
        }

        try
        {
            await _gateEvaluationStore.RecordGateEvaluationAsync(
                new TransitionGateEvaluationCapture(
                    runId,
                    DateTimeOffset.UtcNow,
                    request,
                    transition,
                    gate,
                    result),
                cancellationToken);
        }
        catch
        {
            // Gate evaluation persistence is supporting evidence; the transition result already carries gate state.
        }
    }

    private async Task TryRecordEffectsAsync(
        string runId,
        TransitionRuntimeRequest request,
        WorkflowTransitionDefinition definition,
        EffectExecutionResult effects,
        CancellationToken cancellationToken)
    {
        if (_effectStore is null)
        {
            return;
        }

        foreach (EffectExecutionRecord effect in effects.Effects)
        {
            EffectCategory category = definition.Effects
                .FirstOrDefault(item => item.Identity == effect.Effect)
                ?.Category ?? EffectCategory.Evidence;
            try
            {
                await _effectStore.RecordEffectAsync(
                    new TransitionEffectRecordCapture(
                        runId,
                        DateTimeOffset.UtcNow,
                        request,
                        definition.Identity,
                        effect.Effect,
                        category,
                        effect.Status,
                        effect.Explanation,
                        effect.Evidence),
                    cancellationToken);
            }
            catch
            {
                // Effect record persistence is supporting evidence; the returned effect result remains authoritative.
            }
        }
    }

    private static bool NeedsRecoveryMarker(TransitionDurableState state) =>
        state is TransitionDurableState.Blocked
            or TransitionDurableState.Failed
            or TransitionDurableState.Cancelled
            or TransitionDurableState.Stalled
            or TransitionDurableState.EffectsPartiallyApplied;

    private static RuntimeOutcomeKind MapGateStatus(GateStatus status) =>
        status switch
        {
            GateStatus.Waiting => RuntimeOutcomeKind.Waiting,
            GateStatus.Ambiguous => RuntimeOutcomeKind.Ambiguous,
            GateStatus.Invalid => RuntimeOutcomeKind.Failed,
            _ => RuntimeOutcomeKind.Blocked,
        };

    private static TransitionRuntimeResult Result(
        RuntimeOutcomeKind outcome,
        TransitionDurableState durableState,
        WorkflowTransitionIdentity transition,
        GateResult? inputGate,
        GateResult? outputGate,
        ProductValidationResult? validation,
        EffectExecutionResult? effects,
        IReadOnlyList<WorkflowTransitionIdentity> successors,
        string explanation,
        IReadOnlyList<string> evidence) =>
        new(
            outcome,
            durableState,
            transition,
            inputGate,
            outputGate,
            validation,
            effects,
            successors,
            explanation,
            evidence);
}
