using LoopRelay.Completion.Models;

namespace LoopRelay.Completion.Abstractions;

public interface ICompletedEpicArchiveService
{
    Task<CompletedEpicArchiveResult> ArchiveAndSynthesizeAsync(
        CompletedEpicArchiveRequest request,
        CancellationToken cancellationToken = default);
}
