using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Orchestration.Chaining;

namespace LoopRelay.Cli.Services.Cli;

/// <summary>
/// Application-boundary interpretation of immutable prerequisite evidence. The doctor owns
/// neither workflow outcomes nor exit codes; those mappings remain here and in the CLI runner.
/// </summary>
internal sealed record RuntimePrerequisiteApplicationResult(
    RuntimePrerequisiteInspection? Evidence,
    WorkflowStopReason? StopReason)
{
    public static RuntimePrerequisiteApplicationResult NotRequired { get; } = new(null, null);
}
