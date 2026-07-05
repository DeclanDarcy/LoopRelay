using System.Text.RegularExpressions;

namespace CommandCenter.Roadmap.Cli;

internal sealed partial class RoadmapStateStore(RoadmapArtifacts artifacts)
{
    public async Task SaveAsync(RoadmapStateDocument document)
    {
        var lines = new List<string>
        {
            "# Engineering Loop State",
            string.Empty,
            "## Current State",
            string.Empty,
            document.CurrentState.ToString(),
            string.Empty,
            "## Active Artifacts",
            string.Empty,
            "| Artifact | Path | Status |",
            "|---|---|---|",
        };

        foreach (ArtifactStateRow row in document.ActiveArtifacts)
        {
            lines.Add($"| {row.Artifact} | {row.Path} | {row.Status} |");
        }

        lines.AddRange(
        [
            string.Empty,
            "## Last Transition",
            string.Empty,
            "| Field | Value |",
            "|---|---|",
            $"| From | {document.LastTransition.From} |",
            $"| To | {document.LastTransition.To} |",
            $"| Prompt | {document.LastTransition.Prompt} |",
            $"| Projection | {document.LastTransition.Projection} |",
            $"| Output | {document.LastTransition.Output} |",
            $"| Decision | {document.LastTransition.Decision} |",
            $"| Status | {document.LastTransition.Status} |",
            $"| Started At | {document.LastTransition.StartedAt:O} |",
            $"| Completed At | {(document.LastTransition.CompletedAt is { } completed ? completed.ToString("O") : string.Empty)} |",
            string.Empty,
            "## Blockers",
            string.Empty,
            "| Blocker | Required Next Step |",
            "|---|---|",
        ]);

        foreach (BlockerRow blocker in document.Blockers)
        {
            lines.Add($"| {blocker.Blocker} | {blocker.RequiredNextStep} |");
        }

        lines.AddRange(
        [
            string.Empty,
            "## Decision Ledger Summary",
            string.Empty,
            "| Field | Value |",
            "|---|---|",
            $"| Ledger Path | {RoadmapArtifactPaths.DecisionLedger} |",
            $"| Last Decision ID | {document.LastDecisionId} |",
            $"| Retired Exclusions | {document.RetiredExclusionsCount} |",
            $"| Split Families | {document.SplitFamiliesCount} |",
            string.Empty,
            "## Projection Manifest Summary",
            string.Empty,
            "| Field | Value |",
            "|---|---|",
            $"| Manifest Path | {RoadmapArtifactPaths.ProjectionsManifest} |",
            $"| Valid Projections | {document.ProjectionManifestCounts.Valid} |",
            $"| Stale Projections | {document.ProjectionManifestCounts.Stale} |",
            $"| Invalid Projections | {document.ProjectionManifestCounts.Invalid} |",
            string.Empty,
            "## Next Valid Transitions",
            string.Empty,
        ]);

        foreach (string transition in document.NextValidTransitions)
        {
            lines.Add($"- {transition}");
        }

        lines.AddRange(
        [
            string.Empty,
            "## Runtime State",
            string.Empty,
            "### Retired Epic Exclusions",
            string.Empty,
        ]);

        if (document.RetiredEpicExclusions.Count == 0)
        {
            lines.Add("None");
        }
        else
        {
            foreach (string exclusion in document.RetiredEpicExclusions)
            {
                lines.Add($"- {exclusion}");
            }
        }

        await artifacts.WriteAsync(RoadmapArtifactPaths.State, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    public async Task<RoadmapStateDocument?> LoadAsync()
    {
        string? content = await artifacts.ReadAsync(RoadmapArtifactPaths.State);
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        RoadmapState state = RoadmapState.CoreReady;
        Match stateMatch = CurrentStateRegex().Match(content);
        if (stateMatch.Success && Enum.TryParse(stateMatch.Groups["state"].Value.Trim(), out RoadmapState parsed))
        {
            state = parsed;
        }

        IReadOnlyList<string> retired = ParseRetiredExclusions(content);
        var transition = new RoadmapTransitionSummary(
            state,
            state,
            "None",
            "None",
            "None",
            "None",
            TransitionStatus.Completed,
            DateTimeOffset.MinValue,
            null);

        return new RoadmapStateDocument(
            state,
            [],
            transition,
            [],
            "None",
            retired.Count,
            0,
            new ProjectionManifestCounts(0, 0, 0),
            [],
            retired);
    }

    private static IReadOnlyList<string> ParseRetiredExclusions(string content)
    {
        int start = content.IndexOf("### Retired Epic Exclusions", StringComparison.Ordinal);
        if (start < 0)
        {
            return [];
        }

        string tail = content[start..];
        int next = tail.IndexOf("\n### ", "### Retired Epic Exclusions".Length, StringComparison.Ordinal);
        string section = next < 0 ? tail : tail[..next];
        return section.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
            .Select(line => line[2..].Trim())
            .Where(line => line.Length > 0)
            .ToList();
    }

    [GeneratedRegex(@"## Current State\s+(?<state>[A-Za-z0-9]+)", RegexOptions.CultureInvariant)]
    private static partial Regex CurrentStateRegex();
}
