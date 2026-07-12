using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Infrastructure.Primitives.Diagnostics;

namespace LoopRelay.Infrastructure.Services.Diagnostics;

public sealed class CodexRuntimePrerequisiteInspector(
    Func<string, string?>? getEnvironmentVariable = null,
    Func<string, bool>? fileExists = null) : IRuntimePrerequisiteInspector
{
    public const string CodexExecutableVariable = "CODEX_EXECUTABLE";
    public const string CodexHomeVariable = "CODEX_HOME";

    private readonly Func<string, string?> _getEnvironmentVariable =
        getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
    private readonly Func<string, bool> _fileExists = fileExists ?? File.Exists;

    public string Provider => "codex";

    public IReadOnlyList<RuntimePrerequisiteFinding> Inspect(ResolvedRuntimeHostProfile profile)
    {
        var findings = new List<RuntimePrerequisiteFinding>();
        InspectLaunchCapability(profile, findings);
        InspectExecutable(findings);
        InspectRuntimeHome(findings);
        return findings;
    }

    private static void InspectLaunchCapability(
        ResolvedRuntimeHostProfile profile,
        List<RuntimePrerequisiteFinding> findings)
    {
        if (profile.Capabilities.OneShotExecution || profile.Capabilities.PersistentSessions)
        {
            return;
        }

        findings.Add(new RuntimePrerequisiteFinding(
            RuntimePrerequisiteFindingCode.InsufficientLaunchCapability,
            "runtime.codex.launch_capability.missing",
            RuntimePrerequisiteFindingSeverity.Error,
            "The selected Codex runtime declares no executable one-shot or persistent-session capability."));
    }

    private void InspectExecutable(List<RuntimePrerequisiteFinding> findings)
    {
        string? value = _getEnvironmentVariable(CodexExecutableVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            findings.Add(new RuntimePrerequisiteFinding(
                RuntimePrerequisiteFindingCode.MissingRequiredExecutable,
                "runtime.codex_executable.missing",
                RuntimePrerequisiteFindingSeverity.Error,
                $"{CodexExecutableVariable} is not set."));
            return;
        }

        if (LooksLikePath(value) && !_fileExists(value))
        {
            findings.Add(new RuntimePrerequisiteFinding(
                RuntimePrerequisiteFindingCode.InvalidProviderInstallation,
                "runtime.codex_executable.not_found",
                RuntimePrerequisiteFindingSeverity.Error,
                $"{CodexExecutableVariable} points to a missing file: {value}."));
        }
    }

    private void InspectRuntimeHome(List<RuntimePrerequisiteFinding> findings)
    {
        if (!string.IsNullOrWhiteSpace(_getEnvironmentVariable(CodexHomeVariable)))
        {
            return;
        }

        findings.Add(new RuntimePrerequisiteFinding(
            RuntimePrerequisiteFindingCode.MissingOptionalRuntimeDirectory,
            "runtime.codex_home.default",
            RuntimePrerequisiteFindingSeverity.Info,
            $"{CodexHomeVariable} is not set; codex session rollouts resolve under the user profile default."));
    }

    private static bool LooksLikePath(string value) =>
        value.Contains(Path.DirectorySeparatorChar)
        || value.Contains(Path.AltDirectorySeparatorChar)
        || Path.IsPathRooted(value);
}
