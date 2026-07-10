using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Runtime;

public sealed class TransitionRuntimeTests
{
    [Fact]
    public async Task Missing_required_input_blocks_before_prompt_execution()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.Products.Result = new ProductResolutionResult([], [RuntimeHarness.InputRequirement], [], [], []);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Blocked, result.Outcome);
        Assert.Equal(TransitionDurableState.Blocked, result.DurableState);
        Assert.False(harness.Executor.WasCalled);
        Assert.Empty(harness.RunStore.Completions);
        TransitionBlockerCapture blocker = Assert.Single(harness.Blockers.Blockers);
        Assert.Equal(harness.Request.Workflow, blocker.Request.Workflow);
        Assert.Equal(harness.Request.Stage, blocker.Request.Stage);
        Assert.Equal(RuntimeHarness.Definition.Identity, blocker.Transition);
        Assert.Equal(BlockerCategory.Validation, blocker.Category);
        Assert.Contains("Input gate blocked", blocker.Reason, StringComparison.Ordinal);
        TransitionRecoveryMarkerCapture recovery = Assert.Single(harness.RecoveryMarkers.Markers);
        Assert.Equal(harness.Request.Workflow, recovery.Request.Workflow);
        Assert.Equal(harness.Request.Stage, recovery.Request.Stage);
        Assert.Equal(RuntimeHarness.Definition.Identity, recovery.Transition);
        Assert.Equal(TransitionDurableState.Blocked, recovery.DurableState);
        Assert.Equal(RuntimeHarness.Definition.Recovery, recovery.Recovery);
        TransitionGateEvaluationCapture gate = Assert.Single(harness.GateEvaluations.Evaluations);
        Assert.Equal(RuntimeHarness.Definition.InputGate.Identity, gate.Gate.Identity);
        Assert.Equal(GateStatus.Blocked, gate.Result.Status);
    }

    [Fact]
    public async Task Stale_required_input_blocks_when_freshness_is_required()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        ProductRecord stale = RuntimeHarness.Product(
            RuntimeHarness.InputProduct,
            WorkflowIdentity.TraditionalRoadmap,
            new WorkflowTransitionIdentity("ProduceInput"),
            ProductFreshness.Stale,
            ProductValidationState.Valid);
        harness.Products.Result = new ProductResolutionResult([stale], [], [stale], [], []);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Blocked, result.Outcome);
        Assert.False(harness.Executor.WasCalled);
    }

    [Fact]
    public async Task Invalid_required_input_blocks_before_prompt_execution()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        ProductRecord invalid = RuntimeHarness.Product(
            RuntimeHarness.InputProduct,
            WorkflowIdentity.TraditionalRoadmap,
            new WorkflowTransitionIdentity("ProduceInput"),
            ProductFreshness.Fresh,
            ProductValidationState.Invalid);
        harness.Products.Result = new ProductResolutionResult([invalid], [], [], [invalid], []);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Blocked, result.Outcome);
        Assert.False(harness.Executor.WasCalled);
    }

    [Fact]
    public async Task Prompt_failure_does_not_complete_transition()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.Executor.Result = new PromptExecutionResult(
            PromptExecutionStatus.Failed,
            "partial output",
            TimeSpan.FromMilliseconds(10),
            new Dictionary<string, string>(),
            "agent failed");

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Equal(TransitionDurableState.Failed, result.DurableState);
        Assert.Empty(harness.RunStore.Completions);
        Assert.Contains(harness.Evidence.Failures, failure => failure.Contains("agent failed", StringComparison.Ordinal));
        TransitionRecoveryMarkerCapture recovery = Assert.Single(harness.RecoveryMarkers.Markers);
        Assert.Equal(TransitionDurableState.Failed, recovery.DurableState);
        Assert.Equal(RuntimeOutcomeKind.Failed, recovery.Outcome);
        Assert.Equal(RuntimeHarness.Definition.Recovery, recovery.Recovery);
    }

    [Fact]
    public async Task Prompt_context_block_stops_before_prompt_execution()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.ContextBuilder.BlockedException = new PromptContextBlockedException(
            "Active Epic prompt context is missing.",
            [".agents/epic.md"]);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Blocked, result.Outcome);
        Assert.Equal(TransitionDurableState.Blocked, result.DurableState);
        Assert.Equal("Active Epic prompt context is missing.", result.Explanation);
        Assert.Contains(".agents/epic.md", result.Evidence);
        Assert.False(harness.Executor.WasCalled);
        Assert.Empty(harness.RunStore.Started);
        Assert.Empty(harness.RunStore.Completions);
        TransitionBlockerCapture blocker = Assert.Single(harness.Blockers.Blockers);
        Assert.Equal(harness.Request.Workflow, blocker.Request.Workflow);
        Assert.Equal(harness.Request.Stage, blocker.Request.Stage);
        Assert.Equal(RuntimeHarness.Definition.Identity, blocker.Transition);
        Assert.Equal(BlockerCategory.Transition, blocker.Category);
        Assert.Contains(".agents/epic.md", blocker.Evidence);
        TransitionRecoveryMarkerCapture recovery = Assert.Single(harness.RecoveryMarkers.Markers);
        Assert.Equal(TransitionDurableState.Blocked, recovery.DurableState);
        Assert.Equal(RuntimeHarness.Definition.Recovery, recovery.Recovery);
        Assert.Contains(".agents/epic.md", recovery.Evidence);
    }

    [Fact]
    public async Task Malformed_output_does_not_satisfy_the_output_gate()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.Interpreter.Output = new InterpretedTransitionOutput(
            OutputInterpretationStatus.Malformed,
            [],
            "Output was malformed.",
            ["malformed-output.md"]);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Equal(TransitionDurableState.Failed, result.DurableState);
        Assert.Null(result.OutputGate);
        Assert.False(harness.Effects.WasCalled);
    }

    [Fact]
    public async Task Missing_output_does_not_satisfy_the_output_gate()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.Validator.Result = new ProductValidationResult(
            ProductValidationStatus.Missing,
            [],
            [RuntimeHarness.OutputProduct],
            [],
            [],
            [],
            "Required output product is missing.",
            ["missing-output.md"]);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.NotNull(result.OutputGate);
        Assert.False(result.OutputGate!.IsSatisfied);
        Assert.False(harness.Effects.WasCalled);
    }

    [Fact]
    public async Task Invalid_output_remains_evidence_but_not_a_usable_product()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.Validator.Result = new ProductValidationResult(
            ProductValidationStatus.Invalid,
            [RuntimeHarness.ValidOutput],
            [],
            [RuntimeHarness.OutputProduct],
            [],
            [],
            "Output product failed validation.",
            ["invalid-output.md"]);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Equal(ProductValidationStatus.Invalid, result.ProductValidation!.Status);
        Assert.Contains("invalid-output.md", result.Evidence);
        Assert.False(harness.Effects.WasCalled);
    }

    [Fact]
    public async Task Partial_effect_failure_is_observable_and_does_not_complete_transition()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.Effects.Result = new EffectExecutionResult(
            EffectExecutionStatus.PartiallyFailed,
            [
                new EffectExecutionRecord(
                    new EffectIdentity("write-output"),
                    EffectExecutionStatus.Succeeded,
                    "Output persisted.",
                    ["output.md"]),
                new EffectExecutionRecord(
                    new EffectIdentity("publish-output"),
                    EffectExecutionStatus.Failed,
                    "Publication failed.",
                    ["publish-failure.md"]),
            ],
            "One effect failed after earlier effects succeeded.",
            ["publish-failure.md"]);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Equal(TransitionDurableState.EffectsPartiallyApplied, result.DurableState);
        Assert.Equal(EffectExecutionStatus.PartiallyFailed, result.Effects!.Status);
        Assert.Empty(harness.RunStore.Completions);
        TransitionRecoveryMarkerCapture recovery = Assert.Single(harness.RecoveryMarkers.Markers);
        Assert.Equal(TransitionDurableState.EffectsPartiallyApplied, recovery.DurableState);
        Assert.Equal(RuntimeOutcomeKind.Failed, recovery.Outcome);
        Assert.Equal(RuntimeHarness.Definition.Recovery, recovery.Recovery);
        Assert.Equal(
            [EffectExecutionStatus.Succeeded, EffectExecutionStatus.Failed],
            harness.EffectRecords.Records.Select(effect => effect.Status).ToArray());
        Assert.Equal(
            [EffectCategory.ProductPersistence, EffectCategory.Evidence],
            harness.EffectRecords.Records.Select(effect => effect.Category).ToArray());
    }

    [Fact]
    public async Task Stalled_effect_returns_stalled_runtime_outcome_with_recovery_evidence()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.Effects.Result = new EffectExecutionResult(
            EffectExecutionStatus.Stalled,
            [
                new EffectExecutionRecord(
                    new EffectIdentity("evaluate-progress"),
                    EffectExecutionStatus.Stalled,
                    "No substantive progress was detected.",
                    ["stall.md"]),
            ],
            "Commit gate stalled.",
            ["stall.md"]);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Stalled, result.Outcome);
        Assert.Equal(TransitionDurableState.Stalled, result.DurableState);
        Assert.Equal(EffectExecutionStatus.Stalled, result.Effects!.Status);
        Assert.Empty(harness.RunStore.Completions);
        Assert.Contains(harness.Evidence.Events, evidence => evidence.EventName == "TransitionStalled");
        TransitionRecoveryMarkerCapture recovery = Assert.Single(harness.RecoveryMarkers.Markers);
        Assert.Equal(TransitionDurableState.Stalled, recovery.DurableState);
        Assert.Equal(RuntimeOutcomeKind.Stalled, recovery.Outcome);
        TransitionEffectRecordCapture effect = Assert.Single(harness.EffectRecords.Records);
        Assert.Equal(EffectExecutionStatus.Stalled, effect.Status);
    }

    [Fact]
    public async Task Cancellation_remains_cancellation()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.Executor.ThrowCancellation = true;

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Cancelled, result.Outcome);
        Assert.Equal(TransitionDurableState.Cancelled, result.DurableState);
        Assert.Empty(harness.RunStore.Completions);
        TransitionRecoveryMarkerCapture recovery = Assert.Single(harness.RecoveryMarkers.Markers);
        Assert.Equal(TransitionDurableState.Cancelled, recovery.DurableState);
        Assert.Equal(RuntimeOutcomeKind.Cancelled, recovery.Outcome);
        Assert.Equal(RuntimeHarness.Definition.Recovery, recovery.Recovery);
    }

    [Fact]
    public async Task Persistence_failure_is_returned_as_failed_runtime_result()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.RunStore.ThrowOnCompleted = true;

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Equal(TransitionDurableState.Failed, result.DurableState);
        Assert.Contains("completed persistence failed", result.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Representative_transition_executes_fully_through_the_runtime()
    {
        RuntimeHarness harness = RuntimeHarness.Create();

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Completed, result.Outcome);
        Assert.Equal(TransitionDurableState.Completed, result.DurableState);
        Assert.True(result.InputGate!.IsSatisfied);
        Assert.True(result.OutputGate!.IsSatisfied);
        Assert.True(result.ProductValidation!.IsValid);
        Assert.True(result.Effects!.IsSuccess);
        Assert.Equal([new WorkflowTransitionIdentity("NextTransition")], result.EligibleSuccessors);
        Assert.Equal(
            [
                TransitionDurableState.Started,
                TransitionDurableState.PromptCompleted,
                TransitionDurableState.OutputInterpreted,
                TransitionDurableState.OutputValidated,
                TransitionDurableState.EffectsApplied,
                TransitionDurableState.Completed,
            ],
            harness.RunStore.States);
        Assert.Matches("^[a-f0-9]{64}$", harness.RunStore.Started.Single().InputSnapshot.Hash);
        Assert.Empty(harness.RecoveryMarkers.Markers);
        Assert.Equal(
            [RuntimeHarness.Definition.InputGate.Identity, RuntimeHarness.Definition.OutputGate.Identity],
            harness.GateEvaluations.Evaluations.Select(evaluation => evaluation.Gate.Identity).ToArray());
        Assert.All(harness.GateEvaluations.Evaluations, evaluation => Assert.Equal(GateStatus.Satisfied, evaluation.Result.Status));
        TransitionEffectRecordCapture effect = Assert.Single(harness.EffectRecords.Records);
        Assert.Equal(harness.RunStore.Started.Single().RunId, effect.RunId);
        Assert.Equal(new EffectIdentity("write-output"), effect.Effect);
        Assert.Equal(EffectCategory.ProductPersistence, effect.Category);
        Assert.Equal(EffectExecutionStatus.Succeeded, effect.Status);
    }

    [Fact]
    public void Input_snapshot_hash_is_stable_and_changes_when_product_causal_identity_changes()
    {
        WorkflowTransitionDefinition definition = RuntimeHarness.Definition;
        ProductRecord input = RuntimeHarness.Product(
            RuntimeHarness.InputProduct,
            WorkflowIdentity.TraditionalRoadmap,
            new WorkflowTransitionIdentity("ProduceInput"),
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            causalIdentity: "hash-a");
        ProductRecord changed = input with
        {
            CausalIdentity = "hash-b",
        };

        TransitionInputSnapshot first = TransitionInputSnapshotHasher.Create(
            definition,
            [input],
            new Dictionary<string, string> { ["projection"] = "p1" });
        TransitionInputSnapshot second = TransitionInputSnapshotHasher.Create(
            definition,
            [input],
            new Dictionary<string, string> { ["projection"] = "p1" });
        TransitionInputSnapshot third = TransitionInputSnapshotHasher.Create(
            definition,
            [changed],
            new Dictionary<string, string> { ["projection"] = "p1" });
        TransitionInputSnapshot withSection = TransitionInputSnapshotHasher.Create(
            definition,
            [input],
            new Dictionary<string, string> { ["projection"] = "p1" },
            [new PromptContextSection("Active Epic", "content-a", ".agents/epic.md", [".agents/epic.md"])]);
        TransitionInputSnapshot changedSection = TransitionInputSnapshotHasher.Create(
            definition,
            [input],
            new Dictionary<string, string> { ["projection"] = "p1" },
            [new PromptContextSection("Active Epic", "content-b", ".agents/epic.md", [".agents/epic.md"])]);

        Assert.Equal(first.Hash, second.Hash);
        Assert.NotEqual(first.Hash, third.Hash);
        Assert.NotEqual(first.Hash, withSection.Hash);
        Assert.NotEqual(withSection.Hash, changedSection.Hash);
    }

    private sealed class RuntimeHarness
    {
        public static readonly ProductIdentity InputProduct = new("InputProduct");
        public static readonly ProductIdentity OutputProduct = new("OutputProduct");
        public static readonly ProductRequirement InputRequirement = new(
            InputProduct,
            DependencyStrength.FreshnessSensitive,
            true,
            "canonical product authority",
            "Input product required.");
        public static readonly ProductRecord ValidInput = Product(
            InputProduct,
            WorkflowIdentity.TraditionalRoadmap,
            new WorkflowTransitionIdentity("ProduceInput"),
            ProductFreshness.Fresh,
            ProductValidationState.Valid);
        public static readonly ProductRecord ValidOutput = Product(
            OutputProduct,
            WorkflowIdentity.Plan,
            new WorkflowTransitionIdentity("GenerateOutput"),
            ProductFreshness.Fresh,
            ProductValidationState.Valid);
        public static readonly WorkflowTransitionDefinition Definition = new(
            new WorkflowTransitionIdentity("GenerateOutput"),
            "Generate output product from input product.",
            [InputRequirement],
            Gate("InputGate", InputProduct),
            "GenerateOutputPrompt",
            ExecutionPosture.OneShotAgentPrompt,
            [
                new ProductDefinition(
                    OutputProduct,
                    WorkflowIdentity.Plan,
                    new WorkflowTransitionIdentity("GenerateOutput"),
                    [WorkflowIdentity.Execute],
                    "repository-owned orchestration evidence",
                    "canonical workflow contract",
                    ProductLifecycle.Active,
                    ProductValidationState.Valid,
                    ProductFreshness.Fresh,
                    [],
                    ["output representation"]),
            ],
            Gate("OutputGate", OutputProduct),
            ["OutputProductValidator"],
            [
                new EffectDefinition(
                    new EffectIdentity("write-output"),
                    EffectCategory.ProductPersistence,
                    "Run after output gate satisfaction.",
                    [InputProduct],
                    [OutputProduct],
                    0,
                    "Failure prevents completion."),
            ],
            [],
            [new WorkflowTransitionIdentity("NextTransition")],
            new RecoveryDefinition("RuntimeHarness.Recovery", "Recover from evidence.", ["restart"], ["silent repair"]));

        private RuntimeHarness()
        {
            Definitions = new FakeDefinitionResolver();
            Products = new FakeProductResolver();
            Gates = new FakeGateEvaluator();
            ContextBuilder = new FakePromptContextBuilder();
            Renderer = new FakePromptRenderer();
            Executor = new FakePromptExecutor();
            Interpreter = new FakeOutputInterpreter();
            Validator = new FakeProductValidator();
            Effects = new FakeEffectExecutor();
            RunStore = new RecordingRunStore();
            Evidence = new RecordingEvidenceStore();
            Blockers = new RecordingBlockerStore();
            RecoveryMarkers = new RecordingRecoveryStore();
            GateEvaluations = new RecordingGateEvaluationStore();
            EffectRecords = new RecordingEffectStore();
            Runtime = new TransitionRuntime(
                Definitions,
                Products,
                Gates,
                ContextBuilder,
                Renderer,
                Executor,
                Interpreter,
                Validator,
                Effects,
                RunStore,
                Evidence,
                Blockers,
                RecoveryMarkers,
                GateEvaluations,
                EffectRecords);
            Request = new TransitionRuntimeRequest(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Planning"),
                Definition.Identity,
                new Dictionary<string, string> { ["project-context"] = "hash" });
        }

        public TransitionRuntime Runtime { get; }

        public TransitionRuntimeRequest Request { get; }

        public FakeDefinitionResolver Definitions { get; }

        public FakeProductResolver Products { get; }

        public FakeGateEvaluator Gates { get; }

        public FakePromptContextBuilder ContextBuilder { get; }

        public FakePromptRenderer Renderer { get; }

        public FakePromptExecutor Executor { get; }

        public FakeOutputInterpreter Interpreter { get; }

        public FakeProductValidator Validator { get; }

        public FakeEffectExecutor Effects { get; }

        public RecordingRunStore RunStore { get; }

        public RecordingEvidenceStore Evidence { get; }

        public RecordingBlockerStore Blockers { get; }

        public RecordingRecoveryStore RecoveryMarkers { get; }

        public RecordingGateEvaluationStore GateEvaluations { get; }

        public RecordingEffectStore EffectRecords { get; }

        public static RuntimeHarness Create() => new();

        public static ProductRecord Product(
            ProductIdentity identity,
            WorkflowIdentity workflow,
            WorkflowTransitionIdentity transition,
            ProductFreshness freshness,
            ProductValidationState validation,
            string causalIdentity = "hash") =>
            new(
                identity,
                workflow,
                transition,
                [WorkflowIdentity.Plan],
                "repository-owned orchestration evidence",
                "canonical product authority",
                ["artifact representation"],
                causalIdentity,
                freshness,
                validation,
                ProductLifecycle.Active,
                ["evidence.md"]);

        private static GateDefinition Gate(string identity, params ProductIdentity[] products) =>
            new(
                new GateIdentity(identity),
                "Gate purpose.",
                products.Select(product => new GateRequirementDefinition(
                    $"{identity}.{product}",
                    $"Validate {product}.",
                    product,
                    DependencyStrength.Required,
                    true)).ToArray(),
                "canonical gate authority",
                "Unsatisfied requirements stop progress.");
    }

    private sealed class FakeDefinitionResolver : ITransitionDefinitionResolver
    {
        public Task<WorkflowTransitionDefinition> ResolveAsync(
            TransitionRuntimeRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(RuntimeHarness.Definition);

        public Task<IReadOnlyList<WorkflowTransitionIdentity>> ResolveEligibleSuccessorsAsync(
            WorkflowTransitionDefinition definition,
            IReadOnlyList<ProductRecord> validatedProducts,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkflowTransitionIdentity>>(definition.EligibleSuccessors);
    }

    private sealed class FakeProductResolver : IProductResolver
    {
        public ProductResolutionResult Result { get; set; } =
            new([RuntimeHarness.ValidInput], [], [], [], []);

        public Task<ProductResolutionResult> ResolveAsync(
            IReadOnlyList<ProductRequirement> requirements,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result);
    }

    private sealed class FakeGateEvaluator : IGateEvaluator
    {
        public Task<GateResult> EvaluateInputGateAsync(
            GateDefinition gate,
            ProductResolutionResult inputs,
            CancellationToken cancellationToken)
        {
            GateStatus status = inputs.IsUsable ? GateStatus.Satisfied : GateStatus.Blocked;
            return Task.FromResult(new GateResult(
                status,
                [new GateRequirementResult(gate.Requirements[0].Identity, status, status.ToString(), [])],
                status == GateStatus.Satisfied ? "Input gate satisfied." : "Input gate blocked.",
                status == GateStatus.Satisfied ? [] : ["input-gate.md"]));
        }

        public Task<GateResult> EvaluateOutputGateAsync(
            GateDefinition gate,
            ProductValidationResult validation,
            CancellationToken cancellationToken)
        {
            GateStatus status = validation.IsValid ? GateStatus.Satisfied : GateStatus.Unsatisfied;
            return Task.FromResult(new GateResult(
                status,
                [new GateRequirementResult(gate.Requirements[0].Identity, status, status.ToString(), validation.Evidence)],
                validation.IsValid ? "Output gate satisfied." : "Output gate unsatisfied.",
                validation.Evidence));
        }
    }

    private sealed class FakePromptContextBuilder : IPromptContextBuilder
    {
        public PromptContextBlockedException? BlockedException { get; set; }

        public Task<PromptContext> BuildAsync(
            TransitionRuntimeRequest request,
            WorkflowTransitionDefinition definition,
            ProductResolutionResult inputs,
            CancellationToken cancellationToken)
        {
            if (BlockedException is not null)
            {
                throw BlockedException;
            }

            IReadOnlyDictionary<string, string> metadata =
                request.Metadata ?? new Dictionary<string, string>();
            return Task.FromResult(new PromptContext(
                definition,
                inputs,
                TransitionInputSnapshotHasher.Create(definition, inputs.Products, metadata),
                metadata,
                []));
        }
    }

    private sealed class FakePromptRenderer : IPromptRenderer
    {
        public Task<RenderedPrompt> RenderAsync(
            WorkflowTransitionDefinition definition,
            PromptContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(new RenderedPrompt(definition.PromptIdentity, "prompt text", "rendered-prompt.md"));
    }

    private sealed class FakePromptExecutor : IPromptExecutor
    {
        public bool ThrowCancellation { get; set; }

        public bool WasCalled { get; private set; }

        public PromptExecutionResult Result { get; set; } =
            new(PromptExecutionStatus.Completed, "raw output", TimeSpan.FromMilliseconds(5), new Dictionary<string, string>());

        public Task<PromptExecutionResult> ExecuteAsync(
            WorkflowTransitionDefinition definition,
            RenderedPrompt prompt,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            if (ThrowCancellation)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return Task.FromResult(Result);
        }
    }

    private sealed class FakeOutputInterpreter : IOutputInterpreter
    {
        public InterpretedTransitionOutput Output { get; set; } =
            new(OutputInterpretationStatus.Valid, [RuntimeHarness.ValidOutput], "Output interpreted.", ["interpreted-output.md"]);

        public Task<InterpretedTransitionOutput> InterpretAsync(
            WorkflowTransitionDefinition definition,
            PromptExecutionResult executionResult,
            CancellationToken cancellationToken) =>
            Task.FromResult(Output);
    }

    private sealed class FakeProductValidator : IProductValidator
    {
        public ProductValidationResult Result { get; set; } =
            new(ProductValidationStatus.Valid, [RuntimeHarness.ValidOutput], [], [], [], [], "Products validated.", ["validated-output.md"]);

        public Task<ProductValidationResult> ValidateAsync(
            WorkflowTransitionDefinition definition,
            InterpretedTransitionOutput output,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result);
    }

    private sealed class FakeEffectExecutor : IEffectExecutor
    {
        public bool WasCalled { get; private set; }

        public EffectExecutionResult Result { get; set; } =
            new(
                EffectExecutionStatus.Succeeded,
                [
                    new EffectExecutionRecord(
                        new EffectIdentity("write-output"),
                        EffectExecutionStatus.Succeeded,
                        "Output persisted.",
                        ["output.md"]),
                ],
                "Effects applied.",
                ["effects.md"]);

        public Task<EffectExecutionResult> ExecuteAsync(
            WorkflowTransitionDefinition definition,
            ProductValidationResult validation,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(Result);
        }
    }

    private sealed class RecordingRunStore : ITransitionRunStore
    {
        public List<TransitionRunStarted> Started { get; } = [];

        public List<TransitionDurableState> States { get; } = [];

        public List<TransitionRunCompleted> Completions { get; } = [];

        public bool ThrowOnCompleted { get; set; }

        public Task PersistStartedAsync(TransitionRunStarted started, CancellationToken cancellationToken)
        {
            Started.Add(started);
            States.Add(TransitionDurableState.Started);
            return Task.CompletedTask;
        }

        public Task PersistStateAsync(TransitionRunStateUpdate update, CancellationToken cancellationToken)
        {
            States.Add(update.State);
            return Task.CompletedTask;
        }

        public Task PersistCompletedAsync(TransitionRunCompleted completed, CancellationToken cancellationToken)
        {
            if (ThrowOnCompleted)
            {
                throw new InvalidOperationException("completed persistence failed");
            }

            Completions.Add(completed);
            States.Add(TransitionDurableState.Completed);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEvidenceStore : ITransitionEvidenceStore
    {
        public List<TransitionEvidenceEvent> Events { get; } = [];

        public List<PromptExecutionResult> RawOutputs { get; } = [];

        public List<string> Failures { get; } = [];

        public Task RecordEventAsync(TransitionEvidenceEvent evidence, CancellationToken cancellationToken)
        {
            Events.Add(evidence);
            return Task.CompletedTask;
        }

        public Task RecordRawOutputAsync(
            string runId,
            WorkflowTransitionIdentity transition,
            PromptExecutionResult executionResult,
            CancellationToken cancellationToken)
        {
            RawOutputs.Add(executionResult);
            return Task.CompletedTask;
        }

        public Task RecordFailureAsync(
            string runId,
            WorkflowTransitionIdentity transition,
            string failure,
            CancellationToken cancellationToken)
        {
            Failures.Add(failure);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingBlockerStore : ITransitionBlockerStore
    {
        public List<TransitionBlockerCapture> Blockers { get; } = [];

        public Task RecordBlockerAsync(
            TransitionBlockerCapture blocker,
            CancellationToken cancellationToken)
        {
            Blockers.Add(blocker);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRecoveryStore : ITransitionRecoveryStore
    {
        public List<TransitionRecoveryMarkerCapture> Markers { get; } = [];

        public Task RecordRecoveryMarkerAsync(
            TransitionRecoveryMarkerCapture marker,
            CancellationToken cancellationToken)
        {
            Markers.Add(marker);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingGateEvaluationStore : ITransitionGateEvaluationStore
    {
        public List<TransitionGateEvaluationCapture> Evaluations { get; } = [];

        public Task RecordGateEvaluationAsync(
            TransitionGateEvaluationCapture evaluation,
            CancellationToken cancellationToken)
        {
            Evaluations.Add(evaluation);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEffectStore : ITransitionEffectStore
    {
        public List<TransitionEffectRecordCapture> Records { get; } = [];

        public Task RecordEffectAsync(
            TransitionEffectRecordCapture effect,
            CancellationToken cancellationToken)
        {
            Records.Add(effect);
            return Task.CompletedTask;
        }
    }
}
