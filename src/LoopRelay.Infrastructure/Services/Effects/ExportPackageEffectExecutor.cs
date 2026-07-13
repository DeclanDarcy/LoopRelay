using System.Security.Cryptography;
using System.Text.Json;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Effects;

namespace LoopRelay.Infrastructure.Services.Effects;

public sealed class ExportPackageEffectExecutor(Repository _repository) : IEffectExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public EffectExecutorKey Key => WorkspaceEffectExecutorKeys.ExportPackageWrite;
    public string Version => "1";

    public async Task<EffectExecutionObservation> ExecuteAsync(
        EffectIntent intent,
        CancellationToken cancellationToken)
    {
        ExportPackageEffectPayload payload = Parse(intent);
        byte[] content = Convert.FromBase64String(payload.Base64Content);
        string plannedHash = Hash(content);
        if (!string.Equals(plannedHash, payload.ContentSha256, StringComparison.Ordinal))
            throw new InvalidOperationException("Export-package payload hash is invalid.");
        string path = Resolve(_repository, payload.TargetRelativePath);
        string before = await HashExistingAsync(path, cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + intent.Identity.Value + ".tmp";
        await File.WriteAllBytesAsync(temporary, content, cancellationToken);
        File.Move(temporary, path, overwrite: true);
        string after = await HashExistingAsync(path, cancellationToken);
        bool satisfied = string.Equals(after, plannedHash, StringComparison.Ordinal);
        return new EffectExecutionObservation(
            satisfied ? EffectLifecycle.Succeeded : EffectLifecycle.Failed,
            satisfied ? "Export package byte hash verified." : "Export package byte hash did not match.",
            [payload.TargetRelativePath, after], before, after, satisfied, payload.TargetRelativePath);
    }

    internal static ExportPackageEffectPayload Parse(EffectIntent intent) =>
        JsonSerializer.Deserialize<ExportPackageEffectPayload>(intent.TypedPayload, JsonOptions)
        ?? throw new InvalidOperationException("Export-package effect payload is invalid.");

    internal static string Resolve(Repository repository, string relativePath)
    {
        string root = Path.GetFullPath(repository.Path);
        string path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string relative = Path.GetRelativePath(root, path);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidOperationException("Export-package target escapes the repository root.");
        return path;
    }

    internal static async Task<string> HashExistingAsync(string path, CancellationToken cancellationToken) =>
        File.Exists(path) ? Hash(await File.ReadAllBytesAsync(path, cancellationToken)) : "missing";

    internal static string Hash(byte[] content) =>
        Convert.ToHexStringLower(SHA256.HashData(content));
}

public sealed class ExportPackageEffectReconciler(Repository _repository) : IEffectReconciler
{
    public async Task<EffectReconciliationObservation> ReconcileAsync(
        EffectIntent intent,
        CancellationToken cancellationToken)
    {
        ExportPackageEffectPayload payload = ExportPackageEffectExecutor.Parse(intent);
        string path = ExportPackageEffectExecutor.Resolve(_repository, payload.TargetRelativePath);
        string observed = await ExportPackageEffectExecutor.HashExistingAsync(path, cancellationToken);
        bool satisfied = string.Equals(observed, payload.ContentSha256, StringComparison.Ordinal);
        return new EffectReconciliationObservation(
            satisfied ? EffectReconciliationVerdict.Succeeded : EffectReconciliationVerdict.NotApplied,
            satisfied ? "Export package independently matches the intended byte hash."
                : "Export package is absent or has a different byte hash.",
            [payload.TargetRelativePath, observed], "unknown", observed,
            satisfied ? payload.TargetRelativePath : null);
    }
}
