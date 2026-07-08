using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration.Services;
using LoopRelay.Permissions.Models;

namespace LoopRelay.Plan.Cli;

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
