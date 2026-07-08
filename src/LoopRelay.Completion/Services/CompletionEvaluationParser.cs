using LoopRelay.Completion.Models;

namespace LoopRelay.Completion.Services;

public sealed class CompletionEvaluationParser
{
    public static IReadOnlyList<string> AllowedCompletionStatuses =>
        CompletionCertificationVocabulary.CompletionStatuses;

    public static IReadOnlyList<string> AllowedDriftClassifications =>
        CompletionCertificationVocabulary.DriftClassifications;

    public CompletionEvaluationDecision Parse(string markdown)
    {
        IReadOnlyDictionary<string, string> fields =
            CompletionMarkdownTableParser.ParseFieldTable(markdown, "## Evaluation Summary");
        return new CompletionEvaluationDecision(
            RequiredAllowed(fields, "Overall Completion Status", AllowedCompletionStatuses),
            RequiredAllowed(fields, "Overall Drift Classification", AllowedDriftClassifications),
            RequiredAllowed(fields, "Closure Recommendation", CompletionCertificationVocabulary.ClosureRecommendations));
    }

    private static string RequiredAllowed(
        IReadOnlyDictionary<string, string> fields,
        string field,
        IReadOnlyList<string> allowed)
    {
        if (!fields.TryGetValue(field, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            throw new CompletionMarkdownParseException($"Required completion field missing: {field}");
        }

        string trimmed = value.Trim();
        string? match = allowed.FirstOrDefault(allowedValue => string.Equals(allowedValue, trimmed, StringComparison.Ordinal));
        return match ?? throw new CompletionMarkdownParseException($"Completion field `{field}` has unsupported value `{trimmed}`.");
    }
}
