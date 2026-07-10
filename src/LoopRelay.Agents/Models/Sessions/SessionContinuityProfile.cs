using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Agents.Models.Sessions;

public sealed record SessionParameterSupport(
    SessionOperationSupport Status,
    string Evidence,
    string? Constraint = null);

public sealed record SessionOperationSupportDescriptor(
    SessionOperationSupport Status,
    string ProtocolVersion,
    IReadOnlyDictionary<string, SessionParameterSupport> Parameters,
    string SideEffectClass,
    string ResultContract,
    string PartialResultBehavior,
    string ReconciliationStrategy,
    string Evidence);

public sealed record SessionContinuityProfile
{
    public const string ExcludeTurnsParameter = "excludeTurns";

    public SessionContinuityProfile(
        string provider,
        string clientVersion,
        string? serverVersion,
        string? executableIdentity,
        string? protocolIdentity,
        string? schemaDigest,
        IReadOnlyDictionary<string, bool> offeredClientCapabilities,
        IReadOnlyDictionary<string, string> returnedServerCapabilities,
        IReadOnlyDictionary<SessionContinuityOperation, SessionOperationSupportDescriptor> operations,
        int? maximumRecoverableContext,
        string maximumRecoverableContextSource,
        string evidenceSource,
        string? compatibilityManifestEntry = null,
        string? fixtureIdentity = null,
        DateTimeOffset? negotiatedAt = null)
    {
        Provider = provider;
        ClientVersion = clientVersion;
        ServerVersion = serverVersion;
        ExecutableIdentity = executableIdentity;
        ProtocolIdentity = protocolIdentity;
        SchemaDigest = schemaDigest;
        OfferedClientCapabilities = offeredClientCapabilities;
        ReturnedServerCapabilities = returnedServerCapabilities;
        Operations = operations;
        MaximumRecoverableContext = maximumRecoverableContext;
        MaximumRecoverableContextSource = maximumRecoverableContextSource;
        EvidenceSource = evidenceSource;
        CompatibilityManifestEntry = compatibilityManifestEntry;
        FixtureIdentity = fixtureIdentity;
        NegotiatedAt = negotiatedAt ?? DateTimeOffset.UtcNow;
        Digest = SessionContinuityProfileDigest.Compute(this);
    }

    public string Provider { get; }
    public string ClientVersion { get; }
    public string? ServerVersion { get; }
    public string? ExecutableIdentity { get; }
    public string? ProtocolIdentity { get; }
    public string? SchemaDigest { get; }
    public IReadOnlyDictionary<string, bool> OfferedClientCapabilities { get; }
    public IReadOnlyDictionary<string, string> ReturnedServerCapabilities { get; }
    public IReadOnlyDictionary<SessionContinuityOperation, SessionOperationSupportDescriptor> Operations { get; }
    public int? MaximumRecoverableContext { get; }
    public string MaximumRecoverableContextSource { get; }
    public string EvidenceSource { get; }
    public string? CompatibilityManifestEntry { get; }
    public string? FixtureIdentity { get; }
    public DateTimeOffset NegotiatedAt { get; }
    public string Digest { get; }

    public SessionOperationSupportDescriptor Operation(SessionContinuityOperation operation) =>
        Operations.TryGetValue(operation, out SessionOperationSupportDescriptor? descriptor)
            ? descriptor
            : UnknownOperation;

    public SessionParameterSupport Parameter(SessionContinuityOperation operation, string parameter) =>
        Operation(operation).Parameters.TryGetValue(parameter, out SessionParameterSupport? support)
            ? support
            : new SessionParameterSupport(SessionOperationSupport.Unknown, "No trusted evidence.");

    public static SessionOperationSupportDescriptor UnknownOperation { get; } = new(
        SessionOperationSupport.Unknown,
        "unknown",
        new Dictionary<string, SessionParameterSupport>(StringComparer.Ordinal),
        "unknown",
        "unknown",
        "unknown",
        "none",
        "No trusted evidence.");
}

public static class SessionContinuityProfileDigest
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Compute(SessionContinuityProfile profile)
    {
        var canonical = new
        {
            profile.Provider,
            profile.ClientVersion,
            profile.ServerVersion,
            profile.ExecutableIdentity,
            profile.ProtocolIdentity,
            profile.SchemaDigest,
            OfferedClientCapabilities = profile.OfferedClientCapabilities.OrderBy(x => x.Key, StringComparer.Ordinal),
            ReturnedServerCapabilities = profile.ReturnedServerCapabilities.OrderBy(x => x.Key, StringComparer.Ordinal),
            Operations = profile.Operations.OrderBy(x => x.Key).Select(x => new
            {
                Operation = x.Key.ToString(),
                x.Value.Status,
                x.Value.ProtocolVersion,
                Parameters = x.Value.Parameters.OrderBy(p => p.Key, StringComparer.Ordinal),
                x.Value.SideEffectClass,
                x.Value.ResultContract,
                x.Value.PartialResultBehavior,
                x.Value.ReconciliationStrategy,
                x.Value.Evidence,
            }),
            profile.MaximumRecoverableContext,
            profile.MaximumRecoverableContextSource,
            profile.EvidenceSource,
            profile.CompatibilityManifestEntry,
            profile.FixtureIdentity,
        };

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(canonical, Options);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
