using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Interactions;

public static class InteractionCategoryPolicyRegistry
{
    public static InteractionCategoryPolicy Resolve(
        InteractionCategory category,
        string resolvedPolicyIdentity,
        DateTimeOffset? evaluatedAt = null)
    {
        string schema = category == InteractionCategory.DirtyInputCommitOffer
            ? """{"type":"object","required":["accept"],"properties":{"accept":{"type":"boolean"}},"additionalProperties":false}"""
            : """{"type":"object","required":["decision"],"properties":{"decision":{"type":"string","enum":["accept","reject"]}},"additionalProperties":false}""";
        return new InteractionCategoryPolicy(
            InteractionPolicyEvaluationIdentity.New(),
            category,
            "interaction-question.v1",
            "interaction-response-schema.v1",
            schema,
            Hash(schema),
            InteractionDeadlineBehavior.None,
            null,
            null,
            category switch
            {
                InteractionCategory.DirtyInputCommitOffer => RuntimeOutcomeKind.DirtyInputSurface,
                InteractionCategory.ImportConflict => RuntimeOutcomeKind.CompatibilityImportRequired,
                InteractionCategory.RecoveryAmbiguity => RuntimeOutcomeKind.RecoveryRequired,
                _ => RuntimeOutcomeKind.HumanDecisionRequired,
            },
            category == InteractionCategory.DirtyInputCommitOffer
                ? ["responder-authentication", "mutation-authorization"]
                : ["responder-authentication", "decision-authorization"],
            category switch
            {
                InteractionCategory.DirtyInputCommitOffer => "InteractionBroker/DirtyInputResolver",
                InteractionCategory.ImportConflict => "ImportGateway",
                InteractionCategory.RecoveryAmbiguity => "RecoveryCoordinator",
                _ => "CompletionAuthority",
            },
            resolvedPolicyIdentity,
            evaluatedAt ?? DateTimeOffset.UtcNow);
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed class InteractionBroker(IInteractionStore _store, TimeProvider? timeProvider = null) : IInteractionBroker
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<InteractionAggregate> CreateAsync(
        CreateInteractionCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(command.Request);
        InteractionAggregate? duplicate = await _store.ReadBySemanticIdempotencyKeyAsync(
            command.Request.SemanticIdempotencyKey,
            cancellationToken);
        if (duplicate is not null) return duplicate;
        await _store.PersistRequestAsync(command.Request, cancellationToken);
        return await ShowAsync(new ShowInteractionQuery(command.Request.Identity), cancellationToken);
    }

    public Task<IReadOnlyList<InteractionAggregate>> ListAsync(
        ListInteractionsQuery query,
        CancellationToken cancellationToken = default) => _store.ListAsync(query.OutstandingOnly, cancellationToken);

    public async Task<InteractionAggregate> ShowAsync(
        ShowInteractionQuery query,
        CancellationToken cancellationToken = default) =>
        await _store.ReadAsync(query.Request, cancellationToken)
        ?? throw new KeyNotFoundException($"Interaction request `{query.Request}` was not found.");

    public Task<InteractionAggregate> PresentAsync(
        InteractionRequestIdentity request,
        long expectedRowVersion,
        CancellationToken cancellationToken = default) =>
        AppendAsync(request, InteractionLifecycle.Presented, "Interaction request was presented.", [],
            expectedRowVersion, cancellationToken);

    public async Task<InteractionResponseResult> RespondAsync(
        RespondInteractionCommand command,
        CancellationToken cancellationToken = default)
    {
        InteractionAggregate? aggregate = await _store.ReadAsync(command.Request, cancellationToken);
        if (aggregate is null)
            return new InteractionResponseResult(false, false, null, InteractionRejectionReason.NotFound,
                "Interaction request was not found.", null);
        InteractionRejectionReason? rejection = ValidateResponse(command, aggregate, out string canonical);
        if (rejection is not null)
            return await RejectAsync(aggregate, rejection.Value, command.ExpectedRowVersion, cancellationToken);
        string semanticHash = Hash(canonical);
        var response = new InteractionResponse(
            InteractionResponseIdentity.New(), command.Request, canonical, semanticHash,
            command.SemanticIdempotencyKey, command.TrustEvidence, command.ResponderIdentity, clock.GetUtcNow());
        return await _store.TryAcceptResponseAsync(response, command.ExpectedRowVersion, cancellationToken);
    }

    public Task<InteractionAggregate> CancelAsync(
        CancelInteractionCommand command,
        CancellationToken cancellationToken = default) =>
        AppendAsync(command.Request, InteractionLifecycle.Cancelled, command.Explanation, [],
            command.ExpectedRowVersion, cancellationToken);

    public async Task<InteractionAggregate> ResolveAsync(
        ResolveInteractionCommand command,
        CancellationToken cancellationToken = default)
    {
        InteractionAggregate aggregate = await ShowAsync(new ShowInteractionQuery(command.Request), cancellationToken);
        if (aggregate.State != InteractionLifecycle.Validated || aggregate.AcceptedResponse is null)
            throw new InvalidOperationException("Only a validated response can resolve an interaction.");
        InteractionAggregate resolved = await AppendAsync(
            command.Request, InteractionLifecycle.Resolved, "Validated interaction response was resolved.",
            command.Evidence, command.ExpectedRowVersion, cancellationToken);
        return await AppendAsync(
            command.Request, InteractionLifecycle.ResumeAuthorized,
            "Kernel or Recovery may resume from this durable authorization fact.",
            command.Evidence.Concat([aggregate.AcceptedResponse.Identity.Value]).ToArray(),
            resolved.RowVersion,
            cancellationToken);
    }

    private async Task<InteractionResponseResult> RejectAsync(
        InteractionAggregate aggregate,
        InteractionRejectionReason rejection,
        long expectedRowVersion,
        CancellationToken cancellationToken)
    {
        InteractionAggregate updated;
        try
        {
            updated = await AppendAsync(
                aggregate.Request.Identity,
                InteractionLifecycle.Rejected,
                $"Interaction response rejected: {rejection}.",
                [rejection.ToString()],
                expectedRowVersion,
                cancellationToken);
        }
        catch (InvalidOperationException) when (expectedRowVersion != aggregate.RowVersion)
        {
            updated = await ShowAsync(new ShowInteractionQuery(aggregate.Request.Identity), cancellationToken);
            rejection = InteractionRejectionReason.CompareAndSetConflict;
        }
        return new InteractionResponseResult(false, false, null, rejection,
            $"Interaction response was rejected: {rejection}.", updated);
    }

    private InteractionRejectionReason? ValidateResponse(
        RespondInteractionCommand command,
        InteractionAggregate aggregate,
        out string canonical)
    {
        canonical = string.Empty;
        if (aggregate.State is InteractionLifecycle.Cancelled)
            return InteractionRejectionReason.Cancelled;
        if (aggregate.State is InteractionLifecycle.Expired or InteractionLifecycle.Defaulted)
            return InteractionRejectionReason.Expired;
        if (aggregate.Request.Policy.Deadline is { } deadline && clock.GetUtcNow() > deadline)
            return InteractionRejectionReason.Late;
        if (aggregate.Request.Policy.RequiredTrustEvidence.Except(command.TrustEvidence, StringComparer.Ordinal).Any())
            return InteractionRejectionReason.MissingTrustEvidence;
        if (!InteractionJsonSchemaValidator.TryValidate(
                aggregate.Request.Policy.ResponseJsonSchema, command.ResponseJson, out canonical))
            return InteractionRejectionReason.SchemaInvalid;
        return null;
    }

    private Task<InteractionAggregate> AppendAsync(
        InteractionRequestIdentity request,
        InteractionLifecycle lifecycle,
        string explanation,
        IReadOnlyList<string> evidence,
        long expectedRowVersion,
        CancellationToken cancellationToken) => _store.AppendEventAsync(
        new InteractionLifecycleEvent(
            InteractionEventIdentity.New(), request, lifecycle, explanation, evidence, clock.GetUtcNow()),
        expectedRowVersion,
        cancellationToken);

    private static void ValidateRequest(InteractionRequest request)
    {
        if (request.Identity.IsEmpty || string.IsNullOrWhiteSpace(request.Question) ||
            string.IsNullOrWhiteSpace(request.SemanticIdempotencyKey))
            throw new ArgumentException("Interaction request identity, question, and idempotency key are required.");
        string hash = Hash(request.Policy.ResponseJsonSchema);
        if (!string.Equals(hash, request.Policy.ResponseSchemaHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Interaction response schema hash does not match its policy.");
        if (request.Policy.Category != request.Category)
            throw new InvalidOperationException("Interaction request and resolved policy categories differ.");
        if (request.Policy.DeadlineBehavior == InteractionDeadlineBehavior.None &&
            (request.Policy.Deadline is not null || request.Policy.DefaultResponseJson is not null))
            throw new InvalidOperationException("No-deadline interaction policy cannot hide a deadline or default.");
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

internal static class InteractionJsonSchemaValidator
{
    public static bool TryValidate(string schemaJson, string responseJson, out string canonical)
    {
        canonical = string.Empty;
        try
        {
            using JsonDocument schema = JsonDocument.Parse(schemaJson);
            using JsonDocument response = JsonDocument.Parse(responseJson);
            if (response.RootElement.ValueKind != JsonValueKind.Object) return false;
            string[] required = schema.RootElement.GetProperty("required").EnumerateArray()
                .Select(item => item.GetString()!).ToArray();
            JsonElement properties = schema.RootElement.GetProperty("properties");
            HashSet<string> actual = response.RootElement.EnumerateObject().Select(item => item.Name)
                .ToHashSet(StringComparer.Ordinal);
            if (required.Any(name => !actual.Contains(name))) return false;
            if (schema.RootElement.TryGetProperty("additionalProperties", out JsonElement additional) &&
                additional.ValueKind == JsonValueKind.False && actual.Any(name => !properties.TryGetProperty(name, out _)))
                return false;
            foreach (JsonProperty property in response.RootElement.EnumerateObject())
            {
                if (!properties.TryGetProperty(property.Name, out JsonElement contract)) return false;
                string type = contract.GetProperty("type").GetString()!;
                if ((type == "boolean" && property.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) ||
                    (type == "string" && property.Value.ValueKind != JsonValueKind.String)) return false;
                if (contract.TryGetProperty("enum", out JsonElement values) &&
                    !values.EnumerateArray().Any(item => item.GetString() == property.Value.GetString())) return false;
            }
            canonical = Canonicalize(response.RootElement);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Canonicalize(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(element, writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Write(JsonElement element, Utf8JsonWriter writer)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                Write(property.Value, writer);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (JsonElement item in element.EnumerateArray()) Write(item, writer);
            writer.WriteEndArray();
        }
        else element.WriteTo(writer);
    }
}
