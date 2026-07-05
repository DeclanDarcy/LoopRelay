using System.Text.RegularExpressions;

namespace CommandCenter.Roadmap.Cli;

internal sealed partial class DecisionLedgerStore(RoadmapArtifacts artifacts)
{
    public async Task<string> AppendAsync(DecisionLedgerEntry entry)
    {
        string? existing = await artifacts.ReadAsync(RoadmapArtifactPaths.DecisionLedger);
        string content = string.IsNullOrWhiteSpace(existing) ? "# Decision Ledger\n" : existing;

        content += Environment.NewLine + RenderEntry(entry);
        await artifacts.WriteAsync(RoadmapArtifactPaths.DecisionLedger, content);
        return entry.DecisionId;
    }

    public async Task<string> NextDecisionIdAsync()
    {
        string? existing = await artifacts.ReadAsync(RoadmapArtifactPaths.DecisionLedger);
        if (string.IsNullOrWhiteSpace(existing))
        {
            return "D0001";
        }

        int max = 0;
        foreach (Match match in DecisionIdRegex().Matches(existing))
        {
            if (int.TryParse(match.Groups["number"].Value, out int number))
            {
                max = Math.Max(max, number);
            }
        }

        return $"D{max + 1:0000}";
    }

    public async Task<string> LastDecisionIdAsync()
    {
        string? existing = await artifacts.ReadAsync(RoadmapArtifactPaths.DecisionLedger);
        if (string.IsNullOrWhiteSpace(existing))
        {
            return "None";
        }

        MatchCollection matches = DecisionIdRegex().Matches(existing);
        return matches.Count == 0 ? "None" : $"D{matches.Select(match => int.Parse(match.Groups["number"].Value)).Max():0000}";
    }

    private static string RenderEntry(DecisionLedgerEntry entry) =>
        $"""
        ## {entry.DecisionId}

        | Field | Value |
        |---|---|
        | Timestamp | {entry.Timestamp:O} |
        | State | {entry.State} |
        | Transition | {entry.Transition} |
        | Prompt | {entry.Prompt} |
        | Projection Path | {entry.ProjectionPath} |
        | Input Artifact Paths | {Join(entry.InputArtifactPaths)} |
        | Output Artifact Paths | {Join(entry.OutputArtifactPaths)} |
        | Decision / Disposition | {entry.Decision} |
        | Confidence | {entry.Confidence} |
        | Rationale Excerpt | {entry.RationaleExcerpt.Replace('\n', ' ')} |
        """;

    private static string Join(IReadOnlyList<string> paths) => paths.Count == 0 ? "None" : string.Join("<br>", paths);

    [GeneratedRegex(@"## D(?<number>\d{4})", RegexOptions.CultureInvariant)]
    private static partial Regex DecisionIdRegex();
}
