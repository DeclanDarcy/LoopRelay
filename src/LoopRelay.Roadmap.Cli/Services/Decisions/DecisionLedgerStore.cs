using System.Globalization;
using System.Text.RegularExpressions;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.State;

namespace LoopRelay.Roadmap.Cli.Services.Decisions;

internal sealed partial class DecisionLedgerStore(RoadmapArtifacts _artifacts) : IDecisionLedgerStore
{
    private readonly StructuredDocumentStore<DecisionLedgerPersistenceDocument> _structuredStore = new(
        _artifacts,
        RoadmapArtifactPaths.DecisionLedgerJson,
        DecisionLedgerPersistenceDocument.CurrentSchemaVersion,
        document => document.SchemaVersion,
        DecisionLedgerPersistenceDocument.Validate);

    public async Task<string> AppendAsync(DecisionLedgerEntry entry)
    {
        DecisionLedgerPersistenceDocument ledger = await LoadDocumentAsync();
        var entries = ledger.ToDomain().Append(entry).OrderBy(item => item.DecisionId, StringComparer.Ordinal).ToArray();
        await SaveDocumentAsync(DecisionLedgerPersistenceDocument.FromDomain(entries));
        return entry.DecisionId;
    }

    public async Task<string> NextDecisionIdAsync()
    {
        DecisionLedgerPersistenceDocument ledger = await LoadDocumentAsync();
        int max = ledger.Entries
            .Select(entry => DecisionIdRegex().Match(entry.DecisionId))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture))
            .DefaultIfEmpty(0)
            .Max();

        return $"D{max + 1:0000}";
    }

    public async Task<string> LastDecisionIdAsync()
    {
        DecisionLedgerPersistenceDocument ledger = await LoadDocumentAsync();
        int max = ledger.Entries
            .Select(entry => DecisionIdRegex().Match(entry.DecisionId))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture))
            .DefaultIfEmpty(0)
            .Max();

        return max == 0 ? "None" : $"D{max:0000}";
    }

    private async Task<DecisionLedgerPersistenceDocument> LoadDocumentAsync()
    {
        DecisionLedgerPersistenceDocument? structured = await _structuredStore.LoadAsync();
        if (structured is not null)
        {
            return structured;
        }

        string? content = await _artifacts.ReadAsync(RoadmapArtifactPaths.DecisionLedger);
        if (string.IsNullOrWhiteSpace(content))
        {
            return DecisionLedgerPersistenceDocument.Empty;
        }

        DecisionLedgerPersistenceDocument migrated;
        try
        {
            migrated = ParseLegacyMarkdown(content);
        }
        catch (MarkdownParseException exception)
        {
            throw new RoadmapStepException($"Legacy decision ledger cannot be migrated: {exception.Message}");
        }

        await SaveDocumentAsync(migrated);
        return migrated;
    }

    private async Task SaveDocumentAsync(DecisionLedgerPersistenceDocument document)
    {
        await _structuredStore.SaveAsync(document);
    }

    private static DecisionLedgerPersistenceDocument ParseLegacyMarkdown(string content)
    {
        MarkdownTableParser.ValidateTables(content);
        var entries = new List<DecisionLedgerEntry>();
        MatchCollection matches = DecisionHeadingRegex().Matches(content);
        for (int index = 0; index < matches.Count; index++)
        {
            Match match = matches[index];
            int end = index + 1 < matches.Count ? matches[index + 1].Index : content.Length;
            string section = content[match.Index..end];
            string decisionId = $"D{match.Groups["number"].Value}";
            IReadOnlyDictionary<string, string> fields = MarkdownTableParser.ParseFieldTableStrict(section, $"## {decisionId}");
            entries.Add(new DecisionLedgerEntry(
                decisionId,
                ParseTimestamp(Field(fields, "Timestamp", string.Empty)) ?? DateTimeOffset.MinValue,
                ParseState(Field(fields, "State", RoadmapState.CoreReady.ToString())),
                Field(fields, "Transition", "None"),
                Field(fields, "Prompt", "None"),
                Field(fields, "Projection Path", "None"),
                ParseList(Field(fields, "Input Artifact Paths", "None")),
                ParseList(Field(fields, "Output Artifact Paths", "None")),
                Field(fields, "Decision / Disposition", "None"),
                Field(fields, "Confidence", "Unknown"),
                Field(fields, "Rationale Excerpt", string.Empty)));
        }

        DecisionLedgerPersistenceDocument migrated = DecisionLedgerPersistenceDocument.FromDomain(entries);
        IReadOnlyList<string> errors = DecisionLedgerPersistenceDocument.Validate(migrated);
        if (errors.Count > 0)
        {
            throw new RoadmapStepException($"Legacy decision ledger cannot be migrated because validation failed: {string.Join("; ", errors)}");
        }

        return migrated;
    }

    private static string Field(IReadOnlyDictionary<string, string> row, string field, string fallback) =>
        row.TryGetValue(field, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;

    private static IReadOnlyList<string> ParseList(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), "None", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return value
            .Split("<br>", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(path => !string.Equals(path, "None", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static RoadmapState ParseState(string value) =>
        Enum.TryParse(value, out RoadmapState state) ? state : RoadmapState.CoreReady;

    private static DateTimeOffset? ParseTimestamp(string value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTimeOffset parsed)
            ? parsed
            : null;

    [GeneratedRegex(@"^D(?<number>\d{4})$", RegexOptions.CultureInvariant)]
    private static partial Regex DecisionIdRegex();

    [GeneratedRegex(@"^## D(?<number>\d{4})\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex DecisionHeadingRegex();
}
