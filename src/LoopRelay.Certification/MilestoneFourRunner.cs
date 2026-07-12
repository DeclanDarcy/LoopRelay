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
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Certification;

public sealed class MilestoneFourRunner
{
    private static readonly WorkflowTransitionIdentity Transition = new("LiveRecoveryCanary");
    private static readonly ProductIdentity OutputProduct = new("LiveRecoveryEvidence");
    private static readonly WorkflowTransitionDefinition Definition = CreateDefinition();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<MilestoneFourCertificationResult> RunAsync(
        string codexExecutable,
        string authFile,
        string cliPath,
        string authorityRoot,
        CancellationToken cancellationToken = default)
    {
        string root = Path.Combine(authorityRoot, "milestone-4", Guid.NewGuid().ToString("N"));
        string codexHome = Path.Combine(root, "codex-home");
        Directory.CreateDirectory(codexHome);
        File.Copy(authFile, Path.Combine(codexHome, "auth.json"));
        string? priorHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        string? priorExecutable = Environment.GetEnvironmentVariable("CODEX_EXECUTABLE");
        Environment.SetEnvironmentVariable("CODEX_HOME", codexHome);
        Environment.SetEnvironmentVariable("CODEX_EXECUTABLE", codexExecutable);
        Environment.SetEnvironmentVariable("CODEX_ANALYTICS_ENABLED", "false");
        var cases = new List<RecoveryBoundaryCaseResult>();
        string version = "unknown";
        string schema = "unknown";
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
                "pre-submission-safe-retry",
                TransitionBoundaryKind.PreSubmission,
                TransitionDurableState.Cancelled,
                TransitionRecoveryDisposition.SafeRetry,
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

            return await Finish(cases.All(item => item.Passed)
                ? CertificationClassification.Passed
                : CertificationClassification.ProductRegression);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
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
            var live = new LivePromptExecutor(codexExecutable, repositoryPath);
            var effects = new MarkerEffectExecutor(Path.Combine(repositoryPath, ".LoopRelay", "evidence", "recovery-effect.marker"));

            TransitionRuntime firstRuntime = CreateRuntime(repository, live, effects, fault);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            TransitionRuntimeResult initial = await firstRuntime.RunAsync(Request(runId), timeout.Token);
            TransitionRunIdentity transitionRun = initial.TransitionRun
                ?? throw new InvalidOperationException("Initial transition run identity was not recorded.");

            var restartedStore = new CanonicalTransitionRunStore(new CanonicalWorkflowPersistenceStore(repository));
            TransitionRunRecoverySnapshot snapshot = await restartedStore.LoadRecoveryAsync(transitionRun, cancellationToken)
                ?? throw new InvalidOperationException("Recovery snapshot was not durable across runtime reconstruction.");
            TransitionRecoveryDecision decision = TransitionRecoveryClassifier.Classify(snapshot);
            TransitionRuntime restartedRuntime = CreateRuntime(repository, live, effects, interruptAt: null);
            TransitionRuntimeResult restarted = await restartedRuntime.RunAsync(Request(runId), timeout.Token);
            TransitionRunRecoverySnapshot afterRestart = await new CanonicalTransitionRunStore(
                new CanonicalWorkflowPersistenceStore(repository)).LoadRecoveryAsync(
                    restarted.TransitionRun ?? transitionRun,
                    cancellationToken)
                ?? throw new InvalidOperationException("Restarted recovery snapshot was missing.");

            bool statusExposed = true;
            bool unblockFailedClosed = true;
            if (requirePublicRecovery)
            {
                ProcessResult status = await RunCli(cliPath, repositoryPath, ["status"], cancellationToken);
                ProcessResult unblock = await RunCli(cliPath, repositoryPath, ["unblock"], cancellationToken);
                statusExposed = status.ExitCode == 0 &&
                    status.StandardOutput.Contains("Transition recovery markers:", StringComparison.Ordinal) &&
                    !status.StandardOutput.Contains("Transition recovery markers: (none)", StringComparison.Ordinal);
                unblockFailedClosed = unblock.ExitCode == 4 &&
                    unblock.StandardOutput.Contains("Non-recoverable blockers remain:", StringComparison.Ordinal);
            }

            bool duplicateProvider = live.Calls > expectedProviderCalls;
            bool duplicateEffect = effects.Calls > expectedEffectCalls;
            bool singleRun = afterRestart.Causality.TransitionRun == transitionRun;
            bool passed = initial.DurableState == expectedInitial &&
                decision.Disposition == expectedDisposition &&
                restarted.DurableState == expectedRestart &&
                live.Calls == expectedProviderCalls &&
                effects.Calls == expectedEffectCalls &&
                !duplicateProvider && !duplicateEffect && singleRun &&
                statusExposed && unblockFailedClosed;
            return new RecoveryBoundaryCaseResult(
                caseIdentity,
                fault.ToString(),
                initial.DurableState.ToString(),
                decision.Disposition.ToString(),
                restarted.DurableState.ToString(),
                live.Calls,
                effects.Calls,
                duplicateProvider,
                duplicateEffect,
                statusExposed,
                unblockFailedClosed,
                passed,
                [
                    $"run:{runId}",
                    $"durable-boundaries:{snapshot.Boundaries.Count}",
                    $"terminal-row:{afterRestart.State}",
                    $"provider:{live.Calls}",
                    $"effects:{effects.Calls}",
                ]);
        }

        async Task<MilestoneFourCertificationResult> Finish(CertificationClassification classification)
        {
            string scrubbed = string.Join("\n", cases.SelectMany(item => item.Evidence));
            IReadOnlyList<string> privacy = PrivacyScanner.Scan(scrubbed, authorityRoot);
            if (privacy.Count > 0) classification = CertificationClassification.OracleDrift;
            var result = new MilestoneFourCertificationResult(
                CertificationRunner.ResultSchemaVersion, classification, version, schema, cases, privacy);
            string path = Path.Combine(authorityRoot, "evidence", "milestone-4.latest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using FileStream stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
            return result;
        }
    }

    private static TransitionRuntime CreateRuntime(
        Repository repository,
        LivePromptExecutor live,
        MarkerEffectExecutor effects,
        TransitionBoundaryKind? interruptAt)
    {
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var runs = new CanonicalTransitionRunStore(persistence);
        var evidence = new CanonicalTransitionEvidenceStore(persistence);
        var journal = new CanonicalTransitionBoundaryJournal(persistence, interruptAt);
        var products = new EmptyProductResolver();
        var context = new ContextBuilder();
        var promptStore = new CanonicalRenderedPromptFactStore(persistence);
        var promptRuntime = new FaultInjectingPromptExecutor(
            new LoadingPromptRuntimeDispatcher(promptStore, live),
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

    private static TransitionRuntimeRequest Request(string runId)
    {
        var invocation = new WorkflowInvocation(InvocationModeKind.BoundedTraditional);
        return new TransitionRuntimeRequest(
            WorkflowIdentity.TraditionalRoadmap,
            new WorkflowStageIdentity("Live Recovery"),
            Transition,
            new CanonicalTransitionExecutionContext(
                invocation,
                WorkspaceIdentity.New(),
                new RunIdentity(runId),
                WorkflowInstanceIdentity.New(),
                new PolicyIdentity("policy_milestone_4"),
                new RuntimeProfileIdentity("runtime_milestone_4"),
                new PromptPolicyProfileIdentity("prompt_policy_milestone_4")),
            FreshAttemptAuthorization.Instance,
            new Dictionary<string, string> { ["case"] = "milestone-4" });
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
        public Task<GateResult> EvaluateInputGateAsync(GateDefinition gate, ProductResolutionResult inputs, CancellationToken token) =>
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

    private sealed class LivePromptExecutor(string executable, string repository) : IProviderPromptTransport
    {
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
                AgentModel.Gpt56Luna, AgentEffort.Low, AgentConfigurationAuthority.Brain, repository);
            await using var session = new CodexAppServerSession(spec, process, new DeterministicAgentTokenEstimator());
            AgentTurnResult result = await session.RunTurnAsync(prompt.Fact.RenderedContent, cancellationToken: token);
            return new PromptExecutionResult(
                result.State == AgentTurnState.Completed ? PromptExecutionStatus.Completed : PromptExecutionStatus.Failed,
                result.Output,
                TimeSpan.Zero,
                new Dictionary<string, string> { ["provider-turn"] = result.ProviderTurnId ?? "(unknown)" },
                result.Diagnostics);
        }
    }

    private sealed class OutputInterpreter : IOutputInterpreter
    {
        public Task<InterpretedTransitionOutput> InterpretAsync(
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

    private sealed class MarkerEffectExecutor(string path) : IEffectExecutor
    {
        public int Calls { get; private set; }
        public async Task<EffectExecutionResult> ExecuteAsync(
            WorkflowTransitionDefinition definition,
            ProductValidationResult validation,
            EffectExecutionContext context,
            CancellationToken token)
        {
            Calls++;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "effect-applied\n", token);
            EffectExecutionRecord record = new(
                new EffectIdentity("write-recovery-marker"), EffectExecutionStatus.Succeeded,
                "Independent effect marker written.", ["recovery-effect.marker"]);
            return new EffectExecutionResult(EffectExecutionStatus.Succeeded, [record], "Effect applied.", record.Evidence);
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
