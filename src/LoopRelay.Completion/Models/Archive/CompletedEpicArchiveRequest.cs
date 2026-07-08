using LoopRelay.Completion.Services;
using LoopRelay.Core.Models.Repositories;

namespace LoopRelay.Completion.Models;

public sealed record CompletedEpicArchiveRequest(
    Repository Repository,
    string ActiveEpicPath = CompletionArtifactPaths.ActiveEpic,
    string ArchiveRoot = CompletionArtifactPaths.CompletedEpicsDirectory);
