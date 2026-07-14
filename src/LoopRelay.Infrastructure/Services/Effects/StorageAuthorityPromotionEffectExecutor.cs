using System.Security.Cryptography;
using System.Text.Json;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Effects;

namespace LoopRelay.Infrastructure.Services.Effects;

public sealed class StorageAuthorityPromotionEffectExecutor(Repository _repository) : IEffectExecutor
{
    public EffectExecutorKey Key => WorkspaceEffectExecutorKeys.StorageAuthorityPromotion;
    public string Version => "1";

    public async Task<EffectExecutionObservation> ExecuteAsync(
        EffectIntent intent,
        CancellationToken cancellationToken)
    {
        StorageAuthorityPromotionEffectPayload payload =
            JsonSerializer.Deserialize<StorageAuthorityPromotionEffectPayload>(intent.TypedPayload,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("Storage promotion payload is invalid.");
        string source = Resolve(payload.SourceRelativePath);
        string target = Resolve(payload.TargetRelativePath);
        string sourceHash = await HashAsync(source, cancellationToken);
        if (!string.Equals(sourceHash, payload.ExpectedSha256, StringComparison.Ordinal))
            return new(EffectLifecycle.Failed, "Staged authority hash does not match the planned payload.",
                [sourceHash, payload.ExpectedSha256], sourceHash, "not-promoted", false);
        if (File.Exists(target))
        {
            string existingHash = await HashAsync(target, cancellationToken);
            bool matches = string.Equals(existingHash, sourceHash, StringComparison.Ordinal);
            return new(matches ? EffectLifecycle.Succeeded : EffectLifecycle.Failed,
                matches ? "Existing storage authority matches the planned promotion receipt."
                    : "A different storage authority already exists; promotion is ambiguous.",
                [payload.TargetRelativePath, existingHash], sourceHash, existingHash, matches, existingHash);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string promotion = target + $".promotion-{intent.Identity.Value}";
        File.Copy(source, promotion, overwrite: false);
        string copiedHash = await HashAsync(promotion, cancellationToken);
        if (!string.Equals(copiedHash, sourceHash, StringComparison.Ordinal))
            return new(EffectLifecycle.Unknown, "Copied promotion bytes could not be verified.",
                [sourceHash, copiedHash], sourceHash, copiedHash, false);
        File.Move(promotion, target, overwrite: false);
        string targetHash = await HashAsync(target, cancellationToken);
        bool verified = string.Equals(targetHash, sourceHash, StringComparison.Ordinal);
        return new(verified ? EffectLifecycle.Succeeded : EffectLifecycle.Unknown,
            verified ? "Staged storage authority was absence-guarded and atomically promoted."
                : "Promoted storage authority hash is unknown.",
            [payload.SourceRelativePath, payload.TargetRelativePath, targetHash], sourceHash, targetHash, verified,
            targetHash);
    }

    private string Resolve(string relative)
    {
        string root = Path.GetFullPath(_repository.Path);
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        string scoped = Path.GetRelativePath(root, path);
        if (scoped.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(scoped))
            throw new InvalidOperationException("Storage promotion path escapes the repository root.");
        return path;
    }

    private static async Task<string> HashAsync(string path, CancellationToken token)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, token));
    }
}
