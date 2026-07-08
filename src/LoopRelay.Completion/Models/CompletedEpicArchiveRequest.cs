using System.Globalization;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;

namespace LoopRelay.Completion;

public sealed record CompletedEpicArchiveRequest(
    Repository Repository,
    string ActiveEpicPath = CompletionArtifactPaths.ActiveEpic,
    string ArchiveRoot = CompletionArtifactPaths.CompletedEpicsDirectory);
