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

public sealed class RoadmapLiveRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<RoadmapLiveCertificationResult> RunAsync(
        WorkflowIdentity workflow,
        string codexExecutable,
        string authFile,
        string cliPath,
        string authorityRoot,
        CancellationToken cancellationToken = default)
    {
        bool traditional = workflow == WorkflowIdentity.TraditionalRoadmap;
        string milestone = traditional ? "milestone-9" : "milestone-10";
        string root = Path.Combine(authorityRoot, milestone, Guid.NewGuid().ToString("N"));
        string repositoryPath = Path.Combine(root, "repository");
        string codexHome = Path.Combine(root, "codex-home");
        Directory.CreateDirectory(codexHome);
        File.Copy(authFile, Path.Combine(codexHome, "auth.json"));
        string? priorHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        string? priorExecutable = Environment.GetEnvironmentVariable("CODEX_EXECUTABLE");
        string? priorSettings = Environment.GetEnvironmentVariable("LOOPRELAY_SETTINGS_PATH");
        Environment.SetEnvironmentVariable("CODEX_HOME", codexHome);
        Environment.SetEnvironmentVariable("CODEX_EXECUTABLE", codexExecutable);
        Environment.SetEnvironmentVariable("CODEX_ANALYTICS_ENABLED", "false");
        string effort = CertificationFixtureSettings.BrainEffort;
        string settingsPath = await CertificationFixtureSettings.WriteAsync(
            root, cliPath, cancellationToken);
        Environment.SetEnvironmentVariable("LOOPRELAY_SETTINGS_PATH", settingsPath);
        HashSet<int> initialCodex = CodexProcessIds();
        var transitions = new List<ExecuteTransitionCaseResult>();
        var evidence = new List<string>();
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

            Directory.CreateDirectory(repositoryPath);
            await SeedAsync(repositoryPath, traditional, cancellationToken);
            ProcessResult init = await RunCliAsync(cliPath, repositoryPath, ["storage", "init"], cancellationToken);
            if (init.ExitCode != 0) throw new InvalidOperationException("Roadmap storage init failed.");
            if (traditional)
            {
                var seededRepository = new Repository { Id = Guid.NewGuid(), Name = milestone, Path = repositoryPath };
                var store = new CanonicalWorkflowPersistenceStore(seededRepository);
                DateTimeOffset now = DateTimeOffset.UtcNow;
                string[] seedEvidence = ["certification:milestone-9:roadmap-context-entry"];
                await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
                    WorkflowIdentity.TraditionalRoadmap,
                    WorkflowResolutionState.Resumable,
                    new WorkflowStageIdentity("Roadmap Context"),
                    RuntimeOutcomeKind.Waiting,
                    now,
                    seedEvidence), cancellationToken);
                await store.UpsertStageStateAsync(new CanonicalStageStateRecord(
                    WorkflowIdentity.TraditionalRoadmap,
                    new WorkflowStageIdentity("Roadmap Context"),
                    WorkflowResolutionState.Active,
                    now,
                    seedEvidence), cancellationToken);
                await store.UpsertTransitionRunAsync(new CanonicalTransitionRunRecord(
                    "m9-route-audit-not-applicable",
                    WorkflowIdentity.TraditionalRoadmap,
                    new WorkflowStageIdentity("Epic Preparation"),
                    new WorkflowTransitionIdentity("AuditExistingEpic"),
                    TransitionDurableState.Completed,
                    RuntimeOutcomeKind.Completed,
                    now,
                    now,
                    Hash("m9-route-audit-not-applicable"),
                    "Stage-targeted new-epic route excludes existing-epic audit.",
                    seedEvidence), cancellationToken);
            }
            string[] expected = traditional
                ? [
                    "BootstrapRoadmapCompletionContext",
                    "SelectStrategicInitiative",
                    "CreateEpic",
                    "GenerateMilestoneDeepDivesForEpic",
                    "VerifyPlanEntryContract",
                ]
                : [
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
            string command = traditional ? "traditional" : "eval";
            foreach (string transition in expected)
            {
                ProcessResult run = await RunCliAsync(cliPath, repositoryPath, [command], cancellationToken);
                string? actual = ParseOutputValue(run.StandardOutput, "Transition");
                bool completed = run.ExitCode == 0 && actual == transition;
                transitions.Add(new ExecuteTransitionCaseResult(
                    transition,
                    run.ExitCode,
                    [],
                    completed,
                    Diagnostics(actual, run)));
                if (!completed) break;
            }

            if (transitions.Any(item => !item.Completed))
            {
                evidence.AddRange(ArtifactContractDiagnostics(repositoryPath, ".agents/selection.md", "selection"));
                evidence.AddRange(ArtifactContractDiagnostics(repositoryPath, ".agents/epic.md", "epic"));
            }

            var repository = new Repository { Id = Guid.NewGuid(), Name = milestone, Path = repositoryPath };
            CanonicalWorkflowPersistenceSnapshot snapshot =
                await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync(cancellationToken);
            ProductRecord? epicProduct = snapshot.Products.FirstOrDefault(product => product.Identity == ProductIdentity.PreparedEpic);
            ProductRecord? specsProduct = snapshot.Products.FirstOrDefault(product => product.Identity == ProductIdentity.MilestoneSpecificationSet);
            bool universal = epicProduct is not null && specsProduct is not null &&
                epicProduct.ValidationState == ProductValidationState.Valid &&
                specsProduct.ValidationState == ProductValidationState.Valid &&
                epicProduct.IntendedConsumers.Contains(WorkflowIdentity.Plan) &&
                specsProduct.IntendedConsumers.Contains(WorkflowIdentity.Plan);
            bool producer = epicProduct?.ProducerWorkflow == workflow && specsProduct?.ProducerWorkflow == workflow;
            string epicPath = Path.Combine(repositoryPath, ".agents", "epic.md");
            string specsDirectory = Path.Combine(repositoryPath, ".agents", "specs");
            string epic = File.Exists(epicPath) ? await File.ReadAllTextAsync(epicPath, cancellationToken) : string.Empty;
            string[] specs = Directory.Exists(specsDirectory)
                ? Directory.EnumerateFiles(specsDirectory, "*.md").ToArray()
                : [];
            bool structural = epic.Contains("# Epic", StringComparison.OrdinalIgnoreCase) &&
                epic.Contains("Acceptance", StringComparison.OrdinalIgnoreCase) &&
                specs.Length > 0 && specs.All(path => new FileInfo(path).Length > 0);
            bool bounded = snapshot.WorkflowStates.Any(state =>
                    state.Workflow == workflow && state.State == WorkflowResolutionState.Completed && state.CurrentStage is null) &&
                snapshot.TransitionRuns.All(run => run.Workflow != WorkflowIdentity.Plan) &&
                snapshot.WorkflowStates.All(state => state.Workflow != WorkflowIdentity.Plan);
            bool traceability = true;
            if (!traditional)
            {
                string[] evalArtifacts =
                [
                    ".agents/evals/eval-m10.md",
                    ".agents/eval-dependency-inventory.md",
                    ".agents/eval-hypothesis-inventory.md",
                    ".agents/eval-architectural-catalog.md",
                    ".agents/eval-dag.md",
                    ".agents/next-epic-roadmap.md",
                ];
                traceability = evalArtifacts.All(relative => File.Exists(Path.Combine(
                    repositoryPath, relative.Replace('/', Path.DirectorySeparatorChar))));
                evidence.Add($"eval-trace-artifacts:{evalArtifacts.Count(relative => File.Exists(Path.Combine(repositoryPath, relative.Replace('/', Path.DirectorySeparatorChar))))}/{evalArtifacts.Length}");
            }
            await Task.Delay(500, cancellationToken);
            bool processesClean = CodexProcessIds().All(pid => initialCodex.Contains(pid));
            evidence.AddRange(
            [
                $"workflow:{workflow.Value}",
                $"model:{CertificationFixtureSettings.BrainModel}",
                $"effort:{effort}",
                $"transitions:{transitions.Count}/{expected.Length}",
                $"prepared-epic:{universal}",
                $"producer-preserved:{producer}",
                $"structural-artifacts:{structural}",
                $"bounded-before-plan:{bounded}",
                $"traceability:{traceability}",
            ]);
            bool passed = transitions.Count == expected.Length && transitions.All(item => item.Completed) &&
                universal && producer && structural && bounded && traceability && processesClean;
            return await Finish(LiveProviderFailureClassifier.Classify(passed, codexHome),
                universal, structural, bounded, producer, processesClean);
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

        async Task<RoadmapLiveCertificationResult> Finish(
            CertificationClassification classification,
            bool universal = false,
            bool structural = false,
            bool bounded = false,
            bool producer = false,
            bool processesClean = false)
        {
            IReadOnlyList<string> privacy = PrivacyScanner.Scan(
                string.Join("\n", evidence.Concat(transitions.SelectMany(item => item.Diagnostics))), authorityRoot);
            if (privacy.Count > 0) classification = CertificationClassification.OracleDrift;
            var result = new RoadmapLiveCertificationResult(
                CertificationRunner.ResultSchemaVersion,
                classification,
                workflow.Value,
                version,
                schema,
                transitions,
                universal,
                structural,
                bounded,
                producer,
                processesClean,
                privacy,
                evidence);
            string path = Path.Combine(authorityRoot, "evidence", $"{milestone}.latest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using FileStream stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
            return result;
        }
    }

    private static async Task SeedAsync(string root, bool traditional, CancellationToken token)
    {
        await WriteAsync(root, "README.md", traditional
            ? """
                # Roadmap certification repository

                This repository exposes `IGreetingContract` but has no implementation, executable host, or tests.
                The next strategic initiative is the bounded implementation of that contract as an executable capability.
                """
            : """
                # Roadmap certification repository

                The sole capability is a deterministic greeting file whose acceptance is exact and executable.
                Plan a single implementation-first epic with one bounded milestone and no documentation deliverables.
                """, token);
        if (traditional)
        {
            await WriteAsync(root, "GreetingFixture.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                </Project>
                """, token);
            await WriteAsync(root, "src/IGreetingContract.cs", """
                namespace RoadmapCertification;

                public interface IGreetingContract
                {
                    string GetGreeting();
                }
                """, token);
        }
        string[] traditionalContext =
        [
            "# Purpose\n\nDeliver a deterministic greeting through implemented, executable software capability.",
            "# Capability Model\n\n`IGreetingContract` exists, but its implementation, executable host, and automated verification are missing.",
            "# Invariants\n\nThe greeting result must be exactly `Hello from Loop Relay.` and must be verified by an executable test.",
            "# Strategic Structure\n\nThere is no existing roadmap epic. The next initiative is a New Intermediary Epic named Deterministic Greeting Capability; it is not a split, investigation, or roadmap revision.",
            "# Authority Model\n\nRepository code is authoritative for the existing interface. This context is authoritative for the required missing capability.",
            "# Evaluation Model\n\nCompletion requires a concrete implementation, an executable entry point, and an automated exact-value test.",
            "# Drift And False Success\n\nDocumentation-only output, planning prose, or an unimplemented interface is false success.",
            "# Vocabulary\n\nGreeting capability means the implemented `IGreetingContract`, executable invocation, and automated verification together.",
            "# Eval Details\n\nThe bounded acceptance oracle invokes the capability and compares the exact returned value. No strategic investigation is required.",
        ];
        int index = 0;
        foreach (string path in ProjectContextSourceContract.SourceFiles)
        {
            index++;
            await WriteAsync(root, path, traditional
                ? traditionalContext[index - 1] + "\n"
                : $"# Context {index}\n\nPrefer one deterministic implementation capability with executable acceptance.\n", token);
        }
        if (!traditional)
        {
            await WriteAsync(root, ".agents/evals/eval-m10.md", """
                # Evaluation Intent EVAL-M10-001

                Determine the smallest implementation path for a deterministic `GREETING.md` capability.
                Required outcome: exact content `Hello from Loop Relay.` with one LF and an independent byte check.
                Negative control: prose-only or documentation-only output must not count as implementation.
                """, token);
        }
    }

    private static IReadOnlyList<string> Diagnostics(string? actual, ProcessResult result)
    {
        var diagnostics = new List<string> { $"actual-transition:{actual ?? "missing"}" };
        if (result.ExitCode != 0)
        {
            diagnostics.Add($"explanation:{ParseOutputValue(result.StandardOutput, "Explanation") ?? "missing"}");
            diagnostics.Add($"stdout-digest:{Hash(result.StandardOutput)}");
            diagnostics.Add($"stderr-digest:{Hash(result.StandardError)}");
        }
        return diagnostics;
    }

    private static IReadOnlyList<string> ArtifactContractDiagnostics(
        string repository,
        string relativePath,
        string label)
    {
        string path = Path.Combine(repository, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return [$"{label}-artifact:missing"];
        string[] contractLines = File.ReadLines(path)
            .Select(line => line.Trim())
            .Where(line =>
                line.StartsWith("# ", StringComparison.Ordinal) ||
                line.StartsWith("### Type", StringComparison.Ordinal) ||
                line.StartsWith("| Recommended Outcome |", StringComparison.Ordinal) ||
                line.StartsWith("| Initiative Type |", StringComparison.Ordinal) ||
                line.StartsWith("## Reason", StringComparison.Ordinal))
            .Take(8)
            .ToArray();
        return contractLines.Length == 0
            ? [$"{label}-artifact:no-contract-lines"]
            : contractLines.Select(line => $"{label}-contract:{line}").ToArray();
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
        var start = new ProcessStartInfo
        {
            FileName = file,
            WorkingDirectory = repository,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in all) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("CLI did not start.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(token);
        Task<string> stderr = process.StandardError.ReadToEndAsync(token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(CertificationFixtureSettings.ProviderTurnTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return new ProcessResult(process.ExitCode, await stdout, await stderr);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            return new ProcessResult(124, string.Empty, "Roadmap transition exceeded 12 minutes.");
        }
    }

    private static async Task WriteAsync(string root, string relative, string content, CancellationToken token)
    {
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, token);
    }

    private static HashSet<int> CodexProcessIds() => Process.GetProcessesByName("codex")
        .Select(process => { try { return process.Id; } finally { process.Dispose(); } })
        .ToHashSet();
    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
