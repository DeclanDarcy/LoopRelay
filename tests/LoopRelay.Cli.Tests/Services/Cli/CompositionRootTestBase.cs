using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Application.Contracts;
using LoopRelay.Cli.Services.Cli;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Cli.Services.Planning;
using LoopRelay.Cli.Tests.Models;
using LoopRelay.Cli.Tests.Services.Agents;
using LoopRelay.Completion.Models.Authority;
using LoopRelay.Completion.Services.Authority;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Policy;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Services.Evaluation;
using LoopRelay.Projections.Models.ProjectionArtifacts;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

// LoopRelayCompositionRootTests was one xUnit collection (one class = strictly serial),
// so its wall-clock time was the whole suite's critical path. It has been split into five
// sibling classes sharing this base so xUnit can run them as separate, parallel collections.
//
// Grouping was built from measured per-method durations (a fresh trx run; see
// .superpowers/sdd/tperf-task-4-timings.txt), balanced by duration via a greedy
// longest-processing-time bin-pack across 5 bins -- NOT by the name-prefix buckets the
// original task brief suggested, because those buckets, scored against real data, produce an
// 86.4s Plan_* class that would forfeit more than half of the available parallelism gain.
// Plan_workflow_transitions_run_through_canonical_runtime (30.0s) is a single indivisible
// test and is therefore the hard floor for the slowest class.
//
// Final grouping (41 methods / 44 cases / 185.0s measured total):
//
//   CompositionRootPlanCanonicalRuntimeTests        36.9s  (4 methods)
//     30.0s Plan_workflow_transitions_run_through_canonical_runtime
//      3.6s Plan_scoped_artifact_milestone_without_checkboxes_rolls_back_declared_writes
//      2.8s Prompt_transitions_record_agent_session_and_turn_rows
//      0.5s Execute_commit_evaluation_stall_persists_canonical_evidence
//
//   CompositionRootEvalRoadmapAndContinuityTests    37.2s  (6 methods)
//     17.4s EvalRoadmap_workflow_transitions_run_through_canonical_runtime
//      6.4s Execute_handoff_resumes_exact_implementation_thread_and_restores_slice_facts_after_restart
//      5.0s Resolved_causality_written_into_the_effect_ledger_matches_the_durable_attempt_row
//      4.0s GenerateDecision_honors_the_composed_observers_storage_verdict_over_a_fresh_default
//      2.9s Plan_prompt_transition_renders_generated_prompt_asset_before_executor_integration
//      1.5s TraditionalRoadmap_invalid_prepared_epic_fails_with_recovery_marker
//
//   CompositionRootGuardsAndInvariantsTests         36.7s  (19 methods)
//     14.2s Plan_warm_session_transitions_execute_and_reuse_one_authoring_session
//      6.5s Plan_revision_blocks_precisely_when_exact_thread_resume_fails
//      5.7s Execute_handoff_blocks_precisely_when_exact_implementation_thread_resume_fails
//      4.9s Execute_implementation_rejects_provider_completion_without_implementation_progress
//      3.4s Plan_adversarial_review_context_starts_read_only_prompt_with_plan_and_projection_products
//      2.0s EvalRoadmap_milestone_deep_dive_stops_on_empty_active_epic_context
//      0.0s (x13 methods / 16 cases) all remaining zero-duration guard/retirement/policy checks -- free to place
//           anywhere; grouped here because this class is already guard/invariant-themed:
//           Execute_entry_rejects_milestone_cardinality_that_conflicts_with_strategic_context,
//           Composition_exposes_one_resolved_versioned_policy_for_the_invocation,
//           Session_log_environment_variable_flows_through_the_invocation_layer,
//           SelectChain_returns_single_workflow_chain_for_bounded_plan_and_execute,
//           Create_wires_canonical_observation_resolution_definitions_and_chains,
//           Decision_resume_kill_switch_flows_through_the_invocation_layer,
//           Production_recovery_routes_legacy_session_tables_only_through_the_migration_compatibility_boundary,
//           Production_feature_handlers_cannot_mutate_files_git_or_canonical_progress_directly,
//           Production_decision_session_routes_loop_artifact_rotation_through_durable_effect_authority,
//           Production_cli_composition_does_not_construct_legacy_loop_runner,
//           Policy_source_descriptor_names_the_settings_file_without_its_directory (Theory, 4 cases),
//           Retired_plan_and_roadmap_compositions_are_not_available_as_active_authorities,
//           Verify_execute_entry_contract_stops_on_milestone_set_without_trackable_checkboxes
//
//   CompositionRootTraditionalRoadmapAndSpineTests  37.2s  (6 methods)
//     14.1s TraditionalRoadmap_accepts_inline_code_file_markers_from_provider_output
//      6.7s Run_command_records_workflow_instance_and_attempt_rows_linked_to_the_run
//      5.6s Plan_revision_resumes_exact_authoring_thread_after_composition_restart
//      4.5s Verify_execute_entry_contract_completes_plan_and_persists_execution_readiness
//      3.5s Generate_operational_context_runs_as_deterministic_canonical_artifact_transition
//      2.8s Cancelled_turns_leave_terminal_turn_evidence_instead_of_vanishing_from_the_spine
//
//   CompositionRootExecuteWorkflowAndArtifactTests  37.0s  (6 methods)
//     13.2s Plan_scoped_artifact_transitions_execute_with_operation_profiles_and_persist_products
//      8.4s Execute_workflow_transitions_run_through_canonical_runtime
//      5.4s Execute_implementation_rejects_and_rolls_back_milestone_file_set_changes
//      4.4s Plan_warm_session_materializes_structurally_valid_returned_plan_when_tool_write_is_absent
//      3.0s EvalRoadmap_prompt_transition_renders_generated_prompt_asset_before_executor_integration
//      2.6s Plan_warm_session_prompt_success_without_plan_file_fails_product_validation
//
// None of these class names is a pure family bucket -- every class is a duration-balanced mix.
// Each name reflects its two heaviest/anchor tests' theme; the full membership above is the
// authoritative record. All 41 methods moved verbatim from the original
// LoopRelayCompositionRootTests (deleted by this change); shared static helpers and the two
// nested private fakes below moved here unchanged except private -> protected.
public abstract class CompositionRootTestBase
{
    /// <summary>
    /// Reports the same blocked-storage verdict a production verifier gives for an unusable
    /// workspace authority - a non-empty <see cref="StorageVerificationResult.BlockingConditions"/>,
    /// which makes <see cref="StorageVerificationResult.IsUnusable"/> true - while leaving
    /// <see cref="StorageVerificationResult.UsableAuthority"/> and
    /// <see cref="StorageVerificationResult.Health"/> exactly as the real, healthy repository
    /// reports them, so canonical product resolution earlier in the same attempt (which keys off
    /// <c>UsableAuthority</c>, not <c>IsUnusable</c>) is unaffected. Only the specific gate under
    /// test - <c>DecisionSessionScopeResolver</c>'s <c>IsUnusable</c> check - sees a difference
    /// from the deleted fallback's independent <see cref="FileSystemStorageVerifier"/>, which never
    /// observes this blocking condition because it never runs against this repository at all.
    /// </summary>
    private protected sealed class BlockedAuthorityStorageVerifier : IStorageVerifier
    {
        private readonly FileSystemStorageVerifier inner = new();

        public async Task<StorageVerificationResult> VerifyAsync(
            string repositoryPath,
            CancellationToken cancellationToken)
        {
            StorageVerificationResult result = await inner.VerifyAsync(repositoryPath, cancellationToken);
            return result with
            {
                BlockingConditions =
                [
                    new ResolutionWarning(
                        WarningCategory.Storage,
                        "Test-injected blocking condition.",
                        "test authority",
                        "n/a",
                        []),
                ],
            };
        }
    }

    private protected static async Task AssertPlanStageAsync(
        Repository repository,
        string expectedStage,
        string completedTransition)
    {
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        CanonicalWorkflowStateRecord workflow = Assert.Single(
            snapshot.WorkflowStates,
            state => state.Workflow == WorkflowIdentity.Plan);
        Assert.Equal(new WorkflowStageIdentity(expectedStage), workflow.CurrentStage);
        Assert.Contains(snapshot.TransitionRuns, run =>
            run.Transition == new WorkflowTransitionIdentity(completedTransition) &&
            run.State == TransitionDurableState.Completed);
    }

    private protected static RepositoryObservation EmptyObservation(string repo)
    {
        var storage = new StorageVerificationResult(
            StorageAuthorityKind.Missing,
            UsableAuthority: true,
            StaleExports: [],
            Conflicts: [],
            Corruption: [],
            UnsupportedSchema: [],
            UnresolvedReferences: [],
            PartialTransactions: [],
            BlockingConditions: [],
            Evidence: []);
        return new RepositoryObservation(
            repo,
            new StorageAuthoritySnapshot(storage.Authority, storage.UsableAuthority, "test", []),
            WorkflowStates: [],
            Products: [],
            LifecycleRows: [],
            Evidence: [],
            TransitionRuns: [],
            GitFacts: new ObservedGitFacts(IsRepository: false, HasWorkingTreeChanges: false, CurrentBranch: "unknown", Evidence: []),
            HumanInteractionRequirements: [],
            EvaluationIntentPaths: [],
            StorageVerification: storage);
    }

    private protected static async Task WriteAsync(string root, string relativePath, string content)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    private protected static TransitionRuntimeRequest Request(
        WorkflowIdentity workflow,
        WorkflowStageIdentity stage,
        WorkflowTransitionIdentity transition,
        RunIdentity? run = null)
    {
        var invocation = new WorkflowInvocation(InvocationModeKind.DefaultChained);
        return new TransitionRuntimeRequest(
            workflow,
            stage,
            transition,
            ExecutionContext(invocation, run),
            FreshAttemptAuthorization.Instance);
    }

    private protected static WorkflowControllerRequest ControllerRequest(
        WorkflowInvocation invocation,
        RepositoryObservation observation,
        IReadOnlyList<WorkflowDefinition> definitions) =>
        new(
            invocation,
            observation,
            definitions,
            ExecutionContext(invocation),
            FreshAttemptAuthorization.Instance);


    private protected static CanonicalTransitionExecutionContext ExecutionContext(
        WorkflowInvocation invocation,
        RunIdentity? run = null) =>
        new(
            invocation,
            WorkspaceIdentity.New(),
            run ?? RunIdentity.New(),
            WorkflowInstanceIdentity.New(),
            new PolicyIdentity("policy_test"),
            new RuntimeProfileIdentity("runtime_test"),
            new PromptPolicyProfileIdentity("prompt_policy_test"));


    private protected static Task<TransitionRuntimeResult> RunPlanAsync(
        LoopRelayCompositionRoot composition,
        string stage,
        string transition) =>
        RunSettledAttemptAsync(
            composition,
            Request(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity(stage),
                new WorkflowTransitionIdentity(transition)));


    private protected sealed class CancellingOneShotRuntime : IAgentRuntime
    {
        public AgentRuntimeCapabilities Capabilities { get; } = new("test", true, true, true);

        public Task<IAgentSession> OpenSessionAsync(
            AgentSessionSpec spec,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This fake only runs one-shots.");

        public Task<AgentTurnResult> RunOneShotAsync(
            AgentSessionSpec spec,
            string prompt,
            Func<AgentStreamChunk, Task>? onChunk = null,
            CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException("cancelled mid-send");

        public ValueTask CloseSessionAsync(IAgentSession session) => ValueTask.CompletedTask;
    }

    private protected static Task<TransitionRuntimeResult> RunEvalAsync(
        LoopRelayCompositionRoot composition,
        string stage,
        string transition) =>
        RunSettledAttemptAsync(
            composition,
            Request(
                WorkflowIdentity.EvalRoadmap,
                new WorkflowStageIdentity(stage),
                new WorkflowTransitionIdentity(transition)));


    private protected static Task<TransitionRuntimeResult> RunTraditionalAsync(
        LoopRelayCompositionRoot composition,
        string stage,
        string transition) =>
        RunSettledAttemptAsync(
            composition,
            Request(
                WorkflowIdentity.TraditionalRoadmap,
                new WorkflowStageIdentity(stage),
                new WorkflowTransitionIdentity(transition)));


    private protected static Task<TransitionRuntimeResult> RunExecuteAsync(
        LoopRelayCompositionRoot composition,
        string stage,
        string transition,
        RunIdentity? run = null) =>
        RunSettledAttemptAsync(
            composition,
            Request(
                WorkflowIdentity.Execute,
                new WorkflowStageIdentity(stage),
                new WorkflowTransitionIdentity(transition),
                run));


    private protected static async Task<TransitionRuntimeResult> RunSettledAttemptAsync(
        LoopRelayCompositionRoot composition,
        TransitionRuntimeRequest request)
    {
        CanonicalTransitionExecutionContext original =
            Assert.IsType<CanonicalTransitionExecutionContext>(request.ExecutionContext);
        string workspaceId = await composition.Persistence.ReadWorkspaceIdentityAsync(CancellationToken.None);
        var execution = new CanonicalTransitionExecutionContext(
            original.RootInvocation,
            new WorkspaceIdentity(workspaceId),
            original.Run,
            original.WorkflowInstance,
            original.ResolvedPolicy,
            original.RuntimeProfile,
            original.PromptPolicyProfile);
        TransitionRuntimeResult attempt = await composition.TransitionRuntime.RunAsync(
            request with { ExecutionContext = execution });
        if (!attempt.RequiredEffectsPending)
        {
            return attempt;
        }

        TransitionEffectCoordinationResult coordination = await composition.EffectCoordinator.CoordinateAsync(
            attempt.TransitionRun ?? throw new InvalidOperationException("Effect-pending attempt has no transition identity."),
            CancellationToken.None);
        RuntimeOutcomeKind outcome = coordination.Outcome ?? (coordination.RequiredEffectsPending
            ? RuntimeOutcomeKind.EffectsPending
            : coordination.Failed
                ? RuntimeOutcomeKind.Failed
                : RuntimeOutcomeKind.Completed);
        TransitionDurableState state = outcome switch
        {
            RuntimeOutcomeKind.Completed => TransitionDurableState.Completed,
            RuntimeOutcomeKind.Stalled => TransitionDurableState.Stalled,
            RuntimeOutcomeKind.Failed => TransitionDurableState.Failed,
            RuntimeOutcomeKind.RecoveryRequired => TransitionDurableState.EffectsPartiallyApplied,
            _ => TransitionDurableState.EffectsPending,
        };
        return attempt with
        {
            Outcome = outcome,
            DurableState = state,
            Explanation = coordination.Explanation,
            Evidence = attempt.Evidence.Concat(coordination.Evidence).Distinct(StringComparer.Ordinal).ToArray(),
            RequiredEffectsPending = coordination.RequiredEffectsPending,
        };
    }

    /// <summary>
    /// Asserts that the causal context production actually resolved for an attempt agrees, member by
    /// member, with the attempt's own durable row. The re-derivation below is a literal transcription
    /// of the read <c>ResolveCausalityAsync</c> performed before PERF-11: filter the attempt table
    /// down to the resolved attempt, then take workspace identity from its own single-row read and
    /// the remaining identities off the attempt row. Production no longer performs that read, so the
    /// transcription now exists only here -- which is the point. Were the durable attempt row ever to
    /// disagree with the resolved identity, the causal chain written into evidence would silently
    /// diverge from the attempt table, and this test is what fails.
    /// <para>
    /// Scope, stated precisely. <c>Run</c> and <c>WorkflowInstance</c> are genuinely independent
    /// here: they are read off the persisted attempt row and compared against the value production
    /// carried in memory. <c>TransitionRun</c> and <c>Attempt</c> are keyed by construction -- the row
    /// is selected on them. <c>Workspace</c> is NOT independently derived and this test does not
    /// prove it: the workspace identity has exactly one source in the system, the single row the
    /// workspace store owns, which both sides read (production reads it at run entry,
    /// <c>UnifiedCliRunner.cs:625</c>). The workspace assertion here therefore pins the fixture to
    /// production's sourcing rather than corroborating it. Workspace identity is instead covered in
    /// production on every dispatch by <c>LoadingPromptRuntimeDispatcher.cs:25</c>, whose
    /// <c>RequireSameAttempt</c> check compares the store-derived prompt-fact causality against the
    /// authorization across all five identities including workspace, and so catches drift between
    /// run entry and dispatch.
    /// </para>
    /// </summary>
    private protected static async Task AssertResolvedCausalityMatchesTheAttemptRowAsync(
        Repository repository,
        CanonicalCausalContext resolved)
    {
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        AttemptRecord row = (await persistence.ReadAttemptsAsync(CancellationToken.None))
            .Single(item => item.AttemptId == resolved.Attempt.Value &&
                item.TransitionRunId == resolved.TransitionRun.Value);
        var database = new CanonicalCausalContext(
            new WorkspaceIdentity(await persistence.ReadWorkspaceIdentityAsync(CancellationToken.None)),
            new RunIdentity(row.RunId),
            new WorkflowInstanceIdentity(row.WorkflowInstanceId),
            new TransitionRunIdentity(row.TransitionRunId),
            new AttemptIdentity(row.AttemptId));

        // Compared member by member on the underlying identity string, never with record equality:
        // CanonicalCausalContext is a record, and a synthesised `==` compares any reference-typed
        // member by reference -- which would let a shallow pass hide a nested difference.
        // Substantive: read off the attempt row, compared against what production carried in memory.
        Assert.Equal(resolved.Run.Value, database.Run.Value);
        Assert.Equal(resolved.WorkflowInstance.Value, database.WorkflowInstance.Value);
        // Keyed by construction (the row was selected on this pair), asserted so the transcription
        // above stays honest about which identities production read back off the row.
        Assert.Equal(resolved.TransitionRun.Value, database.TransitionRun.Value);
        Assert.Equal(resolved.Attempt.Value, database.Attempt.Value);
        // Not independent -- one source, read twice. See the scope note above.
        Assert.Equal(resolved.Workspace.Value, database.Workspace.Value);
        // The re-derivation is attempt-level on both sides: neither carries session or turn.
        Assert.Null(resolved.Session);
        Assert.Null(database.Session);
        Assert.Null(resolved.Turn);
        Assert.Null(database.Turn);
        // Guards this comparison's own completeness. Causal-identity values feed the durable causal
        // chain, so a member added to the spine must be added to the comparison above rather than
        // silently escaping it.
        Assert.Equal(7, typeof(CanonicalCausalContext).GetProperties().Length);
    }

    private protected static void EnqueueEvalOutput(
        FakeAgentRuntime runtime,
        string promptIdentity,
        string output)
    {
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Contains("Canonical Runtime Context", prompt, StringComparison.Ordinal);
            Assert.Contains("Runtime Source Boundary", prompt, StringComparison.Ordinal);
            Assert.Contains("Input Product:", prompt, StringComparison.Ordinal);
            Assert.Contains("Do not inspect the repository", prompt, StringComparison.Ordinal);
            Assert.Contains(promptIdentity, prompt.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.DoesNotContain("{projectContext}", prompt, StringComparison.Ordinal);
            if (promptIdentity == "CreateNewEpic")
            {
                Assert.DoesNotContain("{newEpicProposal}", prompt, StringComparison.Ordinal);
                Assert.DoesNotContain("{epicImplementationFirstGuidance}", prompt, StringComparison.Ordinal);
                Assert.DoesNotContain("{epicAuxiliaryArtifactLimits}", prompt, StringComparison.Ordinal);
                Assert.Contains("Project context body 1.", prompt, StringComparison.Ordinal);
                Assert.Contains("# Strategic Initiative Selection", prompt, StringComparison.Ordinal);
            }
            return new AgentTurnResult(runtime.OneShotCalls.Count, AgentTurnState.Completed, output, AgentTokenUsage.Zero);
        }));
    }

    private protected static void EnqueueTraditionalOutput(
        FakeAgentRuntime runtime,
        string promptIdentity,
        string output)
    {
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Contains("Canonical Runtime Context", prompt, StringComparison.Ordinal);
            Assert.Contains(promptIdentity, prompt.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
            return new AgentTurnResult(runtime.OneShotCalls.Count, AgentTurnState.Completed, output, AgentTokenUsage.Zero);
        }));
    }

    private protected static async Task WriteProjectContextAsync(string root)
    {
        int index = 0;
        foreach (string path in ProjectionArtifactPaths.ProjectContextSourceFiles)
        {
            index++;
            await WriteAsync(root, path, $"# Context {index}\n\nProject context body {index}.");
        }
    }

    private protected static async Task<(string Root, Repository Repository, FakeAgentRuntime Runtime, FakeProcessRunner Process)>
        PrepareExecuteContinuityCaseAsync(string prefix)
    {
        string root = Directory.CreateTempSubdirectory(prefix).FullName;
        await WriteAsync(root, ".agents/epic.md", ValidEvalActiveEpic());
        await WriteAsync(root, ".agents/plan.md", "# Plan\n\nCreate src/feature.cs.");
        await WriteAsync(root, ".agents/operational_context.md", "# Operational Context\n\nUse the plan.");
        await WriteAsync(root, ".agents/details.md", "# Details\n\nWrite deterministic text.");
        await WriteAsync(root, ".agents/milestones/m1.md", "# Milestone 1\n\n- [ ] Create feature.");
        await WriteProjectContextAsync(root);
        await GitWorkspace.InitializeWithAgentsInputsAsync(root);
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(root), Path = root };
        var process = new FakeProcessRunner
        {
            Handler = (workingDirectory, args) =>
            {
                if (args.SequenceEqual(["status", "--porcelain"]))
                {
                    return FakeProcessRunner.Ok(
                        workingDirectory.EndsWith(".agents", StringComparison.OrdinalIgnoreCase)
                            ? " M handoffs/handoff.md\n"
                            : " M src/feature.cs\n");
                }

                if (args.SequenceEqual(["status", "--porcelain", "--untracked-files=all"]))
                {
                    return FakeProcessRunner.Ok(" M src/feature.cs\n");
                }

                if (args.SequenceEqual(["diff", "--name-status", "--find-renames", "HEAD", "--"]))
                {
                    return FakeProcessRunner.Ok("M\tsrc/feature.cs\n");
                }

                if (args.SequenceEqual(["branch", "--show-current"]))
                {
                    return FakeProcessRunner.Ok("main\n");
                }

                if (args.Count >= 2 && args[0] == "rev-parse")
                {
                    return FakeProcessRunner.Ok("abc123\n");
                }

                if (args.SequenceEqual(["rev-list", "--count", "@{u}..HEAD"]))
                {
                    return FakeProcessRunner.Ok("0\n");
                }

                return FakeProcessRunner.Ok();
            },
        };
        return (root, repository, new FakeAgentRuntime(new MemoryArtifactStore()), process);
    }

    private protected static string ValidAdversarialProjection() =>
        """
        # Adversarial Plan Review Projection

        ## Purpose

        Test purpose.

        ## Authority Boundary

        Test authority.

        ## Projection Metadata

        | Field | Value |
        |---|---|
        | Intended Consumer | AdversarialPlanReview |

        ## Canonical Vocabulary

        | Term | Definition |
        |---|---|
        | Test | Test definition |

        ## Downstream Use Instructions

        Test downstream instructions.

        ## Projection Integrity Checklist

        - Valid.
        """;


    private protected static string ValidProjection(string title, string intendedConsumer) =>
        $$"""
        {{title}}

        ## Purpose

        Test purpose.

        ## Authority Boundary

        Test authority.

        ## Projection Metadata

        | Field | Value |
        |---|---|
        | Intended Consumer | {{intendedConsumer}} |

        ## Canonical Vocabulary

        | Term | Definition |
        |---|---|
        | Test | Test definition |

        ## Downstream Use Instructions

        Test downstream instructions.

        ## Projection Integrity Checklist

        - Valid.
        """;


    private protected static string Evaluation(string completionStatus, string drift, string recommendation) => $$"""
        # Epic Completion and Drift Evaluation

        ## Evaluation Summary

        | Field | Value |
        |---|---|
        | Epic ID | EPIC-TEST |
        | Epic Name | Test Epic |
        | Overall Completion Status | {{completionStatus}} |
        | Overall Drift Classification | {{drift}} |
        | Evidence Strength | Strong |
        | Closure Recommendation | {{recommendation}} |
        | Primary Reason | Test |
        """;


    private protected static string ValidEvalActiveEpic() =>
        """
        # Epic: Implement Capability

        ## Epic Metadata

        Source: eval fixture.

        ## Strategic Purpose

        Preserve the evaluated capability.

        ## Desired Capability

        The system implements the capability with observable behavior.

        ## Acceptance Criteria

        - The capability has executable validation.

        ## Milestone Roadmap

        | MilestoneID | MilestoneName | Purpose | Outcome | DependsOn | CompletionSignal |
        |---|---|---|---|---|---|
        | M1 | Implement capability | Add the behavior | Capability works | none | Tests pass |
        """;


    private protected static async Task WriteAdversarialReviewProductAsync(
        Repository repository,
        string content)
    {
        string relativePath = ".LoopRelay/evidence/plan/adversarial-review.md";
        string path = Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
        await new CanonicalWorkflowPersistenceStore(repository).UpsertProductAsync(
            new ProductRecord(
                ProductIdentity.AdversarialReview,
                WorkflowIdentity.Plan,
                new WorkflowTransitionIdentity("RunAdversarialReview"),
                [WorkflowIdentity.Plan],
                "repository-owned test evidence",
                "test",
                [relativePath],
                $"review:{content}",
                ProductFreshness.Fresh,
                ProductValidationState.Valid,
                ProductLifecycle.Active,
                [relativePath]));
    }

    private protected static Task WriteRepositoryChangesProductAsync(Repository repository) =>
        new CanonicalWorkflowPersistenceStore(repository).UpsertProductAsync(
            new ProductRecord(
                ProductIdentity.RepositoryChanges,
                WorkflowIdentity.Execute,
                new WorkflowTransitionIdentity("PublishRepositoryState"),
                [WorkflowIdentity.Execute],
                "repository-owned test evidence",
                "test",
                [".LoopRelay/evidence/execute-repository-state/PublishRepositoryState.md"],
                "repository-changes:test",
                ProductFreshness.Fresh,
                ProductValidationState.Valid,
                ProductLifecycle.Active,
                [".LoopRelay/evidence/execute-repository-state/PublishRepositoryState.md"]));


    private protected static async Task AssertEffectStateAsync(
        Repository repository,
        string effectIdentity,
        EffectLifecycle expected)
    {
        IReadOnlyList<EffectWorkItem> effects = await new CanonicalEffectWorkStore(repository)
            .ReadBySemanticOperationAsync($"transition-effect:{effectIdentity}", CancellationToken.None);
        Assert.Contains(effects, effect => effect.State == expected);
    }

    private protected static async Task AssertCanonicalRecoveryPlanAsync(
        Repository repository,
        string sessionIdentity,
        CanonicalRecoveryAction expectedAction)
    {
        var recoveryCase = new RecoveryCaseIdentity($"recoverycase:WarmSession:{sessionIdentity}");
        CanonicalRecoveryCase persisted = Assert.IsType<CanonicalRecoveryCase>(
            await new CanonicalRecoveryStore(repository).ReadCaseAsync(
                recoveryCase,
                CancellationToken.None));
        Assert.Equal(RecoveryScopeKind.WarmSession, persisted.Scope);
        Assert.Contains(
            await new CanonicalRecoveryStore(repository).ReadPlansAsync(
                recoveryCase,
                CancellationToken.None),
            plan => plan.Action == expectedAction);
    }

    private protected static string ReadCompositionSource(string repositoryRoot)
    {
        string directory = Path.Combine(repositoryRoot, "src", "LoopRelay.Cli", "Services", "Cli");
        return string.Join('\n', Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).Contains("Composition", StringComparison.Ordinal) ||
                Path.GetFileName(path) == "LoopRelayCompositionRoot.cs")
            .Order(StringComparer.Ordinal)
            .Select(File.ReadAllText));
    }

    private protected static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "LoopRelay.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
