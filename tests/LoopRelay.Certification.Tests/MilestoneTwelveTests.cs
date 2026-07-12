using LoopRelay.Orchestration.Workflows;
using Xunit;

namespace LoopRelay.Certification.Tests;

public sealed class MilestoneTwelveTests
{
    [Fact]
    public async Task Failure_matrix_covers_every_canonical_transition_and_rejects_negative_oracles()
    {
        string workspace = FindWorkspace();
        string authority = Path.Combine(Path.GetTempPath(), "looprelay-m12-" + Guid.NewGuid().ToString("N"));
        try
        {
            MilestoneTwelveCertificationResult result = await new MilestoneTwelveRunner().RunAsync(
                workspace, authority);

            int expectedTransitions = CanonicalWorkflowDefinitionSketches.CreateAll()
                .Sum(workflow => workflow.Transitions.Count);
            Assert.Equal(CertificationClassification.Passed, result.Classification);
            Assert.Equal(expectedTransitions, result.TransitionClasses.Count);
            Assert.All(result.TransitionClasses, item => Assert.True(item.Passed, item.Transition));
            Assert.All(result.FailureCases, item => Assert.True(item.Passed, item.Identity));
            Assert.All(result.OracleControls, item =>
            {
                Assert.True(item.PositiveControlAccepted, item.OracleClass);
                Assert.True(item.NegativeControlRejected, item.OracleClass);
            });
            Assert.True(result.UnsupportedCapabilitiesReleaseVisible);
            Assert.True(result.NoDuplicateSemanticProgress);
        }
        finally
        {
            if (Directory.Exists(authority)) Directory.Delete(authority, recursive: true);
        }
    }

    [Fact]
    public async Task Platform_probe_certifies_real_local_topology_and_normalized_contract()
    {
        string authority = Path.Combine(Path.GetTempPath(), "looprelay-platform-" + Guid.NewGuid().ToString("N"));
        try
        {
            PlatformCertificationResult result = await new PlatformCertificationRunner().RunAsync(authority);

            Assert.Equal(CertificationClassification.Passed, result.Classification);
            Assert.True(result.SeparatorNormalizationPassed);
            Assert.True(result.LineEndingNormalizationPassed);
            Assert.True(result.Utf8RoundTripPassed);
            Assert.True(result.GitBehaviorPassed);
            Assert.NotEmpty(result.NormalizedContractDigest);
        }
        finally
        {
            if (Directory.Exists(authority)) Directory.Delete(authority, recursive: true);
        }
    }

    [Fact]
    public async Task Retired_or_missing_evidence_returns_critical_dimensions_to_uncovered()
    {
        string workspace = FindWorkspace();
        string authority = Path.Combine(Path.GetTempPath(), "looprelay-continuous-" + Guid.NewGuid().ToString("N"));
        try
        {
            ContinuousCertificationResult result = await new ContinuousCertificationRunner().RunAsync(
                workspace, authority);

            Assert.Equal(CertificationClassification.Blocked, result.Classification);
            Assert.False(result.NoCriticalDimensionAtZero);
            Assert.Contains(result.Dimensions, item =>
                item.ActualLevel == EvidenceLevel.Uncovered && !item.Passed);
            Assert.False(File.Exists(Path.Combine(authority, "evidence", "production-baseline.v1.json")));
        }
        finally
        {
            if (Directory.Exists(authority)) Directory.Delete(authority, recursive: true);
        }
    }

    private static string FindWorkspace()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoopRelay.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Workspace root was not found.");
    }
}
