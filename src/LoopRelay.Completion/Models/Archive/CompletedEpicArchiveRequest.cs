using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Core.Models.Repositories;

namespace LoopRelay.Completion.Models.Archive;

/// <summary>
/// Describes one completed-epic archival.
/// </summary>
/// <param name="ArchiveIndex">
/// The authoritative archive index, when the caller already owns one. Supplying it makes the
/// archival converge: the same index re-archived is observed rather than recomputed, so a repeat
/// execution of a durable effect payload cannot allocate a second archive or re-invoke the
/// synthesis prompt. Leave it null to allocate the next free index.
/// </param>
public sealed record CompletedEpicArchiveRequest(
    Repository Repository,
    string ActiveEpicPath = CompletionArtifactPaths.ActiveEpic,
    string ArchiveRoot = CompletionArtifactPaths.CompletedEpicsDirectory,
    int? ArchiveIndex = null);
