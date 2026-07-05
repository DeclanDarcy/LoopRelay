namespace CommandCenter.Roadmap.Cli;

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
        return new SelectionDecision(outcome, initiative, initiativeType, confidence, primaryReason);
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

internal sealed record SelectionDecision(
    string RecommendedOutcome,
    string RecommendedInitiative,
    string InitiativeType,
    string Confidence,
    string PrimaryReason);
