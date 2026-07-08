namespace LoopRelay.Roadmap.Cli.Models.Execution;

internal sealed record RoadmapExecutionOptions(
    string SandboxIdentifier = "workspace-write",
    bool AllowNetwork = false,
    bool RequiresApproval = true,
    string? ElevatedReason = null)
{
    public static RoadmapExecutionOptions Default { get; } = new();

    public bool IsElevated =>
        string.Equals(SandboxIdentifier, "danger-full-access", StringComparison.Ordinal) ||
        AllowNetwork;

    public static RoadmapExecutionOptions Elevated(string reason) =>
        new("danger-full-access", AllowNetwork: true, RequiresApproval: true, ElevatedReason: reason);

    public void Validate()
    {
        if (IsElevated && string.IsNullOrWhiteSpace(ElevatedReason))
        {
            throw new RoadmapStepException("Elevated roadmap execution requires a non-empty reason.");
        }
    }
}
