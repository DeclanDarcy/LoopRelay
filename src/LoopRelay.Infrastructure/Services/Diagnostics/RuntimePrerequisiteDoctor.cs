using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Infrastructure.Primitives.Diagnostics;

namespace LoopRelay.Infrastructure.Services.Diagnostics;

public sealed class RuntimePrerequisiteDoctor(
    Func<string, string?>? getEnvironmentVariable = null,
    Func<string, bool>? fileExists = null)
{
    public const string CodexExecutableVariable = "CODEX_EXECUTABLE";
    public const string DecisionResumeVariable = "LoopRelay_DECISION_RESUME";

    private readonly Func<string, string?> getEnvironmentVariable =
        getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
    private readonly Func<string, bool> fileExists = fileExists ?? File.Exists;

    public IReadOnlyList<RuntimeDiagnostic> Inspect()
    {
        var diagnostics = new List<RuntimeDiagnostic>();
        InspectCodexExecutable(diagnostics);
        InspectDecisionResume(diagnostics);
        return diagnostics;
    }

    private void InspectCodexExecutable(List<RuntimeDiagnostic> diagnostics)
    {
        string? value = getEnvironmentVariable(CodexExecutableVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(new RuntimeDiagnostic(
                "runtime.codex_executable.missing",
                RuntimeDiagnosticSeverity.Error,
                $"{CodexExecutableVariable} is not set."));
            return;
        }

        if (LooksLikePath(value) && !fileExists(value))
        {
            diagnostics.Add(new RuntimeDiagnostic(
                "runtime.codex_executable.not_found",
                RuntimeDiagnosticSeverity.Error,
                $"{CodexExecutableVariable} points to a missing file: {value}."));
        }
    }

    private void InspectDecisionResume(List<RuntimeDiagnostic> diagnostics)
    {
        string? value = getEnvironmentVariable(DecisionResumeVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(new RuntimeDiagnostic(
                "runtime.decision_resume.default",
                RuntimeDiagnosticSeverity.Info,
                $"{DecisionResumeVariable} is not set; decision resume remains enabled by default."));
            return;
        }

        if (IsBooleanFlag(value))
        {
            return;
        }

        diagnostics.Add(new RuntimeDiagnostic(
            "runtime.decision_resume.invalid",
            RuntimeDiagnosticSeverity.Warning,
            $"{DecisionResumeVariable} should be 0, 1, false, or true."));
    }

    private static bool IsBooleanFlag(string value) =>
        string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikePath(string value) =>
        value.Contains(Path.DirectorySeparatorChar)
        || value.Contains(Path.AltDirectorySeparatorChar)
        || Path.IsPathRooted(value);
}
