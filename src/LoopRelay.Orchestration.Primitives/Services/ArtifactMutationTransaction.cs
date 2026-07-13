using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Orchestration.Effects;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LoopRelay.Orchestration.Services;

public sealed class ArtifactMutationTransaction
{
    private readonly IArtifactStore _store;
    private readonly OperationPermissionProfile _profile;
    private readonly Dictionary<string, string> _snapshots;
    private readonly HashSet<string> _absentExactWrites;

    private ArtifactMutationTransaction(
        IArtifactStore store,
        OperationPermissionProfile profile,
        Dictionary<string, string> snapshots,
        HashSet<string> absentExactWrites)
    {
        _store = store;
        _profile = profile;
        _snapshots = snapshots;
        _absentExactWrites = absentExactWrites;
    }

    public static async Task<ArtifactMutationTransaction> CaptureAsync(
        IArtifactStore store,
        OperationPermissionProfile profile)
    {
        var snapshots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var absentExactWrites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string relative in profile.AllowedWrites)
        {
            string? content = await store.ReadAsync(relative);
            if (content is null)
            {
                absentExactWrites.Add(relative);
            }
            else
            {
                snapshots[relative] = content;
            }
        }

        foreach (OperationPathGlob glob in profile.AllowedWriteGlobs)
        {
            IReadOnlyList<string> matches = await store.ListAsync(glob.Directory, glob.Pattern);
            foreach (string match in matches)
            {
                string? content = await store.ReadAsync(match);
                if (content is not null)
                {
                    snapshots[match] = content;
                }
            }
        }

        return new ArtifactMutationTransaction(store, profile, snapshots, absentExactWrites);
    }

    public async Task RestoreAsync()
    {
        foreach (KeyValuePair<string, string> snapshot in _snapshots)
        {
            await _store.WriteAsync(snapshot.Key, snapshot.Value);
        }

        foreach (string absent in _absentExactWrites)
        {
            if (await _store.ExistsAsync(absent))
            {
                await _store.DeleteAsync(absent);
            }
        }

        foreach (OperationPathGlob glob in _profile.AllowedWriteGlobs)
        {
            IReadOnlyList<string> matches = await _store.ListAsync(glob.Directory, glob.Pattern);
            foreach (string match in matches)
            {
                if (!_snapshots.ContainsKey(match))
                {
                    await _store.DeleteAsync(match);
                }
            }
        }
    }

    public async Task<SurfaceRestoreEffectPayload> CreateRestorePayloadAsync()
    {
        var deletes = new HashSet<string>(_absentExactWrites, StringComparer.OrdinalIgnoreCase);
        foreach (OperationPathGlob glob in _profile.AllowedWriteGlobs)
        {
            IReadOnlyList<string> matches = await _store.ListAsync(glob.Directory, glob.Pattern);
            foreach (string match in matches)
                if (!_snapshots.ContainsKey(match)) deletes.Add(match);
        }
        SurfaceRestoreFile[] files = _snapshots.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new SurfaceRestoreFile(item.Key, item.Value,
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(item.Value)))))
            .ToArray();
        string[] deletePaths = deletes.Except(_snapshots.Keys, StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal).ToArray();
        string canonical = JsonSerializer.Serialize(new { files, deletePaths });
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new(files, deletePaths, hash);
    }

    public async Task<IReadOnlyList<string>> DeletedSnapshotFilesAsync()
    {
        var deleted = new List<string>();
        foreach (string relative in _snapshots.Keys)
        {
            if (!await _store.ExistsAsync(relative))
            {
                deleted.Add(relative);
            }
        }

        return deleted;
    }
}
