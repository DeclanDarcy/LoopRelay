using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Effects;

namespace LoopRelay.Infrastructure.Services.Effects;

public sealed class FilesystemWriteEffectExecutor(Repository _repository) : IEffectExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public EffectExecutorKey Key => WorkspaceEffectExecutorKeys.FilesystemWrite;
    public string Version => "1";

    public async Task<EffectExecutionObservation> ExecuteAsync(EffectIntent intent, CancellationToken cancellationToken)
    {
        FilesystemWriteEffectPayload payload = Parse(intent);
        string path = Resolve(_repository, payload.RelativePath);
        string before = await HashExistingAsync(path, cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + intent.Identity.Value + ".tmp";
        await File.WriteAllTextAsync(temporary, payload.Content, cancellationToken);
        File.Move(temporary, path, overwrite: true);
        string after = await HashExistingAsync(path, cancellationToken);
        string expected = Hash(payload.Content);
        bool satisfied = string.Equals(after, expected, StringComparison.Ordinal);
        return new EffectExecutionObservation(
            satisfied ? EffectLifecycle.Succeeded : EffectLifecycle.Failed,
            satisfied ? "Filesystem artifact content hash verified." : "Filesystem artifact hash did not match the intent.",
            [payload.RelativePath, after], before, after, satisfied, payload.RelativePath);
    }

    internal static FilesystemWriteEffectPayload Parse(EffectIntent intent) =>
        JsonSerializer.Deserialize<FilesystemWriteEffectPayload>(intent.TypedPayload, JsonOptions)
        ?? throw new InvalidOperationException("Filesystem-write effect payload is invalid.");

    internal static string Resolve(Repository repository, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string root = Path.GetFullPath(repository.Path);
        string path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string relative = Path.GetRelativePath(root, path);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidOperationException("Filesystem-write target escapes the repository root.");
        return path;
    }

    internal static async Task<string> HashExistingAsync(string path, CancellationToken cancellationToken) =>
        File.Exists(path) ? Hash(await File.ReadAllTextAsync(path, cancellationToken)) : "missing";

    internal static string Hash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}

public sealed class FilesystemWriteEffectReconciler(Repository _repository) : IEffectReconciler
{
    public async Task<EffectReconciliationObservation> ReconcileAsync(
        EffectIntent intent,
        CancellationToken cancellationToken)
    {
        FilesystemWriteEffectPayload payload = FilesystemWriteEffectExecutor.Parse(intent);
        string path = FilesystemWriteEffectExecutor.Resolve(_repository, payload.RelativePath);
        string observed = await FilesystemWriteEffectExecutor.HashExistingAsync(path, cancellationToken);
        string expected = FilesystemWriteEffectExecutor.Hash(payload.Content);
        return string.Equals(observed, expected, StringComparison.Ordinal)
            ? new EffectReconciliationObservation(
                EffectReconciliationVerdict.Succeeded,
                "Filesystem artifact independently matches the intended content hash.",
                [payload.RelativePath, observed], "unknown", observed, payload.RelativePath)
            : new EffectReconciliationObservation(
                EffectReconciliationVerdict.NotApplied,
                "Filesystem artifact does not match the intended content hash.",
                [payload.RelativePath, observed], "unknown", observed);
    }
}
