using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Persistence;

public sealed class CanonicalTransitionPersistenceStoresTests
{
    [Fact]
    public async Task Transition_run_store_persists_started_state_updates_and_completion()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var runStore = new CanonicalTransitionRunStore(persistence);
        WorkflowTransitionDefinition transition = PlanTransition("WriteExecutablePlan");
        var stage = new WorkflowStageIdentity("Planning");
        DateTimeOffset startedAt = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);
        var started = new TransitionRunStarted(
            "run-adapter-001",
            startedAt,
            new TransitionRuntimeRequest(
                WorkflowIdentity.Plan,
                stage,
                transition.Identity,
                new Dictionary<string, string> { ["purpose"] = "adapter-test" }),
            transition,
            new TransitionInputSnapshot("input-hash", [], new Dictionary<string, string> { ["purpose"] = "adapter-test" }, []),
            new RenderedPrompt(transition.PromptIdentity, "prompt text", "prompts/plan.md"));

        await runStore.PersistStartedAsync(started, CancellationToken.None);
        await runStore.PersistStateAsync(
            new TransitionRunStateUpdate(
                started.RunId,
                startedAt.AddSeconds(10),
                transition.Identity,
                TransitionDurableState.OutputValidated,
                "Output validated.",
                ["validation.md"]),
            CancellationToken.None);
        await runStore.PersistCompletedAsync(
            new TransitionRunCompleted(
                started.RunId,
                startedAt.AddSeconds(20),
                transition.Identity,
                new TransitionRuntimeResult(
                    RuntimeOutcomeKind.Completed,
                    TransitionDurableState.Completed,
                    transition.Identity,
                    null,
                    null,
                    null,
                    null,
                    [],
                    "Transition completed.",
                    ["completed.md"])),
            CancellationToken.None);

        CanonicalWorkflowPersistenceSnapshot snapshot = await persistence.LoadSnapshotAsync();
        CanonicalTransitionRunRecord run = Assert.Single(snapshot.TransitionRuns);
        Assert.Equal(started.RunId, run.RunId);
        Assert.Equal(WorkflowIdentity.Plan, run.Workflow);
        Assert.Equal(stage, run.Stage);
        Assert.Equal(transition.Identity, run.Transition);
        Assert.Equal(TransitionDurableState.Completed, run.State);
        Assert.Equal(RuntimeOutcomeKind.Completed, run.Outcome);
        Assert.Equal(startedAt, run.StartedAt);
        Assert.Equal(startedAt.AddSeconds(20), run.CompletedAt);
        Assert.Equal("input-hash", run.InputSnapshotHash);
        Assert.Equal("Transition completed.", run.Explanation);
        Assert.Equal(["completed.md"], run.Evidence);
    }

    [Fact]
    public async Task Transition_evidence_store_appends_structured_events_raw_output_and_failures()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var evidenceStore = new CanonicalTransitionEvidenceStore(persistence);
        WorkflowTransitionIdentity transition = PlanTransition("WriteExecutablePlan").Identity;
        DateTimeOffset recordedAt = new(2026, 7, 10, 12, 1, 0, TimeSpan.Zero);

        await evidenceStore.RecordEventAsync(
            new TransitionEvidenceEvent(
                "run-evidence-001",
                recordedAt,
                transition,
                TransitionDurableState.OutputValidated,
                "OutputValidated",
                "Output validated.",
                ["validation.md"]),
            CancellationToken.None);
        await evidenceStore.RecordRawOutputAsync(
            "run-evidence-001",
            transition,
            new PromptExecutionResult(
                PromptExecutionStatus.Completed,
                "raw prompt output",
                TimeSpan.FromMilliseconds(25),
                new Dictionary<string, string> { ["model"] = "test-model" }),
            CancellationToken.None);
        await evidenceStore.RecordFailureAsync(
            "run-evidence-001",
            transition,
            "validation failed",
            CancellationToken.None);

        CanonicalWorkflowPersistenceSnapshot snapshot = await persistence.LoadSnapshotAsync();
        Assert.Equal(
            ["OutputValidated", "RawPromptOutputCaptured", "TransitionFailure"],
            snapshot.TransitionEvidence.Select(evidence => evidence.EventName).ToArray());
        Assert.Equal(TransitionDurableState.OutputValidated, snapshot.TransitionEvidence[0].State);
        Assert.Equal(TransitionDurableState.PromptCompleted, snapshot.TransitionEvidence[1].State);
        Assert.Equal(TransitionDurableState.Failed, snapshot.TransitionEvidence[2].State);
        Assert.Contains("raw prompt output", snapshot.TransitionEvidence[1].DocumentJson, StringComparison.Ordinal);
        Assert.Contains("validation failed", snapshot.TransitionEvidence[2].DocumentJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Transition_blocker_store_persists_recoverable_canonical_blocker()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var blockerStore = new CanonicalTransitionBlockerStore(persistence);
        WorkflowTransitionIdentity transition = PlanTransition("WriteExecutablePlan").Identity;
        var stage = new WorkflowStageIdentity("Planning");
        DateTimeOffset recordedAt = new(2026, 7, 10, 12, 2, 0, TimeSpan.Zero);

        await blockerStore.RecordBlockerAsync(
            new TransitionBlockerCapture(
                "run-blocker-001",
                recordedAt,
                new TransitionRuntimeRequest(WorkflowIdentity.Plan, stage, transition),
                transition,
                BlockerCategory.Validation,
                "Input gate blocked.",
                "Provide required products.",
                Recoverable: true,
                ["input-gate.md"]),
            CancellationToken.None);

        CanonicalWorkflowPersistenceSnapshot snapshot = await persistence.LoadSnapshotAsync();
        CanonicalBlockerRecord blocker = Assert.Single(snapshot.Blockers);
        Assert.Equal("run-blocker-001:WriteExecutablePlan", blocker.BlockerId);
        Assert.Equal(WorkflowIdentity.Plan, blocker.Workflow);
        Assert.Equal(stage, blocker.Stage);
        Assert.Equal(transition, blocker.Transition);
        Assert.Equal(BlockerCategory.Validation, blocker.Blocker.Category);
        Assert.Equal("canonical transition runtime", blocker.Blocker.Authority);
        Assert.Equal(["input-gate.md"], blocker.Blocker.Evidence);
        Assert.True(blocker.Blocker.Recoverable);
        Assert.Null(blocker.ResolvedAt);
    }

    [Fact]
    public async Task Transition_recovery_store_persists_canonical_recovery_marker()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var recoveryStore = new CanonicalTransitionRecoveryStore(persistence);
        WorkflowTransitionIdentity transition = PlanTransition("WriteExecutablePlan").Identity;
        var stage = new WorkflowStageIdentity("Planning");
        DateTimeOffset recordedAt = new(2026, 7, 10, 12, 3, 0, TimeSpan.Zero);

        await recoveryStore.RecordRecoveryMarkerAsync(
            new TransitionRecoveryMarkerCapture(
                "run-recovery-001",
                recordedAt,
                new TransitionRuntimeRequest(WorkflowIdentity.Plan, stage, transition),
                transition,
                TransitionDurableState.Failed,
                RuntimeOutcomeKind.Failed,
                new RecoveryDefinition(
                    "PlanTransitionRecovery",
                    "Recover by rerunning after inspecting failed transition evidence.",
                    ["rerun", "repair-inputs"],
                    ["silent repair"]),
                "Transition failed.",
                ["failure.md"]),
            CancellationToken.None);

        CanonicalWorkflowPersistenceSnapshot snapshot = await persistence.LoadSnapshotAsync();
        CanonicalRecoveryMarkerRecord marker = Assert.Single(snapshot.RecoveryMarkers);
        Assert.Equal("run-recovery-001:WriteExecutablePlan:Failed", marker.MarkerId);
        Assert.Equal(WorkflowIdentity.Plan, marker.Workflow);
        Assert.Equal(stage, marker.Stage);
        Assert.Equal(transition, marker.Transition);
        Assert.Equal(marker.MarkerId, marker.Recovery.Identity);
        Assert.Equal("Recover by rerunning after inspecting failed transition evidence.", marker.Recovery.Semantics);
        Assert.Equal(["rerun", "repair-inputs"], marker.Recovery.SupportedActions);
        Assert.Equal(["silent repair"], marker.Recovery.UnsupportedActions);
        Assert.Equal(["failure.md"], marker.Evidence);
        Assert.Equal(recordedAt, marker.RecordedAt);
    }

    [Fact]
    public async Task Transition_gate_evaluation_store_persists_canonical_gate_evaluation()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var gateStore = new CanonicalTransitionGateEvaluationStore(persistence);
        WorkflowTransitionDefinition transition = PlanTransition("WriteExecutablePlan");
        var stage = new WorkflowStageIdentity("Planning");
        DateTimeOffset evaluatedAt = new(2026, 7, 10, 12, 4, 0, TimeSpan.Zero);
        var result = new GateResult(
            GateStatus.Blocked,
            [
                new GateRequirementResult(
                    "plan-input",
                    GateStatus.Blocked,
                    "Plan input is missing.",
                    ["input.md"]),
            ],
            "Input gate blocked.",
            ["input.md"]);

        await gateStore.RecordGateEvaluationAsync(
            new TransitionGateEvaluationCapture(
                "run-gate-001",
                evaluatedAt,
                new TransitionRuntimeRequest(WorkflowIdentity.Plan, stage, transition.Identity),
                transition.Identity,
                transition.InputGate,
                result),
            CancellationToken.None);

        CanonicalWorkflowPersistenceSnapshot snapshot = await persistence.LoadSnapshotAsync();
        CanonicalGateEvaluationRecord evaluation = Assert.Single(snapshot.GateEvaluations);
        Assert.Equal(WorkflowIdentity.Plan, evaluation.Workflow);
        Assert.Equal(stage, evaluation.Stage);
        Assert.Equal(transition.Identity, evaluation.Transition);
        Assert.Equal(transition.InputGate.Identity, evaluation.Gate);
        Assert.Equal(GateStatus.Blocked, evaluation.Status);
        Assert.Equal(evaluatedAt, evaluation.EvaluatedAt);
        GateRequirementResult requirement = Assert.Single(evaluation.Requirements);
        Assert.Equal("plan-input", requirement.RequirementIdentity);
        Assert.Equal("Input gate blocked.", evaluation.Explanation);
        Assert.Equal(["input.md"], evaluation.Evidence);
    }

    [Fact]
    public async Task Transition_effect_store_persists_canonical_effect_record()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var effectStore = new CanonicalTransitionEffectStore(persistence);
        WorkflowTransitionIdentity transition = PlanTransition("WriteExecutablePlan").Identity;
        var stage = new WorkflowStageIdentity("Planning");
        DateTimeOffset recordedAt = new(2026, 7, 10, 12, 5, 0, TimeSpan.Zero);

        await effectStore.RecordEffectAsync(
            new TransitionEffectRecordCapture(
                "run-effect-001",
                recordedAt,
                new TransitionRuntimeRequest(WorkflowIdentity.Plan, stage, transition),
                transition,
                new EffectIdentity("persist-plan"),
                EffectCategory.ProductPersistence,
                EffectExecutionStatus.Succeeded,
                "Plan persisted.",
                ["effect.md"]),
            CancellationToken.None);

        CanonicalWorkflowPersistenceSnapshot snapshot = await persistence.LoadSnapshotAsync();
        CanonicalEffectRecord effect = Assert.Single(snapshot.EffectRecords);
        Assert.Equal("run-effect-001", effect.RunId);
        Assert.Equal(new EffectIdentity("persist-plan"), effect.Effect);
        Assert.Equal(EffectCategory.ProductPersistence, effect.Category);
        Assert.Equal(EffectExecutionStatus.Succeeded, effect.Status);
        Assert.Equal(recordedAt, effect.RecordedAt);
        Assert.Equal("Plan persisted.", effect.Explanation);
        Assert.Equal(["effect.md"], effect.Evidence);
    }

    private static WorkflowTransitionDefinition PlanTransition(string identity) =>
        CanonicalWorkflowDefinitionSketches.CreatePlan()
            .Transitions
            .Single(transition => transition.Identity.Value == identity);

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-canonical-transition-").FullName;
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
    }
}
