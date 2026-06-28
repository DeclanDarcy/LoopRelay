namespace CommandCenter.Backend.Tests;

/// <summary>
/// Manual artifact-freshness contracts for the orchestration SSE stream families (m8 slice "Stream contracts").
/// Each fixture pins a stream-trace golden (the Oracle source) to its hand-maintained TypeScript consumer file
/// (the contract artifact) by SHA-256. The verifier flags drift when the source changes without the artifact
/// being regenerated (StaleGeneratedArtifact), or when the artifact is edited without the source moving
/// (UnexpectedManualArtifactModification) — so a future contract change must update both halves together.
/// </summary>
public sealed class OrchestrationFreshnessContractTests
{
    [Fact]
    public void PlanStreamTypeScriptContractArtifactMatchesFreshnessManifest()
    {
        AssertFresh("plan-stream.artifact-freshness.json");
    }

    [Fact]
    public void ExecutionStreamTypeScriptContractArtifactMatchesFreshnessManifest()
    {
        AssertFresh("execution-stream.artifact-freshness.json");
    }

    [Fact]
    public void DecisionStreamTypeScriptContractArtifactMatchesFreshnessManifest()
    {
        AssertFresh("decision-stream.artifact-freshness.json");
    }

    private static void AssertFresh(string fixtureFileName)
    {
        ContractArtifactFreshnessSpec spec = LoadFreshnessSpec(fixtureFileName);
        ContractGeneratedArtifactFreshnessVerifier verifier = new(spec);

        IReadOnlyList<ContractGeneratedArtifactFreshnessDrift> drifts = verifier.Verify(FindRepositoryRoot());

        Assert.Empty(drifts);
    }

    private static ContractArtifactFreshnessSpec LoadFreshnessSpec(string fileName)
    {
        string manifestPath = Path.Combine(AppContext.BaseDirectory, "ContractFixtures", fileName);
        return ContractArtifactFreshnessSpec.Load(manifestPath);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "CommandCenter.UI", "src", "types", "repositories.ts")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
