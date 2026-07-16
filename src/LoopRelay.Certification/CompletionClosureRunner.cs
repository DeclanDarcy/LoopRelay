using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

public sealed class CompletionClosureRunner(ICertificationFailureDiagnoser? failureDiagnoser = null)
{
    private static readonly string[] Transitions =
    [
        "RunCompletionCertification",
        "InterpretCompletionRoute",
        "VerifyWorkflowExitGate",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async Task<CompletionClosureCertificationResult> RunAsync(
        string codexExecutable,
        string authFile,
        string cliPath,
        string authorityRoot,
        CancellationToken cancellationToken = default)
    {
        string root = Path.Combine(authorityRoot, "completion-closure", Guid.NewGuid().ToString("N"));
        string repositoryPath = Path.Combine(root, "repository");
        string codexHome = Path.Combine(root, "codex-home");
        Directory.CreateDirectory(codexHome);
        Directory.CreateDirectory(repositoryPath);
        File.Copy(authFile, Path.Combine(codexHome, "auth.json"));
        string? priorHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        string? priorExecutable = Environment.GetEnvironmentVariable("CODEX_EXECUTABLE");
        string? priorAnalytics = Environment.GetEnvironmentVariable("CODEX_ANALYTICS_ENABLED");
        string? priorSettings = Environment.GetEnvironmentVariable("LOOPRELAY_SETTINGS_PATH");
        Environment.SetEnvironmentVariable("CODEX_HOME", codexHome);
        Environment.SetEnvironmentVariable("CODEX_EXECUTABLE", codexExecutable);
        Environment.SetEnvironmentVariable("CODEX_ANALYTICS_ENABLED", "false");
        string settingsPath = await CertificationFixtureSettings.WriteAsync(root, cliPath, cancellationToken);
        Environment.SetEnvironmentVariable("LOOPRELAY_SETTINGS_PATH", settingsPath);
        HashSet<int> initialCodex = CodexProcessIds();
        var transitions = new List<ExecuteTransitionCaseResult>();
        var evidence = new List<string>();
        string version = "unknown";
        string schema = "unknown";
        bool retainCase = false;
        string? failedInvocationId = null;
        string? failedTransition = null;
        try
        {
            CodexInstalledCompatibilityIdentity identity = CodexCompatibilityIdentityProbe.Resolve();
            version = identity.ServerVersion ?? "unknown";
            schema = identity.SchemaDigest ?? "unknown";
            if (CodexCompatibilityManifest.LoadEmbedded().FindExact(version, schema) is null)
            {
                return await Finish(CertificationClassification.UnsupportedCapability);
            }

            await SeedRepositoryAsync(repositoryPath, cancellationToken);
            ProcessResult preCompletionVerifier = await RunProcessAsync(
                "pwsh",
                ["-NoProfile", "-File", "verify.ps1"],
                repositoryPath,
                TimeSpan.FromMinutes(2),
                cancellationToken);
            if (preCompletionVerifier.ExitCode != 0)
            {
                throw new InvalidOperationException("Pre-completion independent verifier failed.");
            }
            await InitializeGitAsync(repositoryPath, cancellationToken);
            string harnessVerifierEvidence = await HarnessVerifierEvidenceAsync(repositoryPath, cancellationToken);
            await WriteAsync(
                repositoryPath,
                ".agents/evidence/execution/harness-verifier-result.md",
                harnessVerifierEvidence,
                cancellationToken);
            await CommitHarnessEvidenceAsync(repositoryPath, cancellationToken);
            ProcessResult init = await RunCliAsync(cliPath, repositoryPath, ["storage", "init"], cancellationToken);
            if (init.ExitCode != 0) throw new InvalidOperationException("Completion-closure storage initialization failed.");
            var repository = new Repository { Id = Guid.NewGuid(), Name = "completion-closure", Path = repositoryPath };
            await new SqliteExecutionEvidenceStore(repository)
                .WriteAsync("harness-verifier-result", harnessVerifierEvidence);
            await SeedCompletionEntryAsync(repository, cancellationToken);

            bool checkpointAfterCertification = false;
            for (int index = 0; index < Transitions.Length; index++)
            {
                string expected = Transitions[index];
                ProcessResult run = await RunCliAsync(cliPath, repositoryPath, ["execute"], cancellationToken);
                failedInvocationId = run.CertificationInvocationId;
                failedTransition = expected;
                string? actual = ParseOutputValue(run.StandardOutput, "Transition");
                bool completed = run.ExitCode == 0 && string.Equals(actual, expected, StringComparison.Ordinal);
                IReadOnlyList<string> diagnostics = Diagnostics(actual, run);
                if (!completed && expected == "RunCompletionCertification")
                {
                    diagnostics = diagnostics.Concat(CompletionEvaluationDiagnostics(repositoryPath)).ToArray();
                }
                transitions.Add(new ExecuteTransitionCaseResult(
                    expected,
                    run.ExitCode,
                    [],
                    completed,
                    diagnostics));
                if (!completed) break;
                if (index == 0)
                {
                    checkpointAfterCertification = await MetadataExistsAsync(
                        repository,
                        "completion_certification.v1",
                        cancellationToken);
                }
            }

            CanonicalWorkflowPersistenceSnapshot snapshot =
                await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync(cancellationToken);
            bool canonicalClosure = snapshot.WorkflowStates.Any(state =>
                    state.Workflow == WorkflowIdentity.Execute &&
                    state.State == WorkflowResolutionState.Completed &&
                    state.CurrentStage is null) &&
                snapshot.Products.Any(product =>
                    product.Identity == ProductIdentity.CertifiedCompletion &&
                    product.ValidationState == ProductValidationState.Valid);
            bool archiveComplete = File.Exists(Path.Combine(repositoryPath, ".agents", "archive", "epics", "1.md")) &&
                Directory.Exists(Path.Combine(repositoryPath, ".agents", "archive", "epics", "1")) &&
                Directory.EnumerateFiles(
                    Path.Combine(repositoryPath, ".agents", "archive", "epics", "1"),
                    "*",
                    SearchOption.AllDirectories).Any();
            string roadmapContext = await File.ReadAllTextAsync(
                Path.Combine(repositoryPath, ".agents", "core", "roadmap-completion-context.md"),
                cancellationToken);
            bool roadmapUpdated = !roadmapContext.Contains("No completed epics yet", StringComparison.Ordinal) &&
                roadmapContext.Contains("Greeting", StringComparison.OrdinalIgnoreCase);
            bool continuityRetired =
                !await MetadataExistsAsync(repository, "completion_certification.v1", cancellationToken) &&
                !await MetadataExistsAsync(repository, "execution_warm_session.v1", cancellationToken);
            string archivePath = Path.Combine(repositoryPath, ".agents", "archive");
            string archiveBeforeRerun = TreeFingerprint(archivePath);
            string gitBeforeRerun = await GitStateFingerprintAsync(repositoryPath, cancellationToken);
            int sessionFilesBeforeRerun = SessionFileCount(codexHome);
            ProcessResult? rerun = canonicalClosure
                ? await RunCliAsync(cliPath, repositoryPath, ["execute"], cancellationToken)
                : null;
            int sessionFilesAfterRerun = SessionFileCount(codexHome);
            string archiveAfterRerun = TreeFingerprint(archivePath);
            string gitAfterRerun = await GitStateFingerprintAsync(repositoryPath, cancellationToken);
            CanonicalWorkflowPersistenceSnapshot rerunSnapshot =
                await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync(cancellationToken);
            bool closureUnchanged = canonicalClosure && rerunSnapshot.WorkflowStates.Any(state =>
                    state.Workflow == WorkflowIdentity.Execute &&
                    state.State == WorkflowResolutionState.Completed &&
                    state.CurrentStage is null) &&
                rerunSnapshot.Products.Any(product =>
                    product.Identity == ProductIdentity.CertifiedCompletion &&
                    product.ValidationState == ProductValidationState.Valid);
            bool idempotent = rerun is not null && rerun.ExitCode == 0 &&
                ParseOutputValue(rerun.StandardOutput, "Transition") is null &&
                sessionFilesBeforeRerun == sessionFilesAfterRerun &&
                archiveBeforeRerun == archiveAfterRerun &&
                gitBeforeRerun == gitAfterRerun &&
                closureUnchanged;
            ProcessResult verifier = await RunProcessAsync(
                "pwsh",
                ["-NoProfile", "-File", "verify.ps1"],
                repositoryPath,
                TimeSpan.FromMinutes(2),
                cancellationToken);
            bool independentAcceptance = verifier.ExitCode == 0;
            await Task.Delay(500, cancellationToken);
            bool processesClean = CodexProcessIds().All(pid => initialCodex.Contains(pid));
            bool restartRoute = checkpointAfterCertification &&
                transitions.Count > 1 && transitions[1].Completed;
            evidence.AddRange(
            [
                $"model:{CertificationFixtureSettings.BrainModel}",
                $"effort:{CertificationFixtureSettings.BrainEffort}",
                $"completion-checkpoint-after-certification:{checkpointAfterCertification}",
                $"archive-complete:{archiveComplete}",
                $"roadmap-context-updated:{roadmapUpdated}",
                $"canonical-closure:{canonicalClosure}",
                $"continuity-retired:{continuityRetired}",
                $"rerun-session-files:{sessionFilesBeforeRerun}->{sessionFilesAfterRerun}",
                $"rerun-exit:{rerun?.ExitCode.ToString() ?? "not-attempted-before-closure"}",
                $"rerun-stop-reason:{(rerun is null ? "not-attempted-before-closure" : ParseOutputValue(rerun.StandardOutput, "Stop reason") ?? "missing")}",
                $"rerun-archive-unchanged:{archiveBeforeRerun == archiveAfterRerun}",
                $"rerun-git-unchanged:{gitBeforeRerun == gitAfterRerun}",
                $"rerun-closure-unchanged:{closureUnchanged}",
                $"independent-verifier-exit:{verifier.ExitCode}",
            ]);
            bool passed = transitions.Count == Transitions.Length && transitions.All(item => item.Completed) &&
                restartRoute && archiveComplete && roadmapUpdated && canonicalClosure && continuityRetired &&
                idempotent && independentAcceptance && processesClean;
            retainCase = !passed;
            if (retainCase) evidence.Add($"retained-case:completion-closure/{Path.GetFileName(root)}");
            return await Finish(
                LiveProviderFailureClassifier.Classify(passed, codexHome),
                restartRoute,
                archiveComplete,
                roadmapUpdated,
                canonicalClosure,
                continuityRetired,
                idempotent,
                independentAcceptance,
                processesClean);
        }
        catch (Exception exception) when (exception is not OperationCanceledException &&
            exception is not CertificationRetentionException)
        {
            retainCase = true;
            evidence.AddRange([exception.GetType().Name, exception.Message]);
            evidence.Add($"retained-case:completion-closure/{Path.GetFileName(root)}");
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
            if (!retainCase && Directory.Exists(root))
            {
                foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(root, recursive: true);
            }
        }

        async Task<CompletionClosureCertificationResult> Finish(
            CertificationClassification classification,
            bool restartRoute = false,
            bool archiveComplete = false,
            bool roadmapUpdated = false,
            bool canonicalClosure = false,
            bool continuityRetired = false,
            bool idempotent = false,
            bool independentAcceptance = false,
            bool processesClean = false)
        {
            IReadOnlyList<string> privacy = PrivacyScanner.Scan(
                string.Join("\n", evidence.Concat(transitions.SelectMany(item => item.Diagnostics))),
                authorityRoot);
            if (privacy.Count > 0) classification = CertificationClassification.OracleDrift;
            retainCase = CertificationCaseRetention.ShouldPreserve(false, classification);
            string? invocationId = classification == CertificationClassification.Passed
                ? null
                : failedInvocationId ?? $"completion-closure-{Guid.NewGuid():N}";
            var result = new CompletionClosureCertificationResult(
                CertificationEvidenceSchema.Version,
                classification,
                version,
                schema,
                transitions,
                restartRoute,
                archiveComplete,
                roadmapUpdated,
                canonicalClosure,
                continuityRetired,
                idempotent,
                independentAcceptance,
                processesClean,
                privacy,
                evidence,
                invocationId);
            if (classification != CertificationClassification.Passed)
            {
                bool quota = LiveProviderFailureClassifier.HasQuotaExhaustion(codexHome);
                CertificationDiagnosisOutcome diagnosis = await (failureDiagnoser ?? new CertificationFailureDiagnoser())
                    .DiagnoseIfNeededAsync(
                        new CertificationFailureContext(
                            invocationId!,
                            failedTransition is not null,
                            classification,
                            quota,
                            FailureExplanation(transitions, evidence),
                            quota
                                ? ["codex-rollout:used-percent:100", "codex-rollout:last-agent-message:null"]
                                : transitions.LastOrDefault(item => !item.Completed)?.Diagnostics ?? evidence,
                            quota ? "Wait until the confirmed provider quota window resets before an explicit rerun." : null,
                            result,
                            authorityRoot,
                            repositoryPath,
                            codexHome,
                            codexExecutable,
                            CertificationSourceSelection.ResolveExisting(
                            [
                                "src/LoopRelay.Certification/CompletionClosureRunner.cs",
                                "src/LoopRelay.Core/Prompts/GenerateHandoff.prompt",
                                "src/LoopRelay.Cli/Services/Cli/CompositionPromptExecutionOwner.cs",
                            ]),
                            failedTransition),
                        cancellationToken);
                result = result with { AttemptRecord = diagnosis.AttemptRecord, Diagnosis = diagnosis };
            }
            string path = Path.Combine(authorityRoot, "evidence", "completion-closure.latest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using FileStream stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
            return result;
        }
    }

    private static async Task SeedRepositoryAsync(string root, CancellationToken token)
    {
        await WriteAsync(root, ".gitignore", ".LoopRelay/\n", token);
        await WriteAsync(root, ".gitattributes", "*.ps1 text eol=lf\n*.md text eol=lf\n", token);
        await WriteAsync(root, "README.md", "# Completion certification repository\n", token);
        await WriteAsync(root, "GREETING.md", "Hello from Loop Relay.\n", token);
        await WriteAsync(root, "verify.ps1", """
            $path = Join-Path $PSScriptRoot 'GREETING.md'
            if (-not (Test-Path -LiteralPath $path)) { exit 1 }
            if ((Get-Content -LiteralPath $path -Raw) -ne "Hello from Loop Relay.`n") { exit 1 }
            exit 0
            """, token);
        await WriteAsync(root, ".agents/epic.md", """
            # Epic: Deterministic Greeting Capability

            ## Epic Metadata
            | Field | Value |
            |---|---|
            | Epic ID | M11 |

            ## Strategic Purpose
            Deliver one independently verifiable executable greeting capability.

            ## Desired Capability
            The repository returns the exact greeting required by its acceptance oracle.

            ## Acceptance Criteria
            - `verify.ps1` exits zero against repository truth.

            ## Constraints
            - `verify.ps1` remains byte-identical to its committed Git baseline.
            - `GREETING.md` contains only the exact required greeting and newline.

            ## Success Conditions
            - Independent harness evidence records a zero verifier exit.
            - Git object identity proves the verifier was not modified.
            - The sole milestone is checked only after the acceptance signal passes.

            ## Milestone Roadmap
            | Milestone ID | Milestone Name | Purpose | Outcome | Depends On | Completion Signal |
            |---|---|---|---|---|---|
            | M1 | Greeting | Implement capability | Exact greeting | None | Verifier exits zero |
            """, token);
        await WriteAsync(root, ".agents/plan.md", "# Plan\n\nImplement and verify the deterministic greeting.\n", token);
        await WriteAsync(root, ".agents/milestones/m1.md", """
            # Milestone 1: Deterministic Greeting

            ## Intended Outcome
            The exact greeting capability exists and is independently executable.

            ## Acceptance Criteria
            - [x] `GREETING.md` contains the exact required greeting and one newline.
            - [x] `verify.ps1` exits zero without modification from its committed baseline.

            ## Validation Strategy
            Run `pwsh -NoProfile -File verify.ps1`, record its exit code, compare the verifier's working-tree
            Git blob identity with `HEAD:verify.ps1`, and record SHA-256 identities for verifier and capability.

            ## Completion Evidence
            Harness-owned evidence is committed under `.agents/evidence/execution` and persisted in SQLite
            before completion certification. Its ordering record proves the verifier passed before this checked
            completion claim was submitted to certification.
            """, token);
        await WriteAsync(root, ".agents/handoffs/handoff.md", "# Handoff\n\nGreeting implemented and verifier passed.\n", token);
        await WriteAsync(root, ".agents/decisions.md", "# Decisions\n\nUse exact byte-level acceptance.\n", token);
        await WriteAsync(root, ".agents/operational_delta.md", "# Operational Delta\n\nGreeting capability is complete.\n", token);
        await WriteAsync(root, ".agents/core/roadmap-completion-context.md", "# Roadmap Completion Context\n\nNo completed epics yet.\n", token);
        await WriteAsync(root, ".LoopRelay/evidence/execute-repository-state/PublishRepositoryState.md", "# Repository Changes\n\nGREETING.md implemented.\n", token);
        int index = 0;
        string[] context =
        [
            "# Purpose\n\nDeliver deterministic executable capability.",
            "# Capability Model\n\nThe greeting is implemented and independently observable.",
            "# Invariants\n\nThe exact greeting and verifier must remain unchanged.",
            "# Strategic Structure\n\nThe sole epic is ready to close when evidence agrees.",
            "# Authority Model\n\nRepository truth and the verifier can veto model prose.",
            "# Evaluation Model\n\nA zero verifier exit and checked milestone are required.",
            "# Drift And False Success\n\nUnchecked work or verifier failure prohibits closure.",
            "# Vocabulary\n\nCertified completion means repository evidence and policy agree.",
            "# Eval Details\n\nNo evaluation dependency remains unresolved.",
        ];
        foreach (string path in ProjectContextSourceContract.SourceFiles)
        {
            await WriteAsync(root, path, context[index++] + "\n", token);
        }
    }

    private static async Task<string> HarnessVerifierEvidenceAsync(string root, CancellationToken token)
    {
        string verifier = Path.Combine(root, "verify.ps1");
        string capability = Path.Combine(root, "GREETING.md");
        ProcessResult head = await RunProcessAsync(
            "git", ["rev-parse", "HEAD"], root, TimeSpan.FromMinutes(2), token);
        ProcessResult committedVerifier = await RunProcessAsync(
            "git", ["rev-parse", "HEAD:verify.ps1"], root, TimeSpan.FromMinutes(2), token);
        ProcessResult workingVerifier = await RunProcessAsync(
            "git", ["hash-object", "verify.ps1"], root, TimeSpan.FromMinutes(2), token);
        ProcessResult verifierDiff = await RunProcessAsync(
            "git", ["diff", "--exit-code", "--", "verify.ps1"], root, TimeSpan.FromMinutes(2), token);
        ProcessResult status = await RunProcessAsync(
            "git", ["status", "--porcelain=v1"], root, TimeSpan.FromMinutes(2), token);
        if (head.ExitCode != 0 || committedVerifier.ExitCode != 0 || workingVerifier.ExitCode != 0 ||
            verifierDiff.ExitCode != 0 || status.ExitCode != 0)
        {
            throw new InvalidOperationException("Unable to establish independent Git verifier integrity evidence.");
        }
        bool blobIdentity = committedVerifier.StandardOutput.Trim() == workingVerifier.StandardOutput.Trim();
        return $"""
            # Independent Harness Verifier Result

            | Field | Value |
            |---|---|
            | Authority | LoopRelay.Certification independent harness |
            | Command | `pwsh -NoProfile -File verify.ps1` |
            | Exit Code | 0 |
            | Verifier SHA-256 | {Digest(await File.ReadAllBytesAsync(verifier, token))} |
            | Capability SHA-256 | {Digest(await File.ReadAllBytesAsync(capability, token))} |
            | Capability Exists | True |
            | Capability Exact Text | Hello from Loop Relay. plus one newline |
            | Git HEAD | {head.StandardOutput.Trim()} |
            | Committed Verifier Git Blob | {committedVerifier.StandardOutput.Trim()} |
            | Working Verifier Git Blob | {workingVerifier.StandardOutput.Trim()} |
            | Verifier Git Blob Identity Matches | {blobIdentity} |
            | Verifier Diff Exit Code | {verifierDiff.ExitCode} |
            | Repository Status Clean | {string.IsNullOrWhiteSpace(status.StandardOutput)} |
            | Executed Before Completion Certification | True |
            | Evidence Repository Embedded Before Certification | True |
            | Milestone Completion Claim Present Before Certification | True |

            ## Certification Ordering

            1. The harness created the exact greeting capability and immutable verifier.
            2. The harness ran `pwsh -NoProfile -File verify.ps1`; it exited `0`.
            3. Git independently proved that the working verifier blob `{workingVerifier.StandardOutput.Trim()}`
               exactly equals the committed `HEAD:verify.ps1` blob `{committedVerifier.StandardOutput.Trim()}`.
            4. The checked milestone completion claim was already present when the verifier ran.
            5. This evidence is committed into repository truth before completion certification starts.

            Verifier immutability is therefore positively established: the working and committed Git blob
            identities are equal, and `git diff --exit-code -- verify.ps1` returned `0`. This is harness-owned,
            non-model-authored evidence. Repository truth and policy remain authoritative.
            """;
    }

    private static async Task SeedCompletionEntryAsync(Repository repository, CancellationToken token)
    {
        var store = new CanonicalWorkflowPersistenceStore(repository);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string[] seedEvidence = ["certification:completion-closure:completion-entry"];
        await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
            WorkflowIdentity.Execute,
            WorkflowResolutionState.Resumable,
            new WorkflowStageIdentity("Completion"),
            RuntimeOutcomeKind.Waiting,
            now,
            seedEvidence), token);
        await store.UpsertStageStateAsync(new CanonicalStageStateRecord(
            WorkflowIdentity.Execute,
            new WorkflowStageIdentity("Completion"),
            WorkflowResolutionState.Active,
            now,
            seedEvidence), token);
        await store.UpsertProductAsync(Product(
            ProductIdentity.RepositoryChanges,
            new WorkflowTransitionIdentity("PublishRepositoryState"),
            [".LoopRelay/evidence/execute-repository-state/PublishRepositoryState.md"]), token);
        await store.UpsertProductAsync(Product(
            ProductIdentity.ExecutionHandoff,
            new WorkflowTransitionIdentity("GenerateHandoff"),
            [".agents/handoffs/handoff.md"]), token);
        foreach (string transition in new[] { "EvaluateMilestoneCompletion", "RunNonImplementationReview" })
        {
            await store.UpsertTransitionRunAsync(new CanonicalTransitionRunRecord(
                "m11-seed-" + transition,
                WorkflowIdentity.Execute,
                new WorkflowStageIdentity("Completion"),
                new WorkflowTransitionIdentity(transition),
                TransitionDurableState.Completed,
                RuntimeOutcomeKind.Completed,
                now,
                now,
                Digest(transition),
                "Seeded as completed to target singular closure certification.",
                seedEvidence), token);
        }
    }

    private static ProductRecord Product(
        ProductIdentity identity,
        WorkflowTransitionIdentity transition,
        IReadOnlyList<string> paths) =>
        new(
            identity,
            WorkflowIdentity.Execute,
            transition,
            [WorkflowIdentity.Execute],
            "repository-owned certification seed",
            "independent completion-closure fixture",
            paths,
            Digest(string.Join('|', paths)),
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            paths);

    private static async Task<bool> MetadataExistsAsync(
        Repository repository,
        string key,
        CancellationToken token)
    {
        string database = LoopRelayWorkspaceDatabase.Resolve(repository);
        if (!File.Exists(database)) return false;
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(database);
        await connection.OpenAsync(token);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM workspace_metadata WHERE key = $key LIMIT 1;";
        command.Parameters.AddWithValue("$key", key);
        return await command.ExecuteScalarAsync(token) is not null;
    }

    private static async Task InitializeGitAsync(string root, CancellationToken token)
    {
        string caseRoot = Path.GetDirectoryName(root)!;
        string remote = Path.Combine(caseRoot, "repository-remote.git");
        string agentsRemote = Path.Combine(caseRoot, "agents-remote.git");
        Directory.CreateDirectory(remote);
        Directory.CreateDirectory(agentsRemote);
        await RequireGitAsync(caseRoot, ["init", "--bare", "--initial-branch=main", remote], token);
        await RequireGitAsync(caseRoot, ["init", "--bare", "--initial-branch=main", agentsRemote], token);
        string agents = Path.Combine(root, ".agents");
        foreach (string[] arguments in new[]
        {
            new[] { "init", "-b", "main" },
            new[] { "config", "user.email", "certification@looprelay.invalid" },
            new[] { "config", "user.name", "LoopRelay Certification" },
            new[] { "add", "." },
            new[] { "commit", "-m", "seed completion agent artifacts" },
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
            new[] { "commit", "-m", "seed completion certification" },
            new[] { "remote", "add", "origin", remote },
            new[] { "push", "-u", "origin", "main" },
        })
        {
            await RequireGitAsync(root, arguments, token);
        }
    }

    private static async Task CommitHarnessEvidenceAsync(string root, CancellationToken token)
    {
        string agents = Path.Combine(root, ".agents");
        foreach (string[] arguments in new[]
        {
            new[] { "add", "evidence/execution/harness-verifier-result.md" },
            new[] { "commit", "-m", "record independent verifier evidence" },
            new[] { "push", "origin", "main" },
        })
        {
            await RequireGitAsync(agents, arguments, token);
        }
        foreach (string[] arguments in new[]
        {
            new[] { "add", ".agents" },
            new[] { "commit", "-m", "record independent verifier evidence gitlink" },
            new[] { "push", "origin", "main" },
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
        ProcessResult result = await RunProcessAsync(
            "git", arguments, root, TimeSpan.FromMinutes(2), token);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {arguments[0]} failed: {result.StandardError}{result.StandardOutput}");
        }
    }

    private static int SessionFileCount(string codexHome) => Directory.Exists(Path.Combine(codexHome, "sessions"))
        ? Directory.EnumerateFiles(Path.Combine(codexHome, "sessions"), "*.jsonl", SearchOption.AllDirectories).Count()
        : 0;

    private static string TreeFingerprint(string root)
    {
        if (!Directory.Exists(root)) return Digest("missing");
        var fingerprint = new StringBuilder();
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .OrderBy(path => Path.GetRelativePath(root, path), StringComparer.Ordinal))
        {
            fingerprint.Append(Path.GetRelativePath(root, path).Replace('\\', '/'))
                .Append(':')
                .Append(Digest(File.ReadAllBytes(path)))
                .Append('\n');
        }
        return Digest(fingerprint.ToString());
    }

    private static async Task<string> GitStateFingerprintAsync(string root, CancellationToken token)
    {
        ProcessResult head = await RunProcessAsync(
            "git", ["rev-parse", "HEAD"], root, TimeSpan.FromMinutes(2), token);
        ProcessResult status = await RunProcessAsync(
            "git", ["status", "--porcelain=v1", "--untracked-files=all"], root, TimeSpan.FromMinutes(2), token);
        if (head.ExitCode != 0 || status.ExitCode != 0)
        {
            throw new InvalidOperationException("Unable to fingerprint Git state for completion rerun.");
        }
        return Digest(head.StandardOutput + "\n" + status.StandardOutput);
    }

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

    private static IReadOnlyList<string> CompletionEvaluationDiagnostics(string repository)
    {
        string directory = Path.Combine(repository, ".agents", "evidence", "evaluations");
        if (!Directory.Exists(directory)) return ["completion-evaluation:missing"];
        string? latest = Directory.EnumerateFiles(directory, "*.md")
            .OrderBy(File.GetLastWriteTimeUtc)
            .LastOrDefault();
        if (latest is null) return ["completion-evaluation:missing"];
        string[] relevant = File.ReadLines(latest)
            .Select(line => line.Trim())
            .Where(line =>
                line.StartsWith("| Overall Completion Status |", StringComparison.Ordinal) ||
                line.StartsWith("| Overall Drift Classification |", StringComparison.Ordinal) ||
                line.StartsWith("| Closure Recommendation |", StringComparison.Ordinal) ||
                line.StartsWith("| Primary Reason |", StringComparison.Ordinal) ||
                line.StartsWith("| Gap |", StringComparison.Ordinal) ||
                line.StartsWith("| Limitation |", StringComparison.Ordinal) ||
                line.StartsWith("| Residual Work |", StringComparison.Ordinal))
            .Take(16)
            .Select(line => $"completion-evaluation:{line}")
            .ToArray();
        return relevant.Length == 0 ? ["completion-evaluation:no-summary-lines"] : relevant;
    }

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
        string invocationId = CertificationInvocation.NewId();
        ProcessResult result = await RunProcessAsync(
            file,
            all,
            repository,
            CertificationFixtureSettings.ProviderTurnTimeout,
            token,
            invocationId);
        return result with { CertificationInvocationId = invocationId };
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string file,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken token,
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

    private static string Digest(byte[] value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));

    private static string FailureExplanation(
        IReadOnlyList<ExecuteTransitionCaseResult> transitions,
        IReadOnlyList<string> evidence)
    {
        ExecuteTransitionCaseResult? failed = transitions.LastOrDefault(item => !item.Completed);
        return failed is null
            ? evidence.LastOrDefault() ?? "Completion-closure certification failed after its live transition sequence."
            : $"Transition {failed.Transition} failed with exit code {failed.ExitCode}: {string.Join("; ", failed.Diagnostics)}";
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        string? CertificationInvocationId = null);
}
