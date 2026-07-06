using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Services;

public sealed class OptionValidationService : IOptionValidationService
{
    public DecisionOptionValidationResult ValidateOption(
        DecisionOption option,
        DecisionCandidate candidate,
        IReadOnlyList<DecisionOption> acceptedOptions)
    {
        var issues = new List<DecisionOptionValidationIssue>();
        if (string.IsNullOrWhiteSpace(option.Title))
        {
            issues.Add(new DecisionOptionValidationIssue(
                DecisionOptionValidationIssueType.MissingTitle,
                "Option title is required."));
        }

        if (string.IsNullOrWhiteSpace(option.Description))
        {
            issues.Add(new DecisionOptionValidationIssue(
                DecisionOptionValidationIssueType.MissingDescription,
                "Option description is required."));
        }

        if (!IsActionable(option))
        {
            issues.Add(new DecisionOptionValidationIssue(
                DecisionOptionValidationIssueType.NonActionable,
                "Option must describe an actionable resolution path."));
        }

        if (option.Evidence.Count == 0)
        {
            issues.Add(new DecisionOptionValidationIssue(
                DecisionOptionValidationIssueType.MissingEvidence,
                "Option requires supporting evidence."));
        }
        else if (!HasRelatedEvidence(option, candidate))
        {
            issues.Add(new DecisionOptionValidationIssue(
                DecisionOptionValidationIssueType.EvidenceUnrelated,
                "Option evidence is not related to the candidate or repository context."));
        }

        DecisionOption? duplicate = acceptedOptions.FirstOrDefault(accepted => IsDuplicate(option, accepted));
        if (duplicate is not null)
        {
            issues.Add(new DecisionOptionValidationIssue(
                DecisionOptionValidationIssueType.Duplicate,
                $"Option duplicates {duplicate.Id} by normalized title, type, or overlapping evidence."));
        }

        return new DecisionOptionValidationResult(option.Id, issues.Count == 0, issues);
    }

    private static bool IsActionable(DecisionOption option)
    {
        string text = $"{option.Title} {option.Description}";
        return !string.IsNullOrWhiteSpace(option.Title) &&
            !string.IsNullOrWhiteSpace(option.Description) &&
            !text.Contains("TBD", StringComparison.OrdinalIgnoreCase) &&
            !text.Contains("unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasRelatedEvidence(DecisionOption option, DecisionCandidate candidate)
    {
        return option.Evidence.Any(evidence =>
            evidence.Sources.Count == 0 ||
            evidence.Sources.Any(source =>
                string.Equals(source.CandidateId, candidate.Id, StringComparison.Ordinal) ||
                !string.IsNullOrWhiteSpace(source.RelativePath)));
    }

    private static bool IsDuplicate(DecisionOption option, DecisionOption accepted)
    {
        if (option.Type == accepted.Type &&
            string.Equals(Normalize(option.Title), Normalize(accepted.Title), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return option.Type == accepted.Type && EvidenceOverlap(option, accepted) >= 0.75;
    }

    private static double EvidenceOverlap(DecisionOption option, DecisionOption accepted)
    {
        HashSet<string> left = EvidenceKeys(option.Evidence);
        HashSet<string> right = EvidenceKeys(accepted.Evidence);
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        int intersection = left.Intersect(right, StringComparer.OrdinalIgnoreCase).Count();
        int union = left.Union(right, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static HashSet<string> EvidenceKeys(IReadOnlyList<DecisionEvidence> evidence)
    {
        return evidence
            .Select(item => Normalize($"{item.Summary}|{string.Join('|', item.Sources.Select(SourceKey))}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string SourceKey(DecisionSourceReference source)
    {
        return string.Join('|', [
            source.SourceKind,
            source.RelativePath ?? string.Empty,
            source.Section ?? string.Empty,
            source.ItemId ?? string.Empty,
            source.DecisionId?.Value ?? string.Empty,
            source.ProposalId ?? string.Empty,
            source.CandidateId ?? string.Empty,
            source.Excerpt ?? string.Empty
        ]);
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
