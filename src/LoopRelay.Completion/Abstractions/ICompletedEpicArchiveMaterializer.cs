using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;

namespace LoopRelay.Completion.Abstractions;

public interface ICompletedEpicArchiveMaterializer
{
    Task MaterializeAsync(
        IArtifactStore store,
        Repository repository,
        string archiveDirectory,
        CancellationToken cancellationToken = default);
}
