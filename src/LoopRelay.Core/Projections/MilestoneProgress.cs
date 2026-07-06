namespace LoopRelay.Core.Projections;

/// <summary>
/// Read-only per-milestone checkbox progress derived from the milestone markdown
/// (<c>.agents/milestones/m*.md</c>). Agents own milestone agency by checking the boxes; this
/// projection only reports what they have checked. Not an execution input.
/// </summary>
public sealed class MilestoneProgress
{
    public string RelativePath { get; init; } = "";

    public string Name { get; init; } = "";

    public int CompletedTaskCount { get; init; }

    public int TotalTaskCount { get; init; }

    /// <summary>True only when the file has at least one task and every task is checked.</summary>
    public bool IsComplete { get; init; }
}

/// <summary>
/// Overall milestone progress for a repository: the per-milestone breakdown plus the rollup the
/// read-only workspace display surfaces ("N of M milestones complete").
/// </summary>
public sealed class MilestoneProgressRollup
{
    public int CompletedMilestoneCount { get; init; }

    public int TotalMilestoneCount { get; init; }

    public IReadOnlyList<MilestoneProgress> Milestones { get; init; } = [];
}
