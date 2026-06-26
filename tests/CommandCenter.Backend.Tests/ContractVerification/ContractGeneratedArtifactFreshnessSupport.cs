using System.Security.Cryptography;
using System.Text.Json;

namespace CommandCenter.Backend.Tests;

internal sealed class ContractGeneratedArtifactFreshnessVerifier(ContractArtifactFreshnessSpec spec)
{
    public IReadOnlyList<ContractGeneratedArtifactFreshnessDrift> Verify(DirectoryInfo repositoryRoot)
    {
        var drifts = new List<ContractGeneratedArtifactFreshnessDrift>();
        string sourcePath = repositoryRoot.Combine(spec.SourcePath);
        string actualSourceHash = ComputeSha256(sourcePath);
        bool sourceChanged = !StringComparer.OrdinalIgnoreCase.Equals(actualSourceHash, spec.SourceSha256);

        foreach (ContractFreshnessArtifactSpec artifact in spec.Artifacts)
        {
            string artifactPath = repositoryRoot.Combine(artifact.ArtifactPath);
            if (!File.Exists(artifactPath))
            {
                drifts.Add(new ContractGeneratedArtifactFreshnessDrift(
                    spec.ContractId,
                    artifact.ArtifactId,
                    artifact.ArtifactPath,
                    ContractGeneratedArtifactFreshnessDriftKind.MissingExpectedArtifact,
                    "expected contract artifact is missing"));
                continue;
            }

            string actualArtifactHash = ComputeSha256(artifactPath);
            bool artifactChanged = !StringComparer.OrdinalIgnoreCase.Equals(actualArtifactHash, artifact.ArtifactSha256);

            if (sourceChanged && !artifactChanged)
            {
                drifts.Add(new ContractGeneratedArtifactFreshnessDrift(
                    spec.ContractId,
                    artifact.ArtifactId,
                    artifact.ArtifactPath,
                    ContractGeneratedArtifactFreshnessDriftKind.StaleGeneratedArtifact,
                    "Oracle source changed but the contract artifact still matches the previous generated baseline"));
            }

            if (!sourceChanged && artifactChanged)
            {
                drifts.Add(new ContractGeneratedArtifactFreshnessDrift(
                    spec.ContractId,
                    artifact.ArtifactId,
                    artifact.ArtifactPath,
                    ContractGeneratedArtifactFreshnessDriftKind.UnexpectedManualArtifactModification,
                    "contract artifact changed while the Oracle source baseline did not change"));
            }

            if (sourceChanged && artifactChanged)
            {
                drifts.Add(new ContractGeneratedArtifactFreshnessDrift(
                    spec.ContractId,
                    artifact.ArtifactId,
                    artifact.ArtifactPath,
                    ContractGeneratedArtifactFreshnessDriftKind.StaleGeneratedArtifact,
                    "Oracle source and contract artifact both changed; regeneration evidence must update the freshness baseline"));
            }
        }

        return drifts;
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}

internal sealed record ContractArtifactFreshnessSpec(
    string ContractId,
    string SourcePath,
    string SourceSha256,
    IReadOnlyList<ContractFreshnessArtifactSpec> Artifacts)
{
    public static ContractArtifactFreshnessSpec Load(string path)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ContractArtifactFreshnessSpec? spec = JsonSerializer.Deserialize<ContractArtifactFreshnessSpec>(
            File.ReadAllText(path),
            options);

        return spec ?? throw new InvalidOperationException($"Could not parse contract artifact freshness spec at {path}.");
    }
}

internal sealed record ContractFreshnessArtifactSpec(
    string ArtifactId,
    string ArtifactPath,
    string ArtifactKind,
    string ArtifactSha256);

internal sealed record ContractGeneratedArtifactFreshnessDrift(
    string ContractId,
    string ArtifactId,
    string ArtifactPath,
    ContractGeneratedArtifactFreshnessDriftKind Kind,
    string Message);

internal enum ContractGeneratedArtifactFreshnessDriftKind
{
    StaleGeneratedArtifact,
    UnexpectedManualArtifactModification,
    MissingExpectedArtifact
}
