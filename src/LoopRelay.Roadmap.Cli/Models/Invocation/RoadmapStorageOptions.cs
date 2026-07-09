using LoopRelay.Roadmap.Cli.Services.Persistence;

namespace LoopRelay.Roadmap.Cli.Models.Invocation;

internal sealed record RoadmapStorageOptions(
    IReadOnlySet<WorkspaceSyncDomain>? Domains = null,
    bool ForceImport = false,
    bool ForceExport = false,
    bool FullRoundtrip = false)
{
    public static RoadmapStorageOptions Default { get; } = new();

    public WorkspaceSyncOptions ToSyncOptions() =>
        new(Domains, ForceImport, ForceExport);

    public WorkspaceVerificationOptions ToVerificationOptions() =>
        new(Domains, FullRoundtrip);
}
