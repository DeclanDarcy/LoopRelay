using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LoopRelay.Certification;

public sealed class PlatformCertificationRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async Task<PlatformCertificationResult> RunAsync(
        string authorityRoot,
        CancellationToken cancellationToken = default)
    {
        string platform = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsLinux() ? "linux" : "other";
        string root = Path.Combine(authorityRoot, "platform", platform, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var evidence = new List<string>();
        try
        {
            string casePath = Path.Combine(root, "CaseProbe");
            await File.WriteAllTextAsync(casePath, "case", cancellationToken);
            bool caseSensitive = !File.Exists(Path.Combine(root, "caseprobe"));

            string separatorInput = Path.Combine("alpha", "beta", "artifact.md");
            bool separator = separatorInput.Replace('\\', '/') == "alpha/beta/artifact.md";
            bool lineEndings = EvidenceNormalizer.Normalize("alpha\r\nbeta\r\n", root) == "alpha\nbeta" &&
                EvidenceNormalizer.Normalize("alpha\nbeta\n", root) == "alpha\nbeta";
            const string utf8Value = "Loop Relay — Καλημέρα — こんにちは";
            string utf8Path = Path.Combine(root, "utf8.txt");
            await File.WriteAllTextAsync(utf8Path, utf8Value, new UTF8Encoding(false), cancellationToken);
            bool utf8 = await File.ReadAllTextAsync(utf8Path, Encoding.UTF8, cancellationToken) == utf8Value;

            string longPath = root;
            foreach (string segment in new[]
                     {
                         "path-segment-000000000000000000000000000001",
                         "path-segment-000000000000000000000000000002",
                         "path-segment-000000000000000000000000000003",
                         "path-segment-000000000000000000000000000004",
                     })
            {
                longPath = Path.Combine(longPath, segment);
            }
            Directory.CreateDirectory(longPath);
            string lengthProbe = Path.Combine(longPath, "artifact.txt");
            await File.WriteAllTextAsync(lengthProbe, "length", cancellationToken);
            bool pathLength = await File.ReadAllTextAsync(lengthProbe, cancellationToken) == "length";

            string gitRoot = Path.Combine(root, "git-probe");
            Directory.CreateDirectory(gitRoot);
            bool git = (await RunAsync("git", ["init", "-b", "main"], gitRoot, cancellationToken)).ExitCode == 0;
            string script = Path.Combine(gitRoot, "probe.sh");
            await File.WriteAllTextAsync(script, "#!/bin/sh\nprintf 'ok\\n'\n", cancellationToken);
            bool unixExecutable = false;
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(script,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
            ProcessResult add = await RunAsync("git", ["add", "probe.sh"], gitRoot, cancellationToken);
            ProcessResult stage = await RunAsync("git", ["ls-files", "--stage", "probe.sh"], gitRoot, cancellationToken);
            git &= add.ExitCode == 0 && stage.ExitCode == 0 && stage.StandardOutput.Contains("probe.sh", StringComparison.Ordinal);
            unixExecutable = stage.StandardOutput.StartsWith("100755", StringComparison.Ordinal);
            git &= OperatingSystem.IsWindows() || unixExecutable;

            string normalizedDigest = Digest(string.Join('|',
                "platform-contract-v1", separator, lineEndings, utf8, git, pathLength,
                "logical-path:alpha/beta/artifact.md", "utf8:no-bom"));
            evidence.AddRange(
            [
                $"platform:{platform}",
                $"architecture:{RuntimeInformation.ProcessArchitecture}",
                $"case-sensitive:{caseSensitive}",
                $"unix-executable-bit:{unixExecutable}",
                $"separator-normalization:{separator}",
                $"line-ending-normalization:{lineEndings}",
                $"utf8-round-trip:{utf8}",
                $"git-behavior:{git}",
                $"path-length:{pathLength}",
                $"normalized-contract:{normalizedDigest}",
            ]);
            bool passed = platform is "windows" or "linux" && separator && lineEndings && utf8 && git && pathLength;
            IReadOnlyList<string> privacy = PrivacyScanner.Scan(string.Join('\n', evidence), authorityRoot);
            CertificationClassification classification = privacy.Count > 0
                ? CertificationClassification.OracleDrift
                : passed ? CertificationClassification.Passed : CertificationClassification.EnvironmentFailure;
            var result = new PlatformCertificationResult(
                CertificationRunner.ResultSchemaVersion,
                classification,
                platform,
                RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
                caseSensitive,
                unixExecutable,
                separator,
                lineEndings,
                utf8,
                git,
                pathLength,
                normalizedDigest,
                privacy,
                evidence);
            string output = Path.Combine(authorityRoot, "evidence", $"platform-{platform}.latest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await using FileStream stream = File.Create(output);
            await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
            return result;
        }
        finally
        {
            if (Directory.Exists(root))
            {
                foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task<ProcessResult> RunAsync(
        string file,
        IReadOnlyList<string> arguments,
        string workingDirectory,
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
        string stdout = await process.StandardOutput.ReadToEndAsync(token);
        string stderr = await process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private static string Digest(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
