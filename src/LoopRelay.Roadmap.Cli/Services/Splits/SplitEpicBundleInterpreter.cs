using System.Text.RegularExpressions;
using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Models.ArtifactBundles;
using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.Splits;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;

namespace LoopRelay.Roadmap.Cli.Services.Splits;

internal sealed partial class SplitEpicBundleInterpreter(
    IArtifactOutputClassifier? _classifier = null,
    IArtifactValidator? _validator = null)
{

    public SplitEpicBundleInterpretation Interpret(BundleExtractionResult bundle, string rawOutput)
    {
        if (bundle.IsBlocked)
        {
            return IsBlockedSplitOutput(rawOutput)
                ? SplitEpicBundleInterpretation.Blocked(
                    "SplitEpic returned a blocked output instead of child epic files.",
                    [new SplitEpicBundleRejection("SplitEpic output", "Blocked split output contained no child epic files.")])
                : SplitEpicBundleInterpretation.Invalid(
                    bundle.BlockedReason ?? "SplitEpic output did not contain child epic files.",
                    [new SplitEpicBundleRejection("SplitEpic output", bundle.BlockedReason ?? "No FILE markers were found.")]);
        }

        if (bundle.Files.Count == 0)
        {
            return SplitEpicBundleInterpretation.Invalid(
                "SplitEpic output did not contain child epic files.",
                [new SplitEpicBundleRejection("SplitEpic output", "Bundle extraction returned no files.")]);
        }

        var rejectedFiles = new List<SplitEpicBundleRejection>();
        var validatedChildren = new List<OrderedSplitChild>();

        foreach (ExtractedBundleFile file in bundle.Files)
        {
            if (!TryGetChildOrder(file.Path, out int order))
            {
                rejectedFiles.Add(new SplitEpicBundleRejection(
                    file.Path,
                    "SplitEpic only allows numbered child epic targets matching `.agents/epic-N.md`."));
                continue;
            }

            ArtifactOutputClassification classification = (_classifier ?? new ArtifactManagement.EpicAuthoringOutputClassifier()).Classify(file.Content);
            if (classification.Kind != ArtifactOutputKind.Promotable)
            {
                rejectedFiles.Add(new SplitEpicBundleRejection(file.Path, classification.Reason));
                continue;
            }

            ArtifactValidationResult validation = (_validator ?? new EpicArtifactValidator()).Validate(file.Content);
            if (!validation.IsValid)
            {
                rejectedFiles.Add(new SplitEpicBundleRejection(
                    file.Path,
                    validation.Error ?? "Split child epic failed active-epic validation."));
                continue;
            }

            validatedChildren.Add(new OrderedSplitChild(order, file));
        }

        IReadOnlyList<ExtractedBundleFile> orderedChildren = validatedChildren
            .OrderBy(child => child.Order)
            .ThenBy(child => child.File.Path, StringComparer.Ordinal)
            .Select(child => child.File)
            .ToArray();

        if (rejectedFiles.Count > 0)
        {
            return SplitEpicBundleInterpretation.Invalid(
                "SplitEpic bundle contains targets or child epics outside the split contract.",
                rejectedFiles,
                orderedChildren);
        }

        if (orderedChildren.Count == 0)
        {
            return SplitEpicBundleInterpretation.Invalid(
                "SplitEpic bundle did not contain any valid child epic files.",
                [new SplitEpicBundleRejection("SplitEpic output", "No validated `.agents/epic-N.md` child epic files were found.")]);
        }

        ExtractedBundleFile selectedChild = orderedChildren[0];
        return SplitEpicBundleInterpretation.Valid(
            orderedChildren,
            selectedChild,
            "First validated child epic selected by numeric split order.");
    }

    public static bool IsChildEpicPath(string path) => TryGetChildOrder(path, out _);

    private static bool TryGetChildOrder(string path, out int order)
    {
        Match match = SplitChildEpicPathRegex().Match(path);
        if (!match.Success || !int.TryParse(match.Groups["order"].Value, out order))
        {
            order = 0;
            return false;
        }

        return true;
    }

    private static bool IsBlockedSplitOutput(string rawOutput)
    {
        foreach (string line in rawOutput.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                return BlockedHeadingRegex().IsMatch(trimmed);
            }
        }

        return false;
    }

    [GeneratedRegex(@"^\.agents/epic-(?<order>[1-9][0-9]*)\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex SplitChildEpicPathRegex();

    [GeneratedRegex(@"^#\s+.*\bBlocked\b.*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BlockedHeadingRegex();

    private sealed record OrderedSplitChild(int Order, ExtractedBundleFile File);
}
