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

public sealed class CompositionRootExecuteWorkflowAndArtifactTests : CompositionRootTestBase
{
    [Fact]
    public async Task Plan_scoped_artifact_transitions_execute_with_operation_profiles_and_persist_products()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-scoped-artifacts").FullName;
        await WriteAsync(repo, ".agents/plan.md", "# Plan v1\n\nImplement capability.");
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
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Equal("collect-details", spec.OperationPermissionProfile?.Label);
            Assert.Contains("Scoped Operation Contract", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "details.md"), "# Details\n\nShared implementation detail.");
            return new AgentTurnResult(0, AgentTurnState.Completed, "details", AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal("extract-milestones", spec.OperationPermissionProfile?.Label);
            Assert.Contains("Required output glob: .agents/milestones/m*.md", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v2\n\nRewritten with milestone markers.");
            Directory.CreateDirectory(Path.Combine(repo, ".agents", "milestones"));
            File.WriteAllText(
                Path.Combine(repo, ".agents", "milestones", "m1.md"),
                "# Milestone 1\n\n- [ ] Implement scoped artifact runtime.");
            return new AgentTurnResult(1, AgentTurnState.Completed, "milestones", AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal("extract-details", spec.OperationPermissionProfile?.Label);
            Assert.Contains(".agents/milestones/m*.md", prompt, StringComparison.Ordinal);
            Assert.Contains("Preserve write-glob file set: true", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "details.md"), "# Details\n\nRefined shared implementation detail.");
            return new AgentTurnResult(2, AgentTurnState.Completed, "refined", AgentTokenUsage.Zero);
        }));
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository, runtime);

        TransitionRuntimeResult collect = await RunPlanAsync(
            composition, "Execution Preparation", "CollectExecutionDetails");
        TransitionRuntimeResult milestones = await RunPlanAsync(
            composition, "Execution Preparation", "GenerateExecutionMilestones");
        // RefineExecutionDetails declares `.agents/details.md` and `.agents/milestones/`, so the
        // collected details and generated milestones must be committed before refinement reads them.
        await GitWorkspace.CommitAgentsInputsAsync(repo);
        TransitionRuntimeResult refine = await RunPlanAsync(
            composition, "Execution Preparation", "RefineExecutionDetails");

        Assert.True(collect.Outcome == RuntimeOutcomeKind.Completed, collect.Explanation);
        Assert.True(milestones.Outcome == RuntimeOutcomeKind.Completed, milestones.Explanation);
        Assert.True(refine.Outcome == RuntimeOutcomeKind.Completed, refine.Explanation);
        Assert.Equal(3, runtime.OpenSessions);
        Assert.Equal(3, runtime.ClosedSessions);
        Assert.Equal(3, runtime.SessionCalls.Count);
        Assert.Collection(
            runtime.OpenedSpecs.Select(spec => spec.OperationPermissionProfile),
            profile =>
            {
                Assert.NotNull(profile);
                Assert.Equal("collect-details", profile.Label);
                Assert.Equal([OrchestrationArtifactPaths.Plan], profile.AllowedReads);
                Assert.Equal([OrchestrationArtifactPaths.Details], profile.AllowedWrites);
            },
            profile =>
            {
                Assert.NotNull(profile);
                Assert.Equal("extract-milestones", profile.Label);
                Assert.Equal([OrchestrationArtifactPaths.Plan, OrchestrationArtifactPaths.Details], profile.AllowedReads);
                Assert.Equal([OrchestrationArtifactPaths.Plan], profile.AllowedWrites);
                Assert.Equal(".agents/milestones", Assert.Single(profile.AllowedWriteGlobs).Directory);
            },
            profile =>
            {
                Assert.NotNull(profile);
                Assert.Equal("extract-details", profile.Label);
                Assert.Equal([OrchestrationArtifactPaths.Details], profile.AllowedReads);
                Assert.Equal([OrchestrationArtifactPaths.Details], profile.AllowedWrites);
                Assert.Equal(".agents/milestones", Assert.Single(profile.AllowedWriteGlobs).Directory);
            });
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.ExecutionDetails &&
            product.ProducerTransition == new WorkflowTransitionIdentity("RefineExecutionDetails") &&
            product.CausalIdentity.Length == 64);
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.ExecutionMilestoneSet &&
            product.ProducerTransition == new WorkflowTransitionIdentity("RefineExecutionDetails"));
        await AssertEffectStateAsync(repository, "persist-execution-details", EffectLifecycle.Succeeded);
        await AssertEffectStateAsync(repository, "persist-execution-milestones", EffectLifecycle.Succeeded);
        Assert.True(File.Exists(Path.Combine(
            repo,
            ".LoopRelay",
            "evidence",
            "plan-scoped-artifact",
            "RefineExecutionDetails.md")));
    }

    [Fact]
    public async Task Execute_workflow_transitions_run_through_canonical_runtime()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-execute-full-runtime").FullName;
        await WriteAsync(repo, ".agents/epic.md", ValidEvalActiveEpic());
        await WriteAsync(repo, ".agents/core/roadmap-completion-context.md", "# Roadmap Completion Context\n\nCurrent.");
        await WriteAsync(repo, ".agents/plan.md", "# Plan\n\nImplement the runtime capability.");
        await WriteAsync(repo, ".agents/operational_context.md", "# Operational Context\n\nUse the plan.");
        await WriteAsync(repo, ".agents/operational_delta.md", "# Operational Delta\n\nUpdate context.");
        await WriteAsync(repo, ".agents/details.md", "# Details\n\nImplementation detail.");
        await WriteAsync(repo, ".agents/handoffs/handoff.md", "# Prior Handoff\n\nPrevious slice.");
        await WriteAsync(repo, ".agents/milestones/m1.md", "# Milestone 1\n\n- [ ] Implement Execute runtime.");
        await WriteProjectContextAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        var git = new FakeProcessRunner
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
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Decision, spec.Role);
            Assert.Contains("Operational Context", prompt, StringComparison.Ordinal);
            return new AgentTurnResult(0, AgentTurnState.Completed, "# Decisions\n\nImplement feature.cs.", AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
            Assert.Contains("# Plan", prompt, StringComparison.Ordinal);
            Assert.Contains("# Details", prompt, StringComparison.Ordinal);
            Assert.Contains("# Decisions", prompt, StringComparison.Ordinal);
            Assert.Contains("continue executing the current milestone", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("integration is not wired", prompt, StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.Combine(repo, "src"));
            File.WriteAllText(Path.Combine(repo, "src", "feature.cs"), "namespace Test; public static class Feature { }\n");
            File.WriteAllText(
                Path.Combine(repo, ".agents", "milestones", "m1.md"),
                "# Milestone 1\n\n- [x] Implement Execute runtime.");
            // ImplementationSlice is a filesystem-authoritative collaboration product (M3):
            // the slice evidence file must exist on disk for downstream transitions to observe it.
            Directory.CreateDirectory(Path.Combine(repo, ".agents", "evidence", "execution"));
            File.WriteAllText(
                Path.Combine(repo, ".agents", "evidence", "execution", "slice-0001.md"),
                "# Implementation Slice\n\nImplemented feature.cs.");
            return new AgentTurnResult(1, AgentTurnState.Completed, "implemented feature", AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Contains("handoff", prompt, StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.Combine(repo, ".agents", "handoffs"));
            File.WriteAllText(
                Path.Combine(repo, ".agents", "handoffs", "handoff.md"),
                "# Handoff\n\nFeature implemented.");
            return new AgentTurnResult(2, AgentTurnState.Completed, "wrote handoff", AgentTokenUsage.Zero);
        }));
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Contains("EvaluateEpicCompletionAndDrift", prompt, StringComparison.Ordinal);
            return new AgentTurnResult(3, AgentTurnState.Completed, ValidProjection("# Epic Completion Evaluation Projection", "EvaluateEpicCompletionAndDrift"), AgentTokenUsage.Zero);
        }));
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
            Assert.Contains("Evaluate Epic Completion And Drift", prompt, StringComparison.Ordinal);
            return new AgentTurnResult(4, AgentTurnState.Completed, Evaluation("Fully Complete", "None", "Close Epic"), AgentTokenUsage.Zero);
        }));
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
            Assert.Contains("Epic Synthesis Prompt", prompt, StringComparison.Ordinal);
            Assert.Contains("trusted runtime owns persistence", prompt, StringComparison.OrdinalIgnoreCase);
            return new AgentTurnResult(5, AgentTurnState.Completed, """
                # Completed Epic

                ## 1. Epic Purpose

                Preserve the completed capability.

                ## 2. Current State

                Capability complete.
                """, AgentTokenUsage.Zero);
        }));
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Contains("UpdateRoadmapCompletionContext", prompt, StringComparison.Ordinal);
            return new AgentTurnResult(6, AgentTurnState.Completed, ValidProjection("# Roadmap Completion Update Projection", "UpdateRoadmapCompletionContext"), AgentTokenUsage.Zero);
        }));
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
            Assert.Contains("Update Roadmap Completion Context", prompt, StringComparison.Ordinal);
            return new AgentTurnResult(7, AgentTurnState.Completed, "# Roadmap Completion Context\n\nUpdated.", AgentTokenUsage.Zero);
        }));
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository, runtime, git);
        RunIdentity executeRun = RunIdentity.New();

        TransitionRuntimeResult planReady = await RunPlanAsync(composition, "Workflow Completion", "VerifyExecuteEntryContract");
        TransitionRuntimeResult readiness = await RunExecuteAsync(composition, "Execution Readiness", "VerifyExecutionReadiness", executeRun);
        TransitionRuntimeResult decision = await RunExecuteAsync(
            composition,
            "Implementation Planning",
            "GenerateDecision",
            executeRun);
        Assert.Equal(RuntimeOutcomeKind.Completed, decision.Outcome);
        Assert.Equal(new WorkflowTransitionIdentity("GenerateDecision"), decision.Transition);
        TransitionRuntimeResult implementation = await RunExecuteAsync(composition, "Implementation", "ExecuteImplementationSlice", executeRun);
        TransitionRuntimeResult handoff = await RunExecuteAsync(composition, "Execution Continuity", "GenerateHandoff", executeRun);
        TransitionRuntimeResult updateContext = await RunExecuteAsync(composition, "Execution Continuity", "UpdateOperationalContext", executeRun);
        TransitionRuntimeResult publish = await RunExecuteAsync(composition, "Execution Continuity", "PublishRepositoryState", executeRun);
        TransitionRuntimeResult commit = await RunExecuteAsync(composition, "Execution Continuity", "EvaluateCommit", executeRun);
        TransitionRuntimeResult milestones = await RunExecuteAsync(composition, "Completion", "EvaluateMilestoneCompletion", executeRun);
        TransitionRuntimeResult review = await RunExecuteAsync(composition, "Completion", "RunNonImplementationReview", executeRun);
        TransitionRuntimeResult certification = await RunExecuteAsync(composition, "Completion", "RunCompletionCertification", executeRun);
        Assert.NotNull(await new CanonicalCheckpointStore(repository).ReadAsync<CompletionCertificationCheckpoint>(CanonicalCheckpointKeys.CompletionCertification, CancellationToken.None));
        await composition.DisposeAsync();
        composition = LoopRelayCompositionRoot.CreateForTests(repository, runtime, git);
        TransitionRuntimeResult route = await RunExecuteAsync(composition, "Completion", "InterpretCompletionRoute", executeRun);
        await composition.DisposeAsync();
        composition = LoopRelayCompositionRoot.CreateForTests(repository, runtime, git);
        var exitInvocation = new WorkflowInvocation(InvocationModeKind.BoundedExecute);
        RepositoryObservation exitObservation = await composition.ObserveAsync(CancellationToken.None);
        string workspaceId = await composition.Persistence.ReadWorkspaceIdentityAsync(CancellationToken.None);
        KernelResult exitKernel = await composition.OrchestrationKernel.RunAsync(new KernelCommand(
            exitInvocation,
            exitObservation,
            composition.SelectChain(exitInvocation, exitObservation),
            composition.WorkflowCatalog,
            new WorkflowRunContext(
                new WorkspaceIdentity(workspaceId),
                executeRun,
                new PolicyIdentity(composition.Policy.PolicyId),
                composition.RuntimeProfile,
                composition.PromptPolicyProfile,
                composition.AgentRolePolicy.Identity),
            ObservationBudget: 1));
        WorkflowControllerResult exitController = Assert.IsType<WorkflowControllerResult>(
            exitKernel.ChainResult?.ControllerResult);
        TransitionRuntimeResult exit = Assert.IsType<TransitionRuntimeResult>(exitController.Transition);
        Assert.True(
            exitController.StopReason == WorkflowStopReason.TransitionCompleted,
            exitController.Explanation + " | " + exit.Explanation);
        Assert.Equal(new WorkflowTransitionIdentity("VerifyWorkflowExitGate"), exit.Transition);
        Assert.Equal(RuntimeOutcomeKind.EffectsPending, exit.Outcome);
        Assert.Equal(
            RuntimeOutcomeKind.Completed,
            Assert.IsType<TransitionEffectCoordinationResult>(exitController.EffectCoordination).Outcome);

        Assert.All(
            [planReady, readiness, decision, implementation, handoff, updateContext, publish, commit, milestones, review, certification, route],
            result => Assert.True(result.Outcome == RuntimeOutcomeKind.Completed, result.Explanation));
        await composition.DisposeAsync();
        Assert.Equal(2, runtime.OpenSessions);
        Assert.Equal(2, runtime.ClosedSessions);
        Assert.Equal(4, runtime.SessionCalls.Count);
        Assert.Equal(5, runtime.OneShotCalls.Count);
        AgentSessionSpec decisionSpec = runtime.OpenedSpecs.Single(spec => spec.Role == SessionRole.Decision);
        Assert.Equal(AgentModel.Gpt56Sol, decisionSpec.Model);
        Assert.Equal(AgentEffort.XHigh, decisionSpec.Effort);
        Assert.Equal(AgentConfigurationAuthority.Brain, decisionSpec.ConfigurationAuthority);
        AgentSessionSpec executionSpec = runtime.OpenedSpecs.Single(
            spec => spec.Role == SessionRole.OperationalExecution);
        Assert.Equal(AgentModel.Gpt56Sol, executionSpec.Model);
        Assert.Equal(AgentEffort.XHigh, executionSpec.Effort);
        Assert.True(File.Exists(Path.Combine(repo, ".agents", "archive", "epics", "1.md")));
        Assert.True(File.Exists(Path.Combine(repo, ".agents", "archive", "epics", "1", "handoffs", "handoff.0001.md")));
        Assert.True(File.Exists(Path.Combine(repo, ".agents", "archive", "epics", "1", "handoffs", "handoff.md")));
        Assert.True(File.Exists(Path.Combine(repo, ".agents", "archive", "epics", "1", "deltas", "operational_delta.0001.md")));
        Assert.True(File.Exists(Path.Combine(repo, ".agents", "archive", "epics", "1", "decisions", "decisions.0001.md")));
        Assert.Equal(
            "# Roadmap Completion Context\n\nUpdated.",
            await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "core", "roadmap-completion-context.md")));
        string recoveryDirectory = Path.Combine(repo, ".LoopRelay", "evidence", "execute-completion-recovery");
        string recoveryEvidence = string.Join(
            "\n",
            Directory.GetFiles(recoveryDirectory, "*.md").Select(File.ReadAllText));
        Assert.Contains("Completion review", recoveryEvidence, StringComparison.Ordinal);
        Assert.Contains("Evaluate epic completion and drift", recoveryEvidence, StringComparison.Ordinal);
        Assert.Contains("Archive completed execution workspace", recoveryEvidence, StringComparison.Ordinal);
        Assert.Contains("Synthesize completed epic", recoveryEvidence, StringComparison.Ordinal);
        Assert.Contains("Update roadmap completion context", recoveryEvidence, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(recoveryDirectory, "final-closed-state-persistence.md")));
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.Contains(snapshot.WorkflowStates, state =>
            state.Workflow == WorkflowIdentity.Execute &&
            state.State == WorkflowResolutionState.Completed &&
            state.CurrentStage is null);
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.CertifiedCompletion &&
            product.StorageRepresentations.Contains(".LoopRelay/evidence/local-verification/VerifyWorkflowExitGate.md"));
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.CompletionRoute &&
            product.StorageRepresentations.Contains(".LoopRelay/evidence/execute-review/InterpretCompletionRoute-output.md"));
        EffectWorkItem completionArchiveEffect = Assert.Single(
            await new CanonicalEffectWorkStore(repository).ReadBySemanticOperationAsync(
                "completion:archive-and-synthesize",
                CancellationToken.None));
        Assert.Equal(EffectLifecycle.Succeeded, completionArchiveEffect.State);
        Assert.True(completionArchiveEffect.Receipt?.PostconditionSatisfied);
        await AssertEffectStateAsync(repository, "record-certified-completion", EffectLifecycle.Succeeded);
        CanonicalCompletionSnapshot completionSnapshot = await new CanonicalCompletionAuthorityStore(repository)
            .ReadSnapshotAsync(CancellationToken.None);
        CertifiedTerminalFact terminalFact = Assert.Single(completionSnapshot.TerminalFacts);
        CompletionClosurePlan terminalPlan = Assert.Single(completionSnapshot.ClosurePlans);
        Assert.All(
            terminalPlan.Operations.Where(operation =>
                operation.Kind != CompletionClosureOperationKind.CertifiedTerminalFact),
            operation => Assert.Contains(terminalFact.EffectReceipts,
                receipt => receipt.OperationIdentity == operation.Identity));
        IReadOnlyList<EffectWorkItem> closureEffects = await new CanonicalEffectWorkStore(repository)
            .ReadRunAsync(executeRun, CancellationToken.None);
        string[] expectedClosureEffects =
        [
            "completion:archive-and-synthesize",
            "completion:roadmap-context-materialization",
            "completion:nested-agents-commit",
            "completion:nested-agents-push",
            "completion:parent-gitlink-commit",
            "completion:parent-working-tree-commit",
            "completion:parent-repository-push",
            "transition-effect:record-completion-route",
            "transition-effect:record-certified-completion",
            "completion:retire-decision-continuity",
            "completion:retire-execution-warm-session",
            "completion:retire-certification-checkpoint",
        ];
        Assert.All(expectedClosureEffects, semantic =>
        {
            EffectWorkItem effect = Assert.Single(closureEffects,
                item => item.Intent.SemanticOperationKey == semantic);
            Assert.Equal(EffectLifecycle.Succeeded, effect.State);
            Assert.True(effect.Receipt?.PostconditionSatisfied);
        });
        await using (LoopRelayCompositionRoot terminalComposition = LoopRelayCompositionRoot.CreateForTests(repository, runtime, git))
        {
            LoopRelayResult terminalResult = await new LoopRelayApplication(
                new CanonicalCliApplicationService(terminalComposition)).ExecuteAsync(
                new CompletionOperationRequest(
                    new ApplicationRequestContext(
                        ApplicationCorrelationId.New(), repository.Id.ToString("N"), repo,
                        new Dictionary<string, string>()),
                    CompletionOperationKind.Status));
            Assert.Equal(ApplicationOutcomeKind.Completed, terminalResult.Outcome);
            Assert.Equal(0, terminalResult.SuggestedExitCode);
            Assert.Contains("certified terminal", terminalResult.Reason, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Null(await new CanonicalCheckpointStore(repository).ReadAsync<CompletionCertificationCheckpoint>(CanonicalCheckpointKeys.CompletionCertification, CancellationToken.None));
        Assert.Null(await new CanonicalCheckpointStore(repository).ReadAsync<ExecutionWarmSessionContinuity>(CanonicalCheckpointKeys.ExecutionWarmSession, CancellationToken.None));
        RepositoryObservation terminalObservation = await new RepositoryObserver().ObserveAsync(repo);
        Assert.True(Assert.Single(
            terminalObservation.Products,
            product => product.Product.Identity == ProductIdentity.CertifiedCompletion).GateUsable);

        int sessionCallsBeforeRerun = runtime.SessionCalls.Count;
        int oneShotCallsBeforeRerun = runtime.OneShotCalls.Count;
        LoopRelayCompositionRoot rerunComposition = LoopRelayCompositionRoot.CreateForTests(repository, runtime, git);
        WorkflowControllerResult rerun = await rerunComposition.WorkflowController.RunAsync(
            ControllerRequest(
                new WorkflowInvocation(InvocationModeKind.BoundedExecute),
                await rerunComposition.ObserveAsync(CancellationToken.None),
                rerunComposition.WorkflowDefinitions));
        await rerunComposition.DisposeAsync();
        Assert.Equal(WorkflowStopReason.ChainCompleted, rerun.StopReason);
        Assert.Null(rerun.Transition);
        Assert.Equal(sessionCallsBeforeRerun, runtime.SessionCalls.Count);
        Assert.Equal(oneShotCallsBeforeRerun, runtime.OneShotCalls.Count);
    }

    [Fact]
    public async Task Execute_implementation_rejects_and_rolls_back_milestone_file_set_changes()
    {
        (string repo, Repository repository, FakeAgentRuntime runtime, FakeProcessRunner process) =
            await PrepareExecuteContinuityCaseAsync("cc-cli-unified-execute-milestone-file-set");
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "# Decisions\n\nImplement the feature.", AgentTokenUsage.Zero)));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            File.WriteAllText(
                Path.Combine(repo, ".agents", "milestones", "m1.md"),
                "# Milestone 1\n\n- [x] Create feature.");
            File.WriteAllText(
                Path.Combine(repo, ".agents", "milestones", "m1-completion-evidence.md"),
                "# Duplicate M1\n\n- [x] Evidence.");
            return new AgentTurnResult(1, AgentTurnState.Completed, "implemented", AgentTokenUsage.Zero);
        }));

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
        Assert.Contains("milestone file identity set", result.Explanation, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(repo, ".agents", "milestones", "m1-completion-evidence.md")));
        Assert.Contains(
            "- [ ] Create feature.",
            await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "milestones", "m1.md")),
            StringComparison.Ordinal);
        Assert.Null(await new CanonicalCheckpointStore(repository).ReadAsync<ExecutionWarmSessionContinuity>(
            CanonicalCheckpointKeys.ExecutionWarmSession,
            CancellationToken.None));
    }

    [Fact]
    public async Task Plan_warm_session_materializes_structurally_valid_returned_plan_when_tool_write_is_absent()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-returned-markdown").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Active Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        await WriteProjectContextAsync(repo);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(repo), Path = repo };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => new AgentTurnResult(
            0,
            AgentTurnState.Completed,
            """
            # Executable Plan

            ## Milestone 1 — Implement Capability

            Implement the bounded repository capability, preserve its independent verifier, run the verifier,
            and record the exact acceptance result before checking the milestone completion item.
            """,
            AgentTokenUsage.Zero)));
        await using LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository, runtime);

        TransitionRuntimeResult result = await RunPlanAsync(composition, "Planning", "WriteExecutablePlan");

        Assert.Equal(RuntimeOutcomeKind.Completed, result.Outcome);
        string plan = await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "plan.md"));
        Assert.StartsWith("# Executable Plan", plan, StringComparison.Ordinal);
        Assert.Contains("Milestone 1", plan, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvalRoadmap_prompt_transition_renders_generated_prompt_asset_before_executor_integration()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-eval-prompt").FullName;
        await WriteAsync(repo, ".agents/eval-architectural-catalog.md", "# Architectural Catalog");
        await WriteProjectContextAsync(repo);
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
                new WorkflowStageIdentity("Eval DAG"),
                new WorkflowTransitionIdentity("CreateEvalDag")));

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Contains("Prompt execution integration is not wired", result.Explanation, StringComparison.Ordinal);
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        EvalPromptAsset asset = EvalPromptAssetCatalog.GetByTransition(new WorkflowTransitionIdentity("CreateEvalDag"));
        CanonicalRenderedPromptRecord fact = Assert.Single(
            await new CanonicalWorkflowPersistenceStore(repository).ReadRenderedPromptsAsync());
        Assert.Equal(asset.PromptIdentity, fact.PromptIdentity);
        Assert.Equal(asset.SourceHash, fact.TemplateSourceHash);
    }

    [Fact]
    public async Task Plan_warm_session_prompt_success_without_plan_file_fails_product_validation()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-warm-session-missing-plan").FullName;
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
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "claimed success", AgentTokenUsage.Zero)));
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository, runtime);

        TransitionRuntimeResult result = await composition.TransitionRuntime.RunAsync(
            Request(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Planning"),
                new WorkflowTransitionIdentity("WriteExecutablePlan")));

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Equal(TransitionDurableState.Failed, result.DurableState);
        Assert.Contains("completed without `.agents/plan.md`", result.Explanation, StringComparison.Ordinal);
        Assert.Equal(1, runtime.OpenSessions);
        await composition.DisposeAsync();
        Assert.Equal(1, runtime.ClosedSessions);
    }
}
