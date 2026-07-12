using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Runtime;

/// <summary>
/// Executes exactly one authorized attempt. Cross-attempt resume, retry, fork, reconciliation,
/// chaining, and human-escalation policy belong to their respective authorities.
/// </summary>
public sealed class TransitionRuntime(
    ITransitionDefinitionResolver _definitionResolver,
    IProductResolver _productResolver,
    IGateEvaluator _gateEvaluator,
    IPromptContextBuilder _promptContextBuilder,
    IPromptRenderer _promptRenderer,
    IPromptDispatchGateway _promptGateway,
    IOutputInterpreter _outputInterpreter,
    ICandidateProductStore _candidateProducts,
    IProductValidator _productValidator,
    IInputFreshnessValidator _freshnessValidator,
    ITransitionRunStore _runStore,
    IAttemptStore _attemptStore,
    IReadReceiptStore _readReceiptStore,
    ITransitionEvidenceStore _evidenceStore,
    ITransitionGateEvaluationStore _gateEvaluationStore,
    ITransitionCommitStore _commitStore,
    ITransitionBoundaryJournal? _boundaryJournal = null) : ITransitionRuntime
{
    private int boundarySequence;

    public async Task<TransitionRuntimeResult> RunAsync(
        TransitionRuntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExecutionContext is not CanonicalTransitionExecutionContext execution)
        {
            return PreAttemptResult(
                RuntimeOutcomeKind.CompatibilityImportRequired,
                request.Transition,
                "Canonical execution requires a translated causal context; legacy callers must pass through Compatibility Authority.",
                [request.ExecutionContext.GetType().Name]);
        }

        WorkflowTransitionDefinition definition =
            await _definitionResolver.ResolveAsync(request, cancellationToken);
        ProductResolutionResult inputs =
            await _productResolver.ResolveAsync(definition.RequiredInputProducts, cancellationToken);
        GateResult inputGate =
            await _gateEvaluator.EvaluateInputGateAsync(definition.InputGate, inputs, cancellationToken);
        if (!inputGate.IsSatisfied)
        {
            RuntimeOutcomeKind outcome = MapInputGate(inputGate);
            return PreAttemptResult(outcome, definition.Identity, inputGate.Explanation, inputGate.Evidence, inputGate);
        }

        PromptContext promptContext;
        try
        {
            promptContext = await _promptContextBuilder.BuildAsync(request, definition, inputs, cancellationToken);
        }
        catch (PromptContextUnavailableException exception)
        {
            return PreAttemptResult(
                RuntimeOutcomeKind.MissingRequiredInput,
                definition.Identity,
                exception.Message,
                exception.Evidence,
                inputGate);
        }

        (TransitionRunIdentity transitionRun, AttemptIdentity attempt, int attemptIndex) =
            AuthorizeAttempt(request.Authorization, execution);
        CanonicalCausalContext causality = new(
            execution.Workspace,
            execution.Run,
            execution.WorkflowInstance,
            transitionRun,
            attempt);
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;

        // Attempt intent is durable before any prompt rendering or provider-visible work.
        await _attemptStore.PersistAttemptStartedAsync(
            new AttemptRecord(
                attempt.Value,
                transitionRun.Value,
                execution.WorkflowInstance.Value,
                execution.Run.Value,
                attemptIndex,
                startedAt,
                null,
                null,
                execution.ResolvedPolicy.Value),
            cancellationToken);

        ConsumedInputManifestIdentity manifestIdentity = ConsumedInputManifestIdentity.New();
        await _readReceiptStore.AppendAsync(
            new ReadReceiptCapture(
                causality,
                request,
                definition,
                promptContext.ConsumedFiles ?? [],
                inputs.Products,
                "Usable",
                startedAt),
            cancellationToken);

        RenderedPrompt rendered = await _promptRenderer.RenderAsync(
            new PromptRenderRequest(
                definition,
                promptContext,
                causality,
                execution.ResolvedPolicy,
                execution.PromptPolicyProfile,
                manifestIdentity),
            cancellationToken);
        if (rendered.PolicyProfileIdentity != execution.PromptPolicyProfile)
        {
            throw new InvalidOperationException("Prompt renderer returned a policy profile different from the authorized attempt.");
        }

        var composition = new PromptComposition(
            PromptCompositionIdentity.New(),
            rendered.TemplateIdentity,
            rendered.TemplateSourceHash,
            execution.ResolvedPolicy,
            rendered.PolicyProfileIdentity,
            manifestIdentity,
            promptContext.ConsumedFiles ?? [],
            promptContext.Metadata,
            rendered.Text,
            "utf-8");
        var authorization = new PromptDispatchAuthorization(
            causality,
            execution.ResolvedPolicy,
            rendered.PolicyProfileIdentity,
            execution.RuntimeProfile,
            definition.Identity,
            promptContext.InputSnapshot.Hash);
        PreparedPromptDispatch prepared =
            await _promptGateway.PrepareAsync(composition, authorization, cancellationToken);

        await _runStore.PersistStartedAsync(
            new TransitionRunStarted(
                causality,
                startedAt,
                request,
                definition,
                promptContext.InputSnapshot,
                prepared.Prompt),
            cancellationToken);
        await _evidenceStore.RecordEventAsync(
            Event(causality, definition.Identity, TransitionDurableState.Started, "TransitionStarted",
                "Attempt intent, causal inputs, policy, and rendered prompt are durable.",
                [prepared.Prompt.PersistenceIdentity.Value, promptContext.InputSnapshot.Hash]),
            cancellationToken);
        await ObserveBoundaryAsync(
            causality,
            definition.Identity,
            TransitionBoundaryKind.PromptRendered,
            promptContext.InputSnapshot.Hash,
            [prepared.Prompt.PersistenceIdentity.Value],
            cancellationToken);
        await ObserveBoundaryAsync(
            causality,
            definition.Identity,
            TransitionBoundaryKind.DispatchIntended,
            promptContext.InputSnapshot.Hash,
            [prepared.Dispatch.Dispatch.Value],
            cancellationToken);

        PromptExecutionResult executionResult;
        async Task<TransitionRuntimeResult> CompleteInjectedFaultAsync(TransitionFaultInjectedException fault)
        {
            TransitionDurableState state = fault.Observation.Boundary switch
            {
                TransitionBoundaryKind.PreSubmission => TransitionDurableState.Cancelled,
                TransitionBoundaryKind.ProviderCompleted => TransitionDurableState.PromptCompleted,
                _ => TransitionDurableState.ProviderOutcomeUnknown,
            };
            RuntimeOutcomeKind outcome = state == TransitionDurableState.Cancelled
                ? RuntimeOutcomeKind.Cancelled
                : RuntimeOutcomeKind.RecoveryRequired;
            string explanation =
                $"Certification interrupted the transition at the durable {fault.Observation.Boundary} boundary.";
            string[] faultEvidence =
            [
                fault.GetType().Name,
                $"boundary:{fault.Observation.Boundary}",
                prepared.Dispatch.Dispatch.Value,
            ];
            await _evidenceStore.RecordFailureAsync(
                causality,
                definition.Identity,
                explanation,
                CancellationToken.None);
            await _runStore.PersistStateAsync(
                State(causality, definition.Identity, state, explanation, faultEvidence),
                CancellationToken.None);
            await _attemptStore.PersistAttemptCompletedAsync(
                attempt,
                DateTimeOffset.UtcNow,
                outcome.ToString(),
                CancellationToken.None);
            return Result(
                outcome,
                state,
                definition.Identity,
                inputGate,
                null,
                null,
                null,
                [],
                explanation,
                faultEvidence,
                causality,
                attemptCompleted: true);
        }

        try
        {
            executionResult = await _promptGateway.DispatchAsync(prepared, cancellationToken);
        }
        catch (TransitionFaultInjectedException fault)
        {
            return await CompleteInjectedFaultAsync(fault);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            if (FindInjectedFault(exception) is { } fault)
            {
                return await CompleteInjectedFaultAsync(fault);
            }

            string diagnostic = exception.InnerException?.Message ?? exception.Message;
            string explanation =
                "Provider dispatch did not return a normalized outcome; Recovery Authority must reconcile it before any repeat execution. " +
                $"Diagnostic: {diagnostic}";
            await _evidenceStore.RecordFailureAsync(causality, definition.Identity, explanation, CancellationToken.None);
            await _runStore.PersistStateAsync(
                State(causality, definition.Identity, TransitionDurableState.ProviderOutcomeUnknown, explanation,
                    [exception.GetType().Name, prepared.Dispatch.Dispatch.Value]),
                CancellationToken.None);
            await _attemptStore.PersistAttemptCompletedAsync(
                attempt,
                DateTimeOffset.UtcNow,
                RuntimeOutcomeKind.RecoveryRequired.ToString(),
                CancellationToken.None);
            return Result(
                RuntimeOutcomeKind.RecoveryRequired,
                TransitionDurableState.ProviderOutcomeUnknown,
                definition.Identity,
                inputGate,
                null,
                null,
                null,
                [],
                explanation,
                [exception.GetType().Name, prepared.Dispatch.Dispatch.Value],
                causality,
                attemptCompleted: true);
        }

        // Raw outcome and provider evidence are durable before interpretation or validation.
        await _evidenceStore.RecordRawOutputAsync(causality, definition.Identity, executionResult, CancellationToken.None);
        await _evidenceStore.RecordEventAsync(
            Event(causality, definition.Identity, TransitionDurableState.PromptCompleted, "ProviderResultObserved",
                "A normalized provider result is durable.", [prepared.Dispatch.Dispatch.Value]),
            CancellationToken.None);
        await _runStore.PersistStateAsync(
            State(causality, definition.Identity, TransitionDurableState.PromptCompleted,
                "Provider result was captured.", [prepared.Dispatch.Dispatch.Value]),
            CancellationToken.None);
        await ObserveBoundaryAsync(
            causality,
            definition.Identity,
            TransitionBoundaryKind.RawOutputPersisted,
            promptContext.InputSnapshot.Hash,
            [prepared.Dispatch.Dispatch.Value],
            CancellationToken.None);

        if (executionResult.Status != PromptExecutionStatus.Completed)
        {
            RuntimeOutcomeKind outcome = executionResult.Status == PromptExecutionStatus.Cancelled
                ? RuntimeOutcomeKind.Cancelled
                : RuntimeOutcomeKind.Failed;
            TransitionDurableState state = executionResult.Status == PromptExecutionStatus.Cancelled
                ? TransitionDurableState.Cancelled
                : TransitionDurableState.Failed;
            string explanation = executionResult.FailureMessage ?? $"Provider execution {executionResult.Status}.";
            return await CompleteNonSuccessAsync(
                causality,
                definition.Identity,
                inputGate,
                outcome,
                state,
                explanation,
                [],
                CancellationToken.None);
        }

        InterpretedTransitionOutput interpreted =
            await _outputInterpreter.InterpretAsync(definition, executionResult, cancellationToken);
        await _evidenceStore.RecordEventAsync(
            Event(causality, definition.Identity, TransitionDurableState.OutputInterpreted,
                "TransitionOutputInterpreted", interpreted.Explanation, interpreted.Evidence),
            cancellationToken);
        await _runStore.PersistStateAsync(
            State(causality, definition.Identity, TransitionDurableState.OutputInterpreted,
                interpreted.Explanation, interpreted.Evidence),
            cancellationToken);
        if (interpreted.Status != OutputInterpretationStatus.Valid)
        {
            return await CompleteNonSuccessAsync(
                causality,
                definition.Identity,
                inputGate,
                RuntimeOutcomeKind.Failed,
                TransitionDurableState.Failed,
                interpreted.Explanation,
                interpreted.Evidence,
                cancellationToken);
        }

        await _candidateProducts.RegisterAsync(causality, interpreted.CandidateProducts, cancellationToken);
        ProductValidationResult validation =
            await _productValidator.ValidateAsync(definition, interpreted, cancellationToken);
        await _evidenceStore.RecordEventAsync(
            Event(causality, definition.Identity, TransitionDurableState.OutputValidated,
                "TransitionOutputValidated", validation.Explanation, validation.Evidence),
            cancellationToken);
        if (!validation.IsValid)
        {
            return await CompleteNonSuccessAsync(
                causality,
                definition.Identity,
                inputGate,
                validation.Status == ProductValidationStatus.Ambiguous
                    ? RuntimeOutcomeKind.Ambiguous
                    : RuntimeOutcomeKind.Failed,
                validation.Status == ProductValidationStatus.Ambiguous
                    ? TransitionDurableState.Ambiguous
                    : TransitionDurableState.Failed,
                validation.Explanation,
                validation.Evidence,
                cancellationToken,
                validation);
        }

        GateResult outputGate =
            await _gateEvaluator.EvaluateOutputGateAsync(definition.OutputGate, validation, cancellationToken);
        await _gateEvaluationStore.RecordGateEvaluationAsync(
            new TransitionGateEvaluationCapture(
                causality,
                DateTimeOffset.UtcNow,
                request,
                definition.Identity,
                definition.OutputGate,
                outputGate),
            cancellationToken);
        if (!outputGate.IsSatisfied)
        {
            RuntimeOutcomeKind outcome = outputGate.Status switch
            {
                GateStatus.Waiting => RuntimeOutcomeKind.Waiting,
                GateStatus.Ambiguous => RuntimeOutcomeKind.Ambiguous,
                _ => RuntimeOutcomeKind.Failed,
            };
            TransitionDurableState state = outputGate.Status switch
            {
                GateStatus.Waiting => TransitionDurableState.Waiting,
                GateStatus.Ambiguous => TransitionDurableState.Ambiguous,
                _ => TransitionDurableState.Failed,
            };
            return await CompleteNonSuccessAsync(
                causality,
                definition.Identity,
                inputGate,
                outcome,
                state,
                outputGate.Explanation,
                outputGate.Evidence,
                cancellationToken,
                validation,
                outputGate);
        }

        InputFreshnessResult freshness = await _freshnessValidator.ValidateAsync(
            causality,
            request,
            definition,
            promptContext,
            cancellationToken);
        if (!freshness.IsFresh)
        {
            RuntimeOutcomeKind outcome = freshness.Status == InputFreshnessStatus.ConcurrentStateAdvanced
                ? RuntimeOutcomeKind.ConcurrentStateConflict
                : RuntimeOutcomeKind.InputInvalidated;
            TransitionDurableState state = freshness.Status == InputFreshnessStatus.ConcurrentStateAdvanced
                ? TransitionDurableState.ConcurrentStateConflict
                : TransitionDurableState.InputInvalidated;
            return await CompleteNonSuccessAsync(
                causality,
                definition.Identity,
                inputGate,
                outcome,
                state,
                freshness.Explanation,
                freshness.Evidence,
                cancellationToken,
                validation,
                outputGate);
        }

        IReadOnlyList<WorkflowTransitionIdentity> successors =
            await _definitionResolver.ResolveEligibleSuccessorsAsync(definition, validation.Products, cancellationToken);
        await _commitStore.CommitAsync(
            new TransitionCommitCapture(
                causality,
                request,
                definition,
                validation,
                outputGate,
                successors,
                DateTimeOffset.UtcNow),
            cancellationToken);

        bool effectsPending = definition.Effects.Count > 0;
        TransitionDurableState durableState = effectsPending
            ? TransitionDurableState.EffectsPending
            : TransitionDurableState.Completed;
        RuntimeOutcomeKind finalOutcome = effectsPending
            ? RuntimeOutcomeKind.EffectsPending
            : RuntimeOutcomeKind.Completed;
        EffectExecutionResult effectPlan = new(
            effectsPending ? EffectExecutionStatus.Planned : EffectExecutionStatus.Succeeded,
            definition.Effects.Select(effect => new EffectExecutionRecord(
                effect.Identity,
                EffectExecutionStatus.Planned,
                "Required effect intent is durable and awaits Effect Coordinator execution.",
                [effect.Trigger])).ToArray(),
            effectsPending ? "Required effects are pending." : "No external effects are required.",
            definition.Effects.Select(effect => effect.Identity.Value).ToArray());
        return Result(
            finalOutcome,
            durableState,
            definition.Identity,
            inputGate,
            outputGate,
            validation,
            effectPlan,
            successors,
            effectsPending ? "Attempt completed; required effects are pending." : "Transition completed.",
            validation.Evidence.Concat(outputGate.Evidence).ToArray(),
            causality,
            attemptCompleted: true,
            effectsPending: effectsPending);
    }

    private static (TransitionRunIdentity TransitionRun, AttemptIdentity Attempt, int AttemptIndex) AuthorizeAttempt(
        AttemptAuthorization authorization,
        CanonicalTransitionExecutionContext execution) => authorization switch
    {
        FreshAttemptAuthorization => (TransitionRunIdentity.New(), AttemptIdentity.New(), 1),
        RecoveryAttemptAuthorization recovery
            when recovery.Plan.Action == TransitionRecoveryAction.RetryAsNewAttempt &&
                 recovery.Plan.ResultingAttemptMode == RecoveryAttemptMode.RetryExistingTransitionRun &&
                 recovery.Plan.SourceCausality.Workspace == execution.Workspace &&
                 recovery.Plan.SourceCausality.Run == execution.Run &&
                 recovery.Plan.SourceCausality.WorkflowInstance == execution.WorkflowInstance =>
            (recovery.Plan.SourceCausality.TransitionRun, AttemptIdentity.New(), recovery.Plan.NextAttemptIndex),
        RecoveryAttemptAuthorization => throw new InvalidOperationException(
            "The supplied recovery plan does not authorize a new provider attempt for this causal context."),
        _ => throw new InvalidOperationException("Unknown attempt authorization."),
    };

    private async Task<TransitionRuntimeResult> CompleteNonSuccessAsync(
        CanonicalCausalContext causality,
        WorkflowTransitionIdentity transition,
        GateResult inputGate,
        RuntimeOutcomeKind outcome,
        TransitionDurableState state,
        string explanation,
        IReadOnlyList<string> evidence,
        CancellationToken cancellationToken,
        ProductValidationResult? validation = null,
        GateResult? outputGate = null)
    {
        await _runStore.PersistCompletedAsync(
            new TransitionRunCompleted(
                causality,
                DateTimeOffset.UtcNow,
                transition,
                Result(outcome, state, transition, inputGate, outputGate, validation, null, [],
                    explanation, evidence, causality, attemptCompleted: true)),
            cancellationToken);
        await _attemptStore.PersistAttemptCompletedAsync(
            causality.Attempt,
            DateTimeOffset.UtcNow,
            outcome.ToString(),
            cancellationToken);
        return Result(outcome, state, transition, inputGate, outputGate, validation, null, [],
            explanation, evidence, causality, attemptCompleted: true);
    }

    private async Task ObserveBoundaryAsync(
        CanonicalCausalContext causality,
        WorkflowTransitionIdentity transition,
        TransitionBoundaryKind boundary,
        string inputSnapshotHash,
        IReadOnlyList<string> evidence,
        CancellationToken cancellationToken)
    {
        if (_boundaryJournal is null)
        {
            return;
        }

        var observation = new TransitionBoundaryObservation(
            causality,
            transition,
            boundary,
            Interlocked.Increment(ref boundarySequence),
            DateTimeOffset.UtcNow,
            inputSnapshotHash,
            null,
            evidence);
        await _boundaryJournal.RecordAsync(observation, cancellationToken);
        if (_boundaryJournal.ShouldInterrupt(observation))
        {
            throw new TransitionFaultInjectedException(observation);
        }
    }

    private static TransitionEvidenceEvent Event(
        CanonicalCausalContext causality,
        WorkflowTransitionIdentity transition,
        TransitionDurableState state,
        string name,
        string explanation,
        IReadOnlyList<string> evidence) =>
        new(causality, DateTimeOffset.UtcNow, transition, state, name, explanation, evidence);

    private static TransitionRunStateUpdate State(
        CanonicalCausalContext causality,
        WorkflowTransitionIdentity transition,
        TransitionDurableState state,
        string explanation,
        IReadOnlyList<string> evidence) =>
        new(causality, DateTimeOffset.UtcNow, transition, state, explanation, evidence);

    private static TransitionRuntimeResult PreAttemptResult(
        RuntimeOutcomeKind outcome,
        WorkflowTransitionIdentity transition,
        string explanation,
        IReadOnlyList<string> evidence,
        GateResult? inputGate = null) =>
        new(outcome, DurableStateFor(outcome), transition, inputGate, null, null, null, [], explanation, evidence);

    private static TransitionRuntimeResult Result(
        RuntimeOutcomeKind outcome,
        TransitionDurableState state,
        WorkflowTransitionIdentity transition,
        GateResult? inputGate,
        GateResult? outputGate,
        ProductValidationResult? validation,
        EffectExecutionResult? effects,
        IReadOnlyList<WorkflowTransitionIdentity> successors,
        string explanation,
        IReadOnlyList<string> evidence,
        CanonicalCausalContext causality,
        bool attemptCompleted,
        bool effectsPending = false) =>
        new(outcome, state, transition, inputGate, outputGate, validation, effects, successors,
            explanation, evidence, causality.TransitionRun, causality.Attempt, attemptCompleted, effectsPending);

    private static RuntimeOutcomeKind MapInputGate(GateResult gate)
    {
        if (gate.Status == GateStatus.Unsatisfied)
        {
            return gate.Requirements
                .Select(requirement => requirement.UnsatisfiedOutcome)
                .FirstOrDefault(outcome => outcome is not null)
                ?? RuntimeOutcomeKind.MissingRequiredInput;
        }

        return gate.Status switch
        {
            GateStatus.Waiting => RuntimeOutcomeKind.Waiting,
            GateStatus.Ambiguous => RuntimeOutcomeKind.Ambiguous,
            GateStatus.Invalid => RuntimeOutcomeKind.Failed,
            _ => RuntimeOutcomeKind.Failed,
        };
    }

    private static TransitionFaultInjectedException? FindInjectedFault(Exception exception)
    {
        if (exception is TransitionFaultInjectedException fault)
        {
            return fault;
        }

        if (exception is AggregateException aggregate)
        {
            foreach (Exception inner in aggregate.Flatten().InnerExceptions)
            {
                if (FindInjectedFault(inner) is { } nested)
                {
                    return nested;
                }
            }
        }

        return exception.InnerException is null ? null : FindInjectedFault(exception.InnerException);
    }

    private static TransitionDurableState DurableStateFor(RuntimeOutcomeKind outcome) => outcome switch
    {
        RuntimeOutcomeKind.Waiting => TransitionDurableState.Waiting,
        RuntimeOutcomeKind.Ambiguous => TransitionDurableState.Ambiguous,
        RuntimeOutcomeKind.Cancelled => TransitionDurableState.Cancelled,
        RuntimeOutcomeKind.Stalled => TransitionDurableState.Stalled,
        RuntimeOutcomeKind.InputInvalidated => TransitionDurableState.InputInvalidated,
        RuntimeOutcomeKind.ConcurrentStateConflict => TransitionDurableState.ConcurrentStateConflict,
        RuntimeOutcomeKind.EffectsPending => TransitionDurableState.EffectsPending,
        RuntimeOutcomeKind.RecoveryRequired => TransitionDurableState.ProviderOutcomeUnknown,
        RuntimeOutcomeKind.Failed => TransitionDurableState.Failed,
        _ => TransitionDurableState.InputUnsatisfied,
    };
}
