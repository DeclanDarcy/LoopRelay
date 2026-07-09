using System.Security.Cryptography;
using System.Text;
using LoopRelay.Core.Abstractions.Artifacts;

namespace LoopRelay.Core.Services.Artifacts;

public sealed class CanonicalArtifactHasher(ILogicalArtifactResolver resolver) : ICanonicalArtifactHasher
{
    public const string Sha256Algorithm = "sha256";

    public async Task<CanonicalArtifactHash?> HashIfPresentAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        LogicalArtifactResolutionResult result = await resolver.ResolveAsync(relativePath, cancellationToken);
        if (!result.IsResolved)
        {
            return null;
        }

        return Hash(result.Descriptor, result.Content!.Text);
    }

    public async Task<CanonicalArtifactHash> RequireHashAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        LogicalArtifactResolutionResult result = await resolver.ResolveAsync(relativePath, cancellationToken);
        if (!result.IsResolved)
        {
            throw new InvalidOperationException(result.Message ?? $"Logical artifact could not be resolved: {relativePath}");
        }

        return Hash(result.Descriptor, result.Content!.Text);
    }

    public static CanonicalArtifactHash Hash(LogicalArtifactDescriptor descriptor, string content)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return new CanonicalArtifactHash(
            descriptor,
            Sha256Algorithm,
            Convert.ToHexString(bytes).ToLowerInvariant());
    }
}
