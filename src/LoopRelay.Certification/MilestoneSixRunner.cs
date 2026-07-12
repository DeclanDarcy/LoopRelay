using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Agents.Services.Codex.Compatibility;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Core.Services.ProjectContext;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Certification;

public sealed class MilestoneSixRunner
{
    private static readonly string[] Transitions =
    [
        "VerifyExecutionReadiness",
        "GenerateDecision",
        "ExecuteImplementationSlice",
        "GenerateHandoff",
        "UpdateOperationalContext",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<MilestoneSixCertificationResult> RunAsync(
        string codexExecutable,
        string authFile,
        string cliPath,
        string authorityRoot,
        CancellationToken cancellationToken = default)
    {
        string root = Path.Combine(authorityRoot, "milestone-6", Guid.NewGuid().ToString("N"));
        string repositoryPath = Path.Combine(root, "repository");
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
        string settingsPath = await CertificationFixtureSettings.WriteAsync(
            root, cliPath, cancellationToken);
        Environment.SetEnvironmentVariable("LOOPRELAY_SETTINGS_PATH", settingsPath);
        var transitions = new List<ExecuteTransitionCaseResult>();
        var evidence = new List<string>
        {
            $"model:{CertificationFixtureSettings.BrainModel}",
            $"effort:{CertificationFixtureSettings.BrainEffort}",
        };
        string version = "unknown";
        string schema = "unknown";
        HashSet<int> initialCodex = CodexProcessIds();
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
            await SeedRepositoryAsync(repositoryPath, cancellationToken);
            await InitializeGitAsync(
                repositoryPath,
                Path.Combine(root, "repository-remote.git"),
                Path.Combine(root, "agents-remote.git"),
                cancellationToken);
            string verifierHash = Digest(await File.ReadAllBytesAsync(Path.Combine(repositoryPath, "verify.ps1"), cancellationToken));
            ProcessResult init = await RunCliAsync(cliPath, repositoryPath, ["storage", "init"], cancellationToken);
            if (init.ExitCode != 0)
            {
                evidence.Add($"storage-init:{init.ExitCode}:{Digest(init.StandardError)}");
                return await Finish(CertificationClassification.EnvironmentFailure);
            }

            var repository = new Repository { Id = Guid.NewGuid(), Name = "milestone-6", Path = repositoryPath };
            await SeedExecuteEntryProductsAsync(repository, cancellationToken);
            foreach (string expected in Transitions)
            {
                Dictionary<string, string> before = SnapshotRepository(repositoryPath);
                ProcessResult run = await RunCliAsync(cliPath, repositoryPath, ["execute"], cancellationToken);
                Dictionary<string, string> after = SnapshotRepository(repositoryPath);
                string? actual = ParseOutputValue(run.StandardOutput, "Transition");
                bool completed = run.ExitCode == 0 && string.Equals(actual, expected, StringComparison.Ordinal);
                transitions.Add(new ExecuteTransitionCaseResult(
                    expected,
                    run.ExitCode,
                    ChangedPaths(before, after),
                    completed,
                    Diagnostics(actual, run)));
                if (!completed) break;
            }

            CanonicalWorkflowPersistenceSnapshot snapshot =
                await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync(cancellationToken);
            (string? implementationThread, string? implementationContinuity) =
                PromptMetadata(snapshot, "ExecuteImplementationSlice");
            (string? handoffThread, string? handoffContinuity) = PromptMetadata(snapshot, "GenerateHandoff");
            bool sameThread = !string.IsNullOrWhiteSpace(implementationThread) &&
                string.Equals(implementationThread, handoffThread, StringComparison.Ordinal);
            bool restarted = handoffContinuity == "resumed-after-restart";
            bool durableFacts = await HasDurableSliceFactsAsync(repository, cancellationToken);
            bool decisionContinuity = DecisionContinuity(snapshot);
            string greetingPath = Path.Combine(repositoryPath, "GREETING.md");
            string greetingByteDigest = File.Exists(greetingPath)
                ? Digest(await File.ReadAllBytesAsync(greetingPath, cancellationToken))
                : "missing";
            bool exactGreeting = File.Exists(greetingPath) &&
                await File.ReadAllTextAsync(greetingPath, cancellationToken) == "Hello from Loop Relay.\n";
            bool milestoneTicked = Directory.EnumerateFiles(
                    Path.Combine(repositoryPath, ".agents", "milestones"), "m*.md")
                .Select(File.ReadAllText)
                .Any(content => content.Contains("- [x]", StringComparison.OrdinalIgnoreCase));
            ProcessResult verifier = await RunProcessAsync(
                "pwsh", ["-NoProfile", "-File", "verify.ps1"], repositoryPath, cancellationToken);
            bool acceptance = exactGreeting && milestoneTicked && verifier.ExitCode == 0;
            bool verifierUnchanged = verifierHash == Digest(
                await File.ReadAllBytesAsync(Path.Combine(repositoryPath, "verify.ps1"), cancellationToken));
            bool stoppedBeforePublication = snapshot.TransitionRuns.All(run =>
                    run.Transition != new WorkflowTransitionIdentity("PublishRepositoryState")) &&
                snapshot.WorkflowStates.Any(state =>
                    state.Workflow == WorkflowIdentity.Execute &&
                    state.CurrentStage == new WorkflowStageIdentity("Execution Continuity") &&
                    state.State == WorkflowResolutionState.Resumable);
            await Task.Delay(500, cancellationToken);
            bool processesClean = CodexProcessIds().All(pid => initialCodex.Contains(pid));
            evidence.AddRange(
            [
                $"implementation-thread-digest:{Digest(implementationThread ?? string.Empty)}",
                $"implementation-continuity:{implementationContinuity ?? "missing"}",
                $"handoff-continuity:{handoffContinuity ?? "missing"}",
                $"exact-greeting:{exactGreeting}",
                $"greeting-byte-digest:{greetingByteDigest}",
                $"milestone-ticked:{milestoneTicked}",
                $"verifier-exit:{verifier.ExitCode}",
                $"durable-slice-facts:{durableFacts}",
                $"decision-continuity:{decisionContinuity}",
                $"stopped-before-publication:{stoppedBeforePublication}",
            ]);
            bool passed = transitions.Count == Transitions.Length && transitions.All(item => item.Completed) &&
                acceptance && verifierUnchanged && sameThread && restarted && durableFacts &&
                decisionContinuity && stoppedBeforePublication && processesClean;
            return await Finish(LiveProviderFailureClassifier.Classify(passed, codexHome),
                acceptance, verifierUnchanged, sameThread, restarted, durableFacts,
                decisionContinuity, stoppedBeforePublication, processesClean);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            evidence.AddRange([exception.GetType().Name, exception.Message]);
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
            if (Directory.Exists(root))
            {
                foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(root, recursive: true);
            }
        }

        async Task<MilestoneSixCertificationResult> Finish(
            CertificationClassification classification,
            bool acceptance = false,
            bool verifierUnchanged = false,
            bool sameThread = false,
            bool restarted = false,
            bool durableFacts = false,
            bool decisionContinuity = false,
            bool stoppedBeforePublication = false,
            bool processesClean = false)
        {
            string scrubbed = string.Join("\n", evidence.Concat(transitions.SelectMany(item => item.Diagnostics)));
            IReadOnlyList<string> privacy = PrivacyScanner.Scan(scrubbed, authorityRoot);
            if (privacy.Count > 0) classification = CertificationClassification.OracleDrift;
            var result = new MilestoneSixCertificationResult(
                CertificationRunner.ResultSchemaVersion,
                classification,
                version,
                schema,
                transitions,
                acceptance,
                verifierUnchanged,
                sameThread,
                restarted,
                durableFacts,
                decisionContinuity,
                stoppedBeforePublication,
                processesClean,
                privacy,
                evidence);
            string path = Path.Combine(authorityRoot, "evidence", "milestone-6.latest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using FileStream stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
            return result;
        }
    }

    private static async Task SeedRepositoryAsync(string root, CancellationToken token)
    {
        await WriteAsync(root, "README.md", "# Execute certification repository\n", token);
        await WriteAsync(root, "verify.ps1", """
            $path = Join-Path $PSScriptRoot 'GREETING.md'
            if (-not (Test-Path -LiteralPath $path)) { exit 1 }
            $acceptedSha256 = @(
                'bb08ffa7f02136bb40ecc4bd22336169dc4766b2b2de61b32adc668ff1d4d201',
                'c635ad5eaa38531f4496741d649b24e7a917522ab9a8c9e456c7fe038932837c'
            )
            $actualSha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($acceptedSha256 -cnotcontains $actualSha256) { exit 1 }
            exit 0
            """, token);
        await WriteAsync(root, ".agents/epic.md", "# Epic\n\nCreate the exact greeting capability.\n", token);
        await WriteAsync(root, ".agents/plan.md", """
            # Executable Plan

            ## Milestone 1
            (See ./milestones/m1-greeting.md)

            Implement only the repository-owned `GREETING.md` capability. The slice is not complete until
            `verify.ps1` exits 0 and the exact checkbox in `m1-greeting.md` is changed from `[ ]` to `[x]`.
            Do not modify `verify.ps1`.
            """, token);
        await WriteAsync(root, ".agents/operational_context.md", "# Operational Context\n\nRun `pwsh -NoProfile -File verify.ps1` as the independent acceptance signal.\n", token);
        await WriteAsync(root, ".agents/details.md", """
            # Details

            Create `GREETING.md` whose only UTF-8 content is `Hello from Loop Relay.` followed by one LF newline.
            After the verifier passes, you MUST change the exact milestone checkbox from `- [ ]` to `- [x]`
            before ending the implementation turn. Do not edit `verify.ps1` or unrelated files.
            """, token);
        await WriteAsync(root, ".agents/milestones/m1-greeting.md", """
            # Milestone 1: Deterministic greeting

            - [ ] Create `GREETING.md` with the exact required bytes, run `verify.ps1` successfully, then tick this checkbox before ending the turn.
            """, token);
        int index = 0;
        foreach (string path in ProjectContextSourceContract.SourceFiles)
        {
            index++;
            await WriteAsync(root, path, $"# Context {index}\n\nPrefer executable, deterministic acceptance signals.\n", token);
        }
    }

    private static async Task SeedExecuteEntryProductsAsync(Repository repository, CancellationToken token)
    {
        var store = new CanonicalWorkflowPersistenceStore(repository);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
            WorkflowIdentity.Plan, WorkflowResolutionState.Completed, null, RuntimeOutcomeKind.Completed,
            now, ["certification:milestone-6:plan-complete"]), token);
        await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
            WorkflowIdentity.Execute,
            WorkflowResolutionState.Resumable,
            new WorkflowStageIdentity("Execution Readiness"),
            RuntimeOutcomeKind.Waiting,
            now,
            ["certification:milestone-6:execute-entry"]), token);
        await store.UpsertStageStateAsync(new CanonicalStageStateRecord(
            WorkflowIdentity.Execute,
            new WorkflowStageIdentity("Execution Readiness"),
            WorkflowResolutionState.Active,
            now,
            ["certification:milestone-6:execute-entry"]), token);
        (ProductIdentity Identity, string[] Paths)[] products =
        [
            (ProductIdentity.ExecutablePlan, [".agents/plan.md"]),
            (ProductIdentity.OperationalContext, [".agents/operational_context.md"]),
            (ProductIdentity.ExecutionDetails, [".agents/details.md"]),
            (ProductIdentity.ExecutionMilestoneSet, [".agents/milestones/m1-greeting.md"]),
            (ProductIdentity.ExecutionReadiness, [".LoopRelay/evidence/certification/m6-readiness.md"]),
        ];
        foreach ((ProductIdentity product, string[] paths) in products)
        {
            await store.UpsertProductAsync(new ProductRecord(
                product,
                WorkflowIdentity.Plan,
                new WorkflowTransitionIdentity("VerifyExecuteEntryContract"),
                [WorkflowIdentity.Execute],
                "repository-owned certification seed",
                "independent milestone-6 fixture",
                paths,
                Digest(string.Join('|', paths.Select(path => path + ":" + FileHashIfPresent(repository.Path, path)))),
                ProductFreshness.Fresh,
                ProductValidationState.Valid,
                ProductLifecycle.Active,
                paths), token);
        }
    }

    private static bool DecisionContinuity(CanonicalWorkflowPersistenceSnapshot snapshot)
    {
        CanonicalTransitionEvidenceRecord? evidence = snapshot.TransitionEvidence
            .Where(item => item.Transition == new WorkflowTransitionIdentity("GenerateDecision") &&
                item.EventName == "RawPromptOutputCaptured")
            .OrderByDescending(item => item.EvidenceId)
            .FirstOrDefault();
        if (evidence is null) return false;
        PromptExecutionResult? result = JsonSerializer.Deserialize<PromptExecutionResult>(evidence.DocumentJson, JsonOptions);
        return result is not null &&
            result.Metadata.TryGetValue("session-scope", out string? scope) && !string.IsNullOrWhiteSpace(scope) &&
            result.Metadata.TryGetValue("provider-thread-id", out string? thread) && !string.IsNullOrWhiteSpace(thread) &&
            result.Metadata.TryGetValue("lineage-id", out string? lineage) && !string.IsNullOrWhiteSpace(lineage);
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
        string? thread = null;
        string? continuity = null;
        output?.Metadata.TryGetValue("thread-id", out thread);
        output?.Metadata.TryGetValue("continuity", out continuity);
        return (thread, continuity);
    }

    private static async Task<bool> HasDurableSliceFactsAsync(Repository repository, CancellationToken token)
    {
        string database = LoopRelayWorkspaceDatabase.Resolve(repository);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(database);
        await connection.OpenAsync(token);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM workspace_metadata WHERE key = 'execution_warm_session.v1';";
        object? raw = await command.ExecuteScalarAsync(token);
        if (raw is not string json) return false;
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        return root.TryGetProperty("providerThreadId", out JsonElement thread) && thread.GetString() is { Length: > 0 } &&
            root.TryGetProperty("inputSnapshotHash", out JsonElement input) && input.GetString() is { Length: > 0 } &&
            root.TryGetProperty("changedPaths", out JsonElement paths) && paths.ValueKind == JsonValueKind.Array &&
            root.TryGetProperty("sliceBaseline", out JsonElement baseline) && baseline.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("handoffCompleted", out JsonElement handoff) && handoff.GetBoolean();
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
            new[] { "commit", "-m", "seed execute agent artifacts" },
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
            new[] { "commit", "-m", "seed execute certification" },
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
        ProcessResult result = await RunProcessAsync("git", arguments, root, token);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {arguments[0]} failed: {result.StandardError}");
        }
    }

    private static Dictionary<string, string> SnapshotRepository(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path).Replace('\\', '/').StartsWith(".git/", StringComparison.Ordinal) &&
                !Path.GetRelativePath(root, path).Replace('\\', '/').StartsWith(".agents/.git/", StringComparison.Ordinal) &&
                !Path.GetRelativePath(root, path).Replace('\\', '/').StartsWith(".LoopRelay/", StringComparison.Ordinal))
            .ToDictionary(
                path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                path => Digest(File.ReadAllBytes(path)),
                StringComparer.Ordinal);

    private static string[] ChangedPaths(
        IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after) => before.Keys.Concat(after.Keys)
        .Distinct(StringComparer.Ordinal)
        .Where(path => !before.TryGetValue(path, out string? beforeHash) ||
            !after.TryGetValue(path, out string? afterHash) || beforeHash != afterHash)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static IReadOnlyList<string> Diagnostics(string? actual, ProcessResult result)
    {
        var diagnostics = new List<string> { $"actual-transition:{actual ?? "missing"}" };
        if (result.ExitCode != 0)
        {
            diagnostics.Add($"explanation:{ParseOutputValue(result.StandardOutput, "Explanation") ?? "missing"}");
            diagnostics.Add($"stdout-digest:{Digest(result.StandardOutput)}");
            diagnostics.Add($"stderr-digest:{Digest(result.StandardError)}");
        }
        return diagnostics;
    }

    private static string? ParseOutputValue(string output, string label) => output
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Trim())
        .Where(line => line.StartsWith(label + ": ", StringComparison.Ordinal))
        .Select(line => line[(label.Length + 2)..])
        .LastOrDefault();

    private static async Task<ProcessResult> RunCliAsync(
        string cliPath, string repository, IReadOnlyList<string> arguments, CancellationToken token)
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
        return await RunProcessAsync(file, all, repository, token, CertificationFixtureSettings.ProviderTurnTimeout);
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string file,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken token,
        TimeSpan? timeout = null)
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
        timeoutSource.CancelAfter(timeout ?? TimeSpan.FromMinutes(2));
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
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }

    private static HashSet<int> CodexProcessIds() => Process.GetProcessesByName("codex")
        .Select(process => { try { return process.Id; } finally { process.Dispose(); } })
        .ToHashSet();

    private static async Task WriteAsync(string root, string relative, string content, CancellationToken token)
    {
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, token);
    }

    private static string FileHashIfPresent(string root, string relative)
    {
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? Digest(File.ReadAllBytes(path)) : "logical";
    }

    private static string Digest(string value) => Digest(Encoding.UTF8.GetBytes(value));
    private static string Digest(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
