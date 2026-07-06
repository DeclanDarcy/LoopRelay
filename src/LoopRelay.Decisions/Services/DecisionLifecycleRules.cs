using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Services;

public static class DecisionLifecycleRules
{
    private static readonly Dictionary<DecisionState, HashSet<DecisionState>> DecisionTransitions = new()
    {
        [DecisionState.Open] = [DecisionState.UnderReview, DecisionState.Resolved, DecisionState.Archived],
        [DecisionState.UnderReview] = [DecisionState.Resolved, DecisionState.Archived],
        [DecisionState.Resolved] = [DecisionState.Superseded],
        [DecisionState.Superseded] = [DecisionState.Archived]
    };

    private static readonly Dictionary<DecisionCandidateState, HashSet<DecisionCandidateState>> CandidateTransitions = new()
    {
        [DecisionCandidateState.Discovered] =
        [
            DecisionCandidateState.Promoted,
            DecisionCandidateState.Dismissed,
            DecisionCandidateState.Expired,
            DecisionCandidateState.Duplicate
        ],
        [DecisionCandidateState.Promoted] = [DecisionCandidateState.Expired]
    };

    private static readonly Dictionary<DecisionProposalState, HashSet<DecisionProposalState>> ProposalTransitions = new()
    {
        [DecisionProposalState.Draft] = [DecisionProposalState.Generated],
        [DecisionProposalState.Generated] =
        [
            DecisionProposalState.Viewed,
            DecisionProposalState.ReadyForResolution,
            DecisionProposalState.Expired,
            DecisionProposalState.Discarded
        ],
        [DecisionProposalState.Viewed] =
        [
            DecisionProposalState.NeedsRefinement,
            DecisionProposalState.ReadyForResolution,
            DecisionProposalState.Expired,
            DecisionProposalState.Discarded
        ],
        [DecisionProposalState.NeedsRefinement] =
        [
            DecisionProposalState.Refined,
            DecisionProposalState.Expired,
            DecisionProposalState.Discarded
        ],
        [DecisionProposalState.Refined] =
        [
            DecisionProposalState.ReadyForResolution,
            DecisionProposalState.Expired,
            DecisionProposalState.Discarded
        ],
        [DecisionProposalState.ReadyForResolution] =
        [
            DecisionProposalState.Resolved,
            DecisionProposalState.Discarded
        ]
    };

    public static DecisionTransitionResult ValidateDecisionTransition(DecisionState from, DecisionState to, DecisionOutcome? outcome = null)
    {
        if (from == to && from == DecisionState.UnderReview && outcome == DecisionOutcome.Deferred)
        {
            return DecisionTransitionResult.Valid;
        }

        if (!DecisionTransitions.TryGetValue(from, out HashSet<DecisionState>? allowed) || !allowed.Contains(to))
        {
            return DecisionTransitionResult.Invalid($"Decision transition from {from} to {to} is not allowed.");
        }

        if (outcome == DecisionOutcome.Accepted && to != DecisionState.Resolved)
        {
            return DecisionTransitionResult.Invalid("Accepted decisions must transition to Resolved.");
        }

        if (outcome == DecisionOutcome.Rejected && to != DecisionState.Archived)
        {
            return DecisionTransitionResult.Invalid("Rejected decisions must transition to Archived.");
        }

        if (outcome == DecisionOutcome.Deferred && to != DecisionState.UnderReview)
        {
            return DecisionTransitionResult.Invalid("Deferred decisions must transition to UnderReview.");
        }

        return DecisionTransitionResult.Valid;
    }

    public static DecisionTransitionResult ValidateCandidateTransition(DecisionCandidateState from, DecisionCandidateState to)
    {
        return CandidateTransitions.TryGetValue(from, out HashSet<DecisionCandidateState>? allowed) && allowed.Contains(to)
            ? DecisionTransitionResult.Valid
            : DecisionTransitionResult.Invalid($"Candidate transition from {from} to {to} is not allowed.");
    }

    public static DecisionTransitionResult ValidateProposalTransition(DecisionProposalState from, DecisionProposalState to)
    {
        return ProposalTransitions.TryGetValue(from, out HashSet<DecisionProposalState>? allowed) && allowed.Contains(to)
            ? DecisionTransitionResult.Valid
            : DecisionTransitionResult.Invalid($"Proposal transition from {from} to {to} is not allowed.");
    }

    public static DecisionTransitionResult ValidateRelationships(DecisionId decisionId, IReadOnlyList<DecisionRelationship> relationships)
    {
        var seen = new HashSet<(DecisionId TargetDecisionId, DecisionRelationshipType Type)>();

        foreach (DecisionRelationship relationship in relationships)
        {
            if (relationship.SourceDecisionId != decisionId)
            {
                return DecisionTransitionResult.Invalid("Relationship source must match the owning decision.");
            }

            if (relationship.TargetDecisionId == decisionId)
            {
                return DecisionTransitionResult.Invalid("Decision relationships cannot reference the owning decision.");
            }

            if (!seen.Add((relationship.TargetDecisionId, relationship.Type)))
            {
                return DecisionTransitionResult.Invalid("Duplicate decision relationships are not allowed.");
            }
        }

        return DecisionTransitionResult.Valid;
    }
}
