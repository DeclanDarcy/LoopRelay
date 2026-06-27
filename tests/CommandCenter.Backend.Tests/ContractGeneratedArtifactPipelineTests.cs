using System.Security.Cryptography;
using System.Text.Json;

namespace CommandCenter.Backend.Tests;

public sealed class ContractGeneratedArtifactPipelineTests
{
    private const string UpdateGeneratedArtifactsVariable = "COMMANDCENTER_UPDATE_GENERATED_CONTRACTS";

    [Fact]
    public void RepositoryDashboardGenerationPipelineMatchesGeneratedArtifacts()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string sourcePath = repositoryRoot.Combine(
            "tests",
            "CommandCenter.Backend.Tests",
            "ContractFixtures",
            "repository-dashboard.golden.json");
        string irPath = repositoryRoot.Combine(
            "tests",
            "CommandCenter.Backend.Tests",
            "ContractFixtures",
            "repository-dashboard.contract-ir.json");
        string typeScriptPath = repositoryRoot.Combine(
            "src",
            "CommandCenter.UI",
            "src",
            "contracts",
            "generated",
            "repository-dashboard.generated.ts");
        string freshnessManifestPath = repositoryRoot.Combine(
            "tests",
            "CommandCenter.Backend.Tests",
            "ContractFixtures",
            "repository-dashboard.generated-artifact-freshness.json");

        string sourceJson = File.ReadAllText(sourcePath);
        using JsonDocument source = JsonDocument.Parse(sourceJson);

        ContractGenerationIr ir = ContractGenerationIrBuilder.Build(
            "repository-dashboard",
            "Repository dashboard",
            source.RootElement);
        string actualIr = ContractGenerationIrSerializer.Serialize(ir);
        string actualTypeScript = ContractTypeScriptMetadataGenerator.Generate(ir);

        if (StringComparer.Ordinal.Equals(Environment.GetEnvironmentVariable(UpdateGeneratedArtifactsVariable), "1"))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(irPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(typeScriptPath)!);
            File.WriteAllText(irPath, actualIr);
            File.WriteAllText(typeScriptPath, actualTypeScript);
            WriteFreshnessManifest(
                freshnessManifestPath,
                sourcePath,
                typeScriptPath,
                repositoryRoot);
            return;
        }

        Assert.Equal(File.ReadAllText(irPath), actualIr);
        Assert.Equal(File.ReadAllText(typeScriptPath), actualTypeScript);
    }

    [Fact]
    public void RepositoryDashboardGenerationPipelineIsDeterministic()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string sourcePath = repositoryRoot.Combine(
            "tests",
            "CommandCenter.Backend.Tests",
            "ContractFixtures",
            "repository-dashboard.golden.json");

        using JsonDocument source = JsonDocument.Parse(File.ReadAllText(sourcePath));
        ContractGenerationIr firstIr = ContractGenerationIrBuilder.Build(
            "repository-dashboard",
            "Repository dashboard",
            source.RootElement);
        ContractGenerationIr secondIr = ContractGenerationIrBuilder.Build(
            "repository-dashboard",
            "Repository dashboard",
            source.RootElement);

        Assert.Equal(
            ContractGenerationIrSerializer.Serialize(firstIr),
            ContractGenerationIrSerializer.Serialize(secondIr));
        Assert.Equal(
            ContractTypeScriptMetadataGenerator.Generate(firstIr),
            ContractTypeScriptMetadataGenerator.Generate(secondIr));
    }

    [Fact]
    public void GeneratedContractFreshnessVerifierCoversGeneratedRepositoryDashboardArtifact()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        ContractArtifactFreshnessSpec spec = ContractArtifactFreshnessSpec.Load(repositoryRoot.Combine(
            "tests",
            "CommandCenter.Backend.Tests",
            "ContractFixtures",
            "repository-dashboard.generated-artifact-freshness.json"));
        ContractGeneratedArtifactFreshnessVerifier verifier = new(spec);

        IReadOnlyList<ContractGeneratedArtifactFreshnessDrift> drifts = verifier.Verify(repositoryRoot);

        Assert.Empty(drifts);
    }

    [Fact]
    public void GeneratedContractFreshnessVerifierReportsManualChangeForGeneratedArtifact()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        ContractArtifactFreshnessSpec current = ContractArtifactFreshnessSpec.Load(repositoryRoot.Combine(
            "tests",
            "CommandCenter.Backend.Tests",
            "ContractFixtures",
            "repository-dashboard.generated-artifact-freshness.json"));
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

        ContractGeneratedArtifactFreshnessDrift drift = Assert.Single(verifier.Verify(repositoryRoot));

        Assert.Equal(ContractGeneratedArtifactFreshnessDriftKind.UnexpectedManualArtifactModification, drift.Kind);
        Assert.Equal("generated-typescript-repository-dashboard-contract-metadata", drift.ArtifactId);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(directory.Combine("CommandCenter.slnx")))
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

    private static void WriteFreshnessManifest(
        string manifestPath,
        string sourcePath,
        string artifactPath,
        DirectoryInfo repositoryRoot)
    {
        var manifest = new
        {
            contractId = "repository-dashboard",
            sourcePath = ToRepositoryRelativePath(sourcePath, repositoryRoot),
            sourceSha256 = ComputeSha256(sourcePath),
            artifacts = new[]
            {
                new
                {
                    artifactId = "generated-typescript-repository-dashboard-contract-metadata",
                    artifactPath = ToRepositoryRelativePath(artifactPath, repositoryRoot),
                    artifactKind = "generated-typescript-contract-metadata",
                    artifactSha256 = ComputeSha256(artifactPath)
                }
            }
        };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, options) + Environment.NewLine);
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string ToRepositoryRelativePath(string path, DirectoryInfo repositoryRoot)
    {
        return Path.GetRelativePath(repositoryRoot.FullName, path).Replace('\\', '/');
    }
}
