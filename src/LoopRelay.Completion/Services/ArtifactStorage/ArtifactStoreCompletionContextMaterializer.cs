using LoopRelay.Completion.Abstractions;

namespace LoopRelay.Completion.Services.ArtifactStorage;

public sealed class ArtifactStoreCompletionContextMaterializer(CompletionArtifacts _artifacts)
    : ICompletionContextMaterializer
{
    public async Task<string> MaterializeAsync(
        string roadmapCompletionContextPath,
        string evidenceDirectory,
        string evidenceStem,
        string content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _artifacts.WriteAsync(roadmapCompletionContextPath, content);
        return await _artifacts.WriteNumberedEvidenceAsync(evidenceDirectory, evidenceStem, content);
    }
}
