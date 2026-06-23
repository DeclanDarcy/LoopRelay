using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Persistence;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class DecisionResolutionService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository,
    IDecisionArtifactProjectionService projectionService) : IDecisionResolutionService
{
    public async Task<Decision> ResolveProposalAsync(Guid repositoryId, string proposalId, ResolveDecisionCommand command)
    {
        if (command is null)
        {
            throw new ArgumentException("Resolution command is required.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.Rationale))
        {
            throw new ArgumentException("Resolution rationale is required.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.Resolver))
        {
            throw new ArgumentException("Resolver metadata is required.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.SelectedOptionId))
        {
            throw new ArgumentException("Selected option id is required.", nameof(command));
        }

        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionProposal proposal = await GetProposalAsync(repository, proposalId);
        DecisionTransitionResult transition = DecisionLifecycleRules.ValidateProposalTransition(
            proposal.State,
            DecisionProposalState.Resolved);
        if (!transition.IsValid)
        {
            throw new InvalidOperationException(transition.Error);
        }

        DecisionOption? selectedOption = proposal.Options
            .FirstOrDefault(option => string.Equals(option.Id, command.SelectedOptionId.Trim(), StringComparison.Ordinal));
        if (selectedOption is null)
        {
            throw new ArgumentException($"Selected option was not found: {command.SelectedOptionId}", nameof(command));
        }

        DecisionId decisionId = await decisionRepository.AllocateDecisionIdAsync(repository);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string rationale = command.Rationale.Trim();
        string resolver = command.Resolver.Trim();
        string selectedOptionId = selectedOption.Id;
        string proposalFingerprint = Fingerprint(proposal);
        IReadOnlyList<DecisionProposalRevision> revisions = await decisionRepository.ListProposalRevisionsAsync(repository, proposal.Id);
        DecisionState targetDecisionState = TargetStateFor(command.Outcome);
        DecisionTransitionResult decisionTransition = DecisionLifecycleRules.ValidateDecisionTransition(
            DecisionState.Open,
            targetDecisionState,
            command.Outcome);
        if (!decisionTransition.IsValid)
        {
            throw new InvalidOperationException(decisionTransition.Error);
        }

        bool recommendationDiverged = proposal.Recommendation is not null &&
            !string.Equals(proposal.Recommendation.OptionId, selectedOptionId, StringComparison.Ordinal);
        var proposalSource = new DecisionSourceReference(
            "DecisionProposal",
            ProposalPath(proposal.Id),
            DecisionId: decisionId,
            ProposalId: proposal.Id,
            CandidateId: proposal.CandidateId);
        var selectedOptionSource = new DecisionSourceReference(
            "DecisionOption",
            ProposalPath(proposal.Id),
            Section: "Options",
            ItemId: selectedOptionId,
            DecisionId: decisionId,
            ProposalId: proposal.Id,
            CandidateId: proposal.CandidateId,
            Excerpt: selectedOption.Title);

        var resolution = new DecisionResolution(
            command.Outcome,
            selectedOptionId,
            rationale,
            resolver,
            recommendationDiverged,
            now,
            [proposalSource, selectedOptionSource],
            new DecisionResolvedProposalSnapshot(
                proposal.Id,
                proposal.CandidateId,
                proposalFingerprint,
                proposal.State,
                proposal.Title,
                proposal.Context,
                proposal.Options,
                proposal.Tradeoffs,
                proposal.Recommendation,
                proposal.Assumptions,
                proposal.Evidence,
                proposal.History,
                revisions)
            {
                OptionRelationships = proposal.OptionRelationships,
                AnalyzedOptions = proposal.AnalyzedOptions,
                TradeoffComparisons = proposal.TradeoffComparisons,
                TradeoffAnalysisDiagnostics = proposal.TradeoffAnalysisDiagnostics,
                GenerationDiagnostics = proposal.GenerationDiagnostics
            });
        var decision = new Decision(
            decisionId,
            targetDecisionState,
            await ResolveClassificationAsync(repository, proposal.CandidateId),
            proposal.Title,
            proposal.Context,
            new DecisionMetadata(repository.Id, now, now),
            resolution,
            [],
            proposal.Evidence,
            [
                new DecisionHistoryEntry(
                    now,
                    "Resolved",
                    DecisionState.Open.ToString(),
                    targetDecisionState.ToString(),
                    rationale,
                    [proposalSource, selectedOptionSource])
            ]);
        DecisionProposal resolvedProposal = proposal with
        {
            State = DecisionProposalState.Resolved,
            History = proposal.History
                .Concat([
                    new DecisionHistoryEntry(
                        now,
                        "Resolved",
                        proposal.State.ToString(),
                        DecisionProposalState.Resolved.ToString(),
                        rationale,
                        [
                            proposalSource,
                            new DecisionSourceReference(
                                "DecisionRecord",
                                $".agents/decisions/records/{decisionId.Value}/decision.json",
                                DecisionId: decisionId,
                                ProposalId: proposal.Id,
                                CandidateId: proposal.CandidateId)
                        ])
                ])
                .ToArray()
        };

        await decisionRepository.SaveDecisionAsync(repository, decision);
        await projectionService.ProjectDecisionAsync(repository, decision);
        await decisionRepository.SaveProposalAsync(repository, resolvedProposal);
        await projectionService.ProjectProposalAsync(repository, resolvedProposal);
        await projectionService.RefreshDecisionIndexAsync(repository);
        return decision;
    }

    public async Task<Decision> SupersedeDecisionAsync(Guid repositoryId, string decisionId, SupersedeDecisionCommand command)
    {
        if (command is null)
        {
            throw new ArgumentException("Supersede command is required.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.ReplacementDecisionId))
        {
            throw new ArgumentException("Replacement decision id is required.", nameof(command));
        }

        string rationale = RequireText(command.Rationale, "Supersede rationale is required.", nameof(command));
        string resolver = RequireText(command.Resolver, "Resolver metadata is required.", nameof(command));
        Repository repository = await GetRepositoryAsync(repositoryId);
        Decision source = await GetDecisionAsync(repository, decisionId);
        Decision replacement = await GetDecisionAsync(repository, command.ReplacementDecisionId);
        if (source.Id == replacement.Id)
        {
            throw new ArgumentException("A decision cannot supersede itself.", nameof(command));
        }

        DecisionTransitionResult sourceTransition = DecisionLifecycleRules.ValidateDecisionTransition(
            source.State,
            DecisionState.Superseded);
        if (!sourceTransition.IsValid)
        {
            throw new InvalidOperationException(sourceTransition.Error);
        }

        if (replacement.State != DecisionState.Resolved)
        {
            throw new InvalidOperationException("Replacement decision must be Resolved before it can supersede another decision.");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSourceReference sourceDecisionReference = DecisionRecordSource(source.Id);
        DecisionSourceReference replacementDecisionReference = DecisionRecordSource(replacement.Id);
        string reason = $"{rationale} Resolver: {resolver}.";
        DecisionRelationship supersedesRelationship = new(
            replacement.Id,
            source.Id,
            DecisionRelationshipType.Supersedes,
            rationale);
        IReadOnlyList<DecisionRelationship> replacementRelationships = AddRelationship(
            replacement.Relationships,
            supersedesRelationship);
        DecisionTransitionResult relationshipValidation = DecisionLifecycleRules.ValidateRelationships(
            replacement.Id,
            replacementRelationships);
        if (!relationshipValidation.IsValid)
        {
            throw new InvalidOperationException(relationshipValidation.Error);
        }

        Decision superseded = source with
        {
            State = DecisionState.Superseded,
            Metadata = source.Metadata with { UpdatedAt = now },
            History = source.History
                .Concat([
                    new DecisionHistoryEntry(
                        now,
                        "Superseded",
                        source.State.ToString(),
                        DecisionState.Superseded.ToString(),
                        reason,
                        [replacementDecisionReference])
                ])
                .ToArray()
        };
        Decision updatedReplacement = replacement with
        {
            Metadata = replacement.Metadata with { UpdatedAt = now },
            Relationships = replacementRelationships,
            History = replacement.History
                .Concat([
                    new DecisionHistoryEntry(
                        now,
                        "Supersedes",
                        replacement.State.ToString(),
                        replacement.State.ToString(),
                        reason,
                        [sourceDecisionReference])
                ])
                .ToArray()
        };

        await decisionRepository.SaveDecisionAsync(repository, superseded);
        await projectionService.ProjectDecisionAsync(repository, superseded);
        await decisionRepository.SaveDecisionAsync(repository, updatedReplacement);
        await projectionService.ProjectDecisionAsync(repository, updatedReplacement);
        await projectionService.RefreshDecisionIndexAsync(repository);
        return superseded;
    }

    public async Task<Decision> ArchiveDecisionAsync(Guid repositoryId, string decisionId, ArchiveDecisionCommand command)
    {
        if (command is null)
        {
            throw new ArgumentException("Archive command is required.", nameof(command));
        }

        string rationale = RequireText(command.Rationale, "Archive rationale is required.", nameof(command));
        string resolver = RequireText(command.Resolver, "Resolver metadata is required.", nameof(command));
        Repository repository = await GetRepositoryAsync(repositoryId);
        Decision decision = await GetDecisionAsync(repository, decisionId);
        DecisionTransitionResult transition = DecisionLifecycleRules.ValidateDecisionTransition(
            decision.State,
            DecisionState.Archived);
        if (!transition.IsValid)
        {
            throw new InvalidOperationException(transition.Error);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Decision archived = decision with
        {
            State = DecisionState.Archived,
            Metadata = decision.Metadata with { UpdatedAt = now },
            History = decision.History
                .Concat([
                    new DecisionHistoryEntry(
                        now,
                        "Archived",
                        decision.State.ToString(),
                        DecisionState.Archived.ToString(),
                        $"{rationale} Resolver: {resolver}.",
                        [])
                ])
                .ToArray()
        };

        await decisionRepository.SaveDecisionAsync(repository, archived);
        await projectionService.ProjectDecisionAsync(repository, archived);
        await projectionService.RefreshDecisionIndexAsync(repository);
        return archived;
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<DecisionProposal> GetProposalAsync(Repository repository, string proposalId)
    {
        DecisionProposal? proposal = await decisionRepository.GetProposalAsync(repository, proposalId);
        return proposal ?? throw new KeyNotFoundException($"Decision proposal was not found: {proposalId}");
    }

    private async Task<Decision> GetDecisionAsync(Repository repository, string decisionId)
    {
        DecisionId id = DecisionId.Parse(decisionId);
        Decision? decision = await decisionRepository.GetDecisionAsync(repository, id);
        return decision ?? throw new KeyNotFoundException($"Decision was not found: {id.Value}");
    }

    private async Task<DecisionClassification> ResolveClassificationAsync(Repository repository, string candidateId)
    {
        DecisionCandidate? candidate = await decisionRepository.GetCandidateAsync(repository, candidateId);
        return candidate?.Classification ?? DecisionClassification.Tactical;
    }

    private static string Fingerprint(DecisionProposal proposal)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Serialize(proposal));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, DecisionJson.Options);
    }

    private static string ProposalPath(string proposalId)
    {
        return $".agents/decisions/proposals/{proposalId}/proposal.json";
    }

    private static DecisionSourceReference DecisionRecordSource(DecisionId decisionId)
    {
        return new DecisionSourceReference(
            "DecisionRecord",
            $".agents/decisions/records/{decisionId.Value}/decision.json",
            DecisionId: decisionId);
    }

    private static IReadOnlyList<DecisionRelationship> AddRelationship(
        IReadOnlyList<DecisionRelationship> relationships,
        DecisionRelationship relationship)
    {
        if (relationships.Any(existing =>
                existing.SourceDecisionId == relationship.SourceDecisionId &&
                existing.TargetDecisionId == relationship.TargetDecisionId &&
                existing.Type == relationship.Type))
        {
            return relationships;
        }

        return relationships.Concat([relationship]).ToArray();
    }

    private static string RequireText(string? value, string message, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, parameterName);
        }

        return value.Trim();
    }

    private static DecisionState TargetStateFor(DecisionOutcome outcome)
    {
        return outcome switch
        {
            DecisionOutcome.Accepted => DecisionState.Resolved,
            DecisionOutcome.Rejected => DecisionState.Archived,
            DecisionOutcome.Deferred => DecisionState.UnderReview,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unsupported decision outcome.")
        };
    }
}
