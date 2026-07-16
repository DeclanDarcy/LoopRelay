using Xunit;

namespace LoopRelay.Certification.Tests;

public sealed class LiveRunnerDiagnosisIntegrationTests
{
    public static TheoryData<Type> LiveRunners => new()
    {
        typeof(ProviderProfileRunner),
        typeof(TransitionRecoveryRunner),
        typeof(PlanWorkflowRunner),
        typeof(ExecuteWorkflowRunner),
        typeof(RoadmapLiveRunner),
        typeof(CompletionClosureRunner),
        typeof(FullChainLiveRunner),
    };

    [Theory]
    [MemberData(nameof(LiveRunners))]
    public void Every_provider_backed_live_runner_accepts_the_shared_diagnosis_boundary(Type runner)
    {
        Assert.Contains(runner.GetConstructors(), constructor => constructor.GetParameters()
            .Any(parameter => parameter.ParameterType == typeof(ICertificationFailureDiagnoser)));
    }

    [Fact]
    public void Diagnosis_policy_is_an_explicit_quota_only_allowlist()
    {
        CertificationFailureContext context = Context();
        Assert.True(CertificationDiagnosisPolicy.RequiresSessionInspection(context));
        Assert.Equal("confirmed-quota-exhaustion", CertificationDiagnosisPolicy.BypassReason(context with
        {
            Classification = CertificationClassification.ProviderRegression,
            QuotaExhaustionConfirmed = true,
            DeterministicEvidence = ["used-percent:100", "last-agent-message:null"],
            ActionableNextStep = "Wait for the quota window to reset.",
        }));
        Assert.True(CertificationDiagnosisPolicy.RequiresSessionInspection(context with
        {
            Classification = CertificationClassification.ProviderRegression,
            QuotaExhaustionConfirmed = true,
            DeterministicEvidence = ["used-percent:100"],
            ActionableNextStep = null,
        }));
        Assert.True(CertificationDiagnosisPolicy.RequiresSessionInspection(context with
        {
            Classification = CertificationClassification.ProviderRegression,
        }));
        Assert.False(CertificationDiagnosisPolicy.RequiresSessionInspection(context with
        {
            Classification = CertificationClassification.Passed,
        }));
    }

    private static CertificationFailureContext Context() => new(
        "invocation",
        true,
        CertificationClassification.ProductRegression,
        false,
        "failure",
        [],
        null,
        new { failed = true },
        Path.GetTempPath(),
        Path.GetTempPath(),
        Path.GetTempPath(),
        "codex",
        []);
}
