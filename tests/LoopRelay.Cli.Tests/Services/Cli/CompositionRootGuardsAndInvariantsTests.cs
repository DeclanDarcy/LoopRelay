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

public sealed class CompositionRootGuardsAndInvariantsTests : CompositionRootTestBase
{
    [Fact]
    public async Task Plan_warm_session_transitions_execute_and_reuse_one_authoring_session()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-warm-session").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Active Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Planning, spec.Role);
            Assert.Contains("Prompt identity: WritePlan", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v1");
            return new AgentTurnResult(0, AgentTurnState.Completed, "wrote plan", AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Planning, spec.Role);
            Assert.Contains("Prompt identity: ReviewAndRevisePlan", prompt, StringComparison.Ordinal);
            Assert.Contains("Adversarial Review", prompt, StringComparison.Ordinal);
            Assert.Contains("tighten the plan", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v2");
            return new AgentTurnResult(1, AgentTurnState.Completed, "revised plan", AgentTokenUsage.Zero);
        }));
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository, runtime);

        TransitionRuntimeResult write = await RunPlanAsync(composition, "Planning", "WriteExecutablePlan");
        await WriteAdversarialReviewProductAsync(repository, "tighten the plan");
        // RevisePlan declares `.agents/plan.md` as a clean-input surface, so the plan the
        // warm session just wrote must be committed before it is consumed.
        await GitWorkspace.CommitAgentsInputsAsync(repo);
        TransitionRuntimeResult revise = await RunPlanAsync(composition, "Plan Validation", "RevisePlan");

        Assert.Equal(RuntimeOutcomeKind.Completed, write.Outcome);
        Assert.Equal(RuntimeOutcomeKind.Completed, revise.Outcome);
        Assert.Equal(1, runtime.OpenSessions);
        Assert.Equal(1, runtime.ClosedSessions);
        Assert.Equal(2, runtime.SessionCalls.Count);
        Assert.Equal("# Plan v2", await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "plan.md")));
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.ExecutablePlan &&
            product.ProducerTransition == new WorkflowTransitionIdentity("RevisePlan") &&
            product.CausalIdentity.Length == 64);
        await AssertEffectStateAsync(repository, "persist-draft-plan", EffectLifecycle.Succeeded);
        await AssertEffectStateAsync(repository, "persist-reviewed-plan", EffectLifecycle.Succeeded);
    }

    [Fact]
    public async Task Plan_revision_blocks_precisely_when_exact_thread_resume_fails()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-warm-resume-fail").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Active Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        await WriteProjectContextAsync(repo);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(repo), Path = repo };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v1");
            return new AgentTurnResult(0, AgentTurnState.Completed, "wrote plan", AgentTokenUsage.Zero);
        }));
        LoopRelayCompositionRoot first = LoopRelayCompositionRoot.CreateForTests(repository, runtime);
        await RunPlanAsync(first, "Planning", "WriteExecutablePlan");
        PlanWarmSessionContinuity checkpoint = Assert.IsType<PlanWarmSessionContinuity>(
            await new CanonicalCheckpointStore(repository).ReadAsync<PlanWarmSessionContinuity>(CanonicalCheckpointKeys.PlanWarmSession, CancellationToken.None));
        await first.DisposeAsync();
        await WriteAdversarialReviewProductAsync(repository, "tighten after restart");
        runtime.FailResume = true;

        await using LoopRelayCompositionRoot restarted = LoopRelayCompositionRoot.CreateForTests(repository, runtime);
        TransitionRuntimeResult revise = await RunPlanAsync(restarted, "Plan Validation", "RevisePlan");

        Assert.Equal(RuntimeOutcomeKind.RecoveryRequired, revise.Outcome);
        Assert.Equal(TransitionDurableState.ProviderOutcomeUnknown, revise.DurableState);
        Assert.Contains("could not resume the exact authoring thread", revise.Explanation, StringComparison.Ordinal);
        Assert.Equal("# Plan v1", await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "plan.md")));
        Assert.Single(runtime.SessionCalls);
        await AssertCanonicalRecoveryPlanAsync(
            repository, $"plan:{checkpoint.ProviderThreadId}", CanonicalRecoveryAction.ResumeSession);
    }

    [Fact]
    public async Task Execute_handoff_blocks_precisely_when_exact_implementation_thread_resume_fails()
    {
        (string repo, Repository repository, FakeAgentRuntime runtime, FakeProcessRunner process) = await PrepareExecuteContinuityCaseAsync(
            "cc-cli-unified-execute-warm-resume-fail");
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "# Decisions\n\nCreate src/feature.cs.", AgentTokenUsage.Zero)));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            Directory.CreateDirectory(Path.Combine(repo, "src"));
            File.WriteAllText(Path.Combine(repo, "src", "feature.cs"), "feature\n");
            return new AgentTurnResult(1, AgentTurnState.Completed, "implemented", AgentTokenUsage.Zero);
        }));
        LoopRelayCompositionRoot first = LoopRelayCompositionRoot.CreateForTests(repository, runtime, process);
        await RunPlanAsync(first, "Workflow Completion", "VerifyExecuteEntryContract");
        await RunExecuteAsync(first, "Execution Readiness", "VerifyExecutionReadiness");
        await RunExecuteAsync(first, "Implementation Planning", "GenerateDecision");
        await RunExecuteAsync(first, "Implementation", "ExecuteImplementationSlice");
        ExecutionWarmSessionContinuity checkpoint = Assert.IsType<ExecutionWarmSessionContinuity>(
            await new CanonicalCheckpointStore(repository).ReadAsync<ExecutionWarmSessionContinuity>(CanonicalCheckpointKeys.ExecutionWarmSession, CancellationToken.None));
        await first.DisposeAsync();
        runtime.FailResume = true;

        await using LoopRelayCompositionRoot restarted = LoopRelayCompositionRoot.CreateForTests(repository, runtime, process);
        TransitionRuntimeResult handoff = await RunExecuteAsync(
            restarted, "Execution Continuity", "GenerateHandoff");

        Assert.Equal(RuntimeOutcomeKind.RecoveryRequired, handoff.Outcome);
        Assert.Equal(TransitionDurableState.ProviderOutcomeUnknown, handoff.DurableState);
        Assert.Contains("could not resume the exact execution thread", handoff.Explanation, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(repo, ".agents", "handoffs", "handoff.md")));
        await AssertCanonicalRecoveryPlanAsync(
            repository, $"execute:{checkpoint.ProviderThreadId}", CanonicalRecoveryAction.ResumeSession);
    }

    [Fact]
    public async Task Execute_implementation_rejects_provider_completion_without_implementation_progress()
    {
        (string repo, Repository repository, FakeAgentRuntime runtime, FakeProcessRunner process) =
            await PrepareExecuteContinuityCaseAsync("cc-cli-unified-execute-no-progress");
        process.Handler = (workingDirectory, args) =>
        {
            if (args.SequenceEqual(["status", "--porcelain"]) ||
                args.SequenceEqual(["status", "--porcelain", "--untracked-files=all"]) ||
                args.SequenceEqual(["diff", "--name-status", "--find-renames", "HEAD", "--"]))
            {
                return FakeProcessRunner.Ok();
            }

            if (args.SequenceEqual(["branch", "--show-current"]))
            {
                return FakeProcessRunner.Ok("main\n");
            }

            if (args.Count >= 2 && args[0] == "rev-parse")
            {
                return FakeProcessRunner.Ok("abc123\n");
            }

            return FakeProcessRunner.Ok();
        };
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "# Decisions\n\nImplement the feature.", AgentTokenUsage.Zero)));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(1, AgentTurnState.Completed, "No changes were necessary.", AgentTokenUsage.Zero)));

        await using LoopRelayCompositionRoot composition =
            LoopRelayCompositionRoot.CreateForTests(repository, runtime, process);
        await RunPlanAsync(composition, "Workflow Completion", "VerifyExecuteEntryContract");
        await RunExecuteAsync(composition, "Execution Readiness", "VerifyExecutionReadiness");
        await RunExecuteAsync(composition, "Implementation Planning", "GenerateDecision");
        TransitionRuntimeResult result = await RunExecuteAsync(
            composition,
            "Implementation",
            "ExecuteImplementationSlice");

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Contains("no repository implementation delta", result.Explanation, StringComparison.Ordinal);
        Assert.Null(await new CanonicalCheckpointStore(repository).ReadAsync<ExecutionWarmSessionContinuity>(
            CanonicalCheckpointKeys.ExecutionWarmSession,
            CancellationToken.None));
    }

    [Fact]
    public async Task Plan_adversarial_review_context_starts_read_only_prompt_with_plan_and_projection_products()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-review-prompt").FullName;
        await WriteAsync(repo, ".agents/plan.md", "# Plan\n\nImplement capability.");
        await WriteAsync(
            repo,
            PlanPromptContext.AdversarialPlanReviewProjectionPath,
            "# Adversarial Plan Review Projection\n\nProject-specific review context.");
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var composition = LoopRelayCompositionRoot.CreateForTests(repository);

        TransitionRuntimeResult result = await composition.TransitionRuntime.RunAsync(
            Request(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Plan Validation"),
                new WorkflowTransitionIdentity("RunAdversarialReview")));

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Contains("Prompt execution integration is not wired", result.Explanation, StringComparison.Ordinal);
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        CanonicalPromptAsset asset = CanonicalPromptAssetCatalog.GetByPromptIdentity("RunAdversarialReview");
        CanonicalRenderedPromptRecord fact = Assert.Single(
            await new CanonicalWorkflowPersistenceStore(repository).ReadRenderedPromptsAsync());
        Assert.Equal(asset.PromptIdentity, fact.PromptIdentity);
        Assert.Equal(asset.SourceHash, fact.TemplateSourceHash);
        CanonicalTransitionRunRecord run = Assert.Single(snapshot.TransitionRuns);
        Assert.Equal(TransitionDurableState.Failed, run.State);
        Assert.NotNull(run.InputSnapshotHash);
    }

    [Fact]
    public async Task EvalRoadmap_milestone_deep_dive_stops_on_empty_active_epic_context()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-eval-context").FullName;
        Directory.CreateDirectory(Path.Combine(repo, ".agents"));
        await File.WriteAllTextAsync(Path.Combine(repo, ".agents", "epic.md"), "   ");
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var composition = LoopRelayCompositionRoot.CreateForTests(repository);

        TransitionRuntimeResult result = await composition.TransitionRuntime.RunAsync(
            Request(
                WorkflowIdentity.EvalRoadmap,
                new WorkflowStageIdentity("Milestone Specification"),
                new WorkflowTransitionIdentity("GenerateMilestoneDeepDivesForEpic")));

        Assert.Equal(RuntimeOutcomeKind.MissingRequiredInput, result.Outcome);
        Assert.Equal(TransitionDurableState.InputUnsatisfied, result.DurableState);
        Assert.Contains("Active Epic prompt context is empty", result.Explanation, StringComparison.Ordinal);
        Assert.Contains(".agents/epic.md", result.Evidence);
    }

    [Fact]
    public void Execute_entry_rejects_milestone_cardinality_that_conflicts_with_strategic_context()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-plan-cardinality").FullName;
        try
        {
            string context = Path.Combine(root, ".agents", "ctx");
            string milestones = Path.Combine(root, ".agents", "milestones");
            Directory.CreateDirectory(context);
            Directory.CreateDirectory(milestones);
            File.WriteAllText(
                Path.Combine(context, "04-strategic-structure.md"),
                "# Strategic Structure\n\nCreate exactly one implementation milestone.\n");
            File.WriteAllText(Path.Combine(milestones, "m1.md"), "# M1\n");
            File.WriteAllText(Path.Combine(milestones, "m2.md"), "# M2\n");

            Assert.True(LoopRelayCompositionRoot.ExplicitSingleMilestoneInvariantViolated(root, out int actual));
            Assert.Equal(2, actual);

            File.Delete(Path.Combine(milestones, "m2.md"));
            Assert.False(LoopRelayCompositionRoot.ExplicitSingleMilestoneInvariantViolated(root, out actual));
            Assert.Equal(1, actual);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Composition_exposes_one_resolved_versioned_policy_for_the_invocation()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-policy").FullName;
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };

        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository);

        // Every consumer observes this single instance; without workspace or invocation input
        // the built-in defaults resolve with a deterministic versioned identity. This proves
        // same-process determinism only — cross-process stability (against .NET's per-process
        // string-hash randomization) rests on the canonical sorting asserted by
        // OperationalPolicyResolverTests.Canonical_serialization_sorts_permission_collections.
        Assert.StartsWith("pol_v1_", composition.Policy.PolicyId, StringComparison.Ordinal);
        Assert.Equal(32, composition.Policy.MaxUnboundedContinuationSteps);
        Assert.Equal(2, composition.Policy.MaxNoChangesCommits);
        Assert.Equal(2, composition.Policy.OperationalContextGrowthWarningStreak);
        Assert.True(composition.Policy.DecisionSessionResume);
        Assert.Equal(
            composition.Policy.PolicyId,
            LoopRelayCompositionRoot.CreateForTests(repository).Policy.PolicyId);
    }

    [Fact]
    public void Session_log_environment_variable_flows_through_the_invocation_layer()
    {
        // M7: LoopRelay_SESSION_LOG becomes an ambient invocation-layer input for
        // runtime.sessionTelemetry. Its old semantics were "anything but 0/false enables";
        // the raw value now flows through resolver validation, so a garbage value rejects
        // loudly instead of silently enabling telemetry.
        PolicyOverride telemetry = Assert.Single(
            LoopRelayCompositionRoot.CombineInvocationOverrides(
                null,
                name => name == "LoopRelay_SESSION_LOG" ? "0" : null));
        Assert.Equal(OperationalPolicyResolver.SessionTelemetryKey, telemetry.Key);
        Assert.Equal("env:LoopRelay_SESSION_LOG", telemetry.Origin);
        Assert.False(telemetry.IsExplicit);

        Assert.False(ResolveWithSessionLog("0").SessionTelemetry);
        Assert.False(ResolveWithSessionLog("false").SessionTelemetry);
        Assert.True(ResolveWithSessionLog("1").SessionTelemetry);
        Assert.Throws<PolicyResolutionException>(() => ResolveWithSessionLog("verbose"));

        static ResolvedOperationalPolicy ResolveWithSessionLog(string envValue) =>
            OperationalPolicyResolver.Resolve(
                CliPolicyDocument.Empty,
                "settings:test",
                LoopRelayCompositionRoot.CombineInvocationOverrides(
                    null,
                    name => name == "LoopRelay_SESSION_LOG" ? envValue : null),
                PermissionPolicyFactory.Minimum);
    }

    [Fact]
    public void SelectChain_returns_single_workflow_chain_for_bounded_plan_and_execute()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-bounded").FullName;
        var composition = LoopRelayCompositionRoot.CreateForTests(new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        });
        RepositoryObservation observation = EmptyObservation(repo);

        WorkflowChainDefinition plan = composition.SelectChain(
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            observation);
        WorkflowChainDefinition execute = composition.SelectChain(
            new WorkflowInvocation(InvocationModeKind.BoundedExecute),
            observation);

        Assert.Equal([WorkflowIdentity.Plan], plan.Workflows.Select(workflow => workflow.Identity));
        Assert.Equal([WorkflowIdentity.Execute], execute.Workflows.Select(workflow => workflow.Identity));
    }

    [Fact]
    public async Task Create_wires_canonical_observation_resolution_definitions_and_chains()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-composition").FullName;
        Directory.CreateDirectory(Path.Combine(repo, ".agents", "evals"));
        await File.WriteAllTextAsync(Path.Combine(repo, ".agents", "evals", "e1.md"), "# Eval");
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };

        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository);
        RepositoryObservation observation = await composition.ObserveAsync(CancellationToken.None);
        WorkflowResolutionResult resolution = composition.Resolve(
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            observation);
        WorkflowChainDefinition chain = composition.SelectChain(
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            observation);

        Assert.Same(repository, composition.Repository);
        Assert.IsType<TransitionRuntime>(composition.TransitionRuntime);
        Assert.IsType<OrchestrationKernel>(composition.OrchestrationKernel);
        Assert.Equal(4, composition.WorkflowDefinitions.Count);
        Assert.Equal(2, composition.WorkflowChains.Count);
        Assert.Equal(WorkflowIdentity.EvalRoadmap, resolution.Selection.SelectedWorkflow);
        Assert.Equal(WorkflowIdentity.EvalRoadmap, chain.InitialWorkflow);
        Assert.Equal(
            [WorkflowIdentity.EvalRoadmap, WorkflowIdentity.Plan, WorkflowIdentity.Execute],
            chain.Workflows.Select(workflow => workflow.Identity));
        Assert.True(observation.StorageAuthority.UsableAuthority);
    }

    [Fact]
    public void Decision_resume_kill_switch_flows_through_the_invocation_layer()
    {
        // Absent variable: no override at all — the workspace/built-in layers decide.
        Assert.Empty(LoopRelayCompositionRoot.CombineInvocationOverrides(null, _ => null));

        PolicyOverride disabled = Assert.Single(
            LoopRelayCompositionRoot.CombineInvocationOverrides(
                null,
                name => name == "LoopRelay_DECISION_RESUME" ? "0" : null));
        Assert.Equal(OperationalPolicyResolver.DecisionSessionResumeKey, disabled.Key);
        Assert.Equal("0", disabled.Value);
        Assert.Equal("env:LoopRelay_DECISION_RESUME", disabled.Origin);
        Assert.False(disabled.IsExplicit);

        // The raw value flows through resolver validation: "0"/"false" disable, "1"/"true"
        // enable, and a garbage value is rejected loudly instead of silently enabling resume.
        Assert.False(ResolveWithEnv("0").DecisionSessionResume);
        Assert.False(ResolveWithEnv("FALSE").DecisionSessionResume);
        Assert.True(ResolveWithEnv("1").DecisionSessionResume);
        Assert.True(ResolveWithEnv("true").DecisionSessionResume);
        Assert.Throws<PolicyResolutionException>(() => ResolveWithEnv("flase"));

        static ResolvedOperationalPolicy ResolveWithEnv(string envValue) =>
            OperationalPolicyResolver.Resolve(
                CliPolicyDocument.Empty,
                "settings:test",
                LoopRelayCompositionRoot.CombineInvocationOverrides(
                    null,
                    name => name == "LoopRelay_DECISION_RESUME" ? envValue : null),
                PermissionPolicyFactory.Minimum);
    }

    [Fact]
    public void Production_recovery_routes_legacy_session_tables_only_through_the_migration_compatibility_boundary()
    {
        string root = FindRepositoryRoot();
        string composition = ReadCompositionSource(root);
        string canonical = File.ReadAllText(Path.Combine(
            root, "src", "LoopRelay.Orchestration.Primitives", "Recovery", "CanonicalDecisionRecoveryStore.cs"));
        string transitionRecovery = File.ReadAllText(Path.Combine(
            root, "src", "LoopRelay.Orchestration.Primitives", "Runtime", "TransitionFaultsAndRecovery.cs"));

        Assert.Contains("new CanonicalDecisionRecoveryStore", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("executeRecoveryStore ??= new SqliteRecoveryStore", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("FROM session_recovery_", canonical, StringComparison.Ordinal);
        Assert.DoesNotContain("INTO session_recovery_", canonical, StringComparison.Ordinal);
        Assert.DoesNotContain("OperatorUnblock", transitionRecovery, StringComparison.Ordinal);
        Assert.DoesNotContain("TransitionRecoveryAction", transitionRecovery, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_feature_handlers_cannot_mutate_files_git_or_canonical_progress_directly()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root, "src", "LoopRelay.Cli", "Services", "Cli", "LoopRelayCompositionRoot.cs"));
        string executor = File.ReadAllText(Path.Combine(
            root, "src", "LoopRelay.Cli", "Services", "Cli", "CanonicalFeatureEffectExecutor.cs"));

        Assert.DoesNotContain("UpsertProductAsync", executor, StringComparison.Ordinal);
        Assert.DoesNotContain("UpsertStageStateAsync", executor, StringComparison.Ordinal);
        Assert.DoesNotContain("UpsertWorkflowStateAsync", executor, StringComparison.Ordinal);
        Assert.DoesNotContain("new CommitGate", executor, StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllText", executor, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.CreateDirectory", executor, StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllText", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.CreateDirectory", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new CommitGate", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".PublishAsync(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_decision_session_routes_loop_artifact_rotation_through_durable_effect_authority()
    {
        string root = FindRepositoryRoot();
        string composition = ReadCompositionSource(root);
        string decisionSession = File.ReadAllText(Path.Combine(
            root, "src", "LoopRelay.Cli", "Services", "Decisions", "DecisionSession.cs"));

        Assert.Contains("_artifactEffects: new DurableLoopArtifactEffectCoordinator", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("_artifacts.RotateOperationalDeltaAsync", decisionSession, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_cli_composition_does_not_construct_legacy_loop_runner()
    {
        string root = FindRepositoryRoot();
        string cliSource = Path.Combine(root, "src", "LoopRelay.Cli");
        string[] productionSources = Directory
            .EnumerateFiles(cliSource, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(
                Path.Combine("Services", "Execution", "LoopRunner.cs"),
                StringComparison.Ordinal))
            .ToArray();

        Assert.DoesNotContain(
            productionSources,
            path => File.ReadAllText(path).Contains("new LoopRunner", StringComparison.Ordinal));
        Assert.DoesNotContain(
            productionSources,
            path => File.ReadAllText(path).Contains("RoadmapStateMachine", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(cliSource, "Services", "Cli", "LoopCliComposition.cs")));
    }

    // The policy source descriptor is durable authority evidence: it lands in
    // `canonical_policy_resolutions.source_description` and `.provenance_json`, and in
    // `canonical_agent_role_policies.provenance` and `.document_json`. The settings file is
    // resolved from `AppContext.BaseDirectory` (or LOOPRELAY_SETTINGS_PATH), so it lives
    // outside the repository entirely — there is no workspace-relative form of it. Recording
    // the directory therefore pins a row to one machine's filesystem layout and leaks the
    // user's home directory into state that is otherwise a portable fact. The file name is
    // the whole of the information any reader could act on.
    [Theory]
    [InlineData(@"C:\Users\someone\AppData\Local\Temp\authority\settings.json", false, "settings:settings.json")]
    [InlineData(@"C:\Users\someone\AppData\Local\Temp\authority\settings.json", true, "settings:settings.json (default template)")]
    [InlineData("/home/someone/.local/share/looprelay/looprelay.settings.json", false, "settings:looprelay.settings.json")]
    [InlineData("settings.json", false, "settings:settings.json")]
    public void Policy_source_descriptor_names_the_settings_file_without_its_directory(
        string settingsPath,
        bool isDefaultTemplate,
        string expected)
    {
        // Both separators are asserted from one platform on purpose: a descriptor written on
        // Windows must read back the same way anywhere.
        string descriptor = LoopRelayCompositionRoot.DescribePolicySource(settingsPath, isDefaultTemplate);

        Assert.Equal(expected, descriptor);
        Assert.DoesNotContain(@"\", descriptor, StringComparison.Ordinal);
        Assert.DoesNotContain("/", descriptor, StringComparison.Ordinal);
        Assert.DoesNotContain("someone", descriptor, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Retired_plan_and_roadmap_compositions_are_not_available_as_active_authorities()
    {
        string root = FindRepositoryRoot();

        Assert.False(File.Exists(Path.Combine(
            root,
            "src",
            "LoopRelay.Plan.Cli",
            "Services",
            "Cli",
            "PlanCliComposition.cs")));
        Assert.False(File.Exists(Path.Combine(
            root,
            "src",
            "LoopRelay.Roadmap.Cli",
            "Services",
            "Cli",
            "RoadmapCliComposition.cs")));
    }

    [Fact]
    public async Task Verify_execute_entry_contract_stops_on_milestone_set_without_trackable_checkboxes()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-verify-milestone-checkbox").FullName;
        await WriteAsync(repo, ".agents/plan.md", "# Plan");
        await WriteAsync(repo, ".agents/operational_context.md", "# Operational Context");
        await WriteAsync(repo, ".agents/details.md", "# Details");
        await WriteAsync(repo, ".agents/milestones/m1.md", "# Milestone");
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var composition = LoopRelayCompositionRoot.CreateForTests(repository);
        RepositoryObservation before = await composition.ObserveAsync(CancellationToken.None);

        TransitionRuntimeResult result = await composition.TransitionRuntime.RunAsync(
            Request(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Workflow Completion"),
                new WorkflowTransitionIdentity("VerifyExecuteEntryContract")));

        ObservedProduct milestoneSet = Assert.Single(
            before.Products,
            product => product.Product.Identity == ProductIdentity.ExecutionMilestoneSet);
        Assert.False(milestoneSet.GateUsable);
        Assert.Equal(ProductValidationState.Invalid, milestoneSet.Product.ValidationState);
        Assert.Equal(RuntimeOutcomeKind.MissingRequiredInput, result.Outcome);
        Assert.Equal(TransitionDurableState.InputUnsatisfied, result.DurableState);
        Assert.Contains("Input gate unsatisfied", result.Explanation, StringComparison.Ordinal);
        Assert.Contains(".agents/milestones/m1.md", result.Evidence);
    }
}
