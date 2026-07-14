using System.Text.Json;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Agents.Services.Codex.Compatibility;

public sealed class CodexSessionContinuityProfileResolver(CodexCompatibilityManifest _manifest)
{
    public SessionContinuityNegotiationResult Resolve(SessionContinuityNegotiationRequest request)
    {
        bool certifiedProtocolIdentity =
            string.Equals(request.Provider, "codex", StringComparison.Ordinal) &&
            string.Equals(request.ProtocolIdentity, "app-server-v2", StringComparison.Ordinal);
        CodexCompatibilityManifestEntry? certified = certifiedProtocolIdentity
            ? _manifest.FindExact(request.ServerVersion, request.SchemaDigest)
            : null;
        var returnedCapabilities = ReadCapabilities(request.InitializeResult);

        SessionOperationSupport resume = certified?.ResumeSupport ?? SessionOperationSupport.Unknown;
        SessionOperationSupport excludeTurns = certified?.ExcludeTurnsSupport ?? SessionOperationSupport.Unknown;
        SessionOperationSupport fork = certified?.ForkSupport ?? SessionOperationSupport.Unknown;
        SessionOperationSupport read = certified?.ReadSupport ?? SessionOperationSupport.Unknown;
        SessionOperationSupport write = certified?.WriteSupport ?? SessionOperationSupport.Unknown;

        // Explicit server evidence may only narrow a certified profile. Omitted capability fields are not proof.
        resume = NarrowFromBoolean(returnedCapabilities, "threadResume", resume);
        fork = NarrowFromBoolean(returnedCapabilities, "threadFork", fork);
        read = NarrowFromBoolean(returnedCapabilities, "threadRead", read);
        write = NarrowFromBoolean(returnedCapabilities, "conversationWrite", write);
        excludeTurns = NarrowFromBoolean(returnedCapabilities, "experimentalApi", excludeTurns);
        if (!request.OfferExperimentalApi)
        {
            excludeTurns = SessionOperationSupport.Unsupported;
        }

        // The checked real-Codex fixtures certify resume only with experimentalApi offered and
        // excludeTurns=true. Never broaden that evidence into an untested resume request shape.
        if (resume == SessionOperationSupport.Supported &&
            excludeTurns != SessionOperationSupport.Supported)
        {
            resume = SessionOperationSupport.Unknown;
        }

        string evidence = certified is null
            ? certifiedProtocolIdentity
                ? "No exact certified version/schema fixture matched; omitted capabilities remain Unknown."
                : "Provider/protocol identity did not match the Codex app-server v2 fixture authority."
            : $"Exact certified fixture {certified.FixtureIdentity} ({certified.EvidenceDigest}).";

        var operations = new Dictionary<SessionContinuityOperation, SessionOperationSupportDescriptor>
        {
            [SessionContinuityOperation.Resume] = Descriptor(
                resume,
                "thread/resume",
                new Dictionary<string, SessionParameterSupport>(StringComparer.Ordinal)
                {
                    [SessionContinuityProfile.ExcludeTurnsParameter] = new(excludeTurns, evidence),
                },
                "loads-existing-session",
                "same provider thread id; zero submitted turns",
                "none",
                "exact-thread-id-read",
                evidence),
            [SessionContinuityOperation.Fork] = Descriptor(fork, "thread/fork", null, "creates-session", "new child id", "none", "parent-plus-correlation", evidence),
            [SessionContinuityOperation.ConversationRead] = Descriptor(read, "thread/read", null, "read-only", "ordered public projection", "profile-defined", "repeat-read", evidence),
            [SessionContinuityOperation.ConversationWrite] = Descriptor(write, "turn/start", null, "mutates-session", "accepted marker turn", "none", "marker-read", evidence),
            [SessionContinuityOperation.ConversationImport] = Descriptor(SessionOperationSupport.Unknown, "unknown", null, "unknown", "unknown", "unknown", "none", evidence),
            [SessionContinuityOperation.ConversationExport] = Descriptor(SessionOperationSupport.Unknown, "unknown", null, "read-only", "unknown", "unknown", "none", evidence),
            [SessionContinuityOperation.PartialRead] = Descriptor(SessionOperationSupport.Unknown, "thread/read", null, "read-only", "unknown", "unknown", "repeat-read", evidence),
            [SessionContinuityOperation.DeterministicIdentifiers] = Descriptor(SessionOperationSupport.Unknown, "provider", null, "none", "unknown", "none", "provider-evidence", evidence),
        };

        var profile = new SessionContinuityProfile(
            request.Provider,
            request.ClientVersion,
            request.ServerVersion,
            request.ExecutableIdentity,
            request.ProtocolIdentity,
            request.SchemaDigest,
            new Dictionary<string, bool>(StringComparer.Ordinal) { ["experimentalApi"] = request.OfferExperimentalApi },
            returnedCapabilities,
            operations,
            certified?.MaximumRecoverableContext,
            certified is null ? "unknown" : $"fixture:{certified.FixtureIdentity}",
            evidence,
            certified?.Id,
            certified?.FixtureIdentity);

        return new SessionContinuityNegotiationResult(profile, certified is not null, evidence);
    }

    public SessionContinuityProfile NarrowAfterStructuredRejection(
        SessionContinuityProfile profile,
        SessionContinuityOperation operation,
        string? parameter,
        int jsonRpcCode,
        string evidence)
    {
        var operations = profile.Operations.ToDictionary(pair => pair.Key, pair => pair.Value);
        SessionOperationSupportDescriptor current = profile.Operation(operation);
        Dictionary<string, SessionParameterSupport> parameters = current.Parameters.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (parameter is null)
        {
            current = current with { Status = SessionOperationSupport.Unsupported, Evidence = evidence };
        }
        else
        {
            parameters[parameter] = new SessionParameterSupport(
                SessionOperationSupport.Unsupported,
                $"Structured JSON-RPC rejection {jsonRpcCode}: {evidence}");
            current = current with { Parameters = parameters, Evidence = evidence };
        }

        operations[operation] = current;
        return new SessionContinuityProfile(
            profile.Provider,
            profile.ClientVersion,
            profile.ServerVersion,
            profile.ExecutableIdentity,
            profile.ProtocolIdentity,
            profile.SchemaDigest,
            profile.OfferedClientCapabilities,
            profile.ReturnedServerCapabilities,
            operations,
            profile.MaximumRecoverableContext,
            profile.MaximumRecoverableContextSource,
            $"{profile.EvidenceSource}; narrowed by structured rejection",
            profile.CompatibilityManifestEntry,
            profile.FixtureIdentity,
            profile.NegotiatedAt);
    }

    private static SessionOperationSupportDescriptor Descriptor(
        SessionOperationSupport status,
        string protocol,
        IReadOnlyDictionary<string, SessionParameterSupport>? parameters,
        string sideEffect,
        string result,
        string partial,
        string reconcile,
        string evidence) =>
        new(status, protocol, parameters ?? new Dictionary<string, SessionParameterSupport>(StringComparer.Ordinal), sideEffect, result, partial, reconcile, evidence);

    private static SessionOperationSupport NarrowFromBoolean(
        IReadOnlyDictionary<string, string> capabilities,
        string key,
        SessionOperationSupport current) =>
        capabilities.TryGetValue(key, out string? value) && bool.TryParse(value, out bool supported)
            ? supported ? current : SessionOperationSupport.Unsupported
            : current;

    private static IReadOnlyDictionary<string, string> ReadCapabilities(JsonElement initializeResult)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (initializeResult.ValueKind != JsonValueKind.Object
            || !initializeResult.TryGetProperty("capabilities", out JsonElement capabilities)
            || capabilities.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (JsonProperty property in capabilities.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                _ => property.Value.GetRawText(),
            };
        }

        return result;
    }
}
