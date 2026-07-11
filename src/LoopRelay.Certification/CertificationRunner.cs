using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace LoopRelay.Certification;

public sealed class CertificationRunner
{
    public const string ResultSchemaVersion = "1";
    public const string OracleVersion = "1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async Task<CanaryCertificationResult> RunStatusCanaryAsync(
        CertificationOptions options,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        ComposedCaseIdentity identity = FixtureComposer.ValidateAndIdentify(
            FixtureRepository.MinimalText,
            FixtureScenario.StatusCanary);
        var runs = new List<CertificationRunResult>();
        for (int index = 0; index < options.Repetitions; index++)
        {
            runs.Add(await RunCycleAsync(options, identity, cancellationToken));
        }

        bool reproducible = runs.Count >= 2 && runs.Skip(1).All(run =>
            run.Classification == runs[0].Classification &&
            run.ExitCode == runs[0].ExitCode &&
            run.NormalizedStandardOutput == runs[0].NormalizedStandardOutput &&
            run.NormalizedStandardError == runs[0].NormalizedStandardError &&
            run.BaseHash == runs[0].BaseHash &&
            run.PostRunHash == runs[0].PostRunHash &&
            run.Coverage.ProductionDigest == runs[0].Coverage.ProductionDigest);
        CertificationClassification classification = reproducible &&
            runs.All(run => run.Classification == CertificationClassification.Passed)
                ? CertificationClassification.Passed
                : runs.FirstOrDefault(run => run.Classification != CertificationClassification.Passed)?.Classification
                    ?? CertificationClassification.FixtureDrift;
        var result = new CanaryCertificationResult(ResultSchemaVersion, reproducible, classification, runs);
        string summaryPath = Path.Combine(options.CaseAuthorityRoot, "evidence", "status-canary.latest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(summaryPath)!);
        await WriteJsonAsync(summaryPath, result, cancellationToken);
        return result;
    }

    private static async Task<CertificationRunResult> RunCycleAsync(
        CertificationOptions options,
        ComposedCaseIdentity identity,
        CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        string runId = Guid.NewGuid().ToString("N");
        string runRoot = Path.Combine(options.CaseAuthorityRoot, "runs", runId);
        string repositoryPath = Path.Combine(runRoot, "repository");
        string providerHome = Path.Combine(runRoot, "provider-home");
        string evidenceRoot = Path.Combine(options.CaseAuthorityRoot, "evidence", runId);
        Directory.CreateDirectory(providerHome);
        await FixtureComposer.MaterializeAsync(FixtureRepository.MinimalText, repositoryPath, cancellationToken);
        IReadOnlyList<FileObservation> before = FileObserver.Observe(repositoryPath);
        string baseHash = FileObserver.Digest(before);

        ProcessResult process;
        try
        {
            process = await RunCliAsync(options.CliPath, repositoryPath, providerHome, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            process = new ProcessResult(-1, string.Empty, exception.Message);
        }

        IReadOnlyList<FileObservation> after = FileObserver.Observe(repositoryPath);
        string postRunHash = FileObserver.Digest(after);
        IReadOnlyList<FileObservation> mutations = FileObserver.Difference(before, after);
        string stdout = EvidenceNormalizer.Normalize(process.StandardOutput, repositoryPath);
        string stderr = EvidenceNormalizer.Normalize(process.StandardError, repositoryPath);
        var oracles = new List<OracleResult>
        {
            new("exit-code", process.ExitCode == 0, $"Expected status exit code 0; observed {process.ExitCode}.", [$"exit:{process.ExitCode}"]),
            RequiredLinesOracle(stdout),
            new("no-repository-mutation", mutations.Count == 0 && baseHash == postRunHash,
                mutations.Count == 0 ? "Repository snapshot is unchanged." : "Status mutated repository authority.",
                mutations.Select(item => item.Path).ToArray()),
            new("provider-not-started", !Directory.EnumerateFileSystemEntries(providerHome).Any(),
                "Provider home remained empty during non-Codex status canary.", []),
        };
        IReadOnlyList<string> privacy = PrivacyScanner.Scan($"{stdout}\n{stderr}", options.CaseAuthorityRoot);
        CertificationClassification classification = process.ExitCode == -1
            ? CertificationClassification.EnvironmentFailure
            : privacy.Count > 0
                ? CertificationClassification.OracleDrift
                : oracles.All(oracle => oracle.Passed)
                    ? CertificationClassification.Passed
                    : CertificationClassification.ProductRegression;
        CoverageLedger coverage = CoverageLedgerBuilder.Build(options.WorkspaceRoot, classification == CertificationClassification.Passed);
        BehaviorIdentity behavior = BuildBehaviorIdentity(options, identity, coverage);
        DateTimeOffset finishedAt = DateTimeOffset.UtcNow;
        string resultPath = Path.Combine(evidenceRoot, "result.json");
        string stdoutPath = Path.Combine(evidenceRoot, "stdout.txt");
        string stderrPath = Path.Combine(evidenceRoot, "stderr.txt");
        var result = new CertificationRunResult(
            ResultSchemaVersion,
            runId,
            startedAt,
            finishedAt,
            identity,
            behavior,
            classification,
            process.ExitCode,
            stdout,
            stderr,
            baseHash,
            postRunHash,
            mutations,
            oracles,
            privacy,
            coverage,
            ["result.json", "stdout.txt", "stderr.txt"]);

        Directory.CreateDirectory(evidenceRoot);
        await File.WriteAllTextAsync(stdoutPath, stdout, cancellationToken);
        await File.WriteAllTextAsync(stderrPath, stderr, cancellationToken);
        await WriteJsonAsync(resultPath, result, cancellationToken);

        if (!options.RetainCase)
        {
            Directory.Delete(runRoot, recursive: true);
            if (Directory.Exists(runRoot))
            {
                throw new IOException($"Case cleanup failed: {runRoot}");
            }
        }

        return result;
    }

    private static OracleResult RequiredLinesOracle(string stdout)
    {
        string[] required =
        [
            "Repository: <CASE>",
            "Invocation mode:",
            "Selected chain:",
            "Selected workflow:",
            "Current stage:",
            "Next eligible transition:",
            "Storage authority:",
            "User action required:",
        ];
        string[] missing = required.Where(line => !stdout.Contains(line, StringComparison.Ordinal)).ToArray();
        return new OracleResult(
            "status-structure",
            missing.Length == 0,
            missing.Length == 0 ? "All public status fields are present." : "Required public status fields are missing.",
            missing);
    }

    private static BehaviorIdentity BuildBehaviorIdentity(
        CertificationOptions options,
        ComposedCaseIdentity identity,
        CoverageLedger coverage)
    {
        string[] prompts = Directory.Exists(Path.Combine(options.WorkspaceRoot, "src"))
            ? Directory.EnumerateFiles(Path.Combine(options.WorkspaceRoot, "src"), "*.prompt", SearchOption.AllDirectories).ToArray()
            : [];
        string schema = Path.Combine(options.WorkspaceRoot, "src", "LoopRelay.Core", "Services", "Persistence", "LoopRelayWorkspaceDatabase.cs");
        string settings = Path.Combine(options.WorkspaceRoot, "config", "settings.default.json");
        return new BehaviorIdentity(
            identity.RepositoryIdentity + ":" + identity.RepositoryVersion,
            identity.ScenarioIdentity + ":" + identity.ScenarioVersion + ":" + identity.CompositionDigest,
            OracleVersion,
            EvidenceNormalizer.Version,
            coverage.ProductionDigest,
            CoverageLedgerBuilder.HashFiles(prompts, options.WorkspaceRoot),
            CoverageLedgerBuilder.HashFiles([schema], options.WorkspaceRoot),
            HashFile(options.CliPath),
            CoverageLedgerBuilder.HashFiles([settings], options.WorkspaceRoot),
            $"{Environment.OSVersion.Platform}/{System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}",
            "not-initialized",
            "not-exercised",
            "not-exercised",
            "not-exercised",
            "not-exercised",
            EvidenceLevel.LiveTransition);
    }

    private static async Task<ProcessResult> RunCliAsync(
        string cliPath,
        string repositoryPath,
        string providerHome,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = cliPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? "dotnet" : cliPath,
            WorkingDirectory = repositoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (cliPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(cliPath);
        }

        startInfo.ArgumentList.Add("--repo");
        startInfo.ArgumentList.Add(repositoryPath);
        startInfo.ArgumentList.Add("status");
        startInfo.Environment["CODEX_HOME"] = providerHome;
        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Unable to start the public LoopRelay CLI.");
        }

        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private static void ValidateOptions(CertificationOptions options)
    {
        string workspace = Path.GetFullPath(options.WorkspaceRoot);
        string authority = Path.GetFullPath(options.CaseAuthorityRoot);
        if (!Directory.Exists(workspace))
        {
            throw new DirectoryNotFoundException($"Workspace root does not exist: {workspace}");
        }

        if (!File.Exists(options.CliPath))
        {
            throw new FileNotFoundException("Published CLI does not exist.", options.CliPath);
        }

        if (authority == Path.GetPathRoot(authority)?.TrimEnd(Path.DirectorySeparatorChar))
        {
            throw new InvalidOperationException("Case authority root cannot be a filesystem root.");
        }

        if (options.Repetitions < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Repetitions), "At least two cycles prove reset reproducibility.");
        }

        Directory.CreateDirectory(authority);
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
