using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Services;

namespace CommandCenter.Backend.Services;

public sealed class DecisionReasoningCaptureService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository,
    IReasoningRepository reasoningRepository)
    : IDecisionReasoningCaptureService
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task CaptureDecisionSupersededAsync(
        Guid repositoryId,
        Decision supersededDecision,
        SupersedeDecisionCommand command)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        string replacementDecisionId = RequireText(command.ReplacementDecisionId, "Replacement decision id is required.");
        string rationale = RequireText(command.Rationale, "Supersede rationale is required.");
        string resolver = RequireText(command.Resolver, "Resolver metadata is required.");
        DecisionId replacementId = DecisionId.Parse(replacementDecisionId);
        Decision replacementDecision = await decisionRepository.GetDecisionAsync(repository, replacementId)
            ?? throw new KeyNotFoundException($"Replacement decision was not found: {replacementId.Value}");

        string transitionFingerprint = Fingerprint(new
        {
            Transition = "DecisionSuperseded",
            RepositoryId = repository.Id,
            SupersededDecisionId = supersededDecision.Id.Value,
            ReplacementDecisionId = replacementDecision.Id.Value,
            SupersededState = supersededDecision.State,
            ReplacementState = replacementDecision.State,
            Rationale = rationale,
            Resolver = resolver
        });

        ReasoningEvent reasoningEvent = await GetOrCreateDecisionSupersededEventAsync(
            repository,
            supersededDecision,
            replacementDecision,
            rationale,
            resolver,
            transitionFingerprint);

        await CreateSupersedesRelationshipIfMissingAsync(
            repository,
            supersededDecision,
            replacementDecision,
            rationale,
            resolver,
            transitionFingerprint,
            reasoningEvent.Id);
    }

    private async Task<ReasoningEvent> GetOrCreateDecisionSupersededEventAsync(
        Repository repository,
        Decision supersededDecision,
        Decision replacementDecision,
        string rationale,
        string resolver,
        string transitionFingerprint)
    {
        IReadOnlyList<ReasoningEvent> existingEvents = await reasoningRepository.ListEventsAsync(repository);
        ReasoningEvent? existing = existingEvents.FirstOrDefault(reasoningEvent =>
            reasoningEvent.Type == ReasoningEventType.DecisionSuperseded &&
            string.Equals(reasoningEvent.Provenance.Fingerprint, transitionFingerprint, StringComparison.Ordinal));
        if (existing is not null)
        {
            return existing;
        }

        return await reasoningRepository.CreateEventAsync(
            repository,
            new CreateReasoningEventCommand(
                ReasoningEventFamily.DecisionEvolution,
                ReasoningEventType.DecisionSuperseded,
                $"Decision {supersededDecision.Id.Value} superseded by {replacementDecision.Id.Value}",
                new ReasoningNarrative(
                    $"Decision {replacementDecision.Id.Value} replaced decision {supersededDecision.Id.Value}.",
                    $"The authoritative decision lifecycle transition already occurred. Rationale: {rationale}"),
                [
                    DecisionReference(supersededDecision),
                    DecisionReference(replacementDecision)
                ],
                Provenance(supersededDecision, rationale, resolver, transitionFingerprint),
                [],
                ["decision-evolution", "inferred-capture", "supersession"]));
    }

    private async Task CreateSupersedesRelationshipIfMissingAsync(
        Repository repository,
        Decision supersededDecision,
        Decision replacementDecision,
        string rationale,
        string resolver,
        string transitionFingerprint,
        string reasoningEventId)
    {
        IReadOnlyList<ReasoningRelationship> existingRelationships = await reasoningRepository.ListRelationshipsAsync(repository);
        if (existingRelationships.Any(relationship =>
            relationship.Type == ReasoningRelationshipType.Supersedes &&
            relationship.Source.Kind == ReasoningReferenceKind.Decision &&
            relationship.Target.Kind == ReasoningReferenceKind.Decision &&
            string.Equals(relationship.Source.Id, replacementDecision.Id.Value, StringComparison.Ordinal) &&
            string.Equals(relationship.Target.Id, supersededDecision.Id.Value, StringComparison.Ordinal)))
        {
            return;
        }

        try
        {
            await reasoningRepository.CreateRelationshipAsync(
                repository,
                new CreateReasoningRelationshipCommand(
                    ReasoningRelationshipType.Supersedes,
                    DecisionReference(replacementDecision),
                    DecisionReference(supersededDecision),
                    new ReasoningNarrative(
                        $"Decision {replacementDecision.Id.Value} supersedes decision {supersededDecision.Id.Value}.",
                        $"Captured from reasoning event {reasoningEventId}. Rationale: {rationale}"),
                    Provenance(supersededDecision, rationale, resolver, transitionFingerprint)));
        }
        catch (ReasoningConflictException)
        {
            // Another capture path recorded the same explanatory relationship first.
        }
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static ReasoningReference DecisionReference(Decision decision)
    {
        return new ReasoningReference(
            ReasoningReferenceKind.Decision,
            decision.Id.Value,
            DecisionPath(decision.Id),
            Section: "Decision Record",
            Excerpt: decision.Title,
            Fingerprint: Fingerprint(decision));
    }

    private static ReasoningProvenance Provenance(
        Decision supersededDecision,
        string rationale,
        string resolver,
        string transitionFingerprint)
    {
        return new ReasoningProvenance(
            "InferredDecisionSupersession",
            resolver,
            DecisionPath(supersededDecision.Id),
            "History: Superseded",
            rationale,
            transitionFingerprint);
    }

    private static string DecisionPath(DecisionId decisionId)
    {
        return $".agents/decisions/records/{decisionId.Value}/decision.json";
    }

    private static string Fingerprint<T>(T value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string RequireText(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message);
        }

        return value.Trim();
    }
}
