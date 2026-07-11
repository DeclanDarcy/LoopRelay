using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Infrastructure.Primitives.Diagnostics;
using LoopRelay.Infrastructure.Services.Diagnostics;

namespace LoopRelay.Infrastructure.Tests.Services;

public sealed class RuntimePrerequisiteDoctorTests
{
    [Fact]
    public void MissingCodexExecutableIsAnErrorAndUnsetCodexHomeIsInformational()
    {
        var doctor = new RuntimePrerequisiteDoctor(_ => null, _ => false);

        IReadOnlyList<RuntimeDiagnostic> diagnostics = doctor.Inspect();

        Assert.Contains(diagnostics, d =>
            d.Id == "runtime.codex_executable.missing" &&
            d.Severity == RuntimeDiagnosticSeverity.Error);
        Assert.Contains(diagnostics, d =>
            d.Id == "runtime.codex_home.default" &&
            d.Severity == RuntimeDiagnosticSeverity.Info);
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

    [Fact]
    public void SatisfiedPrerequisitesYieldNoDiagnostics()
    {
        var values = new Dictionary<string, string?>
        {
            [RuntimePrerequisiteDoctor.CodexExecutableVariable] = "codex",
            [RuntimePrerequisiteDoctor.CodexHomeVariable] = @"C:\codex-home",
        };
        var doctor = new RuntimePrerequisiteDoctor(
            name => values.TryGetValue(name, out string? value) ? value : null,
            _ => true);

        Assert.Empty(doctor.Inspect());
    }

    [Fact]
    public void PolicyOwnedEnvironmentVariablesAreNotInspected()
    {
        // The policy resolver validates LoopRelay_DECISION_RESUME and LoopRelay_SESSION_LOG and
        // rejects garbage; a second validator here would only drift from it (M7).
        var values = new Dictionary<string, string?>
        {
            [RuntimePrerequisiteDoctor.CodexExecutableVariable] = "codex",
            [RuntimePrerequisiteDoctor.CodexHomeVariable] = @"C:\codex-home",
            ["LoopRelay_DECISION_RESUME"] = "garbage",
            ["LoopRelay_SESSION_LOG"] = "garbage",
        };
        var doctor = new RuntimePrerequisiteDoctor(
            name => values.TryGetValue(name, out string? value) ? value : null,
            _ => true);

        Assert.Empty(doctor.Inspect());
    }

    [Fact]
    public void DefaultDelegatesReadTheRealEnvironment()
    {
        // The nullable delegate parameters default to the real environment and filesystem, so
        // production construction takes no arguments (the pre-M7 null-deref warnings are gone).
        IReadOnlyList<RuntimeDiagnostic> diagnostics = new RuntimePrerequisiteDoctor().Inspect();

        Assert.NotNull(diagnostics);
    }
}
