using System.Globalization;
using System.Text.RegularExpressions;
using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Services;

internal sealed partial class RoadmapStateStore(RoadmapArtifacts artifacts)
{
    private readonly StructuredDocumentStore<RoadmapStatePersistenceDocument> structuredStore = new(
        artifacts,
        RoadmapArtifactPaths.StateJson,
        RoadmapStatePersistenceDocument.CurrentSchemaVersion,
        document => document.SchemaVersion,
        RoadmapStatePersistenceDocument.Validate);

    public async Task SaveAsync(RoadmapStateDocument document)
    {
        RoadmapStatePersistenceDocument persisted = RoadmapStatePersistenceDocument.FromDomain(document);
        await structuredStore.SaveAsync(persisted);
    }

    public async Task<RoadmapStateDocument?> LoadAsync()
    {
        RoadmapStatePersistenceDocument? structured = await structuredStore.LoadAsync();
        if (structured is not null)
        {
            return structured.ToDomain();
        }

        string? content = await artifacts.ReadAsync(RoadmapArtifactPaths.State);
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        RoadmapStateDocument migrated;
        try
        {
            migrated = ParseLegacyMarkdown(content);
        }
        catch (MarkdownParseException exception)
        {
            throw new RoadmapStepException($"Legacy roadmap state cannot be migrated: {exception.Message}");
        }

        await SaveAsync(migrated);
        return migrated;
    }

    private static RoadmapStateDocument ParseLegacyMarkdown(string content)
    {
        MarkdownTableParser.ValidateTables(content);

        RoadmapState state = RoadmapState.CoreReady;
        Match stateMatch = CurrentStateRegex().Match(content);
        if (!stateMatch.Success)
        {
            throw new RoadmapStepException("Legacy roadmap state cannot be migrated because `## Current State` is missing or malformed.");
        }

        if (Enum.TryParse(stateMatch.Groups["state"].Value.Trim(), out RoadmapState parsed))
        {
            state = parsed;
        }
        else
        {
            throw new RoadmapStepException($"Legacy roadmap state cannot be migrated because current state `{stateMatch.Groups["state"].Value.Trim()}` is unknown.");
        }

        IReadOnlyList<ArtifactStateRow> activeArtifacts = ParseActiveArtifacts(content);
        IReadOnlyList<RetiredEpic> retired = ParseRetiredEpics(content);
        RoadmapTransitionSummary transition = ParseLastTransition(content, state);
        RoadmapTransitionIntent transitionIntent = ParseTransitionIntent(content, state);
        IReadOnlyList<BlockerRow> blockers = ParseBlockers(content);
        IReadOnlyList<string> nextTransitions = ParseNextValidTransitions(content);
        DecisionLedgerSummary ledgerSummary = ParseDecisionLedgerSummary(content, retired.Count);
        ProjectionManifestCounts projectionCounts = ParseProjectionManifestCounts(content);

        var document = new RoadmapStateDocument(
            state,
            activeArtifacts,
            transition,
            blockers,
            ledgerSummary.LastDecisionId,
            ledgerSummary.RetiredEpicsCount,
            ledgerSummary.SplitFamiliesCount,
            projectionCounts,
            transitionIntent,
            nextTransitions,
            retired);

        RoadmapStatePersistenceDocument migrated = RoadmapStatePersistenceDocument.FromDomain(document);
        IReadOnlyList<string> errors = RoadmapStatePersistenceDocument.Validate(migrated);
        if (errors.Count > 0)
        {
            throw new RoadmapStepException($"Legacy roadmap state cannot be migrated because validation failed: {string.Join("; ", errors)}");
        }

        return document;
    }

    private static IReadOnlyList<ArtifactStateRow> ParseActiveArtifacts(string content)
    {
        string? section = MarkdownTableParser.TryExtractSection(content, "## Active Artifacts");
        if (section is null)
        {
            return [];
        }

        return MarkdownTableParser.ParseTablesStrict(section)
            .Where(row => row.ContainsKey("Artifact") && row.ContainsKey("Path") && row.ContainsKey("Status"))
            .Select(row => new ArtifactStateRow(row["Artifact"], row["Path"], row["Status"]))
            .Where(row => !string.IsNullOrWhiteSpace(row.Path))
            .ToArray();
    }

    private static RoadmapTransitionSummary ParseLastTransition(string content, RoadmapState fallbackState)
    {
        if (MarkdownTableParser.TryExtractSection(content, "## Last Transition") is null)
        {
            return new RoadmapTransitionSummary(
                fallbackState,
                fallbackState,
                "None",
                "None",
                "None",
                "None",
                TransitionStatus.Completed,
                DateTimeOffset.MinValue,
                null);
        }

        IReadOnlyDictionary<string, string> fields = MarkdownTableParser.ParseFieldTableStrict(content, "## Last Transition");
        return new RoadmapTransitionSummary(
            ParseState(Field(fields, "From", fallbackState.ToString()), fallbackState),
            ParseState(Field(fields, "To", fallbackState.ToString()), fallbackState),
            Field(fields, "Prompt", "None"),
            Field(fields, "Projection", "None"),
            Field(fields, "Output", "None"),
            Field(fields, "Decision", "None"),
            Enum.TryParse(Field(fields, "Status", TransitionStatus.Completed.ToString()), out TransitionStatus status)
                ? status
                : TransitionStatus.Completed,
            ParseTimestamp(Field(fields, "Started At", string.Empty)) ?? DateTimeOffset.MinValue,
            ParseTimestamp(Field(fields, "Completed At", string.Empty)));
    }

    private static RoadmapTransitionIntent ParseTransitionIntent(string content, RoadmapState fallbackState)
    {
        if (MarkdownTableParser.TryExtractSection(content, "## Transition Intent") is null)
        {
            return RoadmapTransitionIntent.Empty(fallbackState);
        }

        IReadOnlyDictionary<string, string> fields = MarkdownTableParser.ParseFieldTableStrict(content, "## Transition Intent");
        string evidenceValue = Field(fields, "Evidence Paths", "None");
        IReadOnlyList<string> evidencePaths = evidenceValue
            .Split("<br>", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(path => !string.Equals(path, "None", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return new RoadmapTransitionIntent(
            Field(fields, "Intent", "None"),
            ParseState(Field(fields, "Dispatch State", fallbackState.ToString()), fallbackState),
            evidencePaths);
    }

    private static IReadOnlyList<BlockerRow> ParseBlockers(string content)
    {
        string? section = MarkdownTableParser.TryExtractSection(content, "## Blockers");
        if (section is null)
        {
            return [];
        }

        return MarkdownTableParser.ParseTablesStrict(section)
            .Where(row => row.ContainsKey("Blocker") && row.ContainsKey("Required Next Step"))
            .Select(row => new BlockerRow(row["Blocker"], row["Required Next Step"]))
            .Where(row => !string.IsNullOrWhiteSpace(row.Blocker))
            .ToArray();
    }

    private static IReadOnlyList<string> ParseNextValidTransitions(string content)
    {
        int start = content.IndexOf("## Next Valid Transitions", StringComparison.Ordinal);
        if (start < 0)
        {
            return [];
        }

        string section = ExtractSubsection(content, start, "## Next Valid Transitions".Length);
        return section.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
            .Select(line => line[2..].Trim())
            .Where(line => line.Length > 0)
            .ToArray();
    }

    private static DecisionLedgerSummary ParseDecisionLedgerSummary(string content, int retiredCount)
    {
        if (MarkdownTableParser.TryExtractSection(content, "## Decision Ledger Summary") is null)
        {
            return new DecisionLedgerSummary("None", retiredCount, 0);
        }

        IReadOnlyDictionary<string, string> fields = MarkdownTableParser.ParseFieldTableStrict(content, "## Decision Ledger Summary");
        return new DecisionLedgerSummary(
            Field(fields, "Last Decision ID", "None"),
            ParseInt(Field(fields, "Retired Epics", retiredCount.ToString(CultureInfo.InvariantCulture)), retiredCount),
            ParseInt(Field(fields, "Split Families", "0"), 0));
    }

    private static ProjectionManifestCounts ParseProjectionManifestCounts(string content)
    {
        if (MarkdownTableParser.TryExtractSection(content, "## Projection Manifest Summary") is null)
        {
            return new ProjectionManifestCounts(0, 0, 0);
        }

        IReadOnlyDictionary<string, string> fields = MarkdownTableParser.ParseFieldTableStrict(content, "## Projection Manifest Summary");
        return new ProjectionManifestCounts(
            ParseInt(Field(fields, "Valid Projections", "0"), 0),
            ParseInt(Field(fields, "Stale Projections", "0"), 0),
            ParseInt(Field(fields, "Invalid Projections", "0"), 0));
    }

    private static IReadOnlyList<RetiredEpic> ParseRetiredEpics(string content)
    {
        int start = content.IndexOf("### Retired Epics", StringComparison.Ordinal);
        if (start >= 0)
        {
            string section = ExtractSubsection(content, start, "### Retired Epics".Length);
            return MarkdownTableParser.ParseTablesStrict(section)
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

    private static RoadmapState ParseState(string value, RoadmapState fallback) =>
        Enum.TryParse(value, out RoadmapState state) ? state : fallback;

    private static int ParseInt(string value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : fallback;

    private static DateTimeOffset? ParseTimestamp(string value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTimeOffset parsed)
            ? parsed
            : null;

    [GeneratedRegex(@"## Current State\s+(?<state>[A-Za-z0-9]+)", RegexOptions.CultureInvariant)]
    private static partial Regex CurrentStateRegex();

    private sealed record DecisionLedgerSummary(
        string LastDecisionId,
        int RetiredEpicsCount,
        int SplitFamiliesCount);
}
