using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Persistence;
using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Services;

public sealed class DecisionOperationalContextAssimilationService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository,
    IDecisionContextService contextService,
    IDecisionArtifactProjectionService projectionService) : IDecisionOperationalContextAssimilationService
{
    public async Task<DecisionAssimilationRecommendation?> GetRecommendationAsync(Guid repositoryId, string decisionId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionId id = DecisionId.Parse(decisionId);
        return await decisionRepository.GetAssimilationRecommendationAsync(repository, id);
    }

    public async Task<DecisionAssimilationRecommendation> ProposeOperationalContextAssimilationAsync(
        Guid repositoryId,
        string decisionId,
        CreateDecisionAssimilationRecommendationCommand? command)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionId id = DecisionId.Parse(decisionId);
        Decision? decision = await decisionRepository.GetDecisionAsync(repository, id);
        if (decision is null)
        {
            throw new KeyNotFoundException($"Decision was not found: {id.Value}");
        }

        if (decision.State != DecisionState.Resolved)
        {
            throw new InvalidOperationException("Only resolved decisions can produce operational-context assimilation recommendations.");
        }

        if (decision.Resolution is null)
        {
            throw new InvalidOperationException("Resolved decision is missing resolution details.");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionContextSnapshot snapshot = await contextService.CreateSnapshotAsync(repositoryId);
        string decisionFingerprint = Fingerprint(decision);
        DecisionSourceReference decisionSource = new(
            "DecisionRecord",
            $".agents/decisions/records/{decision.Id.Value}/decision.json",
            DecisionId: decision.Id);
        DecisionSourceReference contextSource = new(
            "DecisionContextSnapshot",
            $".agents/decisions/contexts/{snapshot.SnapshotId}.json",
            DecisionId: decision.Id,
            Excerpt: snapshot.Fingerprint);
        string projectedStableDecision = ProjectStableDecision(decision);
        var recommendation = new DecisionAssimilationRecommendation(
            decision.Id.Value,
            repository.Id,
            now,
            decisionFingerprint,
            snapshot.SnapshotId,
            snapshot.Fingerprint,
            decision,
            snapshot,
            projectedStableDecision,
            decision.Resolution.Rationale,
            NormalizeOptional(command?.RequestedBy),
            NormalizeOptional(command?.Notes),
            [
                new DecisionEvidence(
                    $"Decision {decision.Id.Value} was resolved with outcome {decision.Resolution.Outcome}.",
                    [decisionSource]),
                new DecisionEvidence(
                    $"Decision context snapshot {snapshot.SnapshotId} captured source fingerprint {snapshot.Fingerprint}.",
                    [contextSource])
            ],
            [decisionSource, contextSource],
            [
                "Recommendation package is advisory and does not mutate operational context.",
                "Continuity remains responsible for merge, review, acceptance, and promotion policy."
            ]);

        await decisionRepository.SaveAssimilationRecommendationAsync(repository, recommendation);
        await projectionService.ProjectDecisionAssimilationRecommendationAsync(repository, recommendation);
        await projectionService.RefreshDecisionIndexAsync(repository);
        return recommendation;
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static string ProjectStableDecision(Decision decision)
    {
        DecisionResolution resolution = decision.Resolution!;
        return $"{decision.Id.Value}: {decision.Title} - {resolution.Outcome}; selected {resolution.SelectedOptionId}. Rationale: {resolution.Rationale}";
    }

    private static string Fingerprint<T>(T value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, DecisionJson.Options));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
