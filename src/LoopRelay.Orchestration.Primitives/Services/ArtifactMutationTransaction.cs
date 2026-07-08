using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Orchestration.Services;

public sealed class ArtifactMutationTransaction
{
    private readonly IArtifactStore _store;
    private readonly Repository _repository;
    private readonly OperationPermissionProfile _profile;
    private readonly Dictionary<string, string> _snapshots;
    private readonly HashSet<string> _absentExactWrites;

    private ArtifactMutationTransaction(
        IArtifactStore store,
        Repository repository,
        OperationPermissionProfile profile,
        Dictionary<string, string> snapshots,
        HashSet<string> absentExactWrites)
    {
        _store = store;
        _repository = repository;
        _profile = profile;
        _snapshots = snapshots;
        _absentExactWrites = absentExactWrites;
    }

    public static async Task<ArtifactMutationTransaction> CaptureAsync(
        IArtifactStore store,
        Repository repository,
        OperationPermissionProfile profile)
    {
        var snapshots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var absentExactWrites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string relative in profile.AllowedWrites)
        {
            string absolute = Resolve(repository, relative);
            string? content = await store.ReadAsync(absolute);
            if (content is null)
            {
                absentExactWrites.Add(absolute);
            }
            else
            {
                snapshots[absolute] = content;
            }
        }

        foreach (OperationPathGlob glob in profile.AllowedWriteGlobs)
        {
            string directory = Resolve(repository, glob.Directory);
            IReadOnlyList<string> matches = await store.ListAsync(directory, glob.Pattern);
            foreach (string match in matches)
            {
                string? content = await store.ReadAsync(match);
                if (content is not null)
                {
                    snapshots[match] = content;
                }
            }
        }

        return new ArtifactMutationTransaction(store, repository, profile, snapshots, absentExactWrites);
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
            string directory = Resolve(_repository, glob.Directory);
            IReadOnlyList<string> matches = await _store.ListAsync(directory, glob.Pattern);
            foreach (string match in matches)
            {
                if (!_snapshots.ContainsKey(match))
                {
                    await _store.DeleteAsync(match);
                }
            }
        }
    }

    public async Task<IReadOnlyList<string>> DeletedSnapshotFilesAsync()
    {
        var deleted = new List<string>();
        foreach (string absolute in _snapshots.Keys)
        {
            if (!await _store.ExistsAsync(absolute))
            {
                deleted.Add(ArtifactPath.ToRepositoryRelativePath(_repository, absolute));
            }
        }

        return deleted;
    }

    private static string Resolve(Repository repository, string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(repository, relativePath);
}
