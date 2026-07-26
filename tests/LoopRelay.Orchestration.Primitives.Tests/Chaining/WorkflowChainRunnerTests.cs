using System.Collections;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Chaining;

public sealed class WorkflowChainRunnerTests
{
    private static readonly IReadOnlyList<WorkflowDefinition> Definitions =
        CanonicalWorkflowCatalog.CreateAll();

    private static WorkflowChainDefinition TraditionalRoadmapChain =>
        CanonicalWorkflowCatalog.CreateChains()
            .Single(chain => chain.InitialWorkflow == WorkflowIdentity.TraditionalRoadmap);

    [Fact]
    public void Canonical_chain_definitions_cover_the_product_driven_route()
    {
        Assert.Equal(
            [WorkflowIdentity.TraditionalRoadmap, WorkflowIdentity.Plan, WorkflowIdentity.Execute],
            TraditionalRoadmapChain.Workflows.Select(workflow => workflow.Identity));
    }

    [Fact]
    public async Task Progression_transfers_promoted_product_identities_and_preserves_root_run()
    {
        RepositoryObservation observation = Observation(
            [Completed(WorkflowIdentity.TraditionalRoadmap)],
            [
                Product(ProductIdentity.PreparedEpic, WorkflowIdentity.TraditionalRoadmap),
                Product(ProductIdentity.MilestoneSpecificationSet, WorkflowIdentity.TraditionalRoadmap),
            ]);
        Harness harness = new(observation);
        WorkflowRunContext context = NewContext();

        WorkflowChainRunResult result = await harness.Runner.RunAsync(new WorkflowChainRunRequest(
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            observation,
            TraditionalRoadmapChain,
            Definitions,
            context,
            FreshAttemptAuthorization.Instance));

        WorkflowBoundaryEvaluation boundary = Assert.Single(result.Boundaries);
        Assert.True(boundary.CanAdvance);
        Assert.Equal(WorkflowIdentity.Plan, boundary.TargetWorkflow);
        Assert.Contains(ProductIdentity.PreparedEpic, result.Decision.ProductTransferManifest);
        Assert.Equal(context.Run, result.Decision.Run);
        Assert.Equal(context.Run, Assert.Single(harness.Instances.Begun).Run);
        Assert.Equal(context.Run, Assert.Single(harness.Boundaries.Captures).Run);
        Assert.Equal(context.Run, Assert.IsType<CanonicalTransitionExecutionContext>(
            Assert.Single(harness.Runtime.Requests).ExecutionContext).Run);
    }

    [Fact]
    public async Task Unsatisfied_successor_gate_stops_with_specific_reason_and_no_runtime_attempt()
    {
        RepositoryObservation observation = Observation(
            [Completed(WorkflowIdentity.TraditionalRoadmap)],
            []);
        Harness harness = new(observation);

        WorkflowChainRunResult result = await harness.Runner.RunAsync(new WorkflowChainRunRequest(
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            observation,
            TraditionalRoadmapChain,
            Definitions,
            NewContext(),
            FreshAttemptAuthorization.Instance));

        Assert.Equal(WorkflowStopReason.MissingRequiredInput, result.StopReason);
        Assert.False(Assert.Single(result.Boundaries).CanAdvance);
        Assert.Empty(harness.Runtime.Requests);
        Assert.Empty(harness.Instances.Begun);
    }

    [Fact]
    public async Task Required_effects_pending_prevents_chain_progression_without_redundant_observation()
    {
        RepositoryObservation observation = Observation();
        Harness harness = new(observation);
        harness.Runtime.Result = RuntimeResult(
            RuntimeOutcomeKind.EffectsPending,
            TransitionDurableState.EffectsPending,
            effectsPending: true);
        harness.Effects.Result = new TransitionEffectCoordinationResult(
            RequiredEffectsPending: true,
            Failed: false,
            "push pending",
            ["effect:push"]);

        WorkflowChainRunResult result = await harness.Runner.RunAsync(new WorkflowChainRunRequest(
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            observation,
            TraditionalRoadmapChain,
            Definitions,
            NewContext(),
            FreshAttemptAuthorization.Instance));

        Assert.Equal(WorkflowStopReason.RequiredEffectsPending, result.StopReason);
        Assert.True(result.Decision.RequiredEffectsPending);
        Assert.Equal(1, harness.Effects.CallCount);
        // The controller no longer performs a post-attempt observation solely to populate a
        // consumerless field, and this path never reaches the chain runner's own boundary-crossing
        // re-observation (that only fires when advancing past a *completed* workflow). Zero
        // observation calls is the correct, exact count here.
        Assert.Equal(0, harness.Observations.CallCount);
    }

    [Fact]
    public async Task Boundary_evidence_failure_fails_closed()
    {
        RepositoryObservation observation = Observation(
            [Completed(WorkflowIdentity.TraditionalRoadmap)],
            [
                Product(ProductIdentity.PreparedEpic, WorkflowIdentity.TraditionalRoadmap),
                Product(ProductIdentity.MilestoneSpecificationSet, WorkflowIdentity.TraditionalRoadmap),
            ]);
        Harness harness = new(observation);
        harness.Boundaries.FailAppend = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Runner.RunAsync(
            new WorkflowChainRunRequest(
                new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
                observation,
                TraditionalRoadmapChain,
                Definitions,
                NewContext(),
                FreshAttemptAuthorization.Instance)));
    }

    [Fact]
    public async Task Kernel_owns_authorization_reobservation_decision_facts_and_passive_budget_exhaustion()
    {
        RepositoryObservation observation = Observation();
        Harness harness = new(observation);
        var decisions = new RecordingKernelDecisionStore();
        var kernel = new OrchestrationKernel(harness.Runner, harness.Observations,
            new DurableKernelAttemptAuthorizationSelector(), decisions);
        WorkflowRunContext context = NewContext();

        KernelResult result = await kernel.RunAsync(new KernelCommand(
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain), observation,
            TraditionalRoadmapChain, CanonicalWorkflowCatalog.Current, context, 1));

        Assert.Equal(WorkflowStopReason.Waiting, result.StopReason);
        Assert.Equal(RuntimeOutcomeKind.Waiting, result.Outcome);
        Assert.Equal(context.Run, result.RootRun);
        Assert.Equal(64, result.SnapshotIdentity.Length);
        Assert.IsType<FreshAttemptAuthorization>(Assert.Single(harness.Runtime.Requests).Authorization);
        KernelDecisionFact decision = Assert.Single(decisions.Decisions);
        Assert.Equal(context.Run, decision.RootRun);
        Assert.Equal(CanonicalWorkflowCatalog.Current.Identity, decision.CatalogIdentity);
        // Exactly one observation for this single completed-attempt cycle: the kernel's own
        // re-observation at the cycle boundary (OrchestrationKernel.RunAsync). The controller's
        // former post-attempt observation, which only fed the consumerless ObservationAfter
        // field, is gone.
        Assert.Equal(1, harness.Observations.CallCount);
    }

    [Fact]
    public async Task Kernel_multi_cycle_run_sequences_stop_reasons_and_drops_redundant_post_attempt_observation()
    {
        RepositoryObservation observation = Observation();
        Harness harness = new(observation);
        // Cycle 0: attempt completes outright -> kernel continues to the next cycle.
        harness.Runtime.Sequence.Enqueue(RuntimeResult());
        // Cycle 1: attempt requires effect coordination that remains pending -> kernel stops.
        harness.Runtime.Sequence.Enqueue(RuntimeResult(
            RuntimeOutcomeKind.EffectsPending,
            TransitionDurableState.EffectsPending,
            effectsPending: true));
        harness.Effects.Result = new TransitionEffectCoordinationResult(
            RequiredEffectsPending: true,
            Failed: false,
            "push pending",
            ["effect:push"]);
        var decisions = new RecordingKernelDecisionStore();
        var kernel = new OrchestrationKernel(harness.Runner, harness.Observations,
            new DurableKernelAttemptAuthorizationSelector(), decisions);
        WorkflowRunContext context = NewContext();

        KernelResult result = await kernel.RunAsync(new KernelCommand(
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain), observation,
            TraditionalRoadmapChain, CanonicalWorkflowCatalog.Current, context, ObservationBudget: 5));

        // Scripted sequence of stop reasons across the two real cycles, recorded in kernel
        // decision facts (one per cycle actually executed, before the loop decides to stop).
        Assert.Equal(
            [WorkflowStopReason.TransitionCompleted, WorkflowStopReason.RequiredEffectsPending],
            decisions.Decisions.Select(decision => decision.Outcome));
        Assert.Equal(WorkflowStopReason.RequiredEffectsPending, result.StopReason);
        Assert.Equal(RuntimeOutcomeKind.EffectsPending, result.Outcome);
        // Both scripted attempts actually ran (the generous budget of 5 proves the loop stopped
        // because of the second attempt's outcome, not because the budget was exhausted).
        Assert.Equal(2, harness.Runtime.Requests.Count);
        // Exactly one observation total: the kernel's cycle-boundary re-observation after cycle 0
        // (StopReason == TransitionCompleted). The per-attempt controller observation that used to
        // fire on *both* cycles is gone, so the count drops by exactly one per attempt (2 -> 0)
        // while the load-bearing kernel-boundary observation (0 -> 1 in this scenario) is untouched.
        Assert.Equal(1, harness.Observations.CallCount);
    }

    [Fact]
    public async Task Kernel_decision_facts_report_the_controller_eligible_and_rejected_alternatives()
    {
        RepositoryObservation observation = Observation();
        Harness harness = new(observation);
        var decisions = new RecordingKernelDecisionStore();
        var kernel = new OrchestrationKernel(harness.Runner, harness.Observations,
            new DurableKernelAttemptAuthorizationSelector(), decisions);

        await kernel.RunAsync(new KernelCommand(
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain), observation,
            TraditionalRoadmapChain, CanonicalWorkflowCatalog.Current, NewContext(), 1));

        // The first TraditionalRoadmap stage ("Roadmap Context") offers two transitions:
        // BootstrapRoadmapCompletionContext requires no input products, while
        // UpdateRoadmapCompletionContext requires the roadmap completion context this observation
        // does not carry. Both decision-fact lists are therefore non-empty, which is exactly what
        // makes them sensitive to *which* resolution the controller reported: a resolution computed
        // from any other (invocation, observation, definitions) triple would change these strings.
        KernelDecisionFact decision = Assert.Single(decisions.Decisions);
        Assert.Equal(["BootstrapRoadmapCompletionContext"], decision.EligibleAlternatives);
        Assert.Equal(
            ["UpdateRoadmapCompletionContext:MissingRequiredInput:"],
            decision.RejectedAlternatives);
        Assert.Equal(
            new WorkflowTransitionIdentity("BootstrapRoadmapCompletionContext"),
            Assert.Single(harness.Runtime.Requests).Transition);
    }

    [Fact]
    public async Task Chain_cycle_resolves_the_workflow_once_and_hands_the_resolution_to_the_controller()
    {
        var workflowStates = new CountingWorkflowStates([]);
        RepositoryObservation observation = Observation(workflowStates);
        Harness harness = new(observation);

        WorkflowChainRunResult result = await harness.Runner.RunAsync(new WorkflowChainRunRequest(
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            observation,
            TraditionalRoadmapChain,
            Definitions,
            NewContext(),
            FreshAttemptAuthorization.Instance));

        Assert.Equal(WorkflowStopReason.TransitionCompleted, result.StopReason);
        // WorkflowResolver.Resolve enumerates observation.WorkflowStates exactly once per call, and
        // on this path it is the only reader: WorkflowExitGateEvaluator (the sole other reader)
        // runs only when the workflow is already completed, which is not this scenario. One
        // enumeration therefore means one resolution for the cycle -- the chain runner resolves and
        // hands the result down instead of the controller resolving the identical triple again.
        Assert.Equal(1, workflowStates.EnumerationCount);
    }

    [Fact]
    public async Task Controller_reresolves_when_the_carried_cycle_was_computed_from_a_different_observation()
    {
        var invocation = new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain);
        var resolver = new WorkflowResolver();

        // The request's own observation: the workflow is already completed, so a correct controller
        // must stop immediately without attempting any transition.
        RepositoryObservation requestObservation = Observation([Completed(WorkflowIdentity.TraditionalRoadmap)]);

        // A resolution computed from a DIFFERENT observation instance -- nothing has completed here,
        // so the workflow is eligible to start and has an eligible transition. This stands in for a
        // stale cycle a caller mistakenly hands down alongside a request carrying a different
        // observation.
        RepositoryObservation staleObservation = Observation();
        WorkflowCycleResolution staleCycle = WorkflowCycleResolution.Resolve(
            resolver, invocation, staleObservation, Definitions);

        // Sanity check: the two observations really do produce observably different resolutions, so
        // this test can only pass if the controller actually discards the stale cycle and re-resolves
        // from its own request.Observation rather than trusting the carried one.
        Assert.Equal(RepositoryClassification.Fresh, staleCycle.Resolution.Classification);

        var runtime = new FakeTransitionRuntime();
        var effects = new FakeEffectCoordinator();
        var controller = new WorkflowController(resolver, runtime, effects);
        WorkflowRunContext context = NewContext();
        var execution = new CanonicalTransitionExecutionContext(
            invocation,
            context.Workspace,
            context.Run,
            WorkflowInstanceIdentity.New(),
            context.Policy,
            context.RuntimeProfile,
            context.PromptPolicyProfile,
            context.AgentRolePolicyIdentity);

        WorkflowControllerResult result = await controller.RunAsync(new WorkflowControllerRequest(
            invocation,
            requestObservation,
            Definitions,
            execution,
            FreshAttemptAuthorization.Instance,
            Interactive: false,
            Cycle: staleCycle));

        // The returned resolution reflects the request's own (completed) observation, not the stale
        // cycle's (fresh) one -- proof the mismatch was detected and the controller re-resolved.
        Assert.Equal(RepositoryClassification.Completed, result.Resolution.Classification);
        Assert.Equal(WorkflowStopReason.ChainCompleted, result.StopReason);
        // A resolution taken from the stale cycle would have found an eligible transition and
        // invoked the runtime; the correctly re-resolved (completed) result must never reach that.
        Assert.Empty(runtime.Requests);
    }

    /// <summary>
    /// Counts how many times the observed workflow states are enumerated, which is a direct count
    /// of <see cref="WorkflowResolver.Resolve"/> calls over this observation.
    /// </summary>
    private sealed class CountingWorkflowStates(IReadOnlyList<ObservedWorkflowState> _states)
        : IReadOnlyList<ObservedWorkflowState>
    {
        public int EnumerationCount { get; private set; }

        public int Count => _states.Count;

        public ObservedWorkflowState this[int index] => _states[index];

        public IEnumerator<ObservedWorkflowState> GetEnumerator()
        {
            EnumerationCount++;
            return _states.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class Harness
    {
        public Harness(RepositoryObservation observation)
        {
            Runtime = new FakeTransitionRuntime();
            Effects = new FakeEffectCoordinator();
            Observations = new StaticObservationSource(observation);
            Instances = new FakeInstanceRecorder();
            Boundaries = new RecordingBoundaryStore();
            var controller = new WorkflowController(
                new WorkflowResolver(), Runtime, Effects);
            Runner = new WorkflowChainRunner(
                new WorkflowResolver(),
                controller,
                new WorkflowEntryGateEvaluator(),
                new WorkflowExitGateEvaluator(),
                new ProductTransferEvaluator(),
                new WorkflowBoundaryEvidenceWriter(Boundaries),
                Instances,
                Observations);
        }

        public FakeTransitionRuntime Runtime { get; }
        public FakeEffectCoordinator Effects { get; }
        public StaticObservationSource Observations { get; }
        public FakeInstanceRecorder Instances { get; }
        public RecordingBoundaryStore Boundaries { get; }
        public WorkflowChainRunner Runner { get; }
    }

    private static WorkflowRunContext NewContext() => new(
        WorkspaceIdentity.New(),
        RunIdentity.New(),
        new PolicyIdentity("policy_test"),
        new RuntimeProfileIdentity("runtime_test"),
        new PromptPolicyProfileIdentity("prompt_policy_test"));

    private static TransitionRuntimeResult RuntimeResult(
        RuntimeOutcomeKind outcome = RuntimeOutcomeKind.Completed,
        TransitionDurableState state = TransitionDurableState.Completed,
        bool effectsPending = false) => new(
        outcome,
        state,
        new WorkflowTransitionIdentity("CreateExecutablePlan"),
        null, null, null, null, [], outcome.ToString(), [],
        TransitionRunIdentity.New(),
        AttemptIdentity.New(),
        AttemptCompleted: true,
        RequiredEffectsPending: effectsPending);

    private sealed class FakeTransitionRuntime : ITransitionRuntime
    {
        public List<TransitionRuntimeRequest> Requests { get; } = [];
        public TransitionRuntimeResult Result { get; set; } = RuntimeResult();

        /// <summary>
        /// Optional scripted per-call outcomes for multi-cycle scenarios. Dequeued in order;
        /// falls back to <see cref="Result"/> once exhausted (or if never populated).
        /// </summary>
        public Queue<TransitionRuntimeResult> Sequence { get; } = new();

        public Task<TransitionRuntimeResult> RunAsync(
            TransitionRuntimeRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            TransitionRuntimeResult result = Sequence.Count > 0 ? Sequence.Dequeue() : Result;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeEffectCoordinator : ITransitionEffectCoordinator
    {
        public int CallCount { get; private set; }
        public TransitionEffectCoordinationResult Result { get; set; } =
            new(false, false, "effects complete", []);

        public Task<TransitionEffectCoordinationResult> CoordinateAsync(
            TransitionRunIdentity transitionRun,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(Result);
        }
    }

    private sealed class StaticObservationSource(RepositoryObservation observation)
        : ICanonicalRepositoryObservationSource
    {
        public int CallCount { get; private set; }

        public Task<RepositoryObservation> ObserveAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(observation);
        }
    }

    private sealed class FakeInstanceRecorder : IWorkflowInstanceRecorder
    {
        public List<(RunIdentity Run, WorkflowIdentity Workflow, WorkflowInstanceIdentity Instance)> Begun { get; } = [];

        public Task<WorkflowInstanceIdentity> BeginInstanceAsync(
            RunIdentity run,
            WorkflowIdentity workflow,
            CancellationToken cancellationToken)
        {
            WorkflowInstanceIdentity instance = WorkflowInstanceIdentity.New();
            Begun.Add((run, workflow, instance));
            return Task.FromResult(instance);
        }

        public Task CompleteInstanceAsync(
            WorkflowInstanceIdentity workflowInstance,
            string status,
            string? outcome,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingBoundaryStore : IChainBoundaryEvidenceStore
    {
        public bool FailAppend { get; set; }
        public List<ChainBoundaryEvidenceCapture> Captures { get; } = [];

        public Task AppendAsync(ChainBoundaryEvidenceCapture capture, CancellationToken cancellationToken)
        {
            if (FailAppend)
            {
                throw new InvalidOperationException("boundary persistence failed");
            }

            Captures.Add(capture);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingKernelDecisionStore : IKernelDecisionStore
    {
        public List<KernelDecisionFact> Decisions { get; } = [];
        public Task AppendAsync(KernelDecisionFact decision, CancellationToken cancellationToken)
        {
            Decisions.Add(decision);
            return Task.CompletedTask;
        }
    }

    private static ObservedWorkflowState Completed(WorkflowIdentity workflow) => new(
        workflow,
        WorkflowResolutionState.Completed,
        null,
        [], [],
        [$"{workflow}.completed.json"]);

    private static RepositoryObservation Observation(
        IReadOnlyList<ObservedWorkflowState>? workflowStates = null,
        IReadOnlyList<ObservedProduct>? products = null)
    {
        var verification = new StorageVerificationResult(
            StorageAuthorityKind.CanonicalSqlite,
            true,
            [], [], [], [], [], [], [],
            ["canonical"]);
        return new RepositoryObservation(
            "repo",
            new StorageAuthoritySnapshot(StorageAuthorityKind.CanonicalSqlite, true, "test", ["canonical"]),
            workflowStates ?? [],
            products ?? [],
            [], [], [],
            new ObservedGitFacts(true, false, "main", [".git"]),
            [], [],
            verification);
    }

    private static ObservedProduct Product(ProductIdentity identity, WorkflowIdentity producer)
    {
        var product = new ProductRecord(
            identity,
            producer,
            new WorkflowTransitionIdentity($"Produce{identity.Value}"),
            [WorkflowIdentity.Plan, WorkflowIdentity.Execute],
            "repository",
            "test",
            [$"{identity.Value}.md"],
            $"causal-{identity.Value}",
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            [$"{identity.Value}.md"]);
        return new ObservedProduct(product, true, product.EvidenceLocations);
    }
}
