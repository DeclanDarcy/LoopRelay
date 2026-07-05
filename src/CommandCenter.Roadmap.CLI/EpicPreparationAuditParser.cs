namespace CommandCenter.Roadmap.Cli;

internal sealed class EpicPreparationAuditParser
{
    private static readonly string[] AllowedDispositions =
    [
        "Realign",
        "Reimagine",
        "Retire",
        "Insufficient Evidence",
    ];

    private static readonly string[] AllowedNextSteps =
    [
        "Realign Epic",
        "Reimagine Epic",
        "Retire Epic",
        "Gather More Evidence",
    ];

    public EpicPreparationAuditDecision Parse(string markdown)
    {
        IReadOnlyDictionary<string, string> fields = MarkdownTableParser.ParseFieldTable(markdown, "## Audit Disposition");
        return new EpicPreparationAuditDecision(
            RequiredAllowed(fields, "Disposition", AllowedDispositions),
            RequiredAllowed(fields, "Confidence", CommonAllowedValues.Confidence),
            RequiredAllowed(fields, "Recommended Next Step", AllowedNextSteps));
    }

    private static string RequiredAllowed(IReadOnlyDictionary<string, string> fields, string field, IReadOnlyList<string> allowed)
    {
        if (!fields.TryGetValue(field, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            throw new MarkdownParseException($"Required audit field missing: {field}");
        }

        string trimmed = value.Trim();
        string? match = allowed.FirstOrDefault(allowedValue => string.Equals(allowedValue, trimmed, StringComparison.Ordinal));
        return match ?? throw new MarkdownParseException($"Audit field `{field}` has unsupported value `{trimmed}`.");
    }
}

internal sealed record EpicPreparationAuditDecision(
    string Disposition,
    string Confidence,
    string RecommendedNextStep);
