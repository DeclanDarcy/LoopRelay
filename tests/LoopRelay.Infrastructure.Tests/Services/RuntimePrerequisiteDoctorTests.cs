using LoopRelay.Infrastructure.Diagnostics;

namespace LoopRelay.Infrastructure.Tests;

public sealed class RuntimePrerequisiteDoctorTests
{
    [Fact]
    public void MissingCodexExecutableIsAnErrorAndUnsetDecisionResumeIsInformational()
    {
        var doctor = new RuntimePrerequisiteDoctor(_ => null, _ => false);

        IReadOnlyList<RuntimeDiagnostic> diagnostics = doctor.Inspect();

        Assert.Contains(diagnostics, d =>
            d.Id == "runtime.codex_executable.missing" &&
            d.Severity == RuntimeDiagnosticSeverity.Error);
        Assert.Contains(diagnostics, d => d.Id == "runtime.decision_resume.default");
    }

    [Fact]
    public void InvalidDecisionResumeFlagIsAWarning()
    {
        var values = new Dictionary<string, string?>
        {
            [RuntimePrerequisiteDoctor.CodexExecutableVariable] = "codex",
            [RuntimePrerequisiteDoctor.DecisionResumeVariable] = "maybe",
        };
        var doctor = new RuntimePrerequisiteDoctor(
            name => values.TryGetValue(name, out string? value) ? value : null,
            _ => true);

        IReadOnlyList<RuntimeDiagnostic> diagnostics = doctor.Inspect();

        Assert.Contains(diagnostics, d =>
            d.Id == "runtime.decision_resume.invalid" &&
            d.Severity == RuntimeDiagnosticSeverity.Warning);
    }

    [Fact]
    public void MissingExplicitCodexExecutableIsAnError()
    {
        var values = new Dictionary<string, string?>
        {
            [RuntimePrerequisiteDoctor.CodexExecutableVariable] = @"C:\missing\codex.exe",
        };
        var doctor = new RuntimePrerequisiteDoctor(
            name => values.TryGetValue(name, out string? value) ? value : null,
            _ => false);

        IReadOnlyList<RuntimeDiagnostic> diagnostics = doctor.Inspect();

        Assert.Contains(diagnostics, d => d.Id == "runtime.codex_executable.not_found");
    }
}
