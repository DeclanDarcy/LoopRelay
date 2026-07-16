using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Agents.Services.Codex.Compatibility;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.ProjectContext;
using LoopRelay.Infrastructure.Services.Artifacts;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;
using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Certification;

public sealed class PlanWorkflowRunner(ICertificationFailureDiagnoser? failureDiagnoser = null)
{
    private static readonly string[] Transitions =
    [
        "WriteExecutablePlan",
        "GenerateAdversarialProjection",
        "RunAdversarialReview",
        "RevisePlan",
        "GenerateOperationalContext",
        "CollectExecutionDetails",
        "GenerateExecutionMilestones",
        "RefineExecutionDetails",
        "VerifyExecuteEntryContract",
    ];

    private static readonly ProductIdentity[] ExecuteEntryProducts =
    [
        ProductIdentity.ExecutablePlan,
        ProductIdentity.OperationalContext,
        ProductIdentity.ExecutionDetails,
        ProductIdentity.ExecutionMilestoneSet,
        ProductIdentity.ExecutionReadiness,
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<PlanWorkflowCertificationResult> RunAsync(
        string codexExecutable,
        string authFile,
        string cliPath,
        string authorityRoot,
        CancellationToken cancellationToken = default)
    {
        string root = Path.Combine(authorityRoot, "plan-workflow", Guid.NewGuid().ToString("N"));
        string codexHome = Path.Combine(root, "codex-home");
        string retentionFallback = Path.Combine(root, "retained-fixture");
        Directory.CreateDirectory(codexHome);
        Directory.CreateDirectory(retentionFallback);
        File.Copy(authFile, Path.Combine(codexHome, "auth.json"));
        string? priorHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        string? priorExecutable = Environment.GetEnvironmentVariable("CODEX_EXECUTABLE");
        string? priorAnalytics = Environment.GetEnvironmentVariable("CODEX_ANALYTICS_ENABLED");
        string? priorSettings = Environment.GetEnvironmentVariable("LOOPRELAY_SETTINGS_PATH");
        Environment.SetEnvironmentVariable("CODEX_HOME", codexHome);
        Environment.SetEnvironmentVariable("CODEX_EXECUTABLE", codexExecutable);
        Environment.SetEnvironmentVariable("CODEX_ANALYTICS_ENABLED", "false");
        string settingsPath = await CertificationFixtureSettings.WriteAsync(
            root, cliPath, cancellationToken);
        Environment.SetEnvironmentVariable("LOOPRELAY_SETTINGS_PATH", settingsPath);
        var cases = new List<PlanProducerCaseResult>();
        string version = "unknown";
        string schema = "unknown";
        string? failedInvocationId = null;
        string? lastInvocationId = null;
        string? failedTransition = null;
        string? failedRepositoryPath = null;
        string? lastRepositoryPath = null;
        bool preserveCase = false;

        try
        {
            CodexInstalledCompatibilityIdentity identity = CodexCompatibilityIdentityProbe.Resolve();
            version = identity.ServerVersion ?? "unknown";
            schema = identity.SchemaDigest ?? "unknown";
            if (CodexCompatibilityManifest.LoadEmbedded().FindExact(version, schema) is null)
            {
                return await Finish(CertificationClassification.UnsupportedCapability);
            }

            // Certification campaigns are candidate-bound runtime evidence. Never reuse a prior
            // live summary here: provider/schema compatibility does not identify the CLI binary,
            // its referenced assemblies, prompt assets, workflow catalog, or fixture oracle.
            cases.Add(await RunProducerCaseAsync(WorkflowIdentity.TraditionalRoadmap));
            cases.Add(await RunProducerCaseAsync(WorkflowIdentity.EvalRoadmap));
            return await Finish(LiveProviderFailureClassifier.Classify(
                cases.All(item => item.Passed), codexHome));
        }
        catch (Exception exception) when (exception is not OperationCanceledException &&
            exception is not CertificationRetentionException)
        {
            cases.Add(new PlanProducerCaseResult(
                "suite-failure", [], false, false, false, false, false, false, false,
                [exception.GetType().Name, exception.Message]));
            return await Finish(CertificationClassification.EnvironmentFailure);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", priorHome);
            Environment.SetEnvironmentVariable("CODEX_EXECUTABLE", priorExecutable);
            Environment.SetEnvironmentVariable("CODEX_ANALYTICS_ENABLED", priorAnalytics);
            Environment.SetEnvironmentVariable("LOOPRELAY_SETTINGS_PATH", priorSettings);
            string authCopy = Path.Combine(codexHome, "auth.json");
            if (File.Exists(authCopy)) File.Delete(authCopy);
            if (!preserveCase && Directory.Exists(root))
            {
                foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(root, recursive: true);
            }
        }

        async Task<PlanProducerCaseResult> RunProducerCaseAsync(WorkflowIdentity producer)
        {
            string caseIdentity = producer == WorkflowIdentity.EvalRoadmap ? "eval-producer" : "traditional-producer";
            string repositoryPath = Path.Combine(root, caseIdentity, "repository");
            lastRepositoryPath = repositoryPath;
            Directory.CreateDirectory(repositoryPath);
            await SeedRepositoryAsync(repositoryPath, cancellationToken);
            await InitializeGitAsync(
                repositoryPath,
                Path.Combine(root, caseIdentity, "repository-remote.git"),
                Path.Combine(root, caseIdentity, "agents-remote.git"),
                cancellationToken);
            HashSet<int> initialCodex = CodexProcessIds();
            ProcessResult init = await RunCliAsync(cliPath, repositoryPath, ["storage", "init"], cancellationToken);
            if (init.ExitCode != 0)
            {
                failedRepositoryPath = repositoryPath;
                return FailedCase(producer, $"storage-init:{init.ExitCode}:{init.StandardError}");
            }

            var repository = new Repository
            {
                Id = Guid.NewGuid(),
                Name = caseIdentity,
                Path = repositoryPath,
            };
            await SeedProducerProductsAsync(repository, producer, cancellationToken);

            var transitionResults = new List<PlanTransitionCaseResult>();
            foreach (string expectedTransition in Transitions)
            {
                Dictionary<string, string> before = SnapshotAgents(repositoryPath);
                ProcessResult run = await RunCliAsync(cliPath, repositoryPath, ["plan"], cancellationToken);
                lastInvocationId = run.CertificationInvocationId;
                Dictionary<string, string> after = SnapshotAgents(repositoryPath);
                string[] changed = ChangedPaths(before, after);
                string? actualTransition = ParseTransition(run.StandardOutput);
                bool completed = run.ExitCode == 0 && string.Equals(actualTransition, expectedTransition, StringComparison.Ordinal);
                if (!completed)
                {
                    failedRepositoryPath = repositoryPath;
                    failedInvocationId = run.CertificationInvocationId;
                    failedTransition = expectedTransition;
                }
                bool mutationValid = AllowedMutations(expectedTransition, changed);
                var diagnostics = CompactDiagnostics(actualTransition, run).ToList();
                if (!completed && actualTransition is not null)
                {
                    diagnostics.AddRange(await EffectDiagnosticsAsync(
                        repository, actualTransition, cancellationToken));
                }
                transitionResults.Add(new PlanTransitionCaseResult(
                    expectedTransition,
                    run.ExitCode,
                    changed,
                    mutationValid,
                    completed,
                    diagnostics));
                if (!completed) break;
            }

            CanonicalWorkflowPersistenceSnapshot snapshot =
                await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync(cancellationToken);
            (string? writeThread, string? writeContinuity) = PromptMetadata(snapshot, "WriteExecutablePlan");
            (string? reviseThread, string? reviseContinuity) = PromptMetadata(snapshot, "RevisePlan");
            bool sameThread = !string.IsNullOrWhiteSpace(writeThread) &&
                string.Equals(writeThread, reviseThread, StringComparison.Ordinal);
            bool restarted = string.Equals(reviseContinuity, "resumed-after-restart", StringComparison.Ordinal);
            ProductIdentity[] actualEntryProducts = snapshot.Products
                .Where(product => ExecuteEntryProducts.Contains(product.Identity))
                .Select(product => product.Identity)
                .Distinct()
                .OrderBy(product => product.Value, StringComparer.Ordinal)
                .ToArray();
            bool exactProducts = actualEntryProducts.SequenceEqual(
                ExecuteEntryProducts.OrderBy(product => product.Value, StringComparer.Ordinal));
            bool planCompleted = snapshot.WorkflowStates.Any(state =>
                state.Workflow == WorkflowIdentity.Plan &&
                state.State == LoopRelay.Orchestration.Resolution.WorkflowResolutionState.Completed &&
                state.CurrentStage is null);
            bool executeNotStarted = snapshot.WorkflowStates.All(state => state.Workflow != WorkflowIdentity.Execute) &&
                snapshot.TransitionRuns.All(run => run.Workflow != WorkflowIdentity.Execute);
            bool bounded = planCompleted && executeNotStarted;
            bool rollback = await VerifyScopedRollbackAsync(Path.Combine(root, caseIdentity, "rollback"));
            await Task.Delay(500, cancellationToken);
            bool processesClean = CodexProcessIds().All(pid => initialCodex.Contains(pid));
            bool passed = transitionResults.Count == Transitions.Length &&
                transitionResults.All(result => result.Completed && result.MutationScopeValid) &&
                sameThread && restarted && exactProducts && bounded && rollback && processesClean;
            if (!passed)
            {
                failedRepositoryPath = repositoryPath;
                failedInvocationId ??= lastInvocationId;
                failedTransition ??= transitionResults.LastOrDefault()?.Transition;
            }
            return new PlanProducerCaseResult(
                producer.Value,
                transitionResults,
                sameThread,
                restarted,
                exactProducts,
                bounded,
                rollback,
                processesClean,
                passed,
                [
                    $"producer:{producer.Value}",
                    $"model:{CertificationFixtureSettings.BrainModel}",
                    $"effort:{CertificationFixtureSettings.BrainEffort}",
                    $"authoring-thread-digest:{Digest(writeThread ?? string.Empty)}",
                    $"revision-continuity:{reviseContinuity ?? "missing"}",
                    $"execute-entry-products:{string.Join(',', actualEntryProducts.Select(product => product.Value))}",
                    $"plan-completed:{planCompleted}",
                    $"execute-not-started:{executeNotStarted}",
                ]);
        }

        PlanProducerCaseResult FailedCase(WorkflowIdentity producer, string diagnostic) => new(
            producer.Value, [], false, false, false, false, false, false, false, [diagnostic]);

        async Task<PlanWorkflowCertificationResult> Finish(CertificationClassification classification)
        {
            string scrubbed = string.Join("\n", cases.SelectMany(item => item.Evidence)
                .Concat(cases.SelectMany(item => item.Transitions).SelectMany(item => item.Diagnostics)));
            IReadOnlyList<string> privacy = PrivacyScanner.Scan(scrubbed, authorityRoot);
            if (privacy.Count > 0) classification = CertificationClassification.OracleDrift;
            preserveCase = CertificationCaseRetention.ShouldPreserve(false, classification);
            string? invocationId = classification == CertificationClassification.Passed
                ? null
                : failedInvocationId ?? lastInvocationId ?? $"plan-workflow-{Guid.NewGuid():N}";
            var result = new PlanWorkflowCertificationResult(
                CertificationEvidenceSchema.Version, classification, version, schema, cases, privacy, invocationId);
            if (classification != CertificationClassification.Passed)
            {
                bool quota = LiveProviderFailureClassifier.HasQuotaExhaustion(codexHome);
                CertificationDiagnosisOutcome diagnosis = await (failureDiagnoser ?? new CertificationFailureDiagnoser())
                    .DiagnoseIfNeededAsync(
                        new CertificationFailureContext(
                            invocationId!,
                            invocationId == failedInvocationId || lastInvocationId is not null,
                            classification,
                            quota,
                            FailureExplanation(cases),
                            quota
                                ? ["codex-rollout:used-percent:100", "codex-rollout:last-agent-message:null"]
                                : cases.SelectMany(item => item.Evidence)
                                    .Concat(cases.SelectMany(item => item.Transitions)
                                        .SelectMany(item => item.Diagnostics)).ToArray(),
                            quota ? "Wait until the confirmed provider quota window resets before an explicit rerun." : null,
                            result,
                            authorityRoot,
                            failedRepositoryPath ?? lastRepositoryPath ?? retentionFallback,
                            codexHome,
                            codexExecutable,
                            CertificationSourceSelection.ResolveExisting(
                            [
                                "src/LoopRelay.Certification/PlanWorkflowRunner.cs",
                                "src/LoopRelay.Orchestration.Primitives/Workflows/CanonicalWorkflowDefinitionSketches.cs",
                            ]),
                            failedTransition ?? cases.LastOrDefault()?.Transitions.LastOrDefault()?.Transition),
                        cancellationToken);
                result = result with { AttemptRecord = diagnosis.AttemptRecord, Diagnosis = diagnosis };
            }
            string path = Path.Combine(authorityRoot, "evidence", "plan-workflow.latest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using FileStream stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
            return result;
        }
    }

    private static async Task SeedRepositoryAsync(string root, CancellationToken token)
    {
        await WriteAsync(root, "README.md", "# Plan certification repository\n\nA disposable repository for a tiny text-only capability.\n", token);
        await WriteAsync(root, ".agents/epic.md", """
            # Epic: Add a deterministic greeting capability

            ## Strategic Purpose
            Provide a tiny, independently verifiable text capability.

            ## Desired Capability
            Add `GREETING.md` containing the exact line `Hello from Loop Relay.` during Execute.

            ## Acceptance Criteria
            - `GREETING.md` exists.
            - Its only content is `Hello from Loop Relay.` followed by a newline.

            ## Milestone Roadmap
            | MilestoneID | MilestoneName | Purpose | CompletionSignal |
            |---|---|---|---|
            | M1 | Greeting | Add the deterministic greeting | Exact file content matches |
            """, token);
        await WriteAsync(root, ".agents/specs/m1.md", """
            # Milestone M1: Greeting

            Create `GREETING.md` with exactly `Hello from Loop Relay.` and a trailing newline.
            Verification: compare the file bytes to the required UTF-8 text.
            """, token);
        int index = 0;
        foreach (string relative in ProjectContextSourceContract.SourceFiles)
        {
            index++;
            await WriteAsync(root, relative, $"# Project Context {index}\n\nPlan a minimal, deterministic, repository-scoped text change.\n", token);
        }
    }

    private static async Task SeedProducerProductsAsync(
        Repository repository,
        WorkflowIdentity producer,
        CancellationToken token)
    {
        string producerTransition = producer == WorkflowIdentity.EvalRoadmap
            ? "VerifyEvalPlanEntryContract"
            : "VerifyTraditionalPlanEntryContract";
        var store = new CanonicalWorkflowPersistenceStore(repository);
        await store.UpsertProductAsync(new ProductRecord(
            ProductIdentity.PreparedEpic,
            producer,
            new WorkflowTransitionIdentity(producerTransition),
            [WorkflowIdentity.Plan],
            "repository-owned certification seed",
            "independent plan-workflow fixture",
            [".agents/epic.md"],
            Digest(await File.ReadAllTextAsync(Path.Combine(repository.Path, ".agents", "epic.md"), token)),
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            [".agents/epic.md"]), token);
        await store.UpsertProductAsync(new ProductRecord(
            ProductIdentity.MilestoneSpecificationSet,
            producer,
            new WorkflowTransitionIdentity(producerTransition),
            [WorkflowIdentity.Plan],
            "repository-owned certification seed",
            "independent plan-workflow fixture",
            [".agents/specs/m1.md"],
            Digest(await File.ReadAllTextAsync(Path.Combine(repository.Path, ".agents", "specs", "m1.md"), token)),
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            [".agents/specs/m1.md"]), token);
    }

    private static async Task<bool> VerifyScopedRollbackAsync(string root)
    {
        Directory.CreateDirectory(root);
        var repository = new Repository { Id = Guid.NewGuid(), Name = "rollback", Path = root };
        await WriteAsync(root, ".agents/plan.md", "# Original plan\n", default);
        PlanScopedArtifactOperationSpec operation = PlanScopedArtifactOperationCatalog.Get(
            new WorkflowTransitionIdentity("GenerateExecutionMilestones"));
        var profile = new OperationPermissionProfile(
            operation.Label,
            root,
            operation.AllowedReads,
            operation.AllowedReadGlobs,
            operation.AllowedWrites,
            operation.AllowedWriteGlobs);
        var store = new FileSystemArtifactStore();
        ArtifactMutationTransaction transaction = await ArtifactMutationTransaction.CaptureAsync(
            new RepositoryArtifactStore(store, repository),
            profile);
        await WriteAsync(root, ".agents/plan.md", "# Invalid rewritten plan\n", default);
        await WriteAsync(root, ".agents/milestones/m1.md", "# Invalid milestone without checkbox\n", default);
        await transaction.RestoreAsync();
        return await File.ReadAllTextAsync(Path.Combine(root, ".agents", "plan.md")) == "# Original plan\n" &&
            !File.Exists(Path.Combine(root, ".agents", "milestones", "m1.md"));
    }

    private static async Task<IReadOnlyList<string>> EffectDiagnosticsAsync(
        Repository repository,
        string transition,
        CancellationToken cancellationToken)
    {
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync(cancellationToken);
        CanonicalTransitionRunRecord? run = snapshot.TransitionRuns.LastOrDefault(item =>
            string.Equals(item.Transition.Value, transition, StringComparison.Ordinal));
        if (run is null) return ["effect-plan:missing-transition-run"];
        IReadOnlyList<LoopRelay.Orchestration.Effects.EffectWorkItem> plan =
            await new CanonicalEffectWorkStore(repository).ReadPlanAsync(
                new LoopRelay.Core.Models.Identity.TransitionRunIdentity(run.RunId), cancellationToken);
        return plan.Select(item =>
            $"effect:{item.Intent.Executor.Value}:{item.State}:{item.Receipt?.PostconditionSatisfied}:" +
            string.Join('>', item.Events.Select(value => $"{value.State}:{OneLine(value.Explanation)}")))
            .ToArray();
    }

    private static async Task InitializeGitAsync(
        string root,
        string remote,
        string agentsRemote,
        CancellationToken token)
    {
        Directory.CreateDirectory(remote);
        Directory.CreateDirectory(agentsRemote);
        await RequireGitAsync(Path.GetDirectoryName(remote)!, ["init", "--bare", "--initial-branch=main", remote], token);
        await RequireGitAsync(Path.GetDirectoryName(agentsRemote)!, ["init", "--bare", "--initial-branch=main", agentsRemote], token);
        string agents = Path.Combine(root, ".agents");
        foreach (string[] arguments in new[]
        {
            new[] { "init", "-b", "main" },
            new[] { "config", "user.email", "certification@looprelay.invalid" },
            new[] { "config", "user.name", "LoopRelay Certification" },
            new[] { "add", "." },
            new[] { "commit", "-m", "seed plan agent artifacts" },
            new[] { "remote", "add", "origin", agentsRemote },
            new[] { "push", "-u", "origin", "main" },
        })
        {
            await RequireGitAsync(agents, arguments, token);
        }
        foreach (string[] arguments in new[]
        {
            new[] { "init", "-b", "main" },
            new[] { "config", "user.email", "certification@looprelay.invalid" },
            new[] { "config", "user.name", "LoopRelay Certification" },
            new[] { "add", "." },
            new[] { "commit", "-m", "seed plan certification" },
            new[] { "remote", "add", "origin", remote },
            new[] { "push", "-u", "origin", "main" },
        })
        {
            await RequireGitAsync(root, arguments, token);
        }
    }

    private static async Task RequireGitAsync(
        string root,
        IReadOnlyList<string> arguments,
        CancellationToken token)
    {
        ProcessResult result = await RunProcessAsync("git", arguments, root, token, TimeSpan.FromMinutes(2));
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {arguments[0]} failed: {result.StandardError}");
        }
    }

    private static (string? ThreadId, string? Continuity) PromptMetadata(
        CanonicalWorkflowPersistenceSnapshot snapshot,
        string transition)
    {
        CanonicalTransitionEvidenceRecord? record = snapshot.TransitionEvidence
            .Where(item => item.Transition == new WorkflowTransitionIdentity(transition) &&
                item.EventName == "RawPromptOutputCaptured")
            .OrderByDescending(item => item.EvidenceId)
            .FirstOrDefault();
        if (record is null) return (null, null);
        PromptExecutionResult? output = JsonSerializer.Deserialize<PromptExecutionResult>(record.DocumentJson, JsonOptions);
        string? threadId = null;
        string? continuity = null;
        output?.Metadata.TryGetValue("thread-id", out threadId);
        output?.Metadata.TryGetValue("continuity", out continuity);
        return (threadId, continuity);
    }

    private static IReadOnlyList<string> CompactDiagnostics(string? actualTransition, ProcessResult result)
    {
        var diagnostics = new List<string> { $"actual-transition:{actualTransition ?? "missing"}" };
        if (result.ExitCode != 0)
        {
            diagnostics.Add($"stdout-digest:{Digest(result.StandardOutput)}");
            diagnostics.Add($"stderr-digest:{Digest(result.StandardError)}");
            diagnostics.Add($"explanation:{ParseOutputValue(result.StandardOutput, "Explanation") ?? "missing"}");
            diagnostics.Add($"stderr-summary:{OneLine(result.StandardError)}");
        }
        return diagnostics;
    }

    private static string? ParseOutputValue(string output, string label) => output
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Trim())
        .Where(line => line.StartsWith(label + ": ", StringComparison.Ordinal))
        .Select(line => OneLine(line[(label.Length + 2)..]))
        .LastOrDefault();

    private static string OneLine(string value)
    {
        string normalized = string.Join(' ', value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 240 ? normalized : normalized[..240];
    }

    private static string? ParseTransition(string stdout) => stdout
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Trim())
        .Where(line => line.StartsWith("Transition: ", StringComparison.Ordinal))
        .Select(line => line["Transition: ".Length..])
        .LastOrDefault();

    private static Dictionary<string, string> SnapshotAgents(string root)
    {
        string agents = Path.Combine(root, ".agents");
        if (!Directory.Exists(agents)) return new Dictionary<string, string>(StringComparer.Ordinal);
        return Directory.EnumerateFiles(agents, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(agents, path).Replace('\\', '/')
                .StartsWith(".git/", StringComparison.Ordinal))
            .ToDictionary(
                path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                path => Digest(File.ReadAllBytes(path)),
                StringComparer.Ordinal);
    }

    private static string[] ChangedPaths(
        IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after) => before.Keys.Concat(after.Keys)
        .Distinct(StringComparer.Ordinal)
        .Where(path => !before.TryGetValue(path, out string? beforeHash) ||
            !after.TryGetValue(path, out string? afterHash) ||
            !string.Equals(beforeHash, afterHash, StringComparison.Ordinal))
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static bool AllowedMutations(string transition, IReadOnlyList<string> changed) => changed.All(path =>
        transition switch
        {
            "WriteExecutablePlan" or "RevisePlan" => path == ".agents/plan.md",
            "GenerateAdversarialProjection" => path is ".agents/projections/adversarial-plan-review.md"
                or ".agents/projections/manifest.md" or ".agents/projections/manifest.json",
            "RunAdversarialReview" or "VerifyExecuteEntryContract" => false,
            "GenerateOperationalContext" => path == ".agents/operational_context.md",
            "CollectExecutionDetails" => path == ".agents/details.md",
            "GenerateExecutionMilestones" => path == ".agents/plan.md" || IsMilestone(path),
            "RefineExecutionDetails" => path == ".agents/details.md" || IsMilestone(path),
            _ => false,
        });

    private static bool IsMilestone(string path) =>
        path.StartsWith(".agents/milestones/", StringComparison.Ordinal) &&
        Path.GetFileName(path).StartsWith('m') && path.EndsWith(".md", StringComparison.Ordinal);

    private static HashSet<int> CodexProcessIds() => Process.GetProcessesByName("codex")
        .Select(process =>
        {
            try { return process.Id; }
            finally { process.Dispose(); }
        })
        .ToHashSet();

    private static async Task<ProcessResult> RunCliAsync(
        string cliPath,
        string repository,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var all = new List<string>();
        string file = cliPath;
        if (cliPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            file = "dotnet";
            all.Add(cliPath);
        }
        all.AddRange(["--repo", repository]);
        all.AddRange(arguments);
        string invocationId = CertificationInvocation.NewId();
        ProcessResult result = await RunProcessAsync(
            file,
            all,
            repository,
            cancellationToken,
            CertificationFixtureSettings.ProviderTurnTimeout,
            invocationId);
        return result with { CertificationInvocationId = invocationId };
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string file,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken,
        TimeSpan timeoutValue,
        string? certificationInvocationId = null)
    {
        var start = new ProcessStartInfo
        {
            FileName = file,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (certificationInvocationId is { Length: > 0 })
            CertificationInvocation.Apply(start, certificationInvocationId);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("CLI did not start.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutValue);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return new ProcessResult(process.ExitCode, await stdout, await stderr);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            return new ProcessResult(
                124,
                await ReadCompletedOrEmptyAsync(stdout),
                $"Process exceeded the {timeoutValue.TotalMinutes:0}-minute timeout.");
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }

    private static async Task<string> ReadCompletedOrEmptyAsync(Task<string> read)
    {
        Task completed = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(2)));
        return completed == read && read.Status == TaskStatus.RanToCompletion ? await read : string.Empty;
    }

    private static async Task WriteAsync(string root, string relative, string content, CancellationToken token)
    {
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, token);
    }

    private static string Digest(string value) => Digest(Encoding.UTF8.GetBytes(value));
    private static string Digest(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));

    private static string FailureExplanation(IReadOnlyList<PlanProducerCaseResult> cases)
    {
        PlanTransitionCaseResult? failed = cases.SelectMany(item => item.Transitions)
            .LastOrDefault(item => !item.Completed || !item.MutationScopeValid);
        return failed is null
            ? cases.SelectMany(item => item.Evidence).LastOrDefault()
                ?? "Plan workflow certification failed after its live transition sequence."
            : $"Transition {failed.Transition} failed with exit code {failed.ExitCode}: {string.Join("; ", failed.Diagnostics)}";
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        string? CertificationInvocationId = null);
}
