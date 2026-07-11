using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Runtime;

public sealed class TransitionRuntimeTests
{
    [Fact]
    public async Task Missing_required_input_stops_before_prompt_execution()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.Products.Result = new ProductResolutionResult([], [RuntimeHarness.InputRequirement], [], [], []);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.MissingRequiredInput, result.Outcome);
        Assert.Equal(TransitionDurableState.InputUnsatisfied, result.DurableState);
        Assert.False(harness.Executor.WasCalled);
        Assert.Empty(harness.RunStore.Completions);
        TransitionWarningCapture warning = Assert.Single(harness.Warnings.Warnings);
        Assert.Equal(harness.Request.Workflow, warning.Request.Workflow);
        Assert.Equal(harness.Request.Stage, warning.Request.Stage);
        Assert.Equal(RuntimeHarness.Definition.Identity, warning.Transition);
        Assert.Equal(WarningCategory.Validation, warning.Category);
        Assert.Contains("Input gate unsatisfied", warning.Concern, StringComparison.Ordinal);
        TransitionRecoveryMarkerCapture recovery = Assert.Single(harness.RecoveryMarkers.Markers);
        Assert.Equal(harness.Request.Workflow, recovery.Request.Workflow);
        Assert.Equal(harness.Request.Stage, recovery.Request.Stage);
        Assert.Equal(RuntimeHarness.Definition.Identity, recovery.Transition);
        Assert.Equal(TransitionDurableState.InputUnsatisfied, recovery.DurableState);
        Assert.Equal(RuntimeHarness.Definition.Recovery, recovery.Recovery);
        TransitionGateEvaluationCapture gate = Assert.Single(harness.GateEvaluations.Evaluations);
        Assert.Equal(RuntimeHarness.Definition.InputGate.Identity, gate.Gate.Identity);
        Assert.Equal(GateStatus.Unsatisfied, gate.Result.Status);
    }

    [Fact]
    public async Task Stale_required_input_stops_when_freshness_is_required()
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

        Assert.Equal(RuntimeOutcomeKind.MissingRequiredInput, result.Outcome);
        Assert.False(harness.Executor.WasCalled);
    }

    [Fact]
    public async Task Invalid_required_input_stops_before_prompt_execution()
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

        Assert.Equal(RuntimeOutcomeKind.MissingRequiredInput, result.Outcome);
        Assert.False(harness.Executor.WasCalled);
    }

    [Fact]
    public async Task Unsatisfied_clean_input_requirement_maps_to_dirty_input_surface_outcome()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        WorkflowTransitionDefinition definition = WithCleanInputRequirement();
        harness.Definitions.Definition = definition;
        harness.Gates.InputResult = new GateResult(
            GateStatus.Unsatisfied,
            [
                new GateRequirementResult(
                    definition.InputGate.Requirements[0].Identity,
                    GateStatus.Satisfied,
                    "Required input product is resolved and usable.",
                    ["evidence.md"]),
                new GateRequirementResult(
                    "InputGate.CleanInput",
                    GateStatus.Unsatisfied,
                    "Input surface '.agents/' has uncommitted changes; commit the listed files under '.agents/' before rerunning.",
                    [".agents/epic.md"]),
            ],
            "Input surface '.agents/' has uncommitted changes; commit the listed files under '.agents/' before rerunning.",
            [".agents/epic.md"]);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.DirtyInputSurface, result.Outcome);
        Assert.Equal(TransitionDurableState.InputUnsatisfied, result.DurableState);
        Assert.False(harness.Executor.WasCalled);
        Assert.Empty(harness.RunStore.Completions);
        Assert.Contains(".agents/epic.md", result.Evidence);
        TransitionWarningCapture warning = Assert.Single(harness.Warnings.Warnings);
        Assert.Equal(WarningCategory.Validation, warning.Category);
        Assert.Contains("Commit the listed files", warning.Remediation, StringComparison.Ordinal);
        TransitionRecoveryMarkerCapture recovery = Assert.Single(harness.RecoveryMarkers.Markers);
        Assert.Equal(TransitionDurableState.InputUnsatisfied, recovery.DurableState);
        Assert.Equal(RuntimeOutcomeKind.DirtyInputSurface, recovery.Outcome);
    }

    [Fact]
    public async Task First_unsatisfied_requirement_kind_selects_the_specific_input_label()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        WorkflowTransitionDefinition definition = WithCleanInputRequirement();
        harness.Definitions.Definition = definition;
        harness.Gates.InputResult = new GateResult(
            GateStatus.Unsatisfied,
            [
                new GateRequirementResult(
                    definition.InputGate.Requirements[0].Identity,
                    GateStatus.Unsatisfied,
                    "Required input product 'InputProduct' is missing; produce it before rerunning.",
                    ["InputProduct"]),
                new GateRequirementResult(
                    "InputGate.CleanInput",
                    GateStatus.Unsatisfied,
                    "Input surface '.agents/' has uncommitted changes; commit the listed files under '.agents/' before rerunning.",
                    [".agents/epic.md"]),
            ],
            "Required input product 'InputProduct' is missing; produce it before rerunning.",
            ["InputProduct", ".agents/epic.md"]);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.MissingRequiredInput, result.Outcome);
        Assert.Equal(TransitionDurableState.InputUnsatisfied, result.DurableState);
        Assert.False(harness.Executor.WasCalled);
    }

    [Fact]
    public async Task Declared_unsatisfied_outcome_overrides_the_shape_fallback()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        WorkflowTransitionDefinition definition = WithCleanInputRequirement();
        harness.Definitions.Definition = definition;
        harness.Gates.InputResult = new GateResult(
            GateStatus.Unsatisfied,
            [
                new GateRequirementResult(
                    definition.InputGate.Requirements[0].Identity,
                    GateStatus.Satisfied,
                    "Required input product is resolved and usable.",
                    ["evidence.md"]),
                new GateRequirementResult(
                    "InputGate.CleanInput",
                    GateStatus.Unsatisfied,
                    "Input surface '.agents/' cannot resolve to a commit because no git working tree exists.",
                    [".agents/"],
                    RuntimeOutcomeKind.UnversionedInputSurface),
            ],
            "Input surface '.agents/' cannot resolve to a commit because no git working tree exists.",
            [".agents/"]);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.UnversionedInputSurface, result.Outcome);
        Assert.Equal(TransitionDurableState.InputUnsatisfied, result.DurableState);
        Assert.False(harness.Executor.WasCalled);
        Assert.Empty(harness.RunStore.Completions);
        TransitionWarningCapture warning = Assert.Single(harness.Warnings.Warnings);
        Assert.Equal(WarningCategory.Validation, warning.Category);
        Assert.Contains("Initialize git", warning.Remediation, StringComparison.Ordinal);
        TransitionRecoveryMarkerCapture recovery = Assert.Single(harness.RecoveryMarkers.Markers);
        Assert.Equal(TransitionDurableState.InputUnsatisfied, recovery.DurableState);
        Assert.Equal(RuntimeOutcomeKind.UnversionedInputSurface, recovery.Outcome);
    }

    [Theory]
    [InlineData(RuntimeOutcomeKind.DirtyInputSurface, RuntimeOutcomeKind.UnversionedInputSurface, RuntimeOutcomeKind.UnversionedInputSurface)]
    [InlineData(RuntimeOutcomeKind.UnversionedInputSurface, RuntimeOutcomeKind.DirtyInputSurface, RuntimeOutcomeKind.UnversionedInputSurface)]
    [InlineData(RuntimeOutcomeKind.UnversionedInputSurface, RuntimeOutcomeKind.MissingRequiredInput, RuntimeOutcomeKind.MissingRequiredInput)]
    [InlineData(RuntimeOutcomeKind.MissingRequiredInput, RuntimeOutcomeKind.DirtyInputSurface, RuntimeOutcomeKind.MissingRequiredInput)]
    public async Task Worst_declared_unsatisfied_outcome_wins_regardless_of_requirement_order(
        RuntimeOutcomeKind first,
        RuntimeOutcomeKind second,
        RuntimeOutcomeKind expected)
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.Gates.InputResult = new GateResult(
            GateStatus.Unsatisfied,
            [
                new GateRequirementResult(
                    "InputGate.First",
                    GateStatus.Unsatisfied,
                    "First requirement is unsatisfied.",
                    ["first.md"],
                    first),
                new GateRequirementResult(
                    "InputGate.Second",
                    GateStatus.Unsatisfied,
                    "Second requirement is unsatisfied.",
                    ["second.md"],
                    second),
            ],
            "Input gate unsatisfied.",
            ["first.md", "second.md"]);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(TransitionDurableState.InputUnsatisfied, result.DurableState);
        Assert.False(harness.Executor.WasCalled);
    }

    [Fact]
    public async Task Declared_outcome_on_a_satisfied_requirement_does_not_affect_the_shape_fallback()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        WorkflowTransitionDefinition definition = WithCleanInputRequirement();
        harness.Definitions.Definition = definition;
        harness.Gates.InputResult = new GateResult(
            GateStatus.Unsatisfied,
            [
                new GateRequirementResult(
                    definition.InputGate.Requirements[0].Identity,
                    GateStatus.Satisfied,
                    "Required input product is resolved and usable.",
                    ["evidence.md"],
                    RuntimeOutcomeKind.MissingRequiredInput),
                new GateRequirementResult(
                    "InputGate.CleanInput",
                    GateStatus.Unsatisfied,
                    "Input surface '.agents/' has uncommitted changes; commit the listed files under '.agents/' before rerunning.",
                    [".agents/epic.md"]),
            ],
            "Input surface '.agents/' has uncommitted changes; commit the listed files under '.agents/' before rerunning.",
            [".agents/epic.md"]);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.DirtyInputSurface, result.Outcome);
        Assert.Equal(TransitionDurableState.InputUnsatisfied, result.DurableState);
        Assert.False(harness.Executor.WasCalled);
    }

    [Fact]
    public async Task Waiting_output_gate_maps_to_waiting_outcome_and_waiting_durable_state()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.Gates.OutputResult = new GateResult(
            GateStatus.Waiting,
            [
                new GateRequirementResult(
                    RuntimeHarness.Definition.OutputGate.Requirements[0].Identity,
                    GateStatus.Waiting,
                    "Output confirmation is pending.",
                    ["pending-output.md"]),
            ],
            "Output confirmation is pending.",
            ["pending-output.md"]);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Waiting, result.Outcome);
        Assert.Equal(TransitionDurableState.Waiting, result.DurableState);
        Assert.True(harness.Executor.WasCalled);
        Assert.Empty(harness.RunStore.Completions);
        TransitionWarningCapture warning = Assert.Single(harness.Warnings.Warnings);
        Assert.Equal(WarningCategory.Transition, warning.Category);
        Assert.Contains("Satisfy the output gate", warning.Remediation, StringComparison.Ordinal);
        Assert.Empty(harness.RecoveryMarkers.Markers);
    }

    [Fact]
    public async Task Ambiguous_output_gate_maps_to_ambiguous_outcome_and_ambiguous_durable_state()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.Gates.OutputResult = new GateResult(
            GateStatus.Ambiguous,
            [
                new GateRequirementResult(
                    RuntimeHarness.Definition.OutputGate.Requirements[0].Identity,
                    GateStatus.Ambiguous,
                    "Output evidence is contradictory.",
                    ["ambiguous-output.md"]),
            ],
            "Output evidence is contradictory.",
            ["ambiguous-output.md"]);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Ambiguous, result.Outcome);
        Assert.Equal(TransitionDurableState.Ambiguous, result.DurableState);
        Assert.Empty(harness.RunStore.Completions);
        TransitionWarningCapture warning = Assert.Single(harness.Warnings.Warnings);
        Assert.Equal(WarningCategory.Transition, warning.Category);
        TransitionRecoveryMarkerCapture recovery = Assert.Single(harness.RecoveryMarkers.Markers);
        Assert.Equal(TransitionDurableState.Ambiguous, recovery.DurableState);
        Assert.Equal(RuntimeOutcomeKind.Ambiguous, recovery.Outcome);
    }

    [Fact]
    public async Task Dual_shape_requirement_stops_as_missing_required_input_matching_evaluator_precedence()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        WorkflowTransitionDefinition definition = RuntimeHarness.Definition with
        {
            InputGate = RuntimeHarness.Definition.InputGate with
            {
                Requirements =
                [
                    new GateRequirementDefinition(
                        "InputGate.DualShape",
                        "Required input product that also declares an input surface.",
                        RuntimeHarness.InputProduct,
                        DependencyStrength.Required,
                        true,
                        ".agents/"),
                ],
            },
        };
        harness.Definitions.Definition = definition;
        harness.Gates.InputResult = new GateResult(
            GateStatus.Unsatisfied,
            [
                new GateRequirementResult(
                    "InputGate.DualShape",
                    GateStatus.Unsatisfied,
                    "Required input product 'InputProduct' is missing; produce it before rerunning.",
                    ["InputProduct"]),
            ],
            "Required input product 'InputProduct' is missing; produce it before rerunning.",
            ["InputProduct"]);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.MissingRequiredInput, result.Outcome);
        Assert.Equal(TransitionDurableState.InputUnsatisfied, result.DurableState);
        Assert.False(harness.Executor.WasCalled);
    }

    private static WorkflowTransitionDefinition WithCleanInputRequirement() =>
        RuntimeHarness.Definition with
        {
            InputGate = RuntimeHarness.Definition.InputGate with
            {
                Requirements =
                [
                    .. RuntimeHarness.Definition.InputGate.Requirements,
                    new GateRequirementDefinition(
                        "InputGate.CleanInput",
                        "Working tree under '.agents/' is committed input.",
                        null,
                        DependencyStrength.Required,
                        true,
                        ".agents/"),
                ],
            },
        };

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
    public async Task Prompt_context_unavailable_stops_before_prompt_execution()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.ContextBuilder.UnavailableException = new PromptContextUnavailableException(
            "Active Epic prompt context is missing.",
            [".agents/epic.md"]);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.MissingRequiredInput, result.Outcome);
        Assert.Equal(TransitionDurableState.InputUnsatisfied, result.DurableState);
        Assert.Equal("Active Epic prompt context is missing.", result.Explanation);
        Assert.Contains(".agents/epic.md", result.Evidence);
        Assert.False(harness.Executor.WasCalled);
        Assert.Empty(harness.RunStore.Started);
        Assert.Empty(harness.RunStore.Completions);
        TransitionWarningCapture warning = Assert.Single(harness.Warnings.Warnings);
        Assert.Equal(harness.Request.Workflow, warning.Request.Workflow);
        Assert.Equal(harness.Request.Stage, warning.Request.Stage);
        Assert.Equal(RuntimeHarness.Definition.Identity, warning.Transition);
        Assert.Equal(WarningCategory.Transition, warning.Category);
        Assert.Contains(".agents/epic.md", warning.Evidence);
        TransitionRecoveryMarkerCapture recovery = Assert.Single(harness.RecoveryMarkers.Markers);
        Assert.Equal(TransitionDurableState.InputUnsatisfied, recovery.DurableState);
        Assert.Equal(RuntimeHarness.Definition.Recovery, recovery.Recovery);
        Assert.Contains(".agents/epic.md", recovery.Evidence);
    }

    [Fact]
    public async Task Run_appends_a_usable_read_receipt_at_consumption()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.ContextBuilder.ConsumedFiles = [new ConsumedInputFile(".agents/epic.md", "abc123")];

        await harness.Runtime.RunAsync(harness.Request);

        ReadReceiptCapture receipt = Assert.Single(harness.ReadReceipts.Receipts);
        Assert.Equal("Usable", receipt.Validation);
        Assert.Equal(RuntimeHarness.Definition.Identity, receipt.Definition.Identity);
        Assert.StartsWith("tr_", receipt.TransitionRunId, StringComparison.Ordinal);
        Assert.NotNull(receipt.AttemptId);
        Assert.StartsWith("att_", receipt.AttemptId, StringComparison.Ordinal);
        ConsumedInputFile file = Assert.Single(receipt.ConsumedFiles);
        Assert.Equal(".agents/epic.md", file.Path);
        Assert.Equal("abc123", file.Sha256);
        Assert.All(receipt.ConsumedProducts, product =>
            Assert.Contains(
                RuntimeHarness.Definition.RequiredInputProducts,
                requirement => requirement.Product == product.Identity));
    }

    [Fact]
    public async Task Unavailable_prompt_context_appends_a_receipt_with_the_validation_failure()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.ContextBuilder.UnavailableException = new PromptContextUnavailableException(
            "Active Epic prompt context is empty.",
            [".agents/epic.md"],
            [new ConsumedInputFile(".agents/epic.md", "def456")]);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.MissingRequiredInput, result.Outcome);
        ReadReceiptCapture receipt = Assert.Single(harness.ReadReceipts.Receipts);
        Assert.Equal("Active Epic prompt context is empty.", receipt.Validation);
        // The prompt never executed, so no attempt row exists; the receipt must not
        // reference an attempt that was never durably recorded.
        Assert.Null(receipt.AttemptId);
        ConsumedInputFile file = Assert.Single(receipt.ConsumedFiles);
        Assert.Equal("def456", file.Sha256);
    }

    [Fact]
    public async Task Read_receipt_store_failure_does_not_fail_the_transition()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.ReadReceipts.ThrowOnAppend = true;

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.NotEqual(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Empty(harness.ReadReceipts.Receipts);
    }

    [Fact]
    public async Task Effect_execution_receives_the_causal_identity_context()
    {
        RuntimeHarness harness = RuntimeHarness.Create();

        await harness.Runtime.RunAsync(harness.Request);

        Assert.NotNull(harness.Effects.Context);
        Assert.StartsWith("tr_", harness.Effects.Context!.TransitionRunId, StringComparison.Ordinal);
        Assert.NotNull(harness.Effects.Context.AttemptId);
        Assert.StartsWith("att_", harness.Effects.Context.AttemptId, StringComparison.Ordinal);
        // The harness request carries no workspace run or instance; the context reflects that
        // honestly instead of inventing ids.
        Assert.Null(harness.Effects.Context.RunId);
        Assert.Null(harness.Effects.Context.WorkflowInstanceId);
        ReadReceiptCapture receipt = Assert.Single(harness.ReadReceipts.Receipts);
        Assert.Equal(receipt.TransitionRunId, harness.Effects.Context.TransitionRunId);
    }

    [Fact]
    public async Task Every_transition_run_projection_mutation_appends_a_lifecycle_evidence_event()
    {
        RuntimeHarness harness = RuntimeHarness.Create();

        await harness.Runtime.RunAsync(harness.Request);

        // The current-state transition-run row is rewritten through the lifecycle; each rewrite
        // must be backed by an appended fact so the row stays a projection over the ledger:
        // Started and Completed have named events, PromptCompleted is backed by the raw-output
        // append, EffectsApplied by per-effect records, and the two interpretation/validation
        // rewrites by their own named events.
        IReadOnlyList<string> eventNames = harness.Evidence.Events.Select(evidence => evidence.EventName).ToArray();
        Assert.Contains("TransitionStarted", eventNames);
        Assert.Contains("TransitionOutputInterpreted", eventNames);
        Assert.Contains("TransitionOutputValidated", eventNames);
        Assert.Contains("TransitionCompleted", eventNames);
        Assert.Single(harness.Evidence.RawOutputs);
        Assert.NotEmpty(harness.EffectRecords.Records);
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
    public async Task Attempt_is_recorded_with_parents_before_prompt_execution_and_completed_on_success()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        TransitionRuntimeRequest request = harness.Request with
        {
            Run = new RunIdentity("run_parent"),
            WorkflowInstance = new WorkflowInstanceIdentity("wfi_parent"),
        };

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(request);

        Assert.Equal(RuntimeOutcomeKind.Completed, result.Outcome);
        TransitionRunStarted started = Assert.Single(harness.RunStore.Started);
        Assert.StartsWith("tr_", started.RunId, StringComparison.Ordinal);
        AttemptRecord attempt = Assert.Single(harness.Attempts.Started);
        Assert.StartsWith("att_", attempt.AttemptId, StringComparison.Ordinal);
        Assert.Equal(started.RunId, attempt.TransitionRunId);
        Assert.Equal("run_parent", attempt.RunId);
        Assert.Equal("wfi_parent", attempt.WorkflowInstanceId);
        Assert.Equal(1, attempt.AttemptIndex);
        Assert.Null(attempt.CompletedAt);
        Assert.Equal("run_parent", started.WorkspaceRunId);
        Assert.Equal("wfi_parent", started.WorkflowInstanceId);
        Assert.Equal(attempt.AttemptId, started.AttemptId);
        (string completedAttemptId, DateTimeOffset _, string outcome) = Assert.Single(harness.Attempts.Completions);
        Assert.Equal(attempt.AttemptId, completedAttemptId);
        Assert.Equal("Completed", outcome);
        Assert.NotNull(harness.Executor.Context);
        Assert.Equal("run_parent", harness.Executor.Context!.RunId);
        Assert.Equal("wfi_parent", harness.Executor.Context.WorkflowInstanceId);
        Assert.Equal(started.RunId, harness.Executor.Context.TransitionRunId);
        Assert.Equal(attempt.AttemptId, harness.Executor.Context.AttemptId);
    }

    [Fact]
    public async Task Attempt_parents_fall_back_to_empty_strings_without_run_context()
    {
        RuntimeHarness harness = RuntimeHarness.Create();

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Completed, result.Outcome);
        AttemptRecord attempt = Assert.Single(harness.Attempts.Started);
        Assert.Equal(string.Empty, attempt.RunId);
        Assert.Equal(string.Empty, attempt.WorkflowInstanceId);
        Assert.Null(harness.RunStore.Started.Single().WorkspaceRunId);
        Assert.Null(harness.Executor.Context!.RunId);
        Assert.Null(harness.Executor.Context.WorkflowInstanceId);
    }

    [Fact]
    public async Task Executor_receives_the_consumed_input_manifest_for_send_site_fact_minting()
    {
        // Rendered-prompt facts are minted where the text is actually SENT to an agent (the
        // executor's send helpers), never at the render seam — a canonical render that an
        // executor branch discards and re-composes must not be recorded as an agent-bound
        // prompt. The runtime's job is to hand the executor the consumed-input manifest.
        RuntimeHarness harness = RuntimeHarness.Create("pol_v1_0123456789abcdef0123456789abcdef");
        harness.ContextBuilder.ConsumedFiles =
        [
            new ConsumedInputFile(".agents/epic.md", "hash-epic"),
        ];

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Completed, result.Outcome);
        Assert.NotNull(harness.Executor.Context);
        ConsumedInputFile consumed = Assert.Single(harness.Executor.Context!.ConsumedFiles ?? []);
        Assert.Equal(".agents/epic.md", consumed.Path);
        Assert.Equal("hash-epic", consumed.Sha256);
    }

    [Fact]
    public async Task Attempt_records_the_runtimes_resolved_policy_identity()
    {
        RuntimeHarness harness = RuntimeHarness.Create("pol_v1_0123456789abcdef0123456789abcdef");

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Completed, result.Outcome);
        AttemptRecord attempt = Assert.Single(harness.Attempts.Started);
        Assert.Equal("pol_v1_0123456789abcdef0123456789abcdef", attempt.PolicyId);
    }

    [Fact]
    public async Task Attempt_policy_identity_is_null_only_when_explicitly_constructed_without_one()
    {
        // The ctor parameter is required, so a policy-less runtime is a visible decision at the
        // construction site; the attempt then honestly records no policy identity.
        RuntimeHarness harness = RuntimeHarness.Create();

        await harness.Runtime.RunAsync(harness.Request);

        AttemptRecord attempt = Assert.Single(harness.Attempts.Started);
        Assert.Null(attempt.PolicyId);
    }

    [Fact]
    public async Task Unsatisfied_input_records_no_attempt()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.Products.Result = new ProductResolutionResult([], [RuntimeHarness.InputRequirement], [], [], []);

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.MissingRequiredInput, result.Outcome);
        Assert.False(harness.Executor.WasCalled);
        Assert.Empty(harness.Attempts.Started);
        Assert.Empty(harness.Attempts.Completions);
    }

    [Fact]
    public async Task Cancelled_prompt_records_attempt_completion_as_cancelled()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.Executor.ThrowCancellation = true;

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request);

        Assert.Equal(RuntimeOutcomeKind.Cancelled, result.Outcome);
        AttemptRecord attempt = Assert.Single(harness.Attempts.Started);
        (string completedAttemptId, DateTimeOffset _, string outcome) = Assert.Single(harness.Attempts.Completions);
        Assert.Equal(attempt.AttemptId, completedAttemptId);
        Assert.Equal("Cancelled", outcome);
    }

    [Fact]
    public async Task In_band_cancelled_prompt_records_attempt_completion_despite_fired_token()
    {
        RuntimeHarness harness = RuntimeHarness.Create();
        harness.Executor.Result = new PromptExecutionResult(
            PromptExecutionStatus.Cancelled,
            "partial output",
            TimeSpan.FromMilliseconds(10),
            new Dictionary<string, string>());
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        TransitionRuntimeResult result = await harness.Runtime.RunAsync(harness.Request, cancellation.Token);

        Assert.Equal(RuntimeOutcomeKind.Cancelled, result.Outcome);
        Assert.Equal(TransitionDurableState.Cancelled, result.DurableState);
        (string _, DateTimeOffset _, string outcome) = Assert.Single(harness.Attempts.Completions);
        Assert.Equal("Cancelled", outcome);
    }

    [Fact]
    public async Task Failed_prompt_records_attempt_completion_as_failed()
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
        AttemptRecord attempt = Assert.Single(harness.Attempts.Started);
        (string completedAttemptId, DateTimeOffset _, string outcome) = Assert.Single(harness.Attempts.Completions);
        Assert.Equal(attempt.AttemptId, completedAttemptId);
        Assert.Equal("Failed", outcome);
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

        private RuntimeHarness(string? policyIdentity = null)
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
            Warnings = new RecordingWarningStore();
            RecoveryMarkers = new RecordingRecoveryStore();
            GateEvaluations = new RecordingGateEvaluationStore();
            EffectRecords = new RecordingEffectStore();
            Attempts = new RecordingAttemptStore();
            ReadReceipts = new RecordingReadReceiptStore();
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
                policyIdentity,
                Warnings,
                RecoveryMarkers,
                GateEvaluations,
                EffectRecords,
                Attempts,
                ReadReceipts);
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

        public RecordingReadReceiptStore ReadReceipts { get; }

        public FakePromptRenderer Renderer { get; }

        public FakePromptExecutor Executor { get; }

        public FakeOutputInterpreter Interpreter { get; }

        public FakeProductValidator Validator { get; }

        public FakeEffectExecutor Effects { get; }

        public RecordingRunStore RunStore { get; }

        public RecordingEvidenceStore Evidence { get; }

        public RecordingWarningStore Warnings { get; }

        public RecordingRecoveryStore RecoveryMarkers { get; }

        public RecordingGateEvaluationStore GateEvaluations { get; }

        public RecordingEffectStore EffectRecords { get; }

        public RecordingAttemptStore Attempts { get; }

        public static RuntimeHarness Create(string? policyIdentity = null) => new(policyIdentity);

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
        public WorkflowTransitionDefinition Definition { get; set; } = RuntimeHarness.Definition;

        public Task<WorkflowTransitionDefinition> ResolveAsync(
            TransitionRuntimeRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(Definition);

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
        public GateResult? InputResult { get; set; }

        public GateResult? OutputResult { get; set; }

        public Task<GateResult> EvaluateInputGateAsync(
            GateDefinition gate,
            ProductResolutionResult inputs,
            CancellationToken cancellationToken)
        {
            if (InputResult is not null)
            {
                return Task.FromResult(InputResult);
            }

            GateStatus status = inputs.IsUsable ? GateStatus.Satisfied : GateStatus.Unsatisfied;
            return Task.FromResult(new GateResult(
                status,
                [new GateRequirementResult(gate.Requirements[0].Identity, status, status.ToString(), [])],
                status == GateStatus.Satisfied ? "Input gate satisfied." : "Input gate unsatisfied.",
                status == GateStatus.Satisfied ? [] : ["input-gate.md"]));
        }

        public Task<GateResult> EvaluateOutputGateAsync(
            GateDefinition gate,
            ProductValidationResult validation,
            CancellationToken cancellationToken)
        {
            if (OutputResult is not null)
            {
                return Task.FromResult(OutputResult);
            }

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
        public PromptContextUnavailableException? UnavailableException { get; set; }

        public IReadOnlyList<ConsumedInputFile>? ConsumedFiles { get; set; }

        public Task<PromptContext> BuildAsync(
            TransitionRuntimeRequest request,
            WorkflowTransitionDefinition definition,
            ProductResolutionResult inputs,
            CancellationToken cancellationToken)
        {
            if (UnavailableException is not null)
            {
                throw UnavailableException;
            }

            IReadOnlyDictionary<string, string> metadata =
                request.Metadata ?? new Dictionary<string, string>();
            return Task.FromResult(new PromptContext(
                definition,
                inputs,
                TransitionInputSnapshotHasher.Create(definition, inputs.Products, metadata),
                metadata,
                [],
                ConsumedFiles));
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

        public PromptExecutionContext? Context { get; private set; }

        public PromptExecutionResult Result { get; set; } =
            new(PromptExecutionStatus.Completed, "raw output", TimeSpan.FromMilliseconds(5), new Dictionary<string, string>());

        public Task<PromptExecutionResult> ExecuteAsync(
            WorkflowTransitionDefinition definition,
            RenderedPrompt prompt,
            PromptExecutionContext context,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            Context = context;
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

        public EffectExecutionContext? Context { get; private set; }

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
            EffectExecutionContext context,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            Context = context;
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

    private sealed class RecordingReadReceiptStore : IReadReceiptStore
    {
        public List<ReadReceiptCapture> Receipts { get; } = [];

        public bool ThrowOnAppend { get; set; }

        public Task AppendAsync(ReadReceiptCapture capture, CancellationToken cancellationToken)
        {
            if (ThrowOnAppend)
            {
                throw new InvalidOperationException("read receipt store unavailable");
            }

            Receipts.Add(capture);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingWarningStore : ITransitionWarningStore
    {
        public List<TransitionWarningCapture> Warnings { get; } = [];

        public Task RecordWarningAsync(
            TransitionWarningCapture warning,
            CancellationToken cancellationToken)
        {
            Warnings.Add(warning);
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

    private sealed class RecordingAttemptStore : IAttemptStore
    {
        public List<AttemptRecord> Started { get; } = [];

        public List<(string AttemptId, DateTimeOffset CompletedAt, string Outcome)> Completions { get; } = [];

        public Task PersistAttemptStartedAsync(AttemptRecord attempt, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Started.Add(attempt);
            return Task.CompletedTask;
        }

        public Task PersistAttemptCompletedAsync(
            string attemptId,
            DateTimeOffset completedAt,
            string outcome,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Completions.Add((attemptId, completedAt, outcome));
            return Task.CompletedTask;
        }
    }
}
