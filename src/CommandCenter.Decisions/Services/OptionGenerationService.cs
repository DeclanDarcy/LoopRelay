using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class OptionGenerationService : IOptionGenerationService
{
    public IReadOnlyList<DecisionOption> GenerateOptions(
        DecisionCandidate candidate,
        IReadOnlyList<DecisionEvidence> evidence)
    {
        DecisionEvidence[] optionEvidence = EvidenceForOption(evidence, candidate);
        IReadOnlyList<OptionTemplate> templates = TemplatesFor(candidate);
        var options = new List<DecisionOption>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int optionNumber = 1;

        foreach (OptionTemplate template in templates)
        {
            string key = $"{template.Type}|{Normalize(template.Title)}";
            if (!seen.Add(key))
            {
                continue;
            }

            options.Add(new DecisionOption(
                $"option-{optionNumber++}",
                template.Title,
                string.Format(template.Description, candidate.Title, candidate.Summary),
                optionEvidence)
            {
                Type = template.Type,
                Assumptions = template.Assumptions,
                Dependencies = template.Dependencies,
                Diagnostics = template.Diagnostics
            });
        }

        return options.Count >= 2
            ? options
            : AddFallbackOption(options, candidate, optionEvidence);
    }

    private static IReadOnlyList<OptionTemplate> TemplatesFor(DecisionCandidate candidate)
    {
        string[] signalKinds = candidate.Signals
            .Select(signal => signal.Kind)
            .ToArray();

        if (signalKinds.Any(IsConflict))
        {
            return [
                new(
                    DecisionOptionType.Adopt,
                    "Resolve toward the stronger source",
                    "Select the evidence source that best supports '{0}' and align execution to that direction.",
                    ["One source has stronger repository evidence."],
                    ["Conflicting source material can be updated after resolution."],
                    []),
                new(
                    DecisionOptionType.Refactor,
                    "Merge the competing directions",
                    "Synthesize the competing directions behind '{1}' into a narrower decision that preserves compatible constraints.",
                    ["The conflict has overlap that can be reconciled."],
                    ["Merged scope remains actionable."],
                    []),
                new(
                    DecisionOptionType.Investigate,
                    "Investigate before resolving the conflict",
                    "Keep the conflict unresolved until the missing evidence needed for '{0}' is collected.",
                    ["Current evidence may be insufficient for authority."],
                    ["Investigation has a clear evidence target."],
                    [])
            ];
        }

        if (signalKinds.Any(IsArchitecturalFork) || candidate.Classification == DecisionClassification.Architectural)
        {
            return [
                new(
                    DecisionOptionType.Preserve,
                    "Preserve the current architecture",
                    "Keep existing architecture decisions in place while addressing '{0}' with the smallest compatible change.",
                    ["Current architecture remains viable for the candidate scope."],
                    ["Existing architectural boundaries stay authoritative."],
                    []),
                new(
                    DecisionOptionType.Refactor,
                    "Incrementally evolve the architecture",
                    "Refactor the affected area to resolve '{0}' while preserving stable external behavior and repository conventions.",
                    ["The candidate can be resolved through additive or localized architectural change."],
                    ["Refactoring scope can be bounded to candidate evidence."],
                    []),
                new(
                    DecisionOptionType.Replace,
                    "Replace the contested architecture",
                    "Adopt a replacement architecture for the area described by '{1}' and migrate dependent behavior deliberately.",
                    ["The existing architecture is the main constraint on progress."],
                    ["Migration risk can be planned and tested."],
                    [])
            ];
        }

        if (signalKinds.Any(IsOperationalBlocker) || candidate.Classification == DecisionClassification.Operational)
        {
            return [
                new(
                    DecisionOptionType.Adopt,
                    "Fix the blocker directly",
                    "Remove the blocker behind '{0}' so execution can continue on the intended path.",
                    ["The blocker has an identifiable fix path."],
                    ["The fix can be validated before downstream execution resumes."],
                    []),
                new(
                    DecisionOptionType.Refactor,
                    "Work around the blocker",
                    "Choose a bounded workaround for '{0}' that keeps progress moving while isolating follow-up cleanup.",
                    ["A workaround can be kept reversible."],
                    ["Follow-up cleanup remains tracked."],
                    []),
                new(
                    DecisionOptionType.Delay,
                    "Defer the blocked work",
                    "Pause the blocked path from '{1}' and resequence execution toward unblocked work.",
                    ["Unblocked work exists and does not depend on this decision."],
                    ["The deferred blocker remains visible."],
                    [])
            ];
        }

        if (candidate.Classification == DecisionClassification.Strategic)
        {
            return [
                new(
                    DecisionOptionType.Expand,
                    "Accelerate the direction",
                    "Increase commitment to '{0}' and align near-term work around the generated direction.",
                    ["The strategic direction is already supported by repository evidence."],
                    ["Near-term work can absorb the increased focus."],
                    []),
                new(
                    DecisionOptionType.Preserve,
                    "Maintain the current direction",
                    "Keep the current strategic path and resolve '{0}' with minimal scope change.",
                    ["Current strategy is adequate but needs explicit authority."],
                    ["Existing milestone sequencing remains valid."],
                    []),
                new(
                    DecisionOptionType.Constrain,
                    "Reduce the strategic scope",
                    "Narrow '{1}' to the smallest strategic commitment that can be executed confidently.",
                    ["The full strategic scope may be larger than current evidence supports."],
                    ["Reduced scope still delivers useful progress."],
                    [])
            ];
        }

        return [
            new(
                DecisionOptionType.Adopt,
                "Implement now",
                "Resolve '{0}' by implementing the candidate direction in the current milestone.",
                ["The candidate is actionable now."],
                ["Implementation can be verified in this slice."],
                []),
            new(
                DecisionOptionType.Delay,
                "Implement later",
                "Defer '{0}' until prerequisite evidence or sequencing is clearer.",
                ["The candidate can wait without blocking critical work."],
                ["Deferred work remains tracked."],
                []),
            new(
                DecisionOptionType.Refactor,
                "Implement differently",
                "Resolve '{1}' through a narrower or differently scoped implementation path.",
                ["A smaller implementation path can satisfy the candidate."],
                ["Scope change remains visible to reviewers."],
                [])
        ];
    }

    private static IReadOnlyList<DecisionOption> AddFallbackOption(
        IReadOnlyList<DecisionOption> options,
        DecisionCandidate candidate,
        IReadOnlyList<DecisionEvidence> evidence)
    {
        return options.Concat([
            new DecisionOption(
                $"option-{options.Count + 1}",
                "Investigate before resolution",
                $"Collect more evidence before resolving {candidate.Title}.",
                evidence)
            {
                Type = DecisionOptionType.Investigate,
                Assumptions = ["Current evidence may not support multiple actionable paths."],
                Dependencies = ["Reviewer identifies the missing evidence needed for resolution."],
                Diagnostics = ["Fallback option added because generation produced fewer than two unique options."]
            }
        ]).ToArray();
    }

    private static DecisionEvidence[] EvidenceForOption(
        IReadOnlyList<DecisionEvidence> evidence,
        DecisionCandidate candidate)
    {
        return evidence
            .Where(item => item.Sources.Count == 0 ||
                item.Sources.Any(source => source.CandidateId == candidate.Id || source.RelativePath is not null))
            .OrderBy(item => item.Summary, StringComparer.Ordinal)
            .Take(4)
            .ToArray();
    }

    private static bool IsArchitecturalFork(string kind)
    {
        return kind.Contains("ArchitecturalFork", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOperationalBlocker(string kind)
    {
        return kind.Contains("Blocker", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConflict(string kind)
    {
        return kind.Contains("Conflict", StringComparison.OrdinalIgnoreCase) ||
            kind.Contains("Contradiction", StringComparison.OrdinalIgnoreCase) ||
            kind.Contains("Constraint", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record OptionTemplate(
        DecisionOptionType Type,
        string Title,
        string Description,
        IReadOnlyList<string> Assumptions,
        IReadOnlyList<string> Dependencies,
        IReadOnlyList<string> Diagnostics);
}
