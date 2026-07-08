using LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Services.Projections;

namespace LoopRelay.Roadmap.Cli.Services.ExecutionPreparation;

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
        IReadOnlyDictionary<string, string> selectedEpicFields = MarkdownTableParser.ParseFieldTable(markdown, "## Selected Epic");
        return new EpicPreparationAuditDecision(
            Required(selectedEpicFields, "Epic ID"),
            Required(selectedEpicFields, "Epic Name"),
            RequiredAllowed(fields, "Disposition", AllowedDispositions),
            RequiredAllowed(fields, "Confidence", CommonAllowedValues.Confidence),
            Required(fields, "Primary Reason"),
            Required(fields, "Evidence Strength"),
            RequiredAllowed(fields, "Recommended Next Step", AllowedNextSteps));
    }

    private static string Required(IReadOnlyDictionary<string, string> fields, string field)
    {
        if (!fields.TryGetValue(field, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            throw new MarkdownParseException($"Required audit field missing: {field}");
        }

        return value.Trim();
    }

    private static string RequiredAllowed(IReadOnlyDictionary<string, string> fields, string field, IReadOnlyList<string> allowed)
    {
        string trimmed = Required(fields, field);
        string? match = allowed.FirstOrDefault(allowedValue => string.Equals(allowedValue, trimmed, StringComparison.Ordinal));
        return match ?? throw new MarkdownParseException($"Audit field `{field}` has unsupported value `{trimmed}`.");
    }
}
