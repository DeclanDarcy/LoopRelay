using System.Security.Cryptography;
using System.Text.Json;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Effects;

namespace LoopRelay.Infrastructure.Services.Effects;

public sealed class ImportAuthorityPromotionEffectExecutor(Repository _repository) : IEffectExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public EffectExecutorKey Key => WorkspaceEffectExecutorKeys.ImportAuthorityPromotion;
    public string Version => "1";

    public async Task<EffectExecutionObservation> ExecuteAsync(
        EffectIntent intent, CancellationToken cancellationToken)
    {
        ImportAuthorityPromotionEffectPayload payload = Parse(intent);
        string source = Resolve(payload.SourceRelativePath);
        string target = Resolve(payload.TargetRelativePath);
        string? archive = payload.ExistingAuthorityArchiveRelativePath is null
            ? null
            : Resolve(payload.ExistingAuthorityArchiveRelativePath);
        string sourceHash = await HashExistingAsync(source, cancellationToken);
        if (!string.Equals(sourceHash, payload.ExpectedSha256, StringComparison.Ordinal))
            return new(EffectLifecycle.Failed, "Staged import authority does not match its immutable plan.",
                [payload.SourceRelativePath, sourceHash, payload.ExpectedSha256], sourceHash, "not-promoted", false);

        string targetBefore = await HashExistingAsync(target, cancellationToken);
        if (string.Equals(targetBefore, sourceHash, StringComparison.Ordinal))
            return new(EffectLifecycle.Succeeded, "Import authority was already promoted with matching bytes.",
                [payload.TargetRelativePath, targetBefore], targetBefore, targetBefore, true, targetBefore);

        if (File.Exists(target))
        {
            if (archive is null)
                return new(EffectLifecycle.HumanActionRequired,
                    "A different authority exists and the import plan has no archive target.",
                    [payload.TargetRelativePath, targetBefore], targetBefore, "not-promoted", false);
            if (File.Exists(archive))
                return new(EffectLifecycle.HumanActionRequired,
                    "Both the old authority and its planned archive exist; source authority is ambiguous.",
                    [payload.TargetRelativePath, payload.ExistingAuthorityArchiveRelativePath!],
                    targetBefore, "ambiguous", false);
            Directory.CreateDirectory(Path.GetDirectoryName(archive)!);
            File.Move(target, archive, overwrite: false);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string temporary = target + $".promotion-{intent.Identity.Value}";
        if (File.Exists(temporary)) File.Delete(temporary);
        File.Copy(source, temporary, overwrite: false);
        string copiedHash = await HashExistingAsync(temporary, cancellationToken);
        if (!string.Equals(copiedHash, sourceHash, StringComparison.Ordinal))
            return new(EffectLifecycle.Unknown, "Copied import authority could not be verified.",
                [sourceHash, copiedHash], targetBefore, copiedHash, false);
        File.Move(temporary, target, overwrite: false);
        string after = await HashExistingAsync(target, cancellationToken);
        bool satisfied = string.Equals(after, sourceHash, StringComparison.Ordinal);
        return new(satisfied ? EffectLifecycle.Succeeded : EffectLifecycle.Unknown,
            satisfied ? "Verified import authority promotion completed."
                : "Promoted import authority has an unknown byte identity.",
            [payload.SourceRelativePath, payload.TargetRelativePath, after], targetBefore, after,
            satisfied, after);
    }

    internal static ImportAuthorityPromotionEffectPayload Parse(EffectIntent intent) =>
        JsonSerializer.Deserialize<ImportAuthorityPromotionEffectPayload>(intent.TypedPayload, JsonOptions)
        ?? throw new InvalidDataException("Import-authority promotion payload is invalid.");

    internal string Resolve(string relative)
    {
        string root = Path.GetFullPath(_repository.Path);
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        string scoped = Path.GetRelativePath(root, path);
        if (scoped.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(scoped))
            throw new InvalidOperationException("Import-authority promotion path escapes the repository root.");
        return path;
    }

    public static async Task<string> HashExistingAsync(string path, CancellationToken token)
    {
        if (!File.Exists(path)) return "missing";
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, token));
    }
}

public sealed class ImportAuthorityPromotionEffectReconciler(Repository repository) : IEffectReconciler
{
    private readonly ImportAuthorityPromotionEffectExecutor observer = new(repository);

    public async Task<EffectReconciliationObservation> ReconcileAsync(
        EffectIntent intent, CancellationToken cancellationToken)
    {
        ImportAuthorityPromotionEffectPayload payload = ImportAuthorityPromotionEffectExecutor.Parse(intent);
        string target = observer.Resolve(payload.TargetRelativePath);
        string observed = await ImportAuthorityPromotionEffectExecutor.HashExistingAsync(target, cancellationToken);
        bool satisfied = string.Equals(observed, payload.ExpectedSha256, StringComparison.Ordinal);
        return new(satisfied ? EffectReconciliationVerdict.Succeeded : EffectReconciliationVerdict.NotApplied,
            satisfied ? "Import authority independently matches the planned byte identity."
                : "Import authority promotion is not yet observably applied.",
            [payload.TargetRelativePath, observed], "unknown", observed, satisfied ? observed : null);
    }
}
