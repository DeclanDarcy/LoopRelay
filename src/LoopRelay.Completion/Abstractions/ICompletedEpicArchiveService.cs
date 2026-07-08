using LoopRelay.Completion.Models;
using LoopRelay.Completion.Models.Archive;

namespace LoopRelay.Completion.Abstractions;

public interface ICompletedEpicArchiveService
{
    Task<CompletedEpicArchiveResult> ArchiveAndSynthesizeAsync(
        CompletedEpicArchiveRequest request,
        CancellationToken cancellationToken = default);
}
