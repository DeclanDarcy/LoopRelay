using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Core.Models.Repositories;

namespace LoopRelay.Completion.Models.Archive;

public sealed record CompletedEpicArchiveRequest(
    Repository Repository,
    string ActiveEpicPath = CompletionArtifactPaths.ActiveEpic,
    string ArchiveRoot = CompletionArtifactPaths.CompletedEpicsDirectory);
