using System.Diagnostics;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Services;
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
    // Required, not defaulted: constructing a runtime without a resolved policy identity must
    // be a visible decision at the call site, never a silent omission. Null is reserved for
    // hosts that genuinely have no policy authority yet (converging at M17-M19).
    string? _policyIdentity,
    ITransitionWarningStore? _warningStore = null,
    ITransitionRecoveryStore? _recoveryStore = null,
    ITransitionGateEvaluationStore? _gateEvaluationStore = null,
    ITransitionEffectStore? _effectStore = null,
    IAttemptStore? _attemptStore = null,
    IReadReceiptStore? _readReceiptStore = null) : ITransitionRuntime
{
    public async Task<TransitionRuntimeResult> RunAsync(
        TransitionRuntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        string runId = TransitionRunIdentity.New().Value;
        string attemptId = AttemptIdentity.New().Value;
        string? recordedAttemptId = null;
        WorkflowTransitionIdentity transitionIdentity = request.Transition;
        WorkflowTransitionDefinition? resolvedDefinition = null;
        ProductResolutionResult? resolvedInputs = null;
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
            resolvedInputs = inputs;
            inputGate = await _gateEvaluator.EvaluateInputGateAsync(definition.InputGate, inputs, cancellationToken);
            await TryRecordGateEvaluationAsync(runId, request, definition.Identity, definition.InputGate, inputGate, cancellationToken);
            if (!inputGate.IsSatisfied)
            {
                RuntimeOutcomeKind inputOutcome = MapInputGateStatus(definition.InputGate, inputGate);
                TransitionRuntimeResult result = Result(
                    inputOutcome,
                    DurableStateFor(inputOutcome),
                    definition.Identity,
                    inputGate,
                    null,
                    null,
                    null,
                    [],
                    inputGate.Explanation,
                    inputGate.Evidence);
                await TryPersistStateAsync(runId, definition.Identity, result.DurableState, result.Explanation, result.Evidence, cancellationToken);
                await TryRecordEventAsync(runId, definition.Identity, result.DurableState, "TransitionInputUnsatisfied", result.Explanation, result.Evidence, cancellationToken);
                string inputRemediation = inputOutcome switch
                {
                    RuntimeOutcomeKind.DirtyInputSurface =>
                        $"Commit the listed files under the declared input surface before rerunning '{definition.Identity}'.",
                    RuntimeOutcomeKind.UnversionedInputSurface =>
                        $"Initialize git and commit the input surface before rerunning '{definition.Identity}'.",
                    _ => $"Provide valid required input products before rerunning '{definition.Identity}'.",
                };
                await TryRecordWarningAsync(
                    runId,
                    request,
                    definition.Identity,
                    WarningCategory.Validation,
                    result.Explanation,
                    inputRemediation,
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
            // The read has already happened once the context is built; the receipt is consumption
            // evidence and must not be lost to a fired cancellation token.
            await TryAppendReadReceiptAsync(
                runId,
                attemptId,
                request,
                definition,
                context.ConsumedFiles ?? [],
                ConsumedProducts(definition, inputs),
                "Usable",
                CancellationToken.None);
            RenderedPrompt renderedPrompt = await _promptRenderer.RenderAsync(
                definition,
                context,
                cancellationToken);
            DateTimeOffset startedAt = DateTimeOffset.UtcNow;
            await _runStore.PersistStartedAsync(
                new TransitionRunStarted(
                    runId,
                    startedAt,
                    request,
                    definition,
                    context.InputSnapshot,
                    renderedPrompt,
                    request.Run?.Value,
                    request.WorkflowInstance?.Value,
                    attemptId),
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

            recordedAttemptId = attemptId;
            // Every attempt records the invocation's single resolved policy identity.
            await TryPersistAttemptStartedAsync(
                new AttemptRecord(
                    attemptId,
                    runId,
                    request.WorkflowInstance?.Value ?? string.Empty,
                    request.Run?.Value ?? string.Empty,
                    1,
                    startedAt,
                    null,
                    null,
                    _policyIdentity),
                cancellationToken);

            var stopwatch = Stopwatch.StartNew();
            PromptExecutionResult executionResult = await _promptExecutor.ExecuteAsync(
                definition,
                renderedPrompt,
                new PromptExecutionContext(request.Run?.Value, request.WorkflowInstance?.Value, runId, attemptId),
                cancellationToken);
            stopwatch.Stop();

            if (executionResult.Status == PromptExecutionStatus.Cancelled)
            {
                return await CancelledAsync(
                    runId,
                    recordedAttemptId,
                    request,
                    definition,
                    definition.Identity,
                    inputGate,
                    "Prompt execution reported cancellation.",
                    CancellationToken.None);
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
                await TryPersistAttemptCompletedAsync(recordedAttemptId, result.Outcome, cancellationToken);
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
            // Every mutation of the current-state transition-run row is backed by an appended
            // fact, so the row stays a pure projection over the append-only evidence history.
            await TryRecordEventAsync(runId, definition.Identity, TransitionDurableState.OutputInterpreted, "TransitionOutputInterpreted", interpreted.Explanation, interpreted.Evidence, cancellationToken);

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
                await TryPersistAttemptCompletedAsync(recordedAttemptId, result.Outcome, cancellationToken);
                return result;
            }

            validation = await _productValidator.ValidateAsync(definition, interpreted, cancellationToken);
            outputGate = await _gateEvaluator.EvaluateOutputGateAsync(definition.OutputGate, validation, cancellationToken);
            await TryRecordGateEvaluationAsync(runId, request, definition.Identity, definition.OutputGate, outputGate, cancellationToken);
            if (!validation.IsValid || !outputGate.IsSatisfied)
            {
                string explanation = validation.IsValid ? outputGate.Explanation : validation.Explanation;
                IReadOnlyList<string> evidence = validation.IsValid ? outputGate.Evidence : validation.Evidence;
                (RuntimeOutcomeKind outputOutcome, TransitionDurableState outputState, string eventName) =
                    validation.IsValid
                        ? MapOutputGateStatus(outputGate.Status)
                        : (RuntimeOutcomeKind.Failed, TransitionDurableState.Failed, "TransitionProductInvalid");
                TransitionRuntimeResult result = Result(
                    outputOutcome,
                    outputState,
                    definition.Identity,
                    inputGate,
                    outputGate,
                    validation,
                    null,
                    [],
                    explanation,
                    evidence);
                await TryPersistStateAsync(runId, definition.Identity, result.DurableState, result.Explanation, result.Evidence, cancellationToken);
                await TryRecordEventAsync(runId, definition.Identity, result.DurableState, eventName, result.Explanation, result.Evidence, cancellationToken);
                if (outputOutcome is RuntimeOutcomeKind.Waiting or RuntimeOutcomeKind.Ambiguous)
                {
                    await TryRecordWarningAsync(
                        runId,
                        request,
                        definition.Identity,
                        WarningCategory.Transition,
                        result.Explanation,
                        $"Satisfy the output gate of '{definition.Identity}' and rerun to make progress.",
                        result.Evidence,
                        cancellationToken);
                }

                await TryRecordRecoveryMarkerAsync(runId, request, definition, definition.Identity, result, cancellationToken);
                await TryPersistAttemptCompletedAsync(recordedAttemptId, result.Outcome, cancellationToken);
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
            await TryRecordEventAsync(runId, definition.Identity, TransitionDurableState.OutputValidated, "TransitionOutputValidated", validation.Explanation, validation.Evidence, cancellationToken);

            effects = await _effectExecutor.ExecuteAsync(
                definition,
                validation,
                new EffectExecutionContext(runId, recordedAttemptId, request.Run?.Value, request.WorkflowInstance?.Value),
                cancellationToken);
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
                await TryPersistAttemptCompletedAsync(recordedAttemptId, result.Outcome, cancellationToken);
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
            await TryPersistAttemptCompletedAsync(recordedAttemptId, completed.Outcome, cancellationToken);

            return completed;
        }
        catch (OperationCanceledException)
        {
            return await CancelledAsync(
                runId,
                recordedAttemptId,
                request,
                resolvedDefinition,
                transitionIdentity,
                inputGate,
                "Transition execution was cancelled.",
                CancellationToken.None);
        }
        catch (PromptContextUnavailableException exception)
        {
            TransitionRuntimeResult result = Result(
                RuntimeOutcomeKind.MissingRequiredInput,
                TransitionDurableState.InputUnsatisfied,
                transitionIdentity,
                inputGate,
                null,
                null,
                null,
                [],
                exception.Message,
                exception.Evidence);
            // No attempt row is persisted on this path (the prompt never executed), so the
            // receipt references only durably recorded attempts — null here.
            await TryAppendReadReceiptAsync(
                runId,
                recordedAttemptId,
                request,
                resolvedDefinition,
                exception.ConsumedFiles,
                resolvedDefinition is not null && resolvedInputs is not null
                    ? ConsumedProducts(resolvedDefinition, resolvedInputs)
                    : [],
                exception.Message,
                CancellationToken.None);
            await TryPersistStateAsync(runId, transitionIdentity, result.DurableState, result.Explanation, result.Evidence, CancellationToken.None);
            await TryRecordEventAsync(runId, transitionIdentity, result.DurableState, "TransitionPromptContextUnavailable", result.Explanation, result.Evidence, CancellationToken.None);
            await TryRecordWarningAsync(
                runId,
                request,
                transitionIdentity,
                WarningCategory.Transition,
                result.Explanation,
                $"Provide the required prompt context before rerunning '{transitionIdentity}'.",
                result.Evidence,
                CancellationToken.None);
            await TryRecordRecoveryMarkerAsync(runId, request, resolvedDefinition, transitionIdentity, result, CancellationToken.None);
            await TryPersistAttemptCompletedAsync(recordedAttemptId, result.Outcome, CancellationToken.None);
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
            await TryPersistAttemptCompletedAsync(recordedAttemptId, result.Outcome, CancellationToken.None);
            return result;
        }
    }

    private async Task<TransitionRuntimeResult> CancelledAsync(
        string runId,
        string? attemptId,
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
        await TryPersistAttemptCompletedAsync(attemptId, result.Outcome, cancellationToken);
        return result;
    }

    private async Task TryPersistAttemptStartedAsync(
        AttemptRecord attempt,
        CancellationToken cancellationToken)
    {
        if (_attemptStore is null)
        {
            return;
        }

        try
        {
            await _attemptStore.PersistAttemptStartedAsync(attempt, cancellationToken);
        }
        catch
        {
            // Attempt persistence is supporting evidence; the transition result remains authoritative.
        }
    }

    private async Task TryPersistAttemptCompletedAsync(
        string? attemptId,
        RuntimeOutcomeKind outcome,
        CancellationToken cancellationToken)
    {
        if (_attemptStore is null || attemptId is null)
        {
            return;
        }

        try
        {
            // Terminal attempt evidence must survive a fired token; the store write always runs uncancelled.
            await _attemptStore.PersistAttemptCompletedAsync(attemptId, DateTimeOffset.UtcNow, outcome.ToString(), CancellationToken.None);
        }
        catch
        {
            // Attempt persistence is supporting evidence; the transition result remains authoritative.
        }
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

    // A receipt is appended for every consumption event — usable or not — so read-time
    // validation outcomes stay linked to the exact content that was read.
    private async Task TryAppendReadReceiptAsync(
        string runId,
        string? attemptId,
        TransitionRuntimeRequest request,
        WorkflowTransitionDefinition? definition,
        IReadOnlyList<ConsumedInputFile> consumedFiles,
        IReadOnlyList<ProductRecord> consumedProducts,
        string validation,
        CancellationToken cancellationToken)
    {
        if (_readReceiptStore is null || definition is null)
        {
            return;
        }

        try
        {
            await _readReceiptStore.AppendAsync(
                new ReadReceiptCapture(
                    runId,
                    attemptId,
                    request,
                    definition,
                    consumedFiles,
                    consumedProducts,
                    validation,
                    DateTimeOffset.UtcNow),
                cancellationToken);
        }
        catch
        {
            // Receipt persistence is supporting evidence; failing to append must not fail the transition.
        }
    }

    private static IReadOnlyList<ProductRecord> ConsumedProducts(
        WorkflowTransitionDefinition definition,
        ProductResolutionResult inputs) =>
        inputs.Products
            .Where(product => definition.RequiredInputProducts.Any(requirement => requirement.Product == product.Identity))
            .ToArray();

    private async Task TryRecordWarningAsync(
        string runId,
        TransitionRuntimeRequest request,
        WorkflowTransitionIdentity transition,
        WarningCategory category,
        string concern,
        string remediation,
        IReadOnlyList<string> evidence,
        CancellationToken cancellationToken)
    {
        if (_warningStore is null)
        {
            return;
        }

        try
        {
            await _warningStore.RecordWarningAsync(
                new TransitionWarningCapture(
                    runId,
                    DateTimeOffset.UtcNow,
                    request,
                    transition,
                    category,
                    concern,
                    remediation,
                    evidence),
                cancellationToken);
        }
        catch
        {
            // Warning persistence is supporting evidence; the transition result already reports the condition.
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
        state is TransitionDurableState.InputUnsatisfied
            or TransitionDurableState.Ambiguous
            or TransitionDurableState.Failed
            or TransitionDurableState.Cancelled
            or TransitionDurableState.Stalled
            or TransitionDurableState.EffectsPartiallyApplied;

    // The switch stays exhaustive over named members without a default arm so a new GateStatus member
    // is a compile error here; CS8524 (unnamed integral values) is intentionally not handled.
#pragma warning disable CS8524
    private static RuntimeOutcomeKind MapInputGateStatus(GateDefinition gate, GateResult result) =>
        result.Status switch
        {
            GateStatus.Satisfied => RuntimeOutcomeKind.Completed,
            GateStatus.Unsatisfied => UnsatisfiedInputOutcome(gate, result),
            GateStatus.Waiting => RuntimeOutcomeKind.Waiting,
            GateStatus.Ambiguous => RuntimeOutcomeKind.Ambiguous,
            GateStatus.Invalid => RuntimeOutcomeKind.Failed,
        };
#pragma warning restore CS8524

    // Severity order for declared cannot-proceed labels lives here: a missing product always
    // outranks a surface problem, and a surface that cannot resolve to a commit outranks one that
    // is merely dirty. Labels the evaluator declares outside this vocabulary rank last.
    private static int UnsatisfiedOutcomeSeverity(RuntimeOutcomeKind outcome) =>
        outcome switch
        {
            RuntimeOutcomeKind.MissingRequiredInput => 0,
            RuntimeOutcomeKind.UnversionedInputSurface => 1,
            RuntimeOutcomeKind.DirtyInputSurface => 2,
            _ => int.MaxValue,
        };

    // The specific cannot-proceed label prefers the typed discriminator: the worst declared
    // UnsatisfiedOutcome among unsatisfied requirement results wins (see UnsatisfiedOutcomeSeverity).
    // When no requirement declares one, the shape-based fallback applies, matching evaluator
    // precedence: a declared product is evaluated first, so the first unsatisfied requirement stops
    // as MissingRequiredInput even when an input surface is also declared; only a pure clean-input
    // requirement (InputSurface without a product) stops as DirtyInputSurface. Requirement kinds are
    // implicit by shape on the gate definition.
    private static RuntimeOutcomeKind UnsatisfiedInputOutcome(GateDefinition gate, GateResult result)
    {
        IReadOnlyList<GateRequirementResult> unsatisfiedRequirements = result.Requirements
            .Where(requirement => requirement.Status == GateStatus.Unsatisfied)
            .ToArray();
        RuntimeOutcomeKind[] declaredOutcomes = unsatisfiedRequirements
            .Where(requirement => requirement.UnsatisfiedOutcome is not null)
            .Select(requirement => requirement.UnsatisfiedOutcome!.Value)
            .OrderBy(UnsatisfiedOutcomeSeverity)
            .ToArray();
        if (declaredOutcomes.Length > 0)
        {
            return declaredOutcomes[0];
        }

        GateRequirementResult? unsatisfied = unsatisfiedRequirements.Count == 0
            ? null
            : unsatisfiedRequirements[0];
        GateRequirementDefinition? requirementDefinition = unsatisfied is null
            ? null
            : gate.Requirements.FirstOrDefault(item => item.Identity == unsatisfied.RequirementIdentity);
        return requirementDefinition is { Product: null, InputSurface: not null }
            ? RuntimeOutcomeKind.DirtyInputSurface
            : RuntimeOutcomeKind.MissingRequiredInput;
    }

    private static TransitionDurableState DurableStateFor(RuntimeOutcomeKind outcome) =>
        outcome switch
        {
            RuntimeOutcomeKind.MissingRequiredInput => TransitionDurableState.InputUnsatisfied,
            RuntimeOutcomeKind.DirtyInputSurface => TransitionDurableState.InputUnsatisfied,
            RuntimeOutcomeKind.UnversionedInputSurface => TransitionDurableState.InputUnsatisfied,
            RuntimeOutcomeKind.Waiting => TransitionDurableState.Waiting,
            RuntimeOutcomeKind.Ambiguous => TransitionDurableState.Ambiguous,
            _ => TransitionDurableState.Failed,
        };

    // The switch stays exhaustive over named members without a default arm so a new GateStatus member
    // is a compile error here; CS8524 (unnamed integral values) is intentionally not handled. The
    // Satisfied arm is unreachable at the call site (the map only runs on an unsatisfied output gate)
    // but is named so the mapping stays total.
#pragma warning disable CS8524
    private static (RuntimeOutcomeKind Outcome, TransitionDurableState State, string EventName) MapOutputGateStatus(
        GateStatus status) =>
        status switch
        {
            GateStatus.Satisfied => (RuntimeOutcomeKind.Completed, TransitionDurableState.OutputValidated, "TransitionOutputValidated"),
            GateStatus.Unsatisfied => (RuntimeOutcomeKind.Failed, TransitionDurableState.Failed, "TransitionProductInvalid"),
            GateStatus.Waiting => (RuntimeOutcomeKind.Waiting, TransitionDurableState.Waiting, "TransitionOutputWaiting"),
            GateStatus.Invalid => (RuntimeOutcomeKind.Failed, TransitionDurableState.Failed, "TransitionProductInvalid"),
            GateStatus.Ambiguous => (RuntimeOutcomeKind.Ambiguous, TransitionDurableState.Ambiguous, "TransitionOutputAmbiguous"),
        };
#pragma warning restore CS8524

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
