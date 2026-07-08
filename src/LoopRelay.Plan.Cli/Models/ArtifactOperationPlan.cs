using LoopRelay.Core.Models.Repositories;
using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Plan.Cli.Models;

internal sealed record ArtifactOperationPlan(
    string Label,
    string Prompt,
    IReadOnlyList<string> AllowedReads,
    IReadOnlyList<OperationPathGlob> AllowedReadGlobs,
    IReadOnlyList<string> AllowedWrites,
    IReadOnlyList<OperationPathGlob> AllowedWriteGlobs,
    IReadOnlyList<string> RequiredOutputs,
    OperationPathGlob? RequiredOutputGlob,
    string? ChangedGuard,
    bool RequireChecklistInGlob = false)
{
    public OperationPermissionProfile ToPermissionProfile(Repository repository) =>
        new(
            Label,
            repository.Path,
            AllowedReads,
            AllowedReadGlobs,
            AllowedWrites,
            AllowedWriteGlobs);
}
