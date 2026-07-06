using System.Runtime.InteropServices;
using LoopRelay.Execution.Abstractions;
using LoopRelay.Execution.Models;
using LoopRelay.Execution.Primitives;

namespace LoopRelay.Execution.Modules;

public sealed class CodexExecutableResolver : ICodexExecutableResolver
{
    private const string ConfiguredCodexPathVariable = "COMMAND_CENTER_CODEX_PATH";

    public CodexExecutable Resolve()
    {
        string? configuredPath = Environment.GetEnvironmentVariable(ConfiguredCodexPathVariable);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return ResolveConfiguredPath(configuredPath);
        }

        string pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (string directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string candidateName in CandidateExecutableNames())
            {
                string candidate = Path.Combine(directory, candidateName);
                if (File.Exists(candidate) && IsExecutable(candidate))
                {
                    return new CodexExecutable { Path = candidate };
                }
            }
        }

        throw new ExecutionProviderException(
            "ProviderExecutableNotFound",
            "Codex executable was not found. Set COMMAND_CENTER_CODEX_PATH or add codex to PATH.");
    }

    private static CodexExecutable ResolveConfiguredPath(string configuredPath)
    {
        string fullPath = Path.GetFullPath(configuredPath);
        if (!File.Exists(fullPath))
        {
            throw new ExecutionProviderException(
                "ProviderExecutableNotFound",
                $"COMMAND_CENTER_CODEX_PATH points to a missing file: {fullPath}");
        }

        if (!IsExecutable(fullPath))
        {
            throw new ExecutionProviderException(
                "ProviderExecutableNotExecutable",
                $"COMMAND_CENTER_CODEX_PATH does not point to an executable file: {fullPath}");
        }

        return new CodexExecutable { Path = fullPath };
    }

    private static IEnumerable<string> CandidateExecutableNames()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return "codex";
            yield break;
        }

        string[] pathExtensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string extension in pathExtensions)
        {
            yield return "codex" + extension.ToLowerInvariant();
        }

        yield return "codex";
    }

    private static bool IsExecutable(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string extension = Path.GetExtension(path);
            return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".com", StringComparison.OrdinalIgnoreCase);
        }

        UnixFileMode mode = File.GetUnixFileMode(path);
        return mode.HasFlag(UnixFileMode.UserExecute) ||
            mode.HasFlag(UnixFileMode.GroupExecute) ||
            mode.HasFlag(UnixFileMode.OtherExecute);
    }
}
