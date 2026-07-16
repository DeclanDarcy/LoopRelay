using Xunit;

namespace LoopRelay.Certification.Tests;

public sealed class CertificationCaseRetentionTests
{
    [Theory]
    [InlineData(false, CertificationClassification.Passed, false)]
    [InlineData(true, CertificationClassification.Passed, true)]
    [InlineData(false, CertificationClassification.ProductRegression, true)]
    [InlineData(false, CertificationClassification.EnvironmentFailure, true)]
    public void Live_case_retention_preserves_requested_successes_and_all_failures(
        bool retainSuccessfulCase,
        CertificationClassification classification,
        bool expected)
    {
        Assert.Equal(
            expected,
            CertificationCaseRetention.ShouldPreserve(retainSuccessfulCase, classification));
    }
}
