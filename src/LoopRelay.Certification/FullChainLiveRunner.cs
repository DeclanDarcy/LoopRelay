using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LoopRelay.Agents.Services.Codex.Compatibility;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.ProjectContext;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Certification;

public sealed class FullChainLiveRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly string[] TraditionalRoadmap =
    [
        "BootstrapRoadmapCompletionContext",
        "SelectStrategicInitiative",
        "AuditExistingEpic",
        "CreateEpic",
        "GenerateMilestoneDeepDivesForEpic",
        "VerifyPlanEntryContract",
    ];

    private static readonly string[] EvalRoadmap =
    [
        "SelectEvaluationIntent",
        "CreateEvalDependencyInventory",
        "CreateEvalHypothesisInventory",
        "CreateEvalArchitecturalCatalog",
        "CreateEvalDag",
        "CreateNextEpicRoadmap",
        "CreateNextEpicActiveEpic",
        "GenerateMilestoneDeepDivesForEpic",
        "VerifyPlanEntryContract",
    ];

    private static readonly string[] Plan =
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

    private static readonly string[] Execute =
    [
        "GenerateDecision",
        "ExecuteImplementationSlice",
        "GenerateHandoff",
        "UpdateOperationalContext",
        "PublishRepositoryState",
        "EvaluateCommit",
        "EvaluateMilestoneCompletion",
        "RunNonImplementationReview",
        "RunCompletionCertification",
        "InterpretCompletionRoute",
        "VerifyWorkflowExitGate",
    ];

    public async Task<FullChainCertificationResult> RunAsync(
        WorkflowIdentity roadmapWorkflow,
        string codexExecutable,
        string authFile,
        string cliPath,
        string authorityRoot,
        bool retainFailedCase = false,
        CancellationToken cancellationToken = default)
    {
        bool traditional = roadmapWorkflow == WorkflowIdentity.TraditionalRoadmap;
        string milestone = traditional ? "milestone-13" : "milestone-14";
        string root = Path.Combine(authorityRoot, milestone, Guid.NewGuid().ToString("N"));
        string repositoryPath = Path.Combine(root, "repository");
        string remotePath = Path.Combine(root, "remote.git");
        string agentsRemotePath = Path.Combine(root, "agents-remote.git");
        string codexHome = Path.Combine(root, "codex-home");
        Directory.CreateDirectory(codexHome);
        File.Copy(authFile, Path.Combine(codexHome, "auth.json"));
        string? priorHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        string? priorExecutable = Environment.GetEnvironmentVariable("CODEX_EXECUTABLE");
        string? priorAnalytics = Environment.GetEnvironmentVariable("CODEX_ANALYTICS_ENABLED");
        string? priorSettings = Environment.GetEnvironmentVariable("LOOPRELAY_SETTINGS_PATH");
        Environment.SetEnvironmentVariable("CODEX_HOME", codexHome);
        Environment.SetEnvironmentVariable("CODEX_EXECUTABLE", codexExecutable);
        Environment.SetEnvironmentVariable("CODEX_ANALYTICS_ENABLED", "false");
        string effort = traditional ? "xhigh" : "high";
        string settingsPath = await WriteEffortSettingsAsync(root, cliPath, effort, cancellationToken);
        Environment.SetEnvironmentVariable("LOOPRELAY_SETTINGS_PATH", settingsPath);
        HashSet<int> initialCodex = CodexProcessIds();
        var transitions = new List<FullChainTransitionResult>();
        var evidence = new List<string>();
        var total = Stopwatch.StartNew();
        string version = "unknown";
        string schema = "unknown";
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

            Directory.CreateDirectory(repositoryPath);
            await SeedAsync(repositoryPath, traditional, cancellationToken);
            await InitializeGitAsync(repositoryPath, remotePath, agentsRemotePath, cancellationToken);
            string verifierHash = Digest(await File.ReadAllBytesAsync(
                Path.Combine(repositoryPath, "verify.ps1"), cancellationToken));
            ProcessResult storage = await RunCliAsync(cliPath, repositoryPath, ["storage", "init"], cancellationToken);
            if (storage.ExitCode != 0) throw new InvalidOperationException("Full-chain storage initialization failed.");

            (string Workflow, string Transition)[] expected =
                (traditional ? TraditionalRoadmap : EvalRoadmap)
                .Select(item => (roadmapWorkflow.Value, item))
                .Concat(Plan.Select(item => (WorkflowIdentity.Plan.Value, item)))
                .Concat(Execute.Select(item => (WorkflowIdentity.Execute.Value, item)))
                .ToArray();
            string forcedCommand = traditional ? "traditional" : "eval";
            for (int index = 0; index < expected.Length; index++)
            {
                Stopwatch elapsed = Stopwatch.StartNew();
                ProcessResult run;
                if (index == 0)
                {
                    run = await RunDefaultUntilFirstTransitionAsync(
                        cliPath, repositoryPath, expected[index].Transition, cancellationToken);
                }
                else
                {
                    string boundedCommand = expected[index].Workflow == roadmapWorkflow.Value
                        ? forcedCommand
                        : expected[index].Workflow == WorkflowIdentity.Plan.Value ? "plan" : "execute";
                    run = await RunCliAsync(cliPath, repositoryPath, [boundedCommand], cancellationToken);
                }
                elapsed.Stop();
                string? actualWorkflow = ParseOutputValue(run.StandardOutput, "Workflow");
                string? actualTransition = ParseOutputValue(run.StandardOutput, "Transition");
                bool workflowDisplayValid = index == 0
                    ? actualWorkflow == expected[index].Workflow
                    : actualWorkflow is null || actualWorkflow == expected[index].Workflow;
                bool transitionPassed = workflowDisplayValid && actualTransition == expected[index].Transition &&
                    (index == 0 || run.ExitCode == 0);
                transitions.Add(new FullChainTransitionResult(
                    index + 1,
                    expected[index].Workflow,
                    expected[index].Transition,
                    actualWorkflow,
                    actualTransition,
                    run.ExitCode,
                    elapsed.ElapsedMilliseconds,
                    true,
                    transitionPassed,
                    index == 0 && transitionPassed
                        ? ["planned-interruption-after-durable-default-transition"]
                        : Diagnostics(run)));
                if (!transitionPassed) break;
            }

            var repository = new Repository
            {
                Id = Guid.NewGuid(),
                Name = milestone,
                Path = repositoryPath,
            };
            CanonicalWorkflowPersistenceSnapshot snapshot =
                await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync(cancellationToken);
            bool defaultSelection = transitions.Count > 0 && transitions[0].Passed &&
                transitions[0].ActualWorkflow == roadmapWorkflow.Value;
            bool forcedSelection = transitions.Count > 1 && transitions[1].Passed;
            bool boundaries = WorkflowCompleted(snapshot, roadmapWorkflow) &&
                WorkflowCompleted(snapshot, WorkflowIdentity.Plan) &&
                WorkflowCompleted(snapshot, WorkflowIdentity.Execute);
            bool convergence = ProducerConvergence(snapshot, roadmapWorkflow);
            ProcessResult verifier = await RunProcessAsync(
                "pwsh", ["-NoProfile", "-File", "verify.ps1"], repositoryPath,
                TimeSpan.FromMinutes(2), cancellationToken);
            bool verifierUnchanged = verifierHash == Digest(await File.ReadAllBytesAsync(
                Path.Combine(repositoryPath, "verify.ps1"), cancellationToken));
            string greetingPath = Path.Combine(repositoryPath, "GREETING.md");
            bool exactGreeting = File.Exists(greetingPath) &&
                (await File.ReadAllTextAsync(greetingPath, cancellationToken)) is
                    "Hello from Loop Relay.\n" or "Hello from Loop Relay.\r\n";
            bool acceptance = verifier.ExitCode == 0 && verifierUnchanged && exactGreeting;
            ProcessResult localHead = await GitAsync(repositoryPath, ["rev-parse", "HEAD"], cancellationToken);
            ProcessResult remoteHead = await RunProcessAsync(
                "git", ["--git-dir", remotePath, "rev-parse", "refs/heads/main"], root,
                TimeSpan.FromMinutes(2), cancellationToken);
            string agentsPath = Path.Combine(repositoryPath, ".agents");
            ProcessResult agentsLocalHead = await GitAsync(agentsPath, ["rev-parse", "HEAD"], cancellationToken);
            ProcessResult agentsRemoteHead = await RunProcessAsync(
                "git", ["--git-dir", agentsRemotePath, "rev-parse", "refs/heads/main"], root,
                TimeSpan.FromMinutes(2), cancellationToken);
            ProcessResult gitlink = await GitAsync(repositoryPath, ["ls-tree", "HEAD", ".agents"], cancellationToken);
            bool gitPublished = localHead.ExitCode == 0 && remoteHead.ExitCode == 0 &&
                localHead.StandardOutput.Trim() == remoteHead.StandardOutput.Trim() &&
                agentsLocalHead.ExitCode == 0 && agentsRemoteHead.ExitCode == 0 &&
                agentsLocalHead.StandardOutput.Trim() == agentsRemoteHead.StandardOutput.Trim() &&
                gitlink.ExitCode == 0 &&
                gitlink.StandardOutput.Trim().StartsWith("160000 commit ", StringComparison.Ordinal) &&
                gitlink.StandardOutput.Contains(agentsLocalHead.StandardOutput.Trim(), StringComparison.OrdinalIgnoreCase) &&
                snapshot.TransitionRuns.Any(item =>
                    item.Transition == new WorkflowTransitionIdentity("PublishRepositoryState") &&
                    item.State == TransitionDurableState.Completed);
            bool archive = File.Exists(Path.Combine(repositoryPath, ".agents", "archive", "epics", "1.md")) &&
                snapshot.Products.Any(product =>
                    product.Identity == ProductIdentity.CertifiedCompletion &&
                    product.ValidationState == ProductValidationState.Valid);
            bool traceability = traditional || EvalTraceability(repositoryPath);

            int sessionsBefore = SessionFileCount(codexHome);
            string userTreeBefore = UserTreeFingerprint(repositoryPath);
            string gitBefore = await GitFingerprintAsync(repositoryPath, cancellationToken);
            ProcessResult rerun = archive
                ? await RunCliAsync(cliPath, repositoryPath, [], cancellationToken)
                : new ProcessResult(1, string.Empty, "rerun-not-attempted-before-closure");
            int sessionsAfter = SessionFileCount(codexHome);
            string userTreeAfter = UserTreeFingerprint(repositoryPath);
            string gitAfter = await GitFingerprintAsync(repositoryPath, cancellationToken);
            CanonicalWorkflowPersistenceSnapshot rerunSnapshot =
                await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync(cancellationToken);
            bool idempotent = rerun.ExitCode == 0 &&
                ParseOutputValue(rerun.StandardOutput, "Transition") is null &&
                sessionsBefore == sessionsAfter &&
                userTreeBefore == userTreeAfter &&
                gitBefore == gitAfter &&
                WorkflowCompleted(rerunSnapshot, WorkflowIdentity.Execute);
            await Task.Delay(500, cancellationToken);
            bool processesClean = CodexProcessIds().All(initialCodex.Contains);
            total.Stop();
            long providerBytes = ProviderEvidenceBytes(codexHome);
            string budgetDecision = "provisional-release-budget:one-full-chain-per-profile-platform;repeat-targeted-postures-three-times;recertify-on-denominator-drift";
            evidence.AddRange(
            [
                $"model:gpt-5.6-sol",
                $"effort:{effort}",
                $"transitions:{transitions.Count}/{expected.Length}",
                $"default-selection:{defaultSelection}",
                $"forced-selection:{forcedSelection}",
                $"workflow-boundaries:{boundaries}",
                $"producer-convergence:{convergence}",
                $"verifier-exit:{verifier.ExitCode}",
                $"verifier-unchanged:{verifierUnchanged}",
                $"git-publication:{gitPublished}",
                $"archive-closure:{archive}",
                $"traceability:{traceability}",
                $"rerun-sessions:{sessionsBefore}->{sessionsAfter}",
                $"rerun-user-tree-unchanged:{userTreeBefore == userTreeAfter}",
                $"rerun-git-unchanged:{gitBefore == gitAfter}",
                $"elapsed-ms:{total.ElapsedMilliseconds}",
                $"provider-evidence-bytes:{providerBytes}",
                budgetDecision,
            ]);
            bool passed = transitions.Count == expected.Length && transitions.All(item => item.Passed) &&
                defaultSelection && forcedSelection && boundaries && convergence && acceptance &&
                gitPublished && archive && traceability && idempotent && processesClean;
            return await Finish(
                passed ? CertificationClassification.Passed : CertificationClassification.ProductRegression,
                defaultSelection,
                forcedSelection,
                boundaries,
                convergence,
                acceptance,
                gitPublished,
                archive,
                traceability,
                idempotent,
                processesClean,
                total.ElapsedMilliseconds,
                providerBytes,
                budgetDecision);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            total.Stop();
            evidence.AddRange([exception.GetType().Name, exception.Message]);
            return await Finish(CertificationClassification.EnvironmentFailure,
                elapsed: total.ElapsedMilliseconds);
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

        async Task<FullChainCertificationResult> Finish(
            CertificationClassification classification,
            bool defaultSelection = false,
            bool forcedSelection = false,
            bool boundaries = false,
            bool convergence = false,
            bool acceptance = false,
            bool git = false,
            bool archive = false,
            bool traceability = false,
            bool idempotent = false,
            bool processesClean = false,
            long elapsed = 0,
            long providerBytes = 0,
            string budget = "not-measured-before-failure")
        {
            IReadOnlyList<string> privacy = PrivacyScanner.Scan(
                string.Join('\n', evidence.Concat(transitions.SelectMany(item => item.Evidence))), authorityRoot);
            if (privacy.Count > 0) classification = CertificationClassification.OracleDrift;
            preserveCase = retainFailedCase && classification != CertificationClassification.Passed;
            var result = new FullChainCertificationResult(
                CertificationRunner.ResultSchemaVersion,
                classification,
                traditional ? "TraditionalRoadmap->Plan->Execute" : "EvalRoadmap->Plan->Execute",
                version,
                schema,
                transitions,
                defaultSelection,
                forcedSelection,
                boundaries,
                convergence,
                acceptance,
                git,
                archive,
                traceability,
                idempotent,
                processesClean,
                elapsed,
                providerBytes,
                budget,
                privacy,
                evidence);
            string path = Path.Combine(authorityRoot, "evidence", $"{milestone}.latest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using FileStream stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
            return result;
        }
    }

    private static bool WorkflowCompleted(CanonicalWorkflowPersistenceSnapshot snapshot, WorkflowIdentity workflow) =>
        snapshot.WorkflowStates.Any(state => state.Workflow == workflow &&
            state.State == WorkflowResolutionState.Completed && state.CurrentStage is null);

    private static bool ProducerConvergence(
        CanonicalWorkflowPersistenceSnapshot snapshot,
        WorkflowIdentity roadmapWorkflow)
    {
        ProductRecord? epic = snapshot.Products.FirstOrDefault(item => item.Identity == ProductIdentity.PreparedEpic);
        ProductRecord? specs = snapshot.Products.FirstOrDefault(item => item.Identity == ProductIdentity.MilestoneSpecificationSet);
        ProductIdentity[] executeEntry =
        [
            ProductIdentity.ExecutablePlan,
            ProductIdentity.OperationalContext,
            ProductIdentity.ExecutionDetails,
            ProductIdentity.ExecutionMilestoneSet,
            ProductIdentity.ExecutionReadiness,
        ];
        return epic?.ProducerWorkflow == roadmapWorkflow && specs?.ProducerWorkflow == roadmapWorkflow &&
            epic.ValidationState == ProductValidationState.Valid && specs.ValidationState == ProductValidationState.Valid &&
            executeEntry.All(identity => snapshot.Products.Any(product =>
                product.Identity == identity && product.ProducerWorkflow == WorkflowIdentity.Plan &&
                product.ValidationState == ProductValidationState.Valid));
    }

    private static bool EvalTraceability(string root)
    {
        string[] paths =
        [
            ".agents/evals/eval-full-chain.md",
            ".agents/eval-dependency-inventory.md",
            ".agents/eval-hypothesis-inventory.md",
            ".agents/eval-architectural-catalog.md",
            ".agents/eval-dag.md",
            ".agents/next-epic-roadmap.md",
        ];
        return paths.All(path => File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))));
    }

    private static async Task SeedAsync(string root, bool traditional, CancellationToken token)
    {
        await WriteAsync(root, ".gitignore", ".LoopRelay/\n", token);
        await WriteAsync(root, "README.md", """
            # Deterministic Greeting Capability

            The repository does not yet contain `GREETING.md`. The sole required initiative is to create that file
            with exactly `Hello from Loop Relay.` followed by one newline and prove it with the immutable `verify.ps1`.
            Planning prose, tests alone, or documentation changes do not satisfy the capability.
            """, token);
        await WriteAsync(root, "verify.ps1", """
            $path = Join-Path $PSScriptRoot 'GREETING.md'
            if (-not (Test-Path -LiteralPath $path)) { exit 1 }
            $accepted = @(
                'bb08ffa7f02136bb40ecc4bd22336169dc4766b2b2de61b32adc668ff1d4d201',
                'c635ad5eaa38531f4496741d649b24e7a917522ab9a8c9e456c7fe038932837c'
            )
            $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($accepted -cnotcontains $actual) { exit 1 }
            exit 0
            """, token);
        string[] context =
        [
            "# Purpose\n\nDeliver the missing deterministic greeting as real repository capability.",
            "# Capability Model\n\n`GREETING.md` is absent. The desired capability is its exact repository-owned content plus executable verification.",
            "# Invariants\n\nDo not modify `verify.ps1`. The greeting must be exact and the verifier must exit zero.",
            "# Strategic Structure\n\nCreate one bounded intermediary epic with exactly one implementation milestone: M1 implements the exact `GREETING.md` bytes and proves them with the immutable `verify.ps1`; do not split, investigate, or add planning-only milestones.",
            "# Authority Model\n\nRepository bytes and the independent verifier are authoritative over model-authored claims. After `ExecuteImplementationSlice` implements and locally verifies the capability and `GenerateHandoff` records it, the orchestrator-owned `PublishRepositoryState` transition commits and pushes the independent `.agents` repository, then commits its updated gitlink and pushes the parent repository. Execution agents never discover, invoke, preview, or preflight that downstream transition.",
            "# Evaluation Model\n\nThe implementation slice must first create the exact greeting bytes and prove them with the immutable verifier; absence of a callable publisher is not an implementation blocker. After handoff, orchestration runs `PublishRepositoryState`. Completion then requires its success and checked completion state in `.agents/milestones/*.md`; that checklist state and the immutable verifier are the authoritative milestone evidence, with no additional evidence mechanism or follow-up required.",
            "# Drift And False Success\n\nProse-only output, altered verification, or unchecked milestones are false success.",
            "# Vocabulary\n\nGreeting capability means the implemented file, preserved verifier, and published evidence together.",
            "# Eval Details\n\nUse one deterministic exact-value acceptance signal with a negative control for missing or altered content.",
        ];
        int index = 0;
        foreach (string path in ProjectContextSourceContract.SourceFiles)
        {
            await WriteAsync(root, path, context[index++] + "\n", token);
        }
        if (!traditional)
        {
            await WriteAsync(root, ".agents/evals/eval-full-chain.md", """
                # Evaluation Intent EVAL-FULL-CHAIN-001

                Determine and implement the smallest path to a deterministic `GREETING.md` capability.
                Required verdict: `verify.ps1` exits zero without modification and exact greeting content exists.
                Negative control: missing, altered, or prose-only output fails.
                Produce one implementation-first epic with one bounded milestone.
                """, token);
        }
    }

    private static async Task InitializeGitAsync(
        string repository,
        string remote,
        string agentsRemote,
        CancellationToken token)
    {
        Directory.CreateDirectory(remote);
        Directory.CreateDirectory(agentsRemote);
        await RequireGitAsync(Path.GetDirectoryName(remote)!, ["init", "--bare", "--initial-branch=main", remote], token);
        await RequireGitAsync(Path.GetDirectoryName(agentsRemote)!, ["init", "--bare", "--initial-branch=main", agentsRemote], token);
        string agents = Path.Combine(repository, ".agents");
        foreach (string[] arguments in new[]
        {
            new[] { "init", "-b", "main" },
            new[] { "config", "user.email", "certification@looprelay.invalid" },
            new[] { "config", "user.name", "LoopRelay Certification" },
            new[] { "add", "." },
            new[] { "commit", "-m", "seed full-chain agent artifacts" },
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
            new[] { "commit", "-m", "seed full-chain certification" },
            new[] { "remote", "add", "origin", remote },
            new[] { "push", "-u", "origin", "main" },
        })
        {
            await RequireGitAsync(repository, arguments, token);
        }
    }

    private static async Task RequireGitAsync(string root, IReadOnlyList<string> arguments, CancellationToken token)
    {
        ProcessResult result = await GitAsync(root, arguments, token);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {arguments[0]} failed: {result.StandardError.Trim()}");
        }
    }

    private static Task<ProcessResult> GitAsync(
        string root,
        IReadOnlyList<string> arguments,
        CancellationToken token) =>
        RunProcessAsync("git", arguments, root, TimeSpan.FromMinutes(2), token);

    private static async Task<string> GitFingerprintAsync(string root, CancellationToken token)
    {
        ProcessResult head = await GitAsync(root, ["rev-parse", "HEAD"], token);
        ProcessResult status = await GitAsync(root, ["status", "--porcelain=v1", "--untracked-files=all"], token);
        if (head.ExitCode != 0 || status.ExitCode != 0) throw new InvalidOperationException("Git fingerprint failed.");
        return Digest(head.StandardOutput + "\n" + status.StandardOutput);
    }

    private static string UserTreeFingerprint(string root)
    {
        var material = new StringBuilder();
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Where(path => !Path.GetRelativePath(root, path).StartsWith(".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                         !Path.GetRelativePath(root, path).StartsWith(".LoopRelay" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(path => Path.GetRelativePath(root, path), StringComparer.Ordinal))
        {
            material.Append(Path.GetRelativePath(root, path).Replace('\\', '/'))
                .Append(':').Append(Digest(File.ReadAllBytes(path))).Append('\n');
        }
        return Digest(material.ToString());
    }

    private static async Task<string> WriteEffortSettingsAsync(
        string root,
        string cliPath,
        string effort,
        CancellationToken token)
    {
        string source = Path.Combine(Path.GetDirectoryName(cliPath)!, "settings.default.json");
        JsonNode settings = JsonNode.Parse(await File.ReadAllTextAsync(source, token))
            ?? throw new InvalidOperationException("CLI settings template was empty.");
        settings["brainEffort"] = effort;
        string path = Path.Combine(root, "settings.json");
        await File.WriteAllTextAsync(path, settings.ToJsonString(JsonOptions), token);
        return path;
    }

    private static IReadOnlyList<string> Diagnostics(ProcessResult result) => result.ExitCode == 0
        ? []
        :
        [
            $"explanation:{ParseOutputValue(result.StandardOutput, "Explanation") ?? "missing"}",
            $"stdout-digest:{Digest(result.StandardOutput)}",
            $"stderr-digest:{Digest(result.StandardError)}",
        ];

    private static string? ParseOutputValue(string output, string label) => output
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Trim())
        .Where(line => line.StartsWith(label + ": ", StringComparison.Ordinal))
        .Select(line => line[(label.Length + 2)..])
        .LastOrDefault();

    private static async Task<ProcessResult> RunCliAsync(
        string cliPath,
        string repository,
        IReadOnlyList<string> arguments,
        CancellationToken token)
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
        return await RunProcessAsync(file, all, repository, TimeSpan.FromMinutes(30), token);
    }

    private static async Task<ProcessResult> RunDefaultUntilFirstTransitionAsync(
        string cliPath,
        string repository,
        string expectedTransition,
        CancellationToken token)
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
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Default CLI did not start.");
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(token);
        var stdout = new StringBuilder();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(30));
        bool observed = false;
        try
        {
            while (await process.StandardOutput.ReadLineAsync(timeout.Token) is { } line)
            {
                stdout.AppendLine(line);
                if (line.Trim() == $"Transition: {expectedTransition}")
                {
                    observed = true;
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    break;
                }
            }
            await process.WaitForExitAsync(CancellationToken.None);
            return new ProcessResult(observed ? 130 : process.ExitCode, stdout.ToString(), await stderrTask);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            return new ProcessResult(124, stdout.ToString(), "Default CLI exceeded its certification timeout.");
        }
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string file,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken token)
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
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"{file} did not start.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(token);
        Task<string> stderr = process.StandardError.ReadToEndAsync(token);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
            return new ProcessResult(process.ExitCode, await stdout, await stderr);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            return new ProcessResult(124, string.Empty, $"{file} exceeded its certification timeout.");
        }
    }

    private static int SessionFileCount(string codexHome) => Directory.Exists(Path.Combine(codexHome, "sessions"))
        ? Directory.EnumerateFiles(Path.Combine(codexHome, "sessions"), "*.jsonl", SearchOption.AllDirectories).Count()
        : 0;

    private static long ProviderEvidenceBytes(string codexHome) => Directory.Exists(Path.Combine(codexHome, "sessions"))
        ? Directory.EnumerateFiles(Path.Combine(codexHome, "sessions"), "*.jsonl", SearchOption.AllDirectories)
            .Sum(path => new FileInfo(path).Length)
        : 0;

    private static HashSet<int> CodexProcessIds() => Process.GetProcessesByName("codex")
        .Select(process => { try { return process.Id; } finally { process.Dispose(); } })
        .ToHashSet();

    private static async Task WriteAsync(string root, string relative, string content, CancellationToken token)
    {
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, token);
    }

    private static string Digest(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Digest(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
