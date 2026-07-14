using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using LoopRelay.Core.Models.Identity;

namespace LoopRelay.Cli.Abstractions.Persistence;

internal enum LoopHistoryKind
{
    Decisions,
    Handoff,
    OperationalDelta
}

[method: JsonConstructor]
internal sealed record HistoryProviderEvidence(
    HistoryEvidenceItemIdentity Identity,
    string Provider,
    string ProviderThreadId,
    string? ProviderTurnId = null)
{
    public HistoryProviderEvidence(string provider, string providerThreadId, string? providerTurnId = null)
        : this(HistoryEvidenceItemIdentity.New(), provider, providerThreadId, providerTurnId)
    {
    }
}

[method: JsonConstructor]
internal sealed record HistoryContinuityEvidence(
    HistoryEvidenceItemIdentity Identity,
    ContinuityLineageIdentity Lineage,
    string Mechanism)
{
    public HistoryContinuityEvidence(ContinuityLineageIdentity lineage, string mechanism)
        : this(HistoryEvidenceItemIdentity.New(), lineage, mechanism)
    {
    }
}

[method: JsonConstructor]
internal sealed record HistoryRecoveryEvidence(
    HistoryEvidenceItemIdentity Identity,
    RecoveryAttemptIdentity RecoveryAttempt)
{
    public HistoryRecoveryEvidence(RecoveryAttemptIdentity recoveryAttempt)
        : this(HistoryEvidenceItemIdentity.New(), recoveryAttempt)
    {
    }
}

[method: JsonConstructor]
internal sealed record HistoryRepositoryEvidence(
    HistoryEvidenceItemIdentity Identity,
    string? CommitHash,
    IReadOnlyList<string> Paths)
{
    public HistoryRepositoryEvidence(string? commitHash, IReadOnlyList<string> paths)
        : this(HistoryEvidenceItemIdentity.New(), commitHash, paths)
    {
    }
}

[method: JsonConstructor]
internal sealed record HistoryEffectEvidence(
    HistoryEvidenceItemIdentity Identity,
    IReadOnlyList<string> EffectIdentities)
{
    public HistoryEffectEvidence(IReadOnlyList<string> effectIdentities)
        : this(HistoryEvidenceItemIdentity.New(), effectIdentities)
    {
    }
}

internal sealed record HistoryEvidenceAttachments
{
    public HistoryEvidenceAttachments(
        HistoryProviderEvidence? Provider = null,
        HistoryContinuityEvidence? Continuity = null,
        HistoryRecoveryEvidence? Recovery = null,
        HistoryRepositoryEvidence? Repository = null,
        HistoryEffectEvidence? Effects = null)
        : this(HistoryEvidenceSetIdentity.New(), Provider, Continuity, Recovery, Repository, Effects)
    {
    }

    [JsonConstructor]
    public HistoryEvidenceAttachments(
        HistoryEvidenceSetIdentity identity,
        HistoryProviderEvidence? provider,
        HistoryContinuityEvidence? continuity,
        HistoryRecoveryEvidence? recovery,
        HistoryRepositoryEvidence? repository,
        HistoryEffectEvidence? effects)
    {
        if (identity.IsEmpty)
        {
            throw new ArgumentException("History evidence-set identity must not be empty.", nameof(identity));
        }

        Identity = identity;
        Provider = provider;
        Continuity = continuity;
        Recovery = recovery;
        Repository = repository;
        Effects = effects;
    }

    public HistoryEvidenceSetIdentity Identity { get; }

    public HistoryProviderEvidence? Provider { get; }

    public HistoryContinuityEvidence? Continuity { get; }

    public HistoryRecoveryEvidence? Recovery { get; }

    public HistoryRepositoryEvidence? Repository { get; }

    public HistoryEffectEvidence? Effects { get; }

    public static HistoryEvidenceAttachments Empty => new();
}

internal sealed record LoopHistoryAppendRequest
{
    public LoopHistoryAppendRequest(
        LoopHistoryKind kind,
        string content,
        CanonicalCausalContext causality,
        HistoryEvidenceAttachments? evidence = null,
        HistoryFactIdentity? supersedes = null)
    {
        if (string.IsNullOrEmpty(content))
        {
            throw new ArgumentException("History fact content must not be empty.", nameof(content));
        }

        ArgumentNullException.ThrowIfNull(causality);
        Kind = kind;
        Content = content;
        Causality = causality;
        Evidence = evidence ?? HistoryEvidenceAttachments.Empty;
        Supersedes = supersedes;
    }

    public LoopHistoryKind Kind { get; }

    public string Content { get; }

    public CanonicalCausalContext Causality { get; }

    public HistoryEvidenceAttachments Evidence { get; }

    public HistoryFactIdentity? Supersedes { get; }
}

internal sealed record LoopHistoryRecord
{
    public LoopHistoryRecord(
        HistoryFactIdentity identity,
        LoopHistoryKind kind,
        long sequence,
        DateTimeOffset recordedAt,
        string content,
        string contentHash,
        CanonicalCausalContext causality,
        HistoryEvidenceAttachments evidence,
        HistoryFactIdentity? supersedes = null,
        string? materializedRelativePath = null)
    {
        if (identity.IsEmpty || sequence <= 0)
        {
            throw new ArgumentException("History fact identity and sequence must be valid.");
        }

        ArgumentNullException.ThrowIfNull(causality);
        ArgumentNullException.ThrowIfNull(evidence);
        if (!string.Equals(contentHash, ComputeContentHash(content), StringComparison.Ordinal))
        {
            throw new ArgumentException("History content hash does not match its content.", nameof(contentHash));
        }

        Identity = identity;
        Kind = kind;
        Sequence = sequence;
        RecordedAt = recordedAt;
        Content = content;
        ContentHash = contentHash;
        Causality = causality;
        Evidence = evidence;
        Supersedes = supersedes;
        MaterializedRelativePath = materializedRelativePath;
    }

    public HistoryFactIdentity Identity { get; }

    public LoopHistoryKind Kind { get; }

    public long Sequence { get; }

    public DateTimeOffset RecordedAt { get; }

    public string Content { get; }

    public string ContentHash { get; }

    public CanonicalCausalContext Causality { get; }

    public HistoryEvidenceAttachments Evidence { get; }

    public HistoryFactIdentity? Supersedes { get; }

    public string? MaterializedRelativePath { get; }

    public static string ComputeContentHash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}

internal interface ILoopHistoryStore
{
    Task<LoopHistoryRecord> AppendAsync(
        LoopHistoryAppendRequest request,
        CancellationToken cancellationToken = default);

    Task<LoopHistoryRecord?> ReadLatestAsync(
        LoopHistoryKind kind,
        CancellationToken cancellationToken = default);
}
