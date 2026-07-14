using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Primitives.Tests.Runtime;

public sealed class TransitionRuntimeTests
{
    [Fact]
    public async Task Attempt_intent_failure_prevents_prompt_render_and_provider_dispatch()
    {
        Harness harness = new();
        harness.Attempts.FailStart = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Runtime.RunAsync(harness.Request));

        Assert.Equal(0, harness.Renderer.CallCount);
        Assert.Equal(0, harness.Executor.CallCount);
    }

    [Fact]
    public async Task Prompt_fact_failure_prevents_provider_dispatch()
    {
        Harness harness = new();
        harness.Prompts.FailAppend = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Runtime.RunAsync(harness.Request));

        Assert.Equal(1, harness.Renderer.CallCount);
        Assert.Equal(0, harness.Executor.CallCount);
        Assert.Single(harness.Receipts.Captures);
    }

    [Fact]
    public async Task Provider_exception_becomes_recovery_required_without_local_retry()
    {
        Harness harness = new();
        harness.Executor.Exception = new IOException("connection disappeared");

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.RecoveryRequired, result.Outcome);
        Assert.Equal(TransitionDurableState.ProviderOutcomeUnknown, result.DurableState);
        Assert.Equal(1, harness.Executor.CallCount);
        Assert.True(result.AttemptCompleted);
        Assert.NotNull(result.TransitionRun);
        Assert.NotNull(result.Attempt);
        Assert.Equal("RecoveryRequired", Assert.Single(harness.Attempts.Completed).Outcome);
        Assert.Equal(
            RecoveryBoundaryClassification.ProviderUnknown,
            Assert.Single(harness.RecoveryCases.Classifications).Classification);
    }

    [Fact]
    public async Task Raw_outcome_persistence_failure_stops_before_interpretation_or_validation()
    {
        Harness harness = new();
        harness.Evidence.FailRawOutput = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Runtime.RunAsync(harness.Request));

        Assert.Equal(0, harness.Interpreter.CallCount);
        Assert.Equal(0, harness.Validator.CallCount);
        Assert.Null(harness.Commit.Capture);
    }

    [Fact]
    public async Task Stale_inputs_preserve_candidate_evidence_but_deny_atomic_commit()
    {
        Harness harness = new();
        harness.Freshness.Result = new InputFreshnessResult(
            InputFreshnessStatus.InputInvalidated,
            "input changed",
            ["old", "new"]);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.InputInvalidated, result.Outcome);
        Assert.Single(harness.Candidates.Registered);
        Assert.Null(harness.Commit.Capture);
        Assert.Equal("InputInvalidated", Assert.Single(harness.Attempts.Completed).Outcome);
    }

    [Fact]
    public async Task Valid_attempt_atomically_commits_and_returns_required_effects_pending()
    {
        Harness harness = new(withEffect: true);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.EffectsPending, result.Outcome);
        Assert.Equal(TransitionDurableState.EffectsPending, result.DurableState);
        Assert.True(result.AttemptCompleted);
        Assert.True(result.RequiredEffectsPending);
        Assert.NotNull(harness.Commit.Capture);
        Assert.Equal(EffectExecutionStatus.Planned, Assert.Single(result.Effects!.Effects).Status);
    }

    [Fact]
    public async Task Retry_plan_reuses_transition_run_but_mints_a_distinct_attempt()
    {
        Harness first = new();
        TransitionRuntimeResult original = await first.Runtime.RunAsync(first.Request);
        CanonicalTransitionExecutionContext execution = Assert.IsType<CanonicalTransitionExecutionContext>(first.Request.ExecutionContext);
        var source = new CanonicalCausalContext(
            execution.Workspace,
            execution.Run,
            execution.WorkflowInstance,
            original.TransitionRun!.Value,
            original.Attempt!.Value);
        var plan = new TransitionRecoveryPlan(
            RecoveryAttemptIdentity.New(),
            source,
            TransitionRecoveryDisposition.SafeRetry.ToString(),
            CanonicalRecoveryAction.RetryNewAttempt,
            ["submission not accepted"],
            ["durable pre-submission boundary"],
            RecoveryAttemptMode.RetryExistingTransitionRun,
            2);
        Harness retry = new(execution: execution, authorization: new RecoveryAttemptAuthorization(plan));

        TransitionRuntimeResult retried = await retry.Runtime.RunAsync(retry.Request);

        Assert.Equal(original.TransitionRun, retried.TransitionRun);
        Assert.NotEqual(original.Attempt, retried.Attempt);
        Assert.Equal(2, Assert.Single(retry.Attempts.Started).AttemptIndex);
    }

    private sealed class Harness
    {
        public Harness(
            bool withEffect = false,
            CanonicalTransitionExecutionContext? execution = null,
            AttemptAuthorization? authorization = null)
        {
            Definition = DefinitionFor(withEffect);
            Resolver = new FakeDefinitionResolver(Definition);
            Products = new FakeProductResolver();
            Gates = new FakeGateEvaluator();
            Contexts = new FakePromptContextBuilder();
            Renderer = new FakePromptRenderer();
            Prompts = new RecordingPromptStore();
            Executor = new FakePromptExecutor();
            Dispatches = new RecordingDispatchLifecycleStore();
            Gateway = new PromptDispatchGateway(Prompts, Dispatches, Executor);
            Interpreter = new FakeInterpreter();
            Candidates = new RecordingCandidateStore();
            Validator = new FakeValidator();
            Freshness = new FakeFreshnessValidator();
            Runs = new RecordingRunStore();
            Attempts = new RecordingAttemptStore();
            Receipts = new RecordingReceiptStore();
            Evidence = new RecordingEvidenceStore();
            GateEvaluations = new RecordingGateStore();
            Commit = new RecordingCommitStore();
            RecoveryCases = new RecordingRecoveryCaseRecorder();
            Runtime = new TransitionRuntime(
                Resolver,
                Products,
                Gates,
                Contexts,
                Renderer,
                Gateway,
                Interpreter,
                Candidates,
                Validator,
                Freshness,
                Runs,
                Attempts,
                Receipts,
                Evidence,
                GateEvaluations,
                Commit,
                null,
                RecoveryCases);
            CanonicalTransitionExecutionContext context = execution ?? NewExecutionContext();
            Request = new TransitionRuntimeRequest(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Planning"),
                Definition.Identity,
                context,
                authorization ?? FreshAttemptAuthorization.Instance);
        }

        public WorkflowTransitionDefinition Definition { get; }
        public FakeDefinitionResolver Resolver { get; }
        public FakeProductResolver Products { get; }
        public FakeGateEvaluator Gates { get; }
        public FakePromptContextBuilder Contexts { get; }
        public FakePromptRenderer Renderer { get; }
        public RecordingPromptStore Prompts { get; }
        public FakePromptExecutor Executor { get; }
        public RecordingDispatchLifecycleStore Dispatches { get; }
        public PromptDispatchGateway Gateway { get; }
        public FakeInterpreter Interpreter { get; }
        public RecordingCandidateStore Candidates { get; }
        public FakeValidator Validator { get; }
        public FakeFreshnessValidator Freshness { get; }
        public RecordingRunStore Runs { get; }
        public RecordingAttemptStore Attempts { get; }
        public RecordingReceiptStore Receipts { get; }
        public RecordingEvidenceStore Evidence { get; }
        public RecordingGateStore GateEvaluations { get; }
        public RecordingCommitStore Commit { get; }
        public RecordingRecoveryCaseRecorder RecoveryCases { get; }
        public TransitionRuntime Runtime { get; }
        public TransitionRuntimeRequest Request { get; }
    }

    private sealed class RecordingRecoveryCaseRecorder : ICanonicalRecoveryCaseRecorder
    {
        public List<CanonicalRecoveryClassification> Classifications { get; } = [];

        public Task<CanonicalRecoveryClassification> RecordAsync(
            RecoveryScopeKind scope,
            RecoveryCausalSubject subject,
            RecoveryDurableFacts facts,
            CancellationToken cancellationToken = default)
        {
            var recoveryCase = new CanonicalRecoveryCase(
                RecoveryCaseIdentity.New(), scope, subject, DateTimeOffset.UtcNow);
            CanonicalRecoveryClassification classification =
                CanonicalRecoveryClassifier.Classify(recoveryCase, facts);
            Classifications.Add(classification);
            return Task.FromResult(classification);
        }
    }

    private static CanonicalTransitionExecutionContext NewExecutionContext() => new(
        new WorkflowInvocation(InvocationModeKind.BoundedPlan),
        WorkspaceIdentity.New(),
        RunIdentity.New(),
        WorkflowInstanceIdentity.New(),
        new PolicyIdentity("policy_test"),
        new RuntimeProfileIdentity("runtime_test"),
        new PromptPolicyProfileIdentity("prompt_policy_test"));

    private static WorkflowTransitionDefinition DefinitionFor(bool withEffect) => new(
        new WorkflowTransitionIdentity("WritePlan"),
        "write plan",
        [],
        Gate("input"),
        "WritePlan",
        ExecutionPosture.OneShotAgentPrompt,
        [],
        Gate("output"),
        [],
        withEffect
            ? [new EffectDefinition(new EffectIdentity("publish"), EffectCategory.Publication,
                "validated", [], [], 1, "retry")]
            : [],
        [],
        [],
        new RecoveryDefinition("recovery", "recover", ["retry"], []));

    private static GateDefinition Gate(string identity) =>
        new(new GateIdentity(identity), identity, [], "test", "fail");

    private static ProductRecord Candidate() => new(
        new ProductIdentity("plan"),
        WorkflowIdentity.Plan,
        new WorkflowTransitionIdentity("WritePlan"),
        [WorkflowIdentity.Execute],
        "repository",
        "test",
        ["plan.md"],
        "candidate-1",
        ProductFreshness.Fresh,
        ProductValidationState.Valid,
        ProductLifecycle.Active,
        ["plan.md"]);

    private sealed class FakeDefinitionResolver(WorkflowTransitionDefinition definition) : ITransitionDefinitionResolver
    {
        public Task<WorkflowTransitionDefinition> ResolveAsync(TransitionRuntimeRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(definition);

        public Task<IReadOnlyList<WorkflowTransitionIdentity>> ResolveEligibleSuccessorsAsync(
            WorkflowTransitionDefinition resolved,
            IReadOnlyList<ProductRecord> products,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkflowTransitionIdentity>>([]);
    }

    private sealed class FakeProductResolver : IProductResolver
    {
        public Task<ProductResolutionResult> ResolveAsync(
            IReadOnlyList<ProductRequirement> requirements,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ProductResolutionResult([], [], [], [], []));
    }

    private sealed class FakeGateEvaluator : IGateEvaluator
    {
        public Task<GateResult> EvaluateInputGateAsync(
            GateDefinition gate,
            ProductResolutionResult inputs,
            InputGateEvaluationContext context,
            CancellationToken cancellationToken) => Task.FromResult(Satisfied());

        public Task<GateResult> EvaluateOutputGateAsync(
            GateDefinition gate,
            ProductValidationResult validation,
            CancellationToken cancellationToken) => Task.FromResult(Satisfied());

        private static GateResult Satisfied() => new(GateStatus.Satisfied, [], "satisfied", []);
    }

    private sealed class FakePromptContextBuilder : IPromptContextBuilder
    {
        public Task<PromptContext> BuildAsync(
            TransitionRuntimeRequest request,
            WorkflowTransitionDefinition definition,
            ProductResolutionResult inputs,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PromptContext(
                definition,
                inputs,
                new TransitionInputSnapshot("snapshot-1", [], new Dictionary<string, string>(), []),
                new Dictionary<string, string>(),
                [],
                [new ConsumedInputFile("plan.md", new string('a', 64))]));
    }

    private sealed class FakePromptRenderer : IPromptRenderer
    {
        public int CallCount { get; private set; }

        public Task<RenderedPrompt> RenderAsync(PromptRenderRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new RenderedPrompt(
                new PromptTemplateIdentity("template"),
                request.PolicyProfile,
                "rendered prompt",
                "template.prompt",
                "template-hash"));
        }
    }

    private sealed class RecordingPromptStore : IRenderedPromptStore
    {
        public bool FailAppend { get; set; }

        public Task<PersistedRenderedPromptFact> AppendAsync(RenderedPromptFact fact, CancellationToken cancellationToken)
        {
            if (FailAppend)
            {
                throw new InvalidOperationException("prompt persistence failed");
            }

            return Task.FromResult(new PersistedRenderedPromptFact(
                fact,
                RenderedPromptPersistenceIdentity.New(),
                1,
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class FakePromptExecutor : IPromptExecutor
    {
        public int CallCount { get; private set; }
        public Exception? Exception { get; set; }

        public Task<PromptExecutionResult> DispatchAsync(AuthorizedPromptDispatch request, CancellationToken cancellationToken)
        {
            CallCount++;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(new PromptExecutionResult(
                PromptExecutionStatus.Completed,
                "provider output",
                TimeSpan.FromMilliseconds(10),
                new Dictionary<string, string>()));
        }
    }

    private sealed class RecordingDispatchLifecycleStore : IPromptDispatchLifecycleStore
    {
        public List<PromptDispatchLifecycleEvent> Events { get; } = [];

        public Task AppendAsync(PromptDispatchLifecycleEvent dispatchEvent, CancellationToken cancellationToken)
        {
            Events.Add(dispatchEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeInterpreter : IOutputInterpreter
    {
        public int CallCount { get; private set; }

        public Task<InterpretedTransitionOutput> InterpretAsync(
            CanonicalCausalContext causality,
            WorkflowTransitionDefinition definition,
            PromptExecutionResult executionResult,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new InterpretedTransitionOutput(
                OutputInterpretationStatus.Valid, [Candidate()], "interpreted", []));
        }
    }

    private sealed class RecordingCandidateStore : ICandidateProductStore
    {
        public List<ProductRecord> Registered { get; } = [];

        public Task RegisterAsync(
            CanonicalCausalContext causality,
            IReadOnlyList<ProductRecord> candidates,
            CancellationToken cancellationToken)
        {
            Registered.AddRange(candidates);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeValidator : IProductValidator
    {
        public int CallCount { get; private set; }

        public Task<ProductValidationResult> ValidateAsync(
            WorkflowTransitionDefinition definition,
            InterpretedTransitionOutput output,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new ProductValidationResult(
                ProductValidationStatus.Valid,
                output.CandidateProducts,
                [], [], [], [],
                "valid",
                ["validation"]));
        }
    }

    private sealed class FakeFreshnessValidator : IInputFreshnessValidator
    {
        public InputFreshnessResult Result { get; set; } =
            new(InputFreshnessStatus.Fresh, "fresh", ["snapshot-1"]);

        public Task<InputFreshnessResult> ValidateAsync(
            CanonicalCausalContext causality,
            TransitionRuntimeRequest request,
            WorkflowTransitionDefinition definition,
            PromptContext frozenContext,
            CancellationToken cancellationToken) => Task.FromResult(Result);
    }

    private sealed class RecordingRunStore : ITransitionRunStore
    {
        public List<TransitionRunStarted> Started { get; } = [];
        public List<TransitionRunCompleted> Completed { get; } = [];

        public Task PersistStartedAsync(TransitionRunStarted started, CancellationToken cancellationToken)
        {
            Started.Add(started);
            return Task.CompletedTask;
        }

        public Task PersistStateAsync(TransitionRunStateUpdate update, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PersistCompletedAsync(TransitionRunCompleted completed, CancellationToken cancellationToken)
        {
            Completed.Add(completed);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAttemptStore : IAttemptStore
    {
        public bool FailStart { get; set; }
        public List<AttemptRecord> Started { get; } = [];
        public List<(AttemptIdentity Attempt, string Outcome)> Completed { get; } = [];

        public Task PersistAttemptStartedAsync(AttemptRecord attempt, CancellationToken cancellationToken)
        {
            if (FailStart)
            {
                throw new InvalidOperationException("attempt persistence failed");
            }

            Started.Add(attempt);
            return Task.CompletedTask;
        }

        public Task PersistAttemptCompletedAsync(
            AttemptIdentity attempt,
            DateTimeOffset completedAt,
            string outcome,
            CancellationToken cancellationToken)
        {
            Completed.Add((attempt, outcome));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingReceiptStore : IReadReceiptStore
    {
        public List<ReadReceiptCapture> Captures { get; } = [];

        public Task AppendAsync(ReadReceiptCapture capture, CancellationToken cancellationToken)
        {
            Captures.Add(capture);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEvidenceStore : ITransitionEvidenceStore
    {
        public bool FailRawOutput { get; set; }

        public Task RecordEventAsync(TransitionEvidenceEvent evidence, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RecordRawOutputAsync(
            CanonicalCausalContext causality,
            WorkflowTransitionIdentity transition,
            PromptExecutionResult executionResult,
            CancellationToken cancellationToken)
        {
            if (FailRawOutput)
            {
                throw new InvalidOperationException("raw output persistence failed");
            }

            return Task.CompletedTask;
        }

        public Task RecordFailureAsync(
            CanonicalCausalContext causality,
            WorkflowTransitionIdentity transition,
            string failure,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingGateStore : ITransitionGateEvaluationStore
    {
        public Task RecordGateEvaluationAsync(
            TransitionGateEvaluationCapture evaluation,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingCommitStore : ITransitionCommitStore
    {
        public TransitionCommitCapture? Capture { get; private set; }

        public Task CommitAsync(TransitionCommitCapture capture, CancellationToken cancellationToken)
        {
            Capture = capture;
            return Task.CompletedTask;
        }
    }
}
