namespace LoopRelay.Roadmap.Cli;

internal static class RoadmapBlockedArtifact
{
    public static string Render(
        RoadmapState state,
        string transitionName,
        string reason,
        string nextStep,
        string evidencePath,
        string details,
        DateTimeOffset createdAt) =>
        $"""
        # Roadmap Transition Blocked

        | Field | Value |
        |---|---|
        | State | {state} |
        | Transition | {transitionName} |
        | Reason | {reason} |
        | Required Next Step | {nextStep} |
        | Evidence Path | {evidencePath} |
        | Created At | {createdAt:O} |

        ## Details

        {details}
        """;
}
