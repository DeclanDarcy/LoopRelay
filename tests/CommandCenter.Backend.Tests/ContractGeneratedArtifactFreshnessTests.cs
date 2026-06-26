namespace CommandCenter.Backend.Tests;

public sealed class ContractGeneratedArtifactFreshnessTests
{
    [Fact]
    public void RepositoryDashboardTypeScriptContractArtifactMatchesFreshnessManifest()
    {
        ContractArtifactFreshnessSpec spec = LoadFreshnessSpec("repository-dashboard.artifact-freshness.json");
        ContractGeneratedArtifactFreshnessVerifier verifier = new(spec);

        IReadOnlyList<ContractGeneratedArtifactFreshnessDrift> drifts = verifier.Verify(FindRepositoryRoot());

        Assert.Empty(drifts);
    }

    [Fact]
    public void RepositoryWorkspaceTypeScriptContractArtifactMatchesFreshnessManifest()
    {
        ContractArtifactFreshnessSpec spec = LoadFreshnessSpec("repository-workspace.artifact-freshness.json");
        ContractGeneratedArtifactFreshnessVerifier verifier = new(spec);

        IReadOnlyList<ContractGeneratedArtifactFreshnessDrift> drifts = verifier.Verify(FindRepositoryRoot());

        Assert.Empty(drifts);
    }

    [Fact]
    public void FreshnessVerifierReportsStaleGeneratedArtifactWhenOracleSourceChanges()
    {
        ContractArtifactFreshnessSpec current = LoadFreshnessSpec("repository-dashboard.artifact-freshness.json");
        ContractFreshnessArtifactSpec artifact = Assert.Single(current.Artifacts);
        ContractArtifactFreshnessSpec staleSpec = current with
        {
            SourceSha256 = DifferentSha256(current.SourceSha256)
        };
        ContractGeneratedArtifactFreshnessVerifier verifier = new(staleSpec);

        ContractGeneratedArtifactFreshnessDrift drift = Assert.Single(verifier.Verify(FindRepositoryRoot()));

        Assert.Equal(ContractGeneratedArtifactFreshnessDriftKind.StaleGeneratedArtifact, drift.Kind);
        Assert.Equal("repository-dashboard", drift.ContractId);
        Assert.Equal(artifact.ArtifactId, drift.ArtifactId);
        Assert.Equal(artifact.ArtifactPath, drift.ArtifactPath);
    }

    [Fact]
    public void FreshnessVerifierReportsUnexpectedManualArtifactModification()
    {
        ContractArtifactFreshnessSpec current = LoadFreshnessSpec("repository-dashboard.artifact-freshness.json");
        ContractFreshnessArtifactSpec artifact = Assert.Single(current.Artifacts);
        ContractArtifactFreshnessSpec modifiedArtifactSpec = current with
        {
            Artifacts =
            [
                artifact with
                {
                    ArtifactSha256 = DifferentSha256(artifact.ArtifactSha256)
                }
            ]
        };
        ContractGeneratedArtifactFreshnessVerifier verifier = new(modifiedArtifactSpec);

        ContractGeneratedArtifactFreshnessDrift drift = Assert.Single(verifier.Verify(FindRepositoryRoot()));

        Assert.Equal(ContractGeneratedArtifactFreshnessDriftKind.UnexpectedManualArtifactModification, drift.Kind);
        Assert.Equal(artifact.ArtifactId, drift.ArtifactId);
        Assert.Equal(artifact.ArtifactPath, drift.ArtifactPath);
    }

    [Fact]
    public void FreshnessVerifierReportsMissingExpectedArtifact()
    {
        ContractArtifactFreshnessSpec current = LoadFreshnessSpec("repository-dashboard.artifact-freshness.json");
        ContractFreshnessArtifactSpec artifact = Assert.Single(current.Artifacts);
        ContractArtifactFreshnessSpec missingArtifactSpec = current with
        {
            Artifacts =
            [
                artifact with
                {
                    ArtifactPath = "src/CommandCenter.UI/src/types/repositories.generated.missing.ts"
                }
            ]
        };
        ContractGeneratedArtifactFreshnessVerifier verifier = new(missingArtifactSpec);

        ContractGeneratedArtifactFreshnessDrift drift = Assert.Single(verifier.Verify(FindRepositoryRoot()));

        Assert.Equal(ContractGeneratedArtifactFreshnessDriftKind.MissingExpectedArtifact, drift.Kind);
        Assert.Equal(artifact.ArtifactId, drift.ArtifactId);
        Assert.Equal("src/CommandCenter.UI/src/types/repositories.generated.missing.ts", drift.ArtifactPath);
    }

    private static ContractArtifactFreshnessSpec LoadFreshnessSpec(string fileName)
    {
        string manifestPath = Path.Combine(
            AppContext.BaseDirectory,
            "ContractFixtures",
            fileName);

        return ContractArtifactFreshnessSpec.Load(manifestPath);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(directory.Combine("src", "CommandCenter.UI", "src", "types", "repositories.ts")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private static string DifferentSha256(string hash)
    {
        char replacement = hash[0] == '0' ? '1' : '0';
        return replacement + hash[1..];
    }
}
