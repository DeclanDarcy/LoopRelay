using System.Diagnostics;
using System.Text.Json;

namespace LoopRelay.Certification;

public sealed class MilestoneTwoRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async Task<MilestoneTwoCertificationResult> RunAsync(
        CertificationOptions options,
        CancellationToken cancellationToken = default)
    {
        string suiteRoot = Path.Combine(options.CaseAuthorityRoot, "milestone-2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(suiteRoot);
        var results = new List<PublicCliCaseResult>();
        try
        {
            results.Add(await RunCase("status-default", ["status"], 0, false, Seed.Empty,
                ["Invocation mode: DefaultChained", "Selected workflow: TraditionalRoadmap"]));
            results.Add(await RunCase("status-forced-eval", ["--eval", "status"], 0, false, Seed.Empty,
                ["Invocation mode: ForcedEvalChain", "Selected workflow: EvalRoadmap"]));
            results.Add(await RunCase("status-forced-traditional", ["--traditional", "status"], 0, false, Seed.Empty,
                ["Invocation mode: ForcedTraditionalChain", "Selected workflow: TraditionalRoadmap"]));
            results.Add(await RunCase("run-default-storage-block", [], 4, false, Seed.CorruptStorage,
                ["Storage authority:", "Corruption:"]));
            results.Add(await RunCase("bounded-traditional-storage-block", ["traditional"], 4, false, Seed.CorruptStorage,
                ["Storage authority:", "Corruption:"]));
            results.Add(await RunCase("bounded-eval", ["eval"], 4, true, Seed.Empty,
                ["Workflow: EvalRoadmap", "Stop reason: MissingRequiredInput"]));
            results.Add(await RunCase("bounded-plan", ["plan"], 4, true, Seed.Empty,
                ["Workflow: Plan", "Stop reason: MissingRequiredInput"]));
            results.Add(await RunCase("bounded-execute", ["execute"], 4, true, Seed.Empty,
                ["Workflow: Execute", "Stop reason: MissingRequiredInput"]));
            results.Add(await RunCase("storage-verify-missing", ["storage", "verify"], 0, false, Seed.Empty,
                ["Storage authority: Missing"]));
            results.Add(await RunCase("storage-export-missing", ["storage", "export"], 4, false, Seed.Empty,
                ["Storage export stopped because the workspace database is missing."]));
            results.Add(await RunCase("storage-init", ["storage", "init"], 0, true, Seed.Empty,
                ["Storage initialized."]));
            results.Add(await RunCase("storage-import", ["storage", "import"], 0, true, Seed.Empty,
                ["Storage import completed."]));
            results.Add(await RunCase("storage-sync", ["storage", "sync"], 0, true, Seed.Empty,
                ["Storage sync completed."]));
            results.Add(await RunCase("storage-export-initialized", ["storage", "export"], 0, false, Seed.InitializedStorage,
                ["Storage export completed with no filesystem mutations."]));
            results.Add(await RunCase("storage-verify-initialized", ["storage", "verify"], 0, false, Seed.InitializedStorage,
                ["Storage authority: CanonicalSqlite"]));
            results.Add(await RunCase("unblock-retired", ["unblock"], 2, false, Seed.Empty,
                [], expectedError: "Unknown command: unblock"));
            results.Add(await RunCase("invalid-option", ["--invalid-certification-option"], 2, false, Seed.Empty,
                [], expectedError: "Unknown option:"));
        }
        finally
        {
            if (!options.RetainCase && Directory.Exists(suiteRoot))
            {
                NormalizeAttributes(suiteRoot);
                Directory.Delete(suiteRoot, recursive: true);
            }
        }

        CertificationClassification classification = results.All(item => item.Passed)
            ? CertificationClassification.Passed
            : CertificationClassification.ProductRegression;
        var result = new MilestoneTwoCertificationResult(CertificationRunner.ResultSchemaVersion, classification, results);
        string evidencePath = Path.Combine(options.CaseAuthorityRoot, "evidence", "milestone-2.latest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
        await using FileStream stream = File.Create(evidencePath);
        await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
        return result;

        async Task<PublicCliCaseResult> RunCase(
            string identity,
            IReadOnlyList<string> arguments,
            int expectedExit,
            bool mutationExpected,
            Seed seed,
            IReadOnlyList<string> expectedOutput,
            string? expectedError = null)
        {
            string caseRoot = Path.Combine(suiteRoot, identity);
            string repository = Path.Combine(caseRoot, "repository");
            string providerHome = Path.Combine(caseRoot, "provider-home");
            Directory.CreateDirectory(repository);
            Directory.CreateDirectory(providerHome);
            await File.WriteAllTextAsync(Path.Combine(repository, "README.md"), "# deterministic CLI case\n", cancellationToken);
            if (seed == Seed.CorruptStorage)
            {
                string database = Path.Combine(repository, ".LoopRelay", "persistence", "looprelay.sqlite3");
                Directory.CreateDirectory(Path.GetDirectoryName(database)!);
                await File.WriteAllTextAsync(database, "not-a-sqlite-database", cancellationToken);
            }
            else if (seed == Seed.InitializedStorage)
            {
                ProcessResult initialization = await RunCli(options.CliPath, repository, providerHome, ["storage", "init"], cancellationToken);
                if (initialization.ExitCode != 0)
                {
                    return new PublicCliCaseResult(identity, arguments, expectedExit, initialization.ExitCode,
                        mutationExpected, false, false, false, string.Empty,
                        initialization.StandardError, ["storage initialization failed"]);
                }
            }

            IReadOnlyList<FileObservation> before = FileObserver.Observe(repository);
            ProcessResult process = await RunCli(options.CliPath, repository, providerHome, arguments, cancellationToken);
            IReadOnlyList<FileObservation> after = FileObserver.Observe(repository);
            bool mutation = FileObserver.Difference(before, after).Count > 0;
            bool providerState = Directory.EnumerateFileSystemEntries(providerHome).Any();
            string stdout = EvidenceNormalizer.Normalize(process.StandardOutput, repository);
            string stderr = EvidenceNormalizer.Normalize(process.StandardError, repository);
            var diagnostics = new List<string>();
            if (process.ExitCode != expectedExit)
            {
                diagnostics.Add($"expected exit {expectedExit}, observed {process.ExitCode}");
            }

            if (mutation != mutationExpected)
            {
                diagnostics.Add($"expected mutation={mutationExpected}, observed {mutation}");
            }

            foreach (string expected in expectedOutput.Where(expected => !stdout.Contains(expected, StringComparison.Ordinal)))
            {
                diagnostics.Add($"stdout missing: {expected}");
            }

            if (expectedError is not null && !stderr.Contains(expectedError, StringComparison.Ordinal))
            {
                diagnostics.Add($"stderr missing: {expectedError}");
            }

            if (providerState)
            {
                diagnostics.Add("deterministic case wrote provider state");
            }

            IReadOnlyList<string> privacy = PrivacyScanner.Scan($"{stdout}\n{stderr}", options.CaseAuthorityRoot);
            diagnostics.AddRange(privacy.Select(item => $"privacy:{item}"));
            return new PublicCliCaseResult(
                identity,
                arguments,
                expectedExit,
                process.ExitCode,
                mutationExpected,
                mutation,
                providerState,
                diagnostics.Count == 0,
                stdout,
                stderr,
                diagnostics);
        }
    }

    private static async Task<ProcessResult> RunCli(
        string cliPath,
        string repository,
        string providerHome,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
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
        if (cliPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add(cliPath);
        }

        start.ArgumentList.Add("--repo");
        start.ArgumentList.Add(repository);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["CODEX_HOME"] = providerHome;
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start public CLI.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private static void NormalizeAttributes(string root)
    {
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
    }

    private enum Seed { Empty, CorruptStorage, InitializedStorage }
    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
