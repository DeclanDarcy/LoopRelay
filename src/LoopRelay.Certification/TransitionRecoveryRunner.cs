using System.Diagnostics;
using System.Text.Json;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Codex;
using LoopRelay.Agents.Services.Codex.Compatibility;
using LoopRelay.Agents.Services.Process;
using LoopRelay.Agents.Services.Usage;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Certification;

public sealed class TransitionRecoveryRunner(ICertificationFailureDiagnoser? failureDiagnoser = null)
{
    private static readonly WorkflowTransitionIdentity Transition = new("LiveRecoveryCanary");
    private static readonly ProductIdentity OutputProduct = new("LiveRecoveryEvidence");
    private static readonly WorkflowTransitionDefinition Definition = CreateDefinition();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<TransitionRecoveryCertificationResult> RunAsync(
        string codexExecutable,
        string authFile,
        string cliPath,
        string authorityRoot,
        CancellationToken cancellationToken = default)
    {
        string root = Path.Combine(authorityRoot, "transition-recovery", Guid.NewGuid().ToString("N"));
        string codexHome = Path.Combine(root, "codex-home");
        string retentionFallback = Path.Combine(root, "retained-fixture");
        Directory.CreateDirectory(codexHome);
        Directory.CreateDirectory(retentionFallback);
        File.Copy(authFile, Path.Combine(codexHome, "auth.json"));
        string? priorHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        string? priorExecutable = Environment.GetEnvironmentVariable("CODEX_EXECUTABLE");
        Environment.SetEnvironmentVariable("CODEX_HOME", codexHome);
        Environment.SetEnvironmentVariable("CODEX_EXECUTABLE", codexExecutable);
        Environment.SetEnvironmentVariable("CODEX_ANALYTICS_ENABLED", "false");
        var cases = new List<RecoveryBoundaryCaseResult>();
        string version = "unknown";
        string schema = "unknown";
        string? failedInvocationId = null;
        string? failedRepositoryPath = null;
        string? failedCaseIdentity = null;
        string? currentInvocationId = null;
        string? currentRepositoryPath = null;
        string? currentCaseIdentity = null;
        try
        {
            CodexInstalledCompatibilityIdentity identity = CodexCompatibilityIdentityProbe.Resolve();
            version = identity.ServerVersion ?? "unknown";
            schema = identity.SchemaDigest ?? "unknown";
            if (CodexCompatibilityManifest.LoadEmbedded().FindExact(version, schema) is null)
            {
                return await Finish(CertificationClassification.UnsupportedCapability);
            }

            cases.Add(await RunCase(
                "pre-submission-cancelled-salvage",
                TransitionBoundaryKind.PreSubmission,
                TransitionDurableState.Cancelled,
                TransitionRecoveryDisposition.Cancelled,
                TransitionDurableState.Completed,
                expectedProviderCalls: 1,
                expectedEffectCalls: 1,
                requirePublicRecovery: false));
            cases.Add(await RunCase(
                "accepted-reconcile-required",
                TransitionBoundaryKind.RequestAccepted,
                TransitionDurableState.ProviderOutcomeUnknown,
                TransitionRecoveryDisposition.ReconcileProvider,
                TransitionDurableState.ProviderOutcomeUnknown,
                expectedProviderCalls: 1,
                expectedEffectCalls: 0,
                requirePublicRecovery: true));
            cases.Add(await RunCase(
                "provider-complete-materialization",
                TransitionBoundaryKind.ProviderCompleted,
                TransitionDurableState.PromptCompleted,
                TransitionRecoveryDisposition.MaterializeCommittedOutput,
                TransitionDurableState.Completed,
                expectedProviderCalls: 1,
                expectedEffectCalls: 1,
                requirePublicRecovery: false));
            cases.Add(await RunCase(
                "uncertain-effect-fails-closed",
                TransitionBoundaryKind.DuringEffects,
                TransitionDurableState.EffectsPartiallyApplied,
                TransitionRecoveryDisposition.FailClosedUnknownSideEffect,
                TransitionDurableState.EffectsPartiallyApplied,
                expectedProviderCalls: 1,
                expectedEffectCalls: 1,
                requirePublicRecovery: true));
            cases.Add(await RunCase(
                "completion-persisted-zero-work-rerun",
                TransitionBoundaryKind.CompletionPersisted,
                TransitionDurableState.Completed,
                TransitionRecoveryDisposition.ReuseCompleted,
                TransitionDurableState.Completed,
                expectedProviderCalls: 1,
                expectedEffectCalls: 1,
                requirePublicRecovery: false));

            return await Finish(LiveProviderFailureClassifier.Classify(
                cases.All(item => item.Passed), codexHome));
        }
        catch (Exception exception) when (exception is not OperationCanceledException &&
            exception is not CertificationRetentionException)
        {
            failedInvocationId ??= currentInvocationId;
            failedRepositoryPath ??= currentRepositoryPath;
            failedCaseIdentity ??= currentCaseIdentity;
            cases.Add(new RecoveryBoundaryCaseResult(
                "suite-failure", "unknown", "unknown", "unknown", "unknown",
                0, 0, false, false, false, false, false, [exception.GetType().Name, exception.Message]));
            return await Finish(CertificationClassification.EnvironmentFailure);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", priorHome);
            Environment.SetEnvironmentVariable("CODEX_EXECUTABLE", priorExecutable);
            string authCopy = Path.Combine(codexHome, "auth.json");
            if (File.Exists(authCopy)) File.Delete(authCopy);
            if (Directory.Exists(root))
            {
                foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(root, recursive: true);
            }
        }

        async Task<RecoveryBoundaryCaseResult> RunCase(
            string caseIdentity,
            TransitionBoundaryKind fault,
            TransitionDurableState expectedInitial,
            TransitionRecoveryDisposition expectedDisposition,
            TransitionDurableState expectedRestart,
            int expectedProviderCalls,
            int expectedEffectCalls,
            bool requirePublicRecovery)
        {
            string repositoryPath = Path.Combine(root, caseIdentity, "repository");
            Directory.CreateDirectory(repositoryPath);
            await File.WriteAllTextAsync(Path.Combine(repositoryPath, "README.md"), "# recovery canary\n", cancellationToken);
            var repository = new Repository { Id = Guid.NewGuid(), Name = caseIdentity, Path = repositoryPath };
            string runId = $"m4-{caseIdentity}";
            var persistence = new CanonicalWorkflowPersistenceStore(repository);
            CanonicalTransitionExecutionContext execution = await SeedExecutionContextAsync(
                persistence,
                runId,
                cancellationToken);
            var live = new LivePromptExecutor(codexExecutable, repositoryPath, codexHome);
            currentRepositoryPath = repositoryPath;
            currentInvocationId = live.InvocationId;
            currentCaseIdentity = caseIdentity;
            var journal = new CanonicalTransitionBoundaryJournal(persistence, fault);
            var effects = new MarkerEffectExecutor(
                Path.Combine(repositoryPath, ".LoopRelay", "evidence", "recovery-effect.marker"),
                journal);

            TransitionRuntime firstRuntime = CreateRuntime(repository, live, journal);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            TransitionRuntimeResult initial = await firstRuntime.RunAsync(
                Request(execution, FreshAttemptAuthorization.Instance),
                timeout.Token);
            TransitionRunIdentity transitionRun = initial.TransitionRun
                ?? throw new InvalidOperationException("Initial transition run identity was not recorded.");
            if (initial.RequiredEffectsPending)
            {
                await CoordinateEffectsAsync(
                    initial,
                    execution,
                    repository,
                    persistence,
                    effects,
                    journal,
                    timeout.Token);
            }

            var restartedStore = new CanonicalTransitionRunStore(new CanonicalWorkflowPersistenceStore(repository));
            TransitionRunRecoverySnapshot snapshot = await restartedStore.LoadRecoveryAsync(transitionRun, cancellationToken)
                ?? throw new InvalidOperationException("Recovery snapshot was not durable across runtime reconstruction.");
            TransitionRecoveryDecision decision = TransitionRecoveryClassifier.Classify(snapshot);
            TransitionRecoveryPlan recoveryPlan = await new TransitionRecoveryCoordinator(
                    restartedStore,
                    new CanonicalTransitionRecoveryPlanStore(persistence))
                .PlanAsync(transitionRun, cancellationToken);
            var restartJournal = new CanonicalTransitionBoundaryJournal(persistence);
            if (recoveryPlan.Action == CanonicalRecoveryAction.RetryNewAttempt)
            {
                TransitionRuntime restartedRuntime = CreateRuntime(repository, live, restartJournal);
                TransitionRuntimeResult restarted = await restartedRuntime.RunAsync(
                    Request(execution, new RecoveryAttemptAuthorization(recoveryPlan)),
                    timeout.Token);
                if (restarted.RequiredEffectsPending)
                {
                    await CoordinateEffectsAsync(
                        restarted,
                        execution,
                        repository,
                        persistence,
                        effects,
                        restartJournal,
                        timeout.Token);
                }
            }
            else if (decision.Disposition == TransitionRecoveryDisposition.MaterializeCommittedOutput)
            {
                PromptExecutionResult persistedRaw = snapshot.RawOutput
                    ?? throw new InvalidOperationException("Materialization recovery requires durable raw output.");
                var deterministicTransport = new PersistedPromptExecutor(persistedRaw);
                TransitionRuntime restartedRuntime = CreateRuntime(repository, deterministicTransport, restartJournal);
                TransitionRecoveryPlan deterministicContinuation = recoveryPlan with
                {
                    Action = CanonicalRecoveryAction.RetryNewAttempt,
                    ResultingAttemptMode = RecoveryAttemptMode.RetryExistingTransitionRun,
                    NextAttemptIndex = 2,
                };
                TransitionRuntimeResult restarted = await restartedRuntime.RunAsync(
                    Request(execution, new RecoveryAttemptAuthorization(deterministicContinuation)),
                    timeout.Token);
                if (restarted.RequiredEffectsPending)
                {
                    await CoordinateEffectsAsync(
                        restarted,
                        execution,
                        repository,
                        persistence,
                        effects,
                        restartJournal,
                        timeout.Token);
                }
            }
            TransitionRunRecoverySnapshot afterRestart = await new CanonicalTransitionRunStore(
                new CanonicalWorkflowPersistenceStore(repository)).LoadRecoveryAsync(
                    transitionRun,
                    cancellationToken)
                ?? throw new InvalidOperationException("Restarted recovery snapshot was missing.");

            bool statusExposed = true;
            bool unblockFailedClosed = true;
            if (requirePublicRecovery)
            {
                ProcessResult status = await RunCli(cliPath, repositoryPath, ["status"], cancellationToken);
                ProcessResult unblock = await RunCli(cliPath, repositoryPath, ["unblock"], cancellationToken);
                statusExposed = status.ExitCode == 0 && decision.Disposition switch
                {
                    TransitionRecoveryDisposition.ReconcileProvider =>
                        status.StandardOutput.Contains("Pending dispatches:", StringComparison.Ordinal) &&
                        !status.StandardOutput.Contains("Pending dispatches: (none)", StringComparison.Ordinal),
                    TransitionRecoveryDisposition.FailClosedUnknownSideEffect =>
                        status.StandardOutput.Contains("Pending effects:", StringComparison.Ordinal) &&
                        status.StandardOutput.Contains("write-recovery-marker:Unknown", StringComparison.Ordinal),
                    _ => true,
                };
                string unblockOutput = unblock.StandardOutput + "\n" + unblock.StandardError;
                unblockFailedClosed = unblock.ExitCode != 0 &&
                    unblockOutput.Contains("Unknown or invalid command: unblock", StringComparison.Ordinal);
            }

            bool duplicateProvider = live.Calls > expectedProviderCalls;
            bool duplicateEffect = effects.Calls > expectedEffectCalls;
            bool singleRun = afterRestart.Causality.TransitionRun == transitionRun &&
                afterRestart.Causality.Run == execution.Run &&
                afterRestart.Causality.WorkflowInstance == execution.WorkflowInstance;
            bool passed = snapshot.State == expectedInitial &&
                decision.Disposition == expectedDisposition &&
                afterRestart.State == expectedRestart &&
                live.Calls == expectedProviderCalls &&
                effects.Calls == expectedEffectCalls &&
                !duplicateProvider && !duplicateEffect && singleRun &&
                statusExposed && unblockFailedClosed;
            if (!passed)
            {
                failedRepositoryPath = repositoryPath;
                failedInvocationId = live.InvocationId;
                failedCaseIdentity = caseIdentity;
            }
            return new RecoveryBoundaryCaseResult(
                caseIdentity,
                fault.ToString(),
                snapshot.State.ToString(),
                decision.Disposition.ToString(),
                afterRestart.State.ToString(),
                live.Calls,
                effects.Calls,
                duplicateProvider,
                duplicateEffect,
                statusExposed,
                unblockFailedClosed,
                passed,
                [
                    $"run:{runId}",
                    $"model:{CertificationFixtureSettings.BrainModel}",
                    $"durable-boundaries:{snapshot.Boundaries.Count}",
                    $"terminal-row:{afterRestart.State}",
                    $"provider:{live.Calls}",
                    $"effects:{effects.Calls}",
                ]);
        }

        async Task<TransitionRecoveryCertificationResult> Finish(CertificationClassification classification)
        {
            string scrubbed = string.Join("\n", cases.SelectMany(item => item.Evidence));
            IReadOnlyList<string> privacy = PrivacyScanner.Scan(scrubbed, authorityRoot);
            if (privacy.Count > 0) classification = CertificationClassification.OracleDrift;
            string? invocationId = classification == CertificationClassification.Passed
                ? null
                : failedInvocationId ?? currentInvocationId ?? $"transition-recovery-{Guid.NewGuid():N}";
            var result = new TransitionRecoveryCertificationResult(
                CertificationEvidenceSchema.Version, classification, version, schema, cases, privacy, invocationId);
            if (classification != CertificationClassification.Passed)
            {
                bool quota = LiveProviderFailureClassifier.HasQuotaExhaustion(codexHome);
                CertificationDiagnosisOutcome diagnosis = await (failureDiagnoser ?? new CertificationFailureDiagnoser())
                    .DiagnoseIfNeededAsync(
                        new CertificationFailureContext(
                            invocationId!,
                            currentInvocationId is not null,
                            classification,
                            quota,
                            failedCaseIdentity is null && currentCaseIdentity is null
                                ? "Transition-recovery certification failed before a live case completed."
                                : $"Transition-recovery case {failedCaseIdentity ?? currentCaseIdentity} failed.",
                            quota
                                ? ["codex-rollout:used-percent:100", "codex-rollout:last-agent-message:null"]
                                : cases.Where(item => !item.Passed).SelectMany(item => item.Evidence).ToArray(),
                            quota ? "Wait until the confirmed provider quota window resets before an explicit rerun." : null,
                            result,
                            authorityRoot,
                            failedRepositoryPath ?? currentRepositoryPath ?? retentionFallback,
                            codexHome,
                            codexExecutable,
                            CertificationSourceSelection.ResolveExisting(
                            [
                                "src/LoopRelay.Certification/TransitionRecoveryRunner.cs",
                                "src/LoopRelay.Orchestration.Primitives/Runtime/TransitionRuntime.cs",
                            ]),
                            failedCaseIdentity ?? currentCaseIdentity),
                        cancellationToken);
                result = result with { AttemptRecord = diagnosis.AttemptRecord, Diagnosis = diagnosis };
            }
            string path = Path.Combine(authorityRoot, "evidence", "transition-recovery.latest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using FileStream stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
            return result;
        }
    }

    private static TransitionRuntime CreateRuntime(
        Repository repository,
        IProviderPromptTransport promptTransport,
        ITransitionBoundaryJournal journal)
    {
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var runs = new CanonicalTransitionRunStore(persistence);
        var evidence = new CanonicalTransitionEvidenceStore(persistence);
        var products = new EmptyProductResolver();
        var context = new ContextBuilder();
        var promptStore = new CanonicalRenderedPromptFactStore(persistence);
        var promptRuntime = new FaultInjectingPromptExecutor(
            new LoadingPromptRuntimeDispatcher(promptStore, promptTransport),
            evidence,
            journal);
        var promptGateway = new PromptDispatchGateway(
            promptStore,
            new CanonicalPromptDispatchLifecycleStore(persistence),
            promptRuntime);
        return new TransitionRuntime(
            new DefinitionResolver(),
            products,
            new SatisfiedGateEvaluator(),
            context,
            new PromptRenderer(),
            promptGateway,
            new OutputInterpreter(),
            new CanonicalCandidateProductStore(persistence),
            new ProductValidator(),
            new SnapshotInputFreshnessValidator(products, context),
            runs,
            new CanonicalAttemptStore(persistence),
            new NoOpReadReceiptStore(),
            evidence,
            new CanonicalTransitionGateEvaluationStore(persistence),
            new CanonicalTransitionCommitStore(persistence),
            journal);
    }

    private static TransitionRuntimeRequest Request(
        CanonicalTransitionExecutionContext execution,
        AttemptAuthorization authorization)
    {
        return new TransitionRuntimeRequest(
            WorkflowIdentity.TraditionalRoadmap,
            new WorkflowStageIdentity("Live Recovery"),
            Transition,
            execution,
            authorization,
            new Dictionary<string, string> { ["case"] = "transition-recovery" });
    }

    private static async Task<CanonicalTransitionExecutionContext> SeedExecutionContextAsync(
        CanonicalWorkflowPersistenceStore persistence,
        string runId,
        CancellationToken cancellationToken)
    {
        WorkspaceIdentity workspace = new(await persistence.ReadWorkspaceIdentityAsync(cancellationToken));
        var run = new RunIdentity(runId);
        WorkflowInstanceIdentity instance = WorkflowInstanceIdentity.New();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await persistence.UpsertRunAsync(new RunRecord(
            run.Value,
            workspace.Value,
            "transition-recovery-live-recovery",
            InvocationModeKind.BoundedTraditional.ToString(),
            "Active",
            now,
            null,
            null,
            "certification"), cancellationToken);
        await persistence.UpsertWorkflowInstanceAsync(new WorkflowInstanceRecord(
            instance.Value,
            run.Value,
            WorkflowIdentity.TraditionalRoadmap,
            "transition-recovery",
            "Active",
            now,
            null,
            null), cancellationToken);
        return new CanonicalTransitionExecutionContext(
            new WorkflowInvocation(InvocationModeKind.BoundedTraditional),
            workspace,
            run,
            instance,
            new PolicyIdentity("policy_transition_recovery"),
            new RuntimeProfileIdentity("runtime_transition_recovery"),
            new PromptPolicyProfileIdentity("prompt_policy_transition_recovery"));
    }

    private static WorkflowTransitionDefinition CreateDefinition()
    {
        GateDefinition gate = new(
            new GateIdentity("LiveRecovery.Gate"),
            "Deterministic live recovery gate.",
            [new GateRequirementDefinition("LiveRecovery.Explainable", "Canary gate is explainable.", null, DependencyStrength.Required, true)],
            "certification runtime",
            "Failure blocks progress.");
        ProductDefinition product = new(
            OutputProduct, WorkflowIdentity.TraditionalRoadmap, Transition,
            [WorkflowIdentity.TraditionalRoadmap], "disposable repository", "independent canary",
            ProductLifecycle.Active, ProductValidationState.Valid, ProductFreshness.Fresh, [], ["recovery-canary"]);
        return new WorkflowTransitionDefinition(
            Transition,
            "Low-cost live recovery transition.",
            [],
            gate,
            "LiveRecoveryCanary",
            ExecutionPosture.OneShotAgentPrompt,
            [product],
            gate,
            ["LiveRecoveryValidator"],
            [new EffectDefinition(new EffectIdentity("write-recovery-marker"), EffectCategory.Evidence,
                "after validation", [], [OutputProduct], 0, "Failure blocks completion.")],
            [], [],
            new RecoveryDefinition("LiveRecovery.Recovery", "Recover from durable boundary evidence.",
                ["restart", "resume", "reconcile", "rerun"], ["silent repair", "discard state"]));
    }

    private sealed class DefinitionResolver : ITransitionDefinitionResolver
    {
        public Task<WorkflowTransitionDefinition> ResolveAsync(TransitionRuntimeRequest request, CancellationToken token) => Task.FromResult(Definition);
        public Task<IReadOnlyList<WorkflowTransitionIdentity>> ResolveEligibleSuccessorsAsync(
            WorkflowTransitionDefinition definition, IReadOnlyList<ProductRecord> products, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<WorkflowTransitionIdentity>>([]);
    }

    private sealed class EmptyProductResolver : IProductResolver
    {
        public Task<ProductResolutionResult> ResolveAsync(IReadOnlyList<ProductRequirement> requirements, CancellationToken token) =>
            Task.FromResult(new ProductResolutionResult([], [], [], [], []));
    }

    private sealed class SatisfiedGateEvaluator : IGateEvaluator
    {
        public Task<GateResult> EvaluateInputGateAsync(GateDefinition gate, ProductResolutionResult inputs, InputGateEvaluationContext context, CancellationToken token) =>
            Task.FromResult(Satisfied(gate));
        public Task<GateResult> EvaluateOutputGateAsync(GateDefinition gate, ProductValidationResult validation, CancellationToken token) =>
            Task.FromResult(Satisfied(gate));
        private static GateResult Satisfied(GateDefinition gate) => new(
            GateStatus.Satisfied,
            gate.Requirements.Select(item => new GateRequirementResult(item.Identity, GateStatus.Satisfied, "satisfied", ["canary"])).ToArray(),
            "Canary gate satisfied.", ["canary"]);
    }

    private sealed class ContextBuilder : IPromptContextBuilder
    {
        public Task<PromptContext> BuildAsync(TransitionRuntimeRequest request, WorkflowTransitionDefinition definition,
            ProductResolutionResult inputs, CancellationToken token)
        {
            IReadOnlyDictionary<string, string> metadata = request.Metadata ?? new Dictionary<string, string>();
            TransitionInputSnapshot snapshot = TransitionInputSnapshotHasher.Create(definition, [], metadata);
            return Task.FromResult(new PromptContext(definition, inputs, snapshot, metadata, []));
        }
    }

    private sealed class PromptRenderer : IPromptRenderer
    {
        public Task<RenderedPrompt> RenderAsync(PromptRenderRequest request, CancellationToken token) =>
            Task.FromResult(new RenderedPrompt(
                new PromptTemplateIdentity(request.Definition.PromptIdentity),
                request.PolicyProfile,
                "Reply with exactly RECOVERY_OK. Do not call tools or inspect files.",
                "certification/live-recovery"));
    }

    private sealed class LivePromptExecutor(
        string executable,
        string repository,
        string codexHome) : IProviderPromptTransport
    {
        public string InvocationId { get; } = CertificationInvocation.NewId();
        public int Calls { get; private set; }

        public async Task<PromptExecutionResult> DispatchAsync(
            PersistedRenderedPromptFact prompt,
            AuthorizedPromptDispatch dispatch,
            CancellationToken token)
        {
            Calls++;
            IAgentProcess process = await new ProcessRunner().StartInteractiveAsync(
                executable, ["app-server", "--listen", "stdio://"], repository, token);
            var spec = new AgentSessionSpec(
                SessionIdentity.New(), "m4-live-recovery", SessionRole.OperationalExecution,
                new SandboxProfile("read-only", false, false, false),
                CertificationFixtureSettings.BrainAgentModel, AgentEffort.Low, AgentConfigurationAuthority.Brain, repository);
            var session = new CodexAppServerSession(spec, process, new DeterministicAgentTokenEstimator());
            string sessionId = session.SessionId.Value.ToString();
            string? providerThreadId = null;
            AgentTurnResult result = await CertificationDirectTurnLifecycle.RunAndRecordAsync(
                session,
                async cancellationToken =>
                {
                    AgentTurnResult turn = await session.RunTurnAsync(
                        prompt.Fact.RenderedContent,
                        cancellationToken: cancellationToken);
                    providerThreadId = session.ThreadId;
                    return turn;
                },
                (turn, cancellationToken) => CertificationInvocation.RecordDirectTurnAsync(
                    repository,
                    codexHome,
                    InvocationId,
                    sessionId,
                    turn.TurnIndex,
                    providerThreadId,
                    turn.ProviderTurnId,
                    cancellationToken),
                token);
            return new PromptExecutionResult(
                result.State == AgentTurnState.Completed ? PromptExecutionStatus.Completed : PromptExecutionStatus.Failed,
                result.Output,
                TimeSpan.Zero,
                new Dictionary<string, string> { ["provider-turn"] = result.ProviderTurnId ?? "(unknown)" },
                result.Diagnostics);
        }
    }

    private sealed class PersistedPromptExecutor(PromptExecutionResult result) : IProviderPromptTransport
    {
        public Task<PromptExecutionResult> DispatchAsync(
            PersistedRenderedPromptFact prompt,
            AuthorizedPromptDispatch dispatch,
            CancellationToken token) => Task.FromResult(result);
    }

    private sealed class OutputInterpreter : IOutputInterpreter
    {
        public Task<InterpretedTransitionOutput> InterpretAsync(
            CanonicalCausalContext causality,
            WorkflowTransitionDefinition definition, PromptExecutionResult result, CancellationToken token)
        {
            bool valid = result.Status == PromptExecutionStatus.Completed && result.RawOutput.Contains("RECOVERY_OK", StringComparison.Ordinal);
            return Task.FromResult(new InterpretedTransitionOutput(
                valid ? OutputInterpretationStatus.Valid : OutputInterpretationStatus.Malformed,
                valid ? [Product()] : [],
                valid ? "Independent acceptance token observed." : "Acceptance token missing.",
                ["oracle:RECOVERY_OK"]));
        }
    }

    private sealed class ProductValidator : IProductValidator
    {
        public Task<ProductValidationResult> ValidateAsync(
            WorkflowTransitionDefinition definition, InterpretedTransitionOutput output, CancellationToken token)
        {
            bool valid = output.Status == OutputInterpretationStatus.Valid && output.CandidateProducts.Count == 1;
            return Task.FromResult(new ProductValidationResult(
                valid ? ProductValidationStatus.Valid : ProductValidationStatus.Invalid,
                output.CandidateProducts,
                valid ? [] : [OutputProduct],
                [], [], [],
                valid ? "Canary product validated." : "Canary product invalid.",
                output.Evidence));
        }
    }

    private sealed class NoOpReadReceiptStore : IReadReceiptStore
    {
        public Task AppendAsync(ReadReceiptCapture capture, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class MarkerEffectExecutor(
        string path,
        ITransitionBoundaryJournal journal) : ITransitionEffectIntentExecutor
    {
        public int Calls { get; private set; }
        public async Task<EffectExecutionRecord> ExecuteAsync(
            CanonicalCausalContext causality,
            EffectIdentity effect,
            CancellationToken token)
        {
            Calls++;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "effect-applied\n", token);
            var boundary = new TransitionBoundaryObservation(
                causality,
                Transition,
                TransitionBoundaryKind.DuringEffects,
                100 + Calls,
                DateTimeOffset.UtcNow,
                "effect-coordinator",
                null,
                [effect.Value]);
            await journal.RecordAsync(boundary, CancellationToken.None);
            if (journal.ShouldInterrupt(boundary))
            {
                throw new TransitionFaultInjectedException(boundary);
            }
            return new EffectExecutionRecord(
                effect, EffectExecutionStatus.Succeeded,
                "Independent effect marker written.", ["recovery-effect.marker"]);
        }
    }

    private static async Task CoordinateEffectsAsync(
        TransitionRuntimeResult attempt,
        CanonicalTransitionExecutionContext execution,
        Repository repository,
        CanonicalWorkflowPersistenceStore persistence,
        MarkerEffectExecutor effects,
        ITransitionBoundaryJournal journal,
        CancellationToken cancellationToken)
    {
        if (attempt.TransitionRun is not { } transitionRun || attempt.Attempt is not { } attemptIdentity)
        {
            throw new InvalidOperationException("Effect coordination requires durable causal identities.");
        }

        var causality = new CanonicalCausalContext(
            execution.Workspace,
            execution.Run,
            execution.WorkflowInstance,
            transitionRun,
            attemptIdentity);
        await ObserveAsync(TransitionBoundaryKind.BeforeEffects, 90);
        var workStore = new CanonicalEffectWorkStore(repository);
        TransitionEffectExecutorAdapter[] typedExecutors = (await workStore.ReadPlanAsync(
                transitionRun, cancellationToken))
            .Where(item => item.Intent.Executor.Value.StartsWith(
                "canonical-transition-effect:", StringComparison.Ordinal))
            .Select(item => new TransitionEffectExecutorAdapter(
                effects,
                item.Intent.Executor,
                new EffectIdentity(item.Intent.Target.Identity)))
            .ToArray();
        var worker = new LoopRelay.Orchestration.Effects.EffectWorker(
            "certification-m4",
            workStore,
            new LoopRelay.Orchestration.Effects.EffectExecutorRegistry(typedExecutors),
            new TransitionalFeatureEffectReconciler(workStore),
            TimeSpan.FromMinutes(1));
        TransitionEffectCoordinationResult result = await new TransitionEffectCoordinator(
                workStore,
                worker,
                new CanonicalEffectPlanSettlementStore(repository))
            .CoordinateAsync(transitionRun, cancellationToken);
        if (result.RequiredEffectsPending || result.Failed)
        {
            return;
        }

        await ObserveAsync(TransitionBoundaryKind.EffectsApplied, 190);
        try
        {
            await ObserveAsync(TransitionBoundaryKind.CompletionPersisted, 200);
        }
        catch (TransitionFaultInjectedException)
        {
            // Completion was already committed by the effect state store. The injected process
            // boundary models a crash immediately after that durable write.
        }

        async Task ObserveAsync(TransitionBoundaryKind boundaryKind, int sequence)
        {
            var observation = new TransitionBoundaryObservation(
                causality,
                Transition,
                boundaryKind,
                sequence,
                DateTimeOffset.UtcNow,
                "effect-coordinator",
                null,
                [boundaryKind.ToString()]);
            await journal.RecordAsync(observation, CancellationToken.None);
            if (journal.ShouldInterrupt(observation))
            {
                throw new TransitionFaultInjectedException(observation);
            }
        }
    }

    private static ProductRecord Product() => new(
        OutputProduct, WorkflowIdentity.TraditionalRoadmap, Transition,
        [WorkflowIdentity.TraditionalRoadmap], "disposable repository", "independent canary",
        ["recovery-canary"], "m4-live-recovery-product", ProductFreshness.Fresh,
        ProductValidationState.Valid, ProductLifecycle.Active, ["oracle:RECOVERY_OK"]);

    private static async Task<ProcessResult> RunCli(
        string cliPath, string repository, IReadOnlyList<string> arguments, CancellationToken token)
    {
        var start = new ProcessStartInfo
        {
            FileName = cliPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? "dotnet" : cliPath,
            WorkingDirectory = repository,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (cliPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(cliPath);
        start.ArgumentList.Add("--repo");
        start.ArgumentList.Add(repository);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("CLI did not start.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(token);
        Task<string> stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
