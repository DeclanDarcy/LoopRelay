using System.Text.Json;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Effects;

namespace LoopRelay.Infrastructure.Services.Effects;

public sealed class SurfaceRestoreEffectExecutor(Repository _repository) : IEffectExecutor
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public EffectExecutorKey Key => WorkspaceEffectExecutorKeys.SurfaceRestore;
    public string Version => "1";

    public async Task<EffectExecutionObservation> ExecuteAsync(
        EffectIntent intent, CancellationToken cancellationToken)
    {
        SurfaceRestoreEffectPayload payload = Parse(intent);
        foreach (SurfaceRestoreFile file in payload.Files)
        {
            string path = FilesystemWriteEffectExecutor.Resolve(_repository, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporary = path + "." + intent.Identity.Value + ".restore.tmp";
            await File.WriteAllTextAsync(temporary, file.Content, cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        foreach (string relative in payload.DeletePaths)
        {
            string path = FilesystemWriteEffectExecutor.Resolve(_repository, relative);
            if (File.Exists(path)) File.Delete(path);
        }
        (bool satisfied, string[] evidence) = await ObserveAsync(_repository, payload, cancellationToken);
        return new(satisfied ? EffectLifecycle.Succeeded : EffectLifecycle.Failed,
            satisfied ? "Scoped artifact surface restored to its captured manifest."
                : "Scoped artifact surface differs from its captured manifest after restore.",
            evidence, "candidate-surface", payload.ManifestHash, satisfied, payload.ManifestHash);
    }

    internal static SurfaceRestoreEffectPayload Parse(EffectIntent intent) =>
        JsonSerializer.Deserialize<SurfaceRestoreEffectPayload>(intent.TypedPayload, Options)
        ?? throw new InvalidOperationException("Surface-restore payload is invalid.");

    internal static async Task<(bool Satisfied, string[] Evidence)> ObserveAsync(
        Repository repository, SurfaceRestoreEffectPayload payload, CancellationToken cancellationToken)
    {
        var evidence = new List<string>();
        bool satisfied = true;
        foreach (SurfaceRestoreFile file in payload.Files)
        {
            string path = FilesystemWriteEffectExecutor.Resolve(repository, file.RelativePath);
            string hash = await FilesystemWriteEffectExecutor.HashExistingAsync(path, cancellationToken);
            evidence.Add($"{file.RelativePath}:{hash}");
            satisfied &= string.Equals(hash, file.Sha256, StringComparison.Ordinal);
        }
        foreach (string relative in payload.DeletePaths)
        {
            bool exists = File.Exists(FilesystemWriteEffectExecutor.Resolve(repository, relative));
            evidence.Add($"{relative}:{(exists ? "present" : "absent")}");
            satisfied &= !exists;
        }
        return (satisfied, evidence.ToArray());
    }
}

public sealed class SurfaceRestoreEffectReconciler(Repository _repository) : IEffectReconciler
{
    public async Task<EffectReconciliationObservation> ReconcileAsync(
        EffectIntent intent, CancellationToken cancellationToken)
    {
        SurfaceRestoreEffectPayload payload = SurfaceRestoreEffectExecutor.Parse(intent);
        (bool satisfied, string[] evidence) = await SurfaceRestoreEffectExecutor.ObserveAsync(
            _repository, payload, cancellationToken);
        return new(satisfied ? EffectReconciliationVerdict.Succeeded : EffectReconciliationVerdict.NotApplied,
            satisfied ? "Captured artifact surface manifest is restored."
                : "Captured artifact surface manifest is not restored.",
            evidence, "unknown", satisfied ? payload.ManifestHash : "surface-mismatch",
            satisfied ? payload.ManifestHash : null);
    }
}
