using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Services.Projections;

namespace LoopRelay.Roadmap.Cli.Services.Decisions;

internal sealed class SelectionParser
{
    private static readonly string[] AllowedOutcomes =
    [
        "Select Existing Epic",
        "Select New Intermediary Epic",
        "Select Split Epic",
        "Strategic Investigation Required",
        "Roadmap Revision Required",
        "No Suitable Initiative",
    ];

    private static readonly string[] AllowedInitiativeTypes =
    [
        "Existing Roadmap Epic",
        "New Intermediary Epic",
        "Split Epic",
        "Strategic Investigation",
        "Roadmap Revision",
    ];

    public SelectionDecision Parse(string markdown)
    {
        IReadOnlyDictionary<string, string> fields = MarkdownTableParser.ParseFieldTable(markdown, "## Recommendation Summary");
        string outcome = RequiredAllowed(fields, "Recommended Outcome", AllowedOutcomes);
        string initiative = Required(fields, "Recommended Initiative");
        string initiativeType = RequiredAllowed(fields, "Initiative Type", AllowedInitiativeTypes);
        string confidence = RequiredAllowed(fields, "Confidence", CommonAllowedValues.Confidence);
        string primaryReason = fields.TryGetValue("Primary Reason", out string? reason) ? reason : string.Empty;
        (string? existingEpicId, string? existingEpicName) = ParseExistingEpicIdentity(markdown, outcome);
        return new SelectionDecision(outcome, initiative, initiativeType, confidence, primaryReason, existingEpicId, existingEpicName);
    }

    private static (string? EpicId, string? EpicName) ParseExistingEpicIdentity(string markdown, string outcome)
    {
        if (!string.Equals(outcome, "Select Existing Epic", StringComparison.Ordinal))
        {
            return (null, null);
        }

        IReadOnlyDictionary<string, string> fields = MarkdownTableParser.ParseFieldTable(markdown, "## If Existing Roadmap Epic Selected");
        string epicId = Required(fields, "Epic ID");
        string epicName = Required(fields, "Epic Name");
        if (!RetiredEpic.IsKnown(epicId) && !RetiredEpic.IsKnown(epicName))
        {
            throw new MarkdownParseException("Existing roadmap epic selection must include Epic ID or Epic Name.");
        }

        return (epicId, epicName);
    }

    private static string Required(IReadOnlyDictionary<string, string> fields, string field)
    {
        if (!fields.TryGetValue(field, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            throw new MarkdownParseException($"Required selection field missing: {field}");
        }

        return value.Trim();
    }

    private static string RequiredAllowed(IReadOnlyDictionary<string, string> fields, string field, IReadOnlyList<string> allowed)
    {
        string value = Required(fields, field);
        string? match = allowed.FirstOrDefault(allowedValue => string.Equals(allowedValue, value, StringComparison.Ordinal));
        return match ?? throw new MarkdownParseException($"Selection field `{field}` has unsupported value `{value}`.");
    }
}
