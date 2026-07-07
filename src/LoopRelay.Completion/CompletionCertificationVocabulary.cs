namespace LoopRelay.Completion;

public static class CompletionCertificationVocabulary
{
    public static IReadOnlyList<string> CompletionStatuses { get; } =
    [
        "Fully Complete",
        "Functionally Complete",
        "Partially Complete",
        "Not Complete",
        "Inconclusive",
    ];

    public static IReadOnlyList<string> DriftClassifications { get; } =
    [
        "None",
        "Positive",
        "Negative",
        "Mixed",
        "Unknown",
    ];

    public static IReadOnlyList<string> ClosureRecommendations { get; } =
    [
        "Close Epic",
        "Close With Follow-Up",
        "Continue Epic",
        "Reopen Epic",
        "Gather More Evidence",
    ];
}
