namespace LoopRelay.Roadmap.Cli.Models.RoadmapState;

internal sealed record ResumeSafety(bool IsSafe, string Reason)
{
    public static ResumeSafety Safe(string reason) => new(true, reason);

    public static ResumeSafety Unsafe(string reason) => new(false, reason);
}
