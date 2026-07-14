using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Core.Models.Identity;
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

        RuntimePrerequisiteInspection inspection = doctor.Inspect(Profile(), InspectedAt);

        Assert.Equal(RuntimePrerequisiteOverallStatus.Unsatisfied, inspection.OverallStatus);
        Assert.Contains(inspection.Findings, d =>
            d.Id == "runtime.codex_executable.missing" &&
            d.Severity == RuntimePrerequisiteFindingSeverity.Error);
        Assert.Contains(inspection.Findings, d =>
            d.Id == "runtime.codex_home.default" &&
            d.Severity == RuntimePrerequisiteFindingSeverity.Info);
        Assert.Equal("runtime_test", inspection.RuntimeProfile.Value);
        Assert.Equal("codex", inspection.Provider);
    }

    [Fact]
    public void MissingExplicitCodexExecutableIsAnError()
    {
        var values = new Dictionary<string, string?>
        {
            [CodexRuntimePrerequisiteInspector.CodexExecutableVariable] = @"C:\missing\codex.exe",
        };
        var doctor = new RuntimePrerequisiteDoctor(
            name => values.TryGetValue(name, out string? value) ? value : null,
            _ => false);

        RuntimePrerequisiteInspection inspection = doctor.Inspect(Profile(), InspectedAt);

        RuntimePrerequisiteFinding finding = Assert.Single(
            inspection.Findings,
            item => item.Id == "runtime.codex_executable.not_found");
        Assert.Equal(RuntimePrerequisiteFindingCode.InvalidProviderInstallation, finding.Code);
    }

    [Fact]
    public void SatisfiedPrerequisitesYieldNoDiagnostics()
    {
        var values = new Dictionary<string, string?>
        {
            [CodexRuntimePrerequisiteInspector.CodexExecutableVariable] = "codex",
            [CodexRuntimePrerequisiteInspector.CodexHomeVariable] = @"C:\codex-home",
        };
        var doctor = new RuntimePrerequisiteDoctor(
            name => values.TryGetValue(name, out string? value) ? value : null,
            _ => true);

        RuntimePrerequisiteInspection inspection = doctor.Inspect(Profile(), InspectedAt);

        Assert.Equal(RuntimePrerequisiteOverallStatus.Satisfied, inspection.OverallStatus);
        Assert.Empty(inspection.Findings);
    }

    [Fact]
    public void PolicyOwnedEnvironmentVariablesAreNotInspected()
    {
        // The policy resolver validates LoopRelay_DECISION_RESUME and LoopRelay_SESSION_LOG and
        // rejects garbage; a second validator here would only drift from it (M7).
        var values = new Dictionary<string, string?>
        {
            [CodexRuntimePrerequisiteInspector.CodexExecutableVariable] = "codex",
            [CodexRuntimePrerequisiteInspector.CodexHomeVariable] = @"C:\codex-home",
            ["LoopRelay_DECISION_RESUME"] = "garbage",
            ["LoopRelay_SESSION_LOG"] = "garbage",
        };
        var queried = new List<string>();
        var doctor = new RuntimePrerequisiteDoctor(
            name =>
            {
                queried.Add(name);
                return values.TryGetValue(name, out string? value) ? value : null;
            },
            _ => true);

        RuntimePrerequisiteInspection inspection = doctor.Inspect(Profile(), InspectedAt);

        Assert.Equal(RuntimePrerequisiteOverallStatus.Satisfied, inspection.OverallStatus);
        Assert.DoesNotContain("LoopRelay_DECISION_RESUME", queried);
        Assert.DoesNotContain("LoopRelay_SESSION_LOG", queried);
    }

    [Fact]
    public void DefaultDelegatesReadTheRealEnvironment()
    {
        // The nullable delegate parameters default to the real environment and filesystem, so
        // production construction takes no arguments (the pre-M7 null-deref warnings are gone).
        RuntimePrerequisiteInspection inspection = new RuntimePrerequisiteDoctor().Inspect(Profile(), InspectedAt);

        Assert.NotNull(inspection);
    }

    [Fact]
    public void MissingLaunchCapabilityIsATypedUnsatisfiedFinding()
    {
        var values = new Dictionary<string, string?>
        {
            [CodexRuntimePrerequisiteInspector.CodexExecutableVariable] = "codex",
            [CodexRuntimePrerequisiteInspector.CodexHomeVariable] = @"C:\codex-home",
        };
        var doctor = new RuntimePrerequisiteDoctor(
            name => values.TryGetValue(name, out string? value) ? value : null,
            _ => true);
        var profile = new ResolvedRuntimeHostProfile(
            new RuntimeProfileIdentity("runtime_no_launch"),
            new AgentRuntimeCapabilities("codex", false, false, false));

        RuntimePrerequisiteInspection inspection = doctor.Inspect(profile, InspectedAt);

        RuntimePrerequisiteFinding finding = Assert.Single(inspection.Findings);
        Assert.Equal(RuntimePrerequisiteFindingCode.InsufficientLaunchCapability, finding.Code);
        Assert.Equal(RuntimePrerequisiteOverallStatus.Unsatisfied, inspection.OverallStatus);
    }

    [Fact]
    public void UnregisteredProviderFailsClosedWithTypedEvidence()
    {
        var profile = new ResolvedRuntimeHostProfile(
            new RuntimeProfileIdentity("runtime_future"),
            new AgentRuntimeCapabilities("future-provider", true, true, false));

        RuntimePrerequisiteInspection inspection =
            new RuntimePrerequisiteDoctor().Inspect(profile, InspectedAt);

        RuntimePrerequisiteFinding finding = Assert.Single(inspection.Findings);
        Assert.Equal(RuntimePrerequisiteFindingCode.UnsupportedProvider, finding.Code);
        Assert.Equal(RuntimePrerequisiteOverallStatus.Unsatisfied, inspection.OverallStatus);
    }

    private static readonly DateTimeOffset InspectedAt =
        new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private static ResolvedRuntimeHostProfile Profile() =>
        new(
            new RuntimeProfileIdentity("runtime_test"),
            new AgentRuntimeCapabilities("codex", true, true, true));
}
