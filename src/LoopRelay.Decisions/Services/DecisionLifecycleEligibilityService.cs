using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Services;

public sealed class DecisionLifecycleEligibilityService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository) : IDecisionLifecycleEligibilityService
{
    public async Task<DecisionLifecycleEligibilityProjection> GetEligibilityAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        IReadOnlyList<DecisionCandidate> candidates = await decisionRepository.ListCandidatesAsync(repository);
        IReadOnlyList<DecisionProposal> proposals = await decisionRepository.ListProposalsAsync(repository);
        IReadOnlyList<Decision> decisions = await decisionRepository.ListDecisionsAsync(repository);

        return new DecisionLifecycleEligibilityProjection(
            repository.Id,
            candidates.Select(candidate => ProjectCandidate(candidate, proposals)).ToArray(),
            proposals.Select(ProjectProposal).ToArray(),
            decisions.Select(decision => ProjectDecision(decision, decisions)).ToArray(),
            []);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static DecisionLifecycleEntityEligibility ProjectCandidate(
        DecisionCandidate candidate,
        IReadOnlyList<DecisionProposal> proposals)
    {
        bool hasActiveProposal = proposals.Any(proposal =>
            proposal.CandidateId == candidate.Id &&
            proposal.State is not DecisionProposalState.Expired and not DecisionProposalState.Discarded);
        DecisionLifecycleActionEligibility[] actions =
        [
            CandidateAction(candidate.State, DecisionCandidateState.Promoted, "promote_decision_candidate", "Promote", ["reason"]),
            CandidateAction(candidate.State, DecisionCandidateState.Dismissed, "dismiss_decision_candidate", "Dismiss", ["reason"]),
            CandidateAction(candidate.State, DecisionCandidateState.Expired, "expire_decision_candidate", "Expire", ["reason"]),
            CandidateAction(candidate.State, DecisionCandidateState.Duplicate, "mark_decision_candidate_duplicate", "Mark duplicate", ["duplicateOfCandidateId", "reason"]),
            GenerateProposalAction(candidate.State, hasActiveProposal)
        ];

        return Entity(
            "Candidate",
            candidate.Id,
            candidate.State.ToString(),
            actions,
            Enum.GetValues<DecisionCandidateState>().Select(state => state.ToString()));
    }

    private static DecisionLifecycleEntityEligibility ProjectProposal(DecisionProposal proposal)
    {
        DecisionLifecycleActionEligibility[] actions =
        [
            ProposalAction(proposal.State, DecisionProposalState.Viewed, "mark_decision_proposal_viewed", "Mark viewed", ["reason"]),
            ProposalAction(proposal.State, DecisionProposalState.NeedsRefinement, "mark_decision_proposal_needs_refinement", "Needs refinement", ["reason"]),
            ProposalAction(proposal.State, DecisionProposalState.ReadyForResolution, "mark_decision_proposal_ready_for_resolution", "Ready for resolution", ["reason"]),
            ProposalAction(proposal.State, DecisionProposalState.Resolved, "resolve_decision_proposal", "Resolve", ["rationale", "resolver", "selectedOptionId", "outcome"]),
            ProposalAction(proposal.State, DecisionProposalState.Expired, "expire_decision_proposal", "Expire", ["reason"]),
            ProposalAction(proposal.State, DecisionProposalState.Discarded, "discard_decision_proposal", "Discard", ["reason"])
        ];

        return Entity(
            "Proposal",
            proposal.Id,
            proposal.State.ToString(),
            actions,
            Enum.GetValues<DecisionProposalState>().Select(state => state.ToString()));
    }

    private static DecisionLifecycleEntityEligibility ProjectDecision(
        Decision decision,
        IReadOnlyList<Decision> repositoryDecisions)
    {
        bool hasResolvedReplacement = repositoryDecisions.Any(candidate =>
            candidate.Id != decision.Id &&
            candidate.State == DecisionState.Resolved);
        DecisionLifecycleActionEligibility supersede = DecisionAction(
            decision.State,
            DecisionState.Superseded,
            "supersede_decision",
            "Supersede",
            ["replacementDecisionId", "rationale", "resolver"]);
        if (supersede.IsAllowed && !hasResolvedReplacement)
        {
            supersede = supersede with
            {
                IsAllowed = false,
                Reason = "A resolved replacement decision is required before this decision can be superseded."
            };
        }

        DecisionLifecycleActionEligibility[] actions =
        [
            supersede,
            DecisionAction(decision.State, DecisionState.Archived, "archive_decision", "Archive", ["rationale", "resolver"])
        ];

        return Entity(
            "Decision",
            decision.Id.Value,
            decision.State.ToString(),
            actions,
            Enum.GetValues<DecisionState>().Select(state => state.ToString()));
    }

    private static DecisionLifecycleEntityEligibility Entity(
        string entityKind,
        string entityId,
        string currentState,
        IReadOnlyList<DecisionLifecycleActionEligibility> actions,
        IEnumerable<string> allStates)
    {
        IReadOnlyList<DecisionLifecycleActionEligibility> allowed = actions
            .Where(action => action.IsAllowed)
            .ToArray();
        IReadOnlyList<DecisionLifecycleActionEligibility> blocked = actions
            .Where(action => !action.IsAllowed)
            .ToArray();
        var allowedStates = allowed
            .Select(action => action.TargetState)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var blockedStates = allStates
            .Where(state => !string.Equals(state, currentState, StringComparison.Ordinal))
            .Except(allowedStates, StringComparer.Ordinal)
            .Select(state => new DecisionLifecycleBlockedState(
                state,
                $"Transition from {currentState} to {state} is not currently allowed.",
                $"{entityKind}LifecycleRules"))
            .ToArray();

        return new DecisionLifecycleEntityEligibility(
            entityKind,
            entityId,
            currentState,
            allowed,
            blocked,
            allowedStates,
            blockedStates,
            []);
    }

    private static DecisionLifecycleActionEligibility CandidateAction(
        DecisionCandidateState from,
        DecisionCandidateState to,
        string commandName,
        string displayName,
        IReadOnlyList<string> requiredInputs)
    {
        DecisionTransitionResult result = DecisionLifecycleRules.ValidateCandidateTransition(from, to);
        return Action(commandName, displayName, to.ToString(), result, requiredInputs, "DecisionLifecycleRules.ValidateCandidateTransition");
    }

    private static DecisionLifecycleActionEligibility GenerateProposalAction(
        DecisionCandidateState candidateState,
        bool hasActiveProposal)
    {
        DecisionTransitionResult result = candidateState == DecisionCandidateState.Promoted
            ? DecisionTransitionResult.Valid
            : DecisionTransitionResult.Invalid("Only promoted candidates can generate decision proposals.");
        if (result.IsValid && hasActiveProposal)
        {
            result = DecisionTransitionResult.Invalid("An active proposal already exists for this candidate.");
        }

        return Action(
            "generate_decision_proposal",
            "Generate proposal",
            DecisionProposalState.Generated.ToString(),
            result,
            [],
            "DecisionGenerationService.GenerateProposalAsync");
    }

    private static DecisionLifecycleActionEligibility ProposalAction(
        DecisionProposalState from,
        DecisionProposalState to,
        string commandName,
        string displayName,
        IReadOnlyList<string> requiredInputs)
    {
        DecisionTransitionResult result = DecisionLifecycleRules.ValidateProposalTransition(from, to);
        return Action(commandName, displayName, to.ToString(), result, requiredInputs, "DecisionLifecycleRules.ValidateProposalTransition");
    }

    private static DecisionLifecycleActionEligibility DecisionAction(
        DecisionState from,
        DecisionState to,
        string commandName,
        string displayName,
        IReadOnlyList<string> requiredInputs)
    {
        DecisionTransitionResult result = DecisionLifecycleRules.ValidateDecisionTransition(from, to);
        return Action(commandName, displayName, to.ToString(), result, requiredInputs, "DecisionLifecycleRules.ValidateDecisionTransition");
    }

    private static DecisionLifecycleActionEligibility Action(
        string commandName,
        string displayName,
        string targetState,
        DecisionTransitionResult result,
        IReadOnlyList<string> requiredInputs,
        string governingRule)
    {
        return new DecisionLifecycleActionEligibility(
            commandName,
            displayName,
            targetState,
            result.IsValid,
            requiredInputs,
            result.IsValid ? null : result.Error,
            governingRule);
    }
}
