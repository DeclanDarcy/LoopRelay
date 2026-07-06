using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Primitives;
using LoopRelay.Decisions.Services;

namespace LoopRelay.Decisions.Tests;

public sealed class DecisionLifecycleRulesTests
{
    [Theory]
    [InlineData(DecisionState.Open, DecisionState.UnderReview, null)]
    [InlineData(DecisionState.Open, DecisionState.Resolved, DecisionOutcome.Accepted)]
    [InlineData(DecisionState.Open, DecisionState.Archived, DecisionOutcome.Rejected)]
    [InlineData(DecisionState.Open, DecisionState.UnderReview, DecisionOutcome.Deferred)]
    [InlineData(DecisionState.UnderReview, DecisionState.Resolved, DecisionOutcome.Accepted)]
    [InlineData(DecisionState.UnderReview, DecisionState.Archived, DecisionOutcome.Rejected)]
    [InlineData(DecisionState.UnderReview, DecisionState.UnderReview, DecisionOutcome.Deferred)]
    [InlineData(DecisionState.Resolved, DecisionState.Superseded, null)]
    [InlineData(DecisionState.Superseded, DecisionState.Archived, null)]
    public void DecisionTransitionMatrixAllowsPlannedTransitions(
        DecisionState from,
        DecisionState to,
        DecisionOutcome? outcome)
    {
        DecisionTransitionResult result = DecisionLifecycleRules.ValidateDecisionTransition(from, to, outcome);

        Assert.True(result.IsValid, result.Error);
    }

    [Theory]
    [InlineData(DecisionState.Resolved, DecisionState.Open)]
    [InlineData(DecisionState.Archived, DecisionState.Open)]
    [InlineData(DecisionState.Open, DecisionState.Superseded)]
    public void DecisionTransitionMatrixRejectsInvalidTransitions(DecisionState from, DecisionState to)
    {
        DecisionTransitionResult result = DecisionLifecycleRules.ValidateDecisionTransition(from, to);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void DecisionOutcomeMustMatchTargetState()
    {
        DecisionTransitionResult result = DecisionLifecycleRules.ValidateDecisionTransition(
            DecisionState.Open,
            DecisionState.Resolved,
            DecisionOutcome.Rejected);

        Assert.False(result.IsValid);
        Assert.Contains("Rejected", result.Error);
    }

    [Theory]
    [InlineData(DecisionCandidateState.Discovered, DecisionCandidateState.Promoted)]
    [InlineData(DecisionCandidateState.Discovered, DecisionCandidateState.Dismissed)]
    [InlineData(DecisionCandidateState.Discovered, DecisionCandidateState.Expired)]
    [InlineData(DecisionCandidateState.Discovered, DecisionCandidateState.Duplicate)]
    [InlineData(DecisionCandidateState.Promoted, DecisionCandidateState.Expired)]
    public void CandidateTransitionMatrixAllowsPlannedTransitions(DecisionCandidateState from, DecisionCandidateState to)
    {
        DecisionTransitionResult result = DecisionLifecycleRules.ValidateCandidateTransition(from, to);

        Assert.True(result.IsValid, result.Error);
    }

    [Fact]
    public void CandidateTransitionMatrixRejectsTerminalMutations()
    {
        DecisionTransitionResult result = DecisionLifecycleRules.ValidateCandidateTransition(
            DecisionCandidateState.Dismissed,
            DecisionCandidateState.Promoted);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(DecisionProposalState.Draft, DecisionProposalState.Generated)]
    [InlineData(DecisionProposalState.Generated, DecisionProposalState.Viewed)]
    [InlineData(DecisionProposalState.Generated, DecisionProposalState.ReadyForResolution)]
    [InlineData(DecisionProposalState.Generated, DecisionProposalState.Expired)]
    [InlineData(DecisionProposalState.Generated, DecisionProposalState.Discarded)]
    [InlineData(DecisionProposalState.Viewed, DecisionProposalState.NeedsRefinement)]
    [InlineData(DecisionProposalState.Viewed, DecisionProposalState.ReadyForResolution)]
    [InlineData(DecisionProposalState.Viewed, DecisionProposalState.Expired)]
    [InlineData(DecisionProposalState.Viewed, DecisionProposalState.Discarded)]
    [InlineData(DecisionProposalState.NeedsRefinement, DecisionProposalState.Refined)]
    [InlineData(DecisionProposalState.NeedsRefinement, DecisionProposalState.Expired)]
    [InlineData(DecisionProposalState.NeedsRefinement, DecisionProposalState.Discarded)]
    [InlineData(DecisionProposalState.Refined, DecisionProposalState.ReadyForResolution)]
    [InlineData(DecisionProposalState.Refined, DecisionProposalState.Expired)]
    [InlineData(DecisionProposalState.Refined, DecisionProposalState.Discarded)]
    [InlineData(DecisionProposalState.ReadyForResolution, DecisionProposalState.Resolved)]
    [InlineData(DecisionProposalState.ReadyForResolution, DecisionProposalState.Discarded)]
    public void ProposalTransitionMatrixAllowsPlannedTransitions(DecisionProposalState from, DecisionProposalState to)
    {
        DecisionTransitionResult result = DecisionLifecycleRules.ValidateProposalTransition(from, to);

        Assert.True(result.IsValid, result.Error);
    }

    [Fact]
    public void ProposalTransitionMatrixRejectsSkippingResolutionReadiness()
    {
        DecisionTransitionResult result = DecisionLifecycleRules.ValidateProposalTransition(
            DecisionProposalState.Generated,
            DecisionProposalState.Resolved);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(DecisionProposalState.Resolved)]
    [InlineData(DecisionProposalState.Expired)]
    [InlineData(DecisionProposalState.Discarded)]
    public void ProposalTransitionMatrixRejectsDiscardFromTerminalStates(DecisionProposalState from)
    {
        DecisionTransitionResult result = DecisionLifecycleRules.ValidateProposalTransition(
            from,
            DecisionProposalState.Discarded);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RelationshipValidationRejectsSelfReferences()
    {
        DecisionId decisionId = DecisionId.Parse("DEC-0001");
        var relationships = new[]
        {
            new DecisionRelationship(decisionId, decisionId, DecisionRelationshipType.RelatedTo)
        };

        DecisionTransitionResult result = DecisionLifecycleRules.ValidateRelationships(decisionId, relationships);

        Assert.False(result.IsValid);
        Assert.Contains("owning decision", result.Error);
    }

    [Fact]
    public void RelationshipValidationRejectsDuplicateTargetAndTypePairs()
    {
        DecisionId decisionId = DecisionId.Parse("DEC-0001");
        DecisionId targetId = DecisionId.Parse("DEC-0002");
        var relationships = new[]
        {
            new DecisionRelationship(decisionId, targetId, DecisionRelationshipType.DependsOn),
            new DecisionRelationship(decisionId, targetId, DecisionRelationshipType.DependsOn)
        };

        DecisionTransitionResult result = DecisionLifecycleRules.ValidateRelationships(decisionId, relationships);

        Assert.False(result.IsValid);
        Assert.Contains("Duplicate", result.Error);
    }
}
