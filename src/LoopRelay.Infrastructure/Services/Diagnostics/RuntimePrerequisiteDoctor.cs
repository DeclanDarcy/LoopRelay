using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Infrastructure.Primitives.Diagnostics;

namespace LoopRelay.Infrastructure.Services.Diagnostics;

public sealed class RuntimePrerequisiteDoctor(
    Func<string, string?>? _getEnvironmentVariable = null,
    Func<string, bool>? _fileExists = null)
{
    public const string CodexExecutableVariable = "CODEX_EXECUTABLE";
    public const string DecisionResumeVariable = "LoopRelay_DECISION_RESUME";
    public const string DecisionRecoveryPolicyVariable = "LoopRelay_DECISION_RECOVERY_POLICY";

    private readonly Func<string, string?> getEnvironmentVariable =
        _getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
    private readonly Func<string, bool> fileExists = _fileExists ?? File.Exists;

    public IReadOnlyList<RuntimeDiagnostic> Inspect()
    {
        var diagnostics = new List<RuntimeDiagnostic>();
        InspectCodexExecutable(diagnostics);
        InspectDecisionResume(diagnostics);
        InspectDecisionRecoveryPolicy(diagnostics);
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

    private void InspectDecisionRecoveryPolicy(List<RuntimeDiagnostic> diagnostics)
    {
        string? value = getEnvironmentVariable(DecisionRecoveryPolicyVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(new RuntimeDiagnostic(
                "runtime.decision_recovery_policy.default",
                RuntimeDiagnosticSeverity.Info,
                $"{DecisionRecoveryPolicyVariable} is not set; resume-only policy is active."));
            return;
        }

        if (value is "resume-only" or "reconstructed" or "certified")
        {
            return;
        }

        diagnostics.Add(new RuntimeDiagnostic(
            "runtime.decision_recovery_policy.invalid",
            RuntimeDiagnosticSeverity.Warning,
            $"{DecisionRecoveryPolicyVariable} must be resume-only, reconstructed, or certified."));
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
