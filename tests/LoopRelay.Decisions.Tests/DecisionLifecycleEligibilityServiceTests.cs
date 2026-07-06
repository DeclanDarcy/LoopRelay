using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Primitives;
using LoopRelay.Decisions.Services;

namespace LoopRelay.Decisions.Tests;

[Collection("ProcessEnvironment")]
public sealed class DecisionLifecycleEligibilityServiceTests
{
    [Fact]
    public async Task EligibilityProjectsAllowedAndBlockedActionsFromLifecycleRules()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new FileSystemDecisionRepository(new FileSystemArtifactStore());
        await decisionRepository.SaveCandidateAsync(repository, CreateCandidate(repository.Id, DecisionCandidateState.Discovered));
        await decisionRepository.SaveProposalAsync(repository, CreateProposal(repository.Id, DecisionProposalState.Generated));
        await decisionRepository.SaveDecisionAsync(repository, CreateDecision(repository.Id, "DEC-0001", DecisionState.Resolved));
        var service = new DecisionLifecycleEligibilityService(
            new StubRepositoryService(repository),
            decisionRepository);

        DecisionLifecycleEligibilityProjection eligibility = await service.GetEligibilityAsync(repository.Id);

        DecisionLifecycleEntityEligibility candidate = Assert.Single(eligibility.Candidates);
        Assert.Equal("Candidate", candidate.EntityKind);
        Assert.Equal("Discovered", candidate.CurrentState);
        Assert.Contains(candidate.AllowedActions, action =>
            action.CommandName == "promote_decision_candidate" &&
            action.TargetState == "Promoted" &&
            action.RequiredInputs.Contains("reason"));
        Assert.Contains(candidate.BlockedActions, action =>
            action.CommandName == "generate_decision_proposal" &&
            action.TargetState == "Generated" &&
            action.Reason == "Only promoted candidates can generate decision proposals." &&
            action.GoverningRule == "DecisionGenerationService.GenerateProposalAsync");
        Assert.Empty(candidate.BlockedNextStates);

        DecisionLifecycleEntityEligibility proposal = Assert.Single(eligibility.Proposals);
        Assert.Contains(proposal.AllowedActions, action =>
            action.CommandName == "mark_decision_proposal_viewed" &&
            action.GoverningRule == "DecisionLifecycleRules.ValidateProposalTransition");
        Assert.Contains(proposal.BlockedActions, action =>
            action.CommandName == "resolve_decision_proposal" &&
            action.Reason!.Contains("Generated to Resolved", StringComparison.Ordinal));

        DecisionLifecycleEntityEligibility decision = Assert.Single(eligibility.Decisions);
        Assert.Contains(decision.BlockedActions, action =>
            action.CommandName == "supersede_decision" &&
            action.Reason == "A resolved replacement decision is required before this decision can be superseded.");
    }

    [Fact]
    public async Task EligibilityAllowsProposalGenerationOnlyForPromotedCandidateWithoutActiveProposal()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new FileSystemDecisionRepository(new FileSystemArtifactStore());
        await decisionRepository.SaveCandidateAsync(repository, CreateCandidate(repository.Id, DecisionCandidateState.Promoted));
        var service = new DecisionLifecycleEligibilityService(
            new StubRepositoryService(repository),
            decisionRepository);

        DecisionLifecycleEligibilityProjection eligibility = await service.GetEligibilityAsync(repository.Id);

        DecisionLifecycleEntityEligibility candidate = Assert.Single(eligibility.Candidates);
        Assert.Contains(candidate.AllowedActions, action =>
            action.CommandName == "generate_decision_proposal" &&
            action.TargetState == "Generated" &&
            action.RequiredInputs.Count == 0 &&
            action.GoverningRule == "DecisionGenerationService.GenerateProposalAsync");

        await decisionRepository.SaveProposalAsync(repository, CreateProposal(repository.Id, DecisionProposalState.Generated));

        DecisionLifecycleEligibilityProjection blockedEligibility = await service.GetEligibilityAsync(repository.Id);

        DecisionLifecycleEntityEligibility blockedCandidate = Assert.Single(blockedEligibility.Candidates);
        Assert.Contains(blockedCandidate.BlockedActions, action =>
            action.CommandName == "generate_decision_proposal" &&
            action.Reason == "An active proposal already exists for this candidate.");
    }

    private static DecisionCandidate CreateCandidate(Guid repositoryId, DecisionCandidateState state)
    {
        return new DecisionCandidate(
            "CAND-0001",
            repositoryId,
            state,
            DecisionCandidatePriority.High,
            DecisionClassification.Architectural,
            "Decide repository persistence",
            "Repository state needs an authoritative persistence decision.",
            "fingerprint",
            [],
            [],
            [],
            [],
            []);
    }

    private static DecisionProposal CreateProposal(Guid repositoryId, DecisionProposalState state)
    {
        return new DecisionProposal(
            "PROP-0001",
            repositoryId,
            "CAND-0001",
            state,
            "Decide repository persistence",
            "Repository state needs an authoritative persistence decision.",
            [new DecisionOption("option-1", "Use repository artifacts", "Persist state under .agents.", [])],
            [],
            null,
            [],
            [],
            []);
    }

    private static Decision CreateDecision(Guid repositoryId, string decisionId, DecisionState state)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new Decision(
            new DecisionId(decisionId),
            state,
            DecisionClassification.Architectural,
            "Use repository artifacts",
            "Repository state is authoritative.",
            new DecisionMetadata(repositoryId, now, now),
            null,
            [],
            [],
            []);
    }

    private static Repository CreateRepository()
    {
        string path = Path.Combine(Path.GetTempPath(), "LoopRelay.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, ".git"));
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path
        };
    }

    private sealed class StubRepositoryService(params Repository[] repositories) : IRepositoryService
    {
        public Task<IReadOnlyList<Repository>> GetAllAsync() => Task.FromResult<IReadOnlyList<Repository>>(repositories);

        public Task<Repository> RegisterAsync(string path)
        {
            throw new NotSupportedException();
        }

        public Task RemoveAsync(Guid id)
        {
            throw new NotSupportedException();
        }
    }
}
