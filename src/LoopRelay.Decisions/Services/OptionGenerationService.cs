using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Services;

public sealed class OptionGenerationService(
    IOptionValidationService? optionValidationService = null) : IOptionGenerationService
{
    private readonly IOptionValidationService optionValidationService =
        optionValidationService ?? new OptionValidationService();

    public DecisionOptionGenerationResult GenerateOptions(
        DecisionCandidate candidate,
        IReadOnlyList<DecisionEvidence> evidence)
    {
        DecisionEvidence[] optionEvidence = EvidenceForOption(evidence, candidate);
        IReadOnlyList<OptionTemplate> templates = TemplatesFor(candidate);
        var options = new List<DecisionOption>();
        var rejected = new List<DecisionOptionValidationResult>();
        var rejectedOptions = new List<DecisionOption>();
        var deduplicatedOptions = new List<DecisionOption>();
        var acceptedValidation = new List<DecisionOptionValidationResult>();
        var diagnostics = new List<string>();
        int optionNumber = 1;

        foreach (OptionTemplate template in templates)
        {
            DecisionOption option = new(
                $"option-{optionNumber++}",
                template.Title,
                string.Format(template.Description, candidate.Title, candidate.Summary),
                optionEvidence)
            {
                Type = template.Type,
                Assumptions = template.Assumptions,
                Dependencies = template.Dependencies,
                Diagnostics = template.Diagnostics
            };
            DecisionOptionValidationResult validation = optionValidationService.ValidateOption(option, candidate, options);
            if (!validation.IsValid)
            {
                rejected.Add(validation);
                rejectedOptions.Add(option);
                if (validation.Issues.Any(issue => issue.Type == DecisionOptionValidationIssueType.Duplicate))
                {
                    deduplicatedOptions.Add(option);
                }

                diagnostics.Add($"Rejected {option.Id}: {string.Join("; ", validation.Issues.Select(issue => issue.Message))}");
                continue;
            }

            acceptedValidation.Add(validation);
            options.Add(option);
        }

        int fallbackCount = 0;
        while (options.Count < 2)
        {
            DecisionOption fallback = CreateFallbackOption(options.Count + 1, candidate, optionEvidence);
            DecisionOptionValidationResult fallbackValidation = optionValidationService.ValidateOption(fallback, candidate, options);
            if (!fallbackValidation.IsValid)
            {
                rejected.Add(fallbackValidation);
                rejectedOptions.Add(fallback);
                if (fallbackValidation.Issues.Any(issue => issue.Type == DecisionOptionValidationIssueType.Duplicate))
                {
                    deduplicatedOptions.Add(fallback);
                }

                break;
            }

            options.Add(fallback);
            acceptedValidation.Add(fallbackValidation);
            fallbackCount++;
        }

        if (fallbackCount > 0)
        {
            diagnostics.Add($"Added {fallbackCount} fallback option(s) because generation produced fewer than two valid unique options.");
        }

        DecisionOptionRelationship[] relationships = BuildRelationships(candidate, options, optionEvidence);
        var generationDiagnostics = new DecisionGenerationDiagnostics(
            templates.Count + fallbackCount,
            options.Count,
            rejected.Count,
            rejected.Count(result => result.Issues.Any(issue => issue.Type == DecisionOptionValidationIssueType.Duplicate)),
            fallbackCount,
            acceptedValidation.Concat(rejected).OrderBy(result => result.OptionId, StringComparer.Ordinal).ToArray(),
            diagnostics.Order(StringComparer.Ordinal).ToArray())
        {
            RejectedOptions = rejectedOptions.OrderBy(option => option.Id, StringComparer.Ordinal).ToArray(),
            DeduplicatedOptions = deduplicatedOptions.OrderBy(option => option.Id, StringComparer.Ordinal).ToArray()
        };

        return new DecisionOptionGenerationResult(options, relationships, generationDiagnostics);
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

    private static DecisionOption CreateFallbackOption(
        int optionNumber,
        DecisionCandidate candidate,
        IReadOnlyList<DecisionEvidence> evidence)
    {
        return new DecisionOption(
            $"option-{optionNumber}",
            "Investigate before resolution",
            $"Collect more evidence before resolving {candidate.Title}.",
            evidence)
        {
            Type = DecisionOptionType.Investigate,
            Assumptions = ["Current evidence may not support multiple actionable paths."],
            Dependencies = ["Reviewer identifies the missing evidence needed for resolution."],
            Diagnostics = ["Fallback option added because generation produced fewer than two valid unique options."]
        };
    }

    private static DecisionOptionRelationship[] BuildRelationships(
        DecisionCandidate candidate,
        IReadOnlyList<DecisionOption> options,
        IReadOnlyList<DecisionEvidence> evidence)
    {
        var relationships = new List<DecisionOptionRelationship>();
        for (int index = 0; index < options.Count; index++)
        {
            for (int otherIndex = index + 1; otherIndex < options.Count; otherIndex++)
            {
                DecisionOption left = options[index];
                DecisionOption right = options[otherIndex];
                DecisionOptionRelationshipType type = RelationshipType(candidate, left, right);
                relationships.Add(new DecisionOptionRelationship(
                    left.Id,
                    right.Id,
                    type,
                    RelationshipRationale(type, left, right),
                    evidence));
            }
        }

        return relationships.ToArray();
    }

    private static DecisionOptionRelationshipType RelationshipType(
        DecisionCandidate candidate,
        DecisionOption left,
        DecisionOption right)
    {
        bool conflictCandidate = candidate.Signals.Any(signal => IsConflict(signal.Kind));
        if (conflictCandidate ||
            left.Type is DecisionOptionType.Replace or DecisionOptionType.Remove or DecisionOptionType.Constrain ||
            right.Type is DecisionOptionType.Replace or DecisionOptionType.Remove or DecisionOptionType.Constrain)
        {
            return DecisionOptionRelationshipType.ConflictsWith;
        }

        if (left.Type == DecisionOptionType.Investigate ||
            right.Type == DecisionOptionType.Investigate ||
            left.Type == DecisionOptionType.Delay ||
            right.Type == DecisionOptionType.Delay)
        {
            return DecisionOptionRelationshipType.DependsOn;
        }

        return DecisionOptionRelationshipType.AlternativeTo;
    }

    private static string RelationshipRationale(
        DecisionOptionRelationshipType type,
        DecisionOption left,
        DecisionOption right)
    {
        return type switch
        {
            DecisionOptionRelationshipType.ConflictsWith =>
                $"{left.Title} and {right.Title} choose incompatible resolution paths.",
            DecisionOptionRelationshipType.DependsOn =>
                $"{left.Title} and {right.Title} differ based on prerequisite evidence or sequencing.",
            _ => $"{left.Title} and {right.Title} are alternative ways to resolve the same candidate."
        };
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

    private sealed record OptionTemplate(
        DecisionOptionType Type,
        string Title,
        string Description,
        IReadOnlyList<string> Assumptions,
        IReadOnlyList<string> Dependencies,
        IReadOnlyList<string> Diagnostics);
}
