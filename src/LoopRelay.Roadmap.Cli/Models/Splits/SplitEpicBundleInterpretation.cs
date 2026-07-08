using LoopRelay.Roadmap.Cli.Models.ArtifactBundles;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;

namespace LoopRelay.Roadmap.Cli.Models.Splits;

internal sealed record SplitEpicBundleInterpretation(
    SplitEpicBundleInterpretationStatus Status,
    IReadOnlyList<ExtractedBundleFile> ValidatedChildEpics,
    ExtractedBundleFile? SelectedChild,
    string SelectedChildRationale,
    string Reason,
    IReadOnlyList<SplitEpicBundleRejection> Rejections)
{
    public bool IsValid => Status == SplitEpicBundleInterpretationStatus.Valid;

    public static SplitEpicBundleInterpretation Valid(
        IReadOnlyList<ExtractedBundleFile> validatedChildEpics,
        ExtractedBundleFile selectedChild,
        string selectedChildRationale) =>
        new(
            SplitEpicBundleInterpretationStatus.Valid,
            validatedChildEpics,
            selectedChild,
            selectedChildRationale,
            "SplitEpic bundle contains validated child epics.",
            []);

    public static SplitEpicBundleInterpretation Blocked(
        string reason,
        IReadOnlyList<SplitEpicBundleRejection> rejections) =>
        new(SplitEpicBundleInterpretationStatus.Blocked, [], null, string.Empty, reason, rejections);

    public static SplitEpicBundleInterpretation Invalid(
        string reason,
        IReadOnlyList<SplitEpicBundleRejection> rejections,
        IReadOnlyList<ExtractedBundleFile>? validatedChildEpics = null) =>
        new(
            SplitEpicBundleInterpretationStatus.Invalid,
            validatedChildEpics ?? [],
            null,
            string.Empty,
            reason,
            rejections);
}
