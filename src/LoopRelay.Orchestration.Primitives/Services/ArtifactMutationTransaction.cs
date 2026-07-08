using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Orchestration.Services;

public sealed class ArtifactMutationTransaction
{
    private readonly IArtifactStore store;
    private readonly Repository repository;
    private readonly OperationPermissionProfile profile;
    private readonly Dictionary<string, string> snapshots;
    private readonly HashSet<string> absentExactWrites;

    private ArtifactMutationTransaction(
        IArtifactStore store,
        Repository repository,
        OperationPermissionProfile profile,
        Dictionary<string, string> snapshots,
        HashSet<string> absentExactWrites)
    {
        this.store = store;
        this.repository = repository;
        this.profile = profile;
        this.snapshots = snapshots;
        this.absentExactWrites = absentExactWrites;
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
        foreach (KeyValuePair<string, string> snapshot in snapshots)
        {
            await store.WriteAsync(snapshot.Key, snapshot.Value);
        }

        foreach (string absent in absentExactWrites)
        {
            if (await store.ExistsAsync(absent))
            {
                await store.DeleteAsync(absent);
            }
        }

        foreach (OperationPathGlob glob in profile.AllowedWriteGlobs)
        {
            string directory = Resolve(repository, glob.Directory);
            IReadOnlyList<string> matches = await store.ListAsync(directory, glob.Pattern);
            foreach (string match in matches)
            {
                if (!snapshots.ContainsKey(match))
                {
                    await store.DeleteAsync(match);
                }
            }
        }
    }

    public async Task<IReadOnlyList<string>> DeletedSnapshotFilesAsync()
    {
        var deleted = new List<string>();
        foreach (string absolute in snapshots.Keys)
        {
            if (!await store.ExistsAsync(absolute))
            {
                deleted.Add(ArtifactPath.ToRepositoryRelativePath(repository, absolute));
            }
        }

        return deleted;
    }

    private static string Resolve(Repository repository, string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(repository, relativePath);
}
