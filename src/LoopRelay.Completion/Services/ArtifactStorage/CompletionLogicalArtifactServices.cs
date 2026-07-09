using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;

namespace LoopRelay.Completion.Services.ArtifactStorage;

internal static class CompletionLogicalArtifactServices
{
    public static ILogicalArtifactResolver CreateResolver(CompletionArtifacts artifacts) =>
        new LogicalArtifactResolver(
        [
            new FileBackedExecutionEvidenceLogicalArtifactProvider(artifacts.ExecutionEvidenceStore),
        ]);
}
