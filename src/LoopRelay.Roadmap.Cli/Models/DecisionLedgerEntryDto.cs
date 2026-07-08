namespace LoopRelay.Roadmap.Cli;

internal sealed partial record DecisionLedgerEntryDto(
    string DecisionId,
    DateTimeOffset Timestamp,
    RoadmapState State,
    string Transition,
    string Prompt,
    string ProjectionPath,
    IReadOnlyList<string> InputArtifactPaths,
    IReadOnlyList<string> OutputArtifactPaths,
    string Decision,
    string Confidence,
    string RationaleExcerpt)
{
    public static DecisionLedgerEntryDto FromDomain(DecisionLedgerEntry entry) =>
        new(
            entry.DecisionId,
            entry.Timestamp,
            entry.State,
            entry.Transition,
            entry.Prompt,
            entry.ProjectionPath,
            entry.InputArtifactPaths.ToArray(),
            entry.OutputArtifactPaths.ToArray(),
            entry.Decision,
            entry.Confidence,
            entry.RationaleExcerpt);

    public DecisionLedgerEntry ToDomain() =>
        new(
            DecisionId,
            Timestamp,
            State,
            Transition,
            Prompt,
            ProjectionPath,
            InputArtifactPaths.ToArray(),
            OutputArtifactPaths.ToArray(),
            Decision,
            Confidence,
            RationaleExcerpt);

    public static bool IsDecisionId(string value) => DecisionIdFormatRegex().IsMatch(value);

    [System.Text.RegularExpressions.GeneratedRegex(@"^D\d{4}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex DecisionIdFormatRegex();
}
