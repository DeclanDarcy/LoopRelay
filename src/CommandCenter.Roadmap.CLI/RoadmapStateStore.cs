using System.Globalization;
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
            $"| Retired Epics | {document.RetiredEpicsCount} |",
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
            "### Retired Epics",
            string.Empty,
        ]);

        if (document.RetiredEpics.Count == 0)
        {
            lines.Add("None");
        }
        else
        {
            lines.Add("| Epic ID | Epic Name | Retired At | Audit Evidence | Primary Reason |");
            lines.Add("|---|---|---|---|---|");
            foreach (RetiredEpic retired in document.RetiredEpics)
            {
                lines.Add(
                    $"| {Escape(retired.EpicId)} | {Escape(retired.EpicName)} | {retired.RetiredAt:O} | {Escape(retired.AuditEvidencePath)} | {Escape(retired.PrimaryReason)} |");
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

        IReadOnlyList<RetiredEpic> retired = ParseRetiredEpics(content);
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

    private static IReadOnlyList<RetiredEpic> ParseRetiredEpics(string content)
    {
        int start = content.IndexOf("### Retired Epics", StringComparison.Ordinal);
        if (start >= 0)
        {
            string section = ExtractSubsection(content, start, "### Retired Epics".Length);
            return MarkdownTableParser.ParseTables(section)
                .Select(ParseRetiredEpicRow)
                .Where(retired => retired.HasStableIdentity)
                .ToList();
        }

        return ParseLegacyRetiredExclusions(content);
    }

    private static RetiredEpic ParseRetiredEpicRow(IReadOnlyDictionary<string, string> row)
    {
        string epicId = Field(row, "Epic ID", "Unknown");
        string epicName = Field(row, "Epic Name", "Unknown");
        string reason = Field(row, "Primary Reason", "Legacy retired epic record.");
        string evidence = Field(row, "Audit Evidence", "Unknown");
        string retiredAtValue = Field(row, "Retired At", string.Empty);
        DateTimeOffset retiredAt = DateTimeOffset.TryParse(
            retiredAtValue,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTimeOffset parsed)
            ? parsed
            : DateTimeOffset.MinValue;

        return new RetiredEpic(epicId, epicName, reason, evidence, retiredAt);
    }

    private static IReadOnlyList<RetiredEpic> ParseLegacyRetiredExclusions(string content)
    {
        int start = content.IndexOf("### Retired Epic Exclusions", StringComparison.Ordinal);
        if (start < 0)
        {
            return [];
        }

        string section = ExtractSubsection(content, start, "### Retired Epic Exclusions".Length);
        return section.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
            .Select(line => line[2..].Trim())
            .Where(line => line.Length > 0)
            .Where(value => !RetiredEpic.IsWorkflowCommand(value))
            .Select(value => new RetiredEpic(
                "Unknown",
                value,
                "Imported from legacy retired epic exclusion state.",
                RoadmapArtifactPaths.State,
                DateTimeOffset.MinValue))
            .ToList();
    }

    private static string ExtractSubsection(string content, int start, int headingLength)
    {
        string tail = content[start..];
        int next = tail.IndexOf("\n### ", headingLength, StringComparison.Ordinal);
        return next < 0 ? tail : tail[..next];
    }

    private static string Field(IReadOnlyDictionary<string, string> row, string field, string fallback) =>
        row.TryGetValue(field, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

    [GeneratedRegex(@"## Current State\s+(?<state>[A-Za-z0-9]+)", RegexOptions.CultureInvariant)]
    private static partial Regex CurrentStateRegex();
}
