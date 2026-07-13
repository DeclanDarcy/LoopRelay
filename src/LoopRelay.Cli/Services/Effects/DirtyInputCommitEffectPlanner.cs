using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Interactions;
using LoopRelay.Orchestration.Persistence;

namespace LoopRelay.Cli.Services.Effects;

internal sealed class DirtyInputCommitEffectPlanner(Repository _repository)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EffectIntent> ScheduleAsync(
        InteractionAggregate interaction,
        CancellationToken cancellationToken)
    {
        if (interaction.Request.Category != InteractionCategory.DirtyInputCommitOffer ||
            interaction.State != InteractionLifecycle.Validated ||
            interaction.AcceptedResponse is null)
        {
            throw new InvalidOperationException("Only a validated dirty-input commit offer can plan a commit effect.");
        }

        using JsonDocument presentation = JsonDocument.Parse(interaction.Request.PresentationJson);
        string surface = presentation.RootElement.GetProperty("surface").GetString()
            ?? throw new InvalidDataException("Dirty-input presentation has no declared surface.");
        var payload = new GitEffectPayload(
            ".",
            $"LoopRelay: commit declared input surface {surface}",
            surface);
        string payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        string payloadHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
        var intent = new EffectIntent(
            EffectIntentIdentity.New(),
            interaction.Request.Subject.Causality,
            $"interaction:dirty-input-commit:{interaction.Request.Identity.Value}",
            GitEffectExecutorKeys.NestedRepositoryCommit,
            "1",
            new EffectTargetDescriptor(
                "GitDeclaredInputSurface",
                surface,
                JsonSerializer.Serialize(new { surface }, JsonOptions)),
            payloadJson,
            payloadHash,
            0,
            [],
            EffectRequiredness.BlockingLocal,
            new EffectCondition("validated-interaction-response", JsonSerializer.Serialize(new
            {
                request = interaction.Request.Identity.Value,
                response = interaction.AcceptedResponse.Identity.Value,
            }, JsonOptions)),
            new EffectCondition("git-surface-clean", JsonSerializer.Serialize(new { surface }, JsonOptions)),
            "independent-git-status-pathspec",
            $"dirty-input-commit:{interaction.Request.Identity.Value}:{interaction.AcceptedResponse.SemanticResponseHash}",
            DateTimeOffset.UtcNow);
        var store = new CanonicalEffectWorkStore(_repository);
        await store.AppendPlanAsync([intent], cancellationToken);
        return (await store.ReadBySemanticOperationAsync(intent.SemanticOperationKey, cancellationToken))
            .Single().Intent;
    }
}
