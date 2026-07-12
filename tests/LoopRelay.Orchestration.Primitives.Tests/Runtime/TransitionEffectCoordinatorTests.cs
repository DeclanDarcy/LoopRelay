using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Runtime;

public sealed class TransitionEffectCoordinatorTests
{
    [Fact]
    public async Task Executes_durable_intents_in_order_and_records_completion()
    {
        var executor = new RecordingExecutor();
        var states = new RecordingStates();
        var coordinator = new TransitionEffectCoordinator(executor, states);

        TransitionEffectCoordinationResult result = await coordinator.CoordinateAsync(
            Attempt("first", "second"), Context(), CancellationToken.None);

        Assert.False(result.RequiredEffectsPending);
        Assert.False(result.Failed);
        Assert.Equal(["first", "second"], executor.Effects);
        Assert.Equal(
            [EffectExecutionStatus.Started, EffectExecutionStatus.Succeeded,
             EffectExecutionStatus.Started, EffectExecutionStatus.Succeeded],
            states.Statuses);
    }

    [Fact]
    public async Task Thrown_external_call_is_unknown_and_requires_reconciliation()
    {
        var executor = new RecordingExecutor { Exception = new IOException("lost response") };
        var states = new RecordingStates();
        var coordinator = new TransitionEffectCoordinator(executor, states);

        TransitionEffectCoordinationResult result = await coordinator.CoordinateAsync(
            Attempt("publish"), Context(), CancellationToken.None);

        Assert.True(result.RequiredEffectsPending);
        Assert.False(result.Failed);
        Assert.Equal(EffectExecutionStatus.Unknown, states.Statuses[^1]);
    }

    [Fact]
    public async Task Normalized_effect_failure_stops_later_effects()
    {
        var executor = new RecordingExecutor { ResultStatus = EffectExecutionStatus.Failed };
        var coordinator = new TransitionEffectCoordinator(executor, new RecordingStates());

        TransitionEffectCoordinationResult result = await coordinator.CoordinateAsync(
            Attempt("first", "second"), Context(), CancellationToken.None);

        Assert.True(result.Failed);
        Assert.Equal(["first"], executor.Effects);
    }

    private static CanonicalTransitionExecutionContext Context() => new(
        new WorkflowInvocation(InvocationModeKind.BoundedPlan),
        WorkspaceIdentity.New(),
        RunIdentity.New(),
        WorkflowInstanceIdentity.New(),
        new PolicyIdentity("policy"),
        new RuntimeProfileIdentity("runtime"),
        new PromptPolicyProfileIdentity("prompt-policy"));

    private static TransitionRuntimeResult Attempt(params string[] effects) => new(
        RuntimeOutcomeKind.EffectsPending,
        TransitionDurableState.EffectsPending,
        new WorkflowTransitionIdentity("transition"),
        null, null, null,
        new EffectExecutionResult(
            EffectExecutionStatus.Planned,
            effects.Select(effect => new EffectExecutionRecord(
                new EffectIdentity(effect), EffectExecutionStatus.Planned, "planned", [])).ToArray(),
            "planned",
            []),
        [],
        "effects pending",
        [],
        TransitionRunIdentity.New(),
        AttemptIdentity.New(),
        AttemptCompleted: true,
        RequiredEffectsPending: true);

    private sealed class RecordingExecutor : ITransitionEffectIntentExecutor
    {
        public List<string> Effects { get; } = [];
        public Exception? Exception { get; set; }
        public EffectExecutionStatus ResultStatus { get; set; } = EffectExecutionStatus.Succeeded;

        public Task<EffectExecutionRecord> ExecuteAsync(
            CanonicalCausalContext causality,
            EffectIdentity effect,
            CancellationToken cancellationToken)
        {
            Effects.Add(effect.Value);
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(new EffectExecutionRecord(effect, ResultStatus, ResultStatus.ToString(), []));
        }
    }

    private sealed class RecordingStates : ITransitionEffectIntentStateStore
    {
        public List<EffectExecutionStatus> Statuses { get; } = [];

        public Task RecordStateAsync(
            TransitionRunIdentity transitionRun,
            EffectIdentity effect,
            EffectExecutionStatus status,
            string? failure,
            CancellationToken cancellationToken)
        {
            Statuses.Add(status);
            return Task.CompletedTask;
        }
    }
}
