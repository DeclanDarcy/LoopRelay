using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Infrastructure.Primitives.Diagnostics;

namespace LoopRelay.Infrastructure.Services.Diagnostics;

/// <summary>
/// Inspects the runtime prerequisites of the sole supported provider (D5: Codex) before any
/// agent launches: the executable the launcher will resolve, and the CODEX_HOME the rollout and
/// resume surfaces read. Wired at the unified run entry (M7): Error diagnostics abort the run
/// with the typed MissingRuntimePrerequisite outcome before any transition executes, replacing
/// the late raw exception the executable resolver would otherwise throw at the first send.
/// Policy-owned environment variables (LoopRelay_DECISION_RESUME, LoopRelay_SESSION_LOG) are
/// deliberately NOT inspected here — the policy resolver validates them and rejects garbage, so
/// a second validator would only drift.
/// </summary>
public sealed class RuntimePrerequisiteDoctor(
    Func<string, string?>? getEnvironmentVariable = null,
    Func<string, bool>? fileExists = null)
{
    public const string CodexExecutableVariable = "CODEX_EXECUTABLE";
    public const string CodexHomeVariable = "CODEX_HOME";

    private readonly Func<string, string?> _getEnvironmentVariable =
        getEnvironmentVariable ?? Environment.GetEnvironmentVariable;

    private readonly Func<string, bool> _fileExists = fileExists ?? File.Exists;

    public IReadOnlyList<RuntimeDiagnostic> Inspect()
    {
        var diagnostics = new List<RuntimeDiagnostic>();
        InspectCodexExecutable(diagnostics);
        InspectCodexHome(diagnostics);
        return diagnostics;
    }

    private void InspectCodexExecutable(List<RuntimeDiagnostic> diagnostics)
    {
        string? value = _getEnvironmentVariable(CodexExecutableVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(new RuntimeDiagnostic(
                "runtime.codex_executable.missing",
                RuntimeDiagnosticSeverity.Error,
                $"{CodexExecutableVariable} is not set."));
            return;
        }

        if (LooksLikePath(value) && !_fileExists(value))
        {
            diagnostics.Add(new RuntimeDiagnostic(
                "runtime.codex_executable.not_found",
                RuntimeDiagnosticSeverity.Error,
                $"{CodexExecutableVariable} points to a missing file: {value}."));
        }
    }

    private void InspectCodexHome(List<RuntimeDiagnostic> diagnostics)
    {
        // Informational only: sessions run without it (codex falls back to ~/.codex), but the
        // rollout-log telemetry surface reads it, so an unset value is worth surfacing.
        if (string.IsNullOrWhiteSpace(_getEnvironmentVariable(CodexHomeVariable)))
        {
            diagnostics.Add(new RuntimeDiagnostic(
                "runtime.codex_home.default",
                RuntimeDiagnosticSeverity.Info,
                $"{CodexHomeVariable} is not set; codex session rollouts resolve under the user profile default."));
        }
    }

    private static bool LooksLikePath(string value) =>
        value.Contains(Path.DirectorySeparatorChar)
        || value.Contains(Path.AltDirectorySeparatorChar)
        || Path.IsPathRooted(value);
}
