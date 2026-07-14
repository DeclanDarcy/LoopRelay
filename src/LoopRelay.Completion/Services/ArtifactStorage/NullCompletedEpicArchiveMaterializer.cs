using LoopRelay.Completion.Abstractions;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;

namespace LoopRelay.Completion.Services.ArtifactStorage;

public sealed class NullCompletedEpicArchiveMaterializer : ICompletedEpicArchiveMaterializer
{
    public static NullCompletedEpicArchiveMaterializer Instance { get; } = new();

    private NullCompletedEpicArchiveMaterializer()
    {
    }

    public Task MaterializeAsync(
        IArtifactStore store,
        Repository repository,
        string archiveDirectory,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
