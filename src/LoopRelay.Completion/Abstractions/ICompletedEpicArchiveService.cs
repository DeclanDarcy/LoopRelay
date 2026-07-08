using System.Globalization;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;

namespace LoopRelay.Completion;

public interface ICompletedEpicArchiveService
{
    Task<CompletedEpicArchiveResult> ArchiveAndSynthesizeAsync(
        CompletedEpicArchiveRequest request,
        CancellationToken cancellationToken = default);
}
