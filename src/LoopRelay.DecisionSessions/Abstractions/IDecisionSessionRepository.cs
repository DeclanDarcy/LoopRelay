using LoopRelay.Core.Repositories;
using LoopRelay.DecisionSessions.Models;
using LoopRelay.DecisionSessions.Primitives;

namespace LoopRelay.DecisionSessions.Abstractions;

public interface IDecisionSessionRepository
{
    Task<DecisionSession> CreateAsync(Repository repository, DecisionSession session);

    Task<DecisionSession> UpdateAsync(Repository repository, DecisionSession session);

    Task<DecisionSession?> GetAsync(Repository repository, DecisionSessionId sessionId);

    Task<DecisionSession?> GetActiveAsync(Repository repository);

    Task<IReadOnlyList<DecisionSession>> ListAsync(Repository repository);

    Task<DecisionSessionMetricsSnapshot?> ReadMetricsSnapshotAsync(Repository repository);

    Task<DecisionSessionMetricsSnapshotStamp?> ReadMetricsSnapshotStampAsync(Repository repository);

    Task WriteMetricsSnapshotAsync(
        Repository repository,
        DecisionSessionMetricsSnapshot snapshot,
        DateTimeOffset? sourceMaxWriteUtc = null,
        string? analysisOptionsVersion = null,
        string? sourceFingerprint = null);

    Task<DecisionSessionEconomicsSnapshot?> ReadEconomicsSnapshotAsync(Repository repository);

    Task WriteEconomicsSnapshotAsync(Repository repository, DecisionSessionEconomicsSnapshot snapshot);

    Task<DecisionSessionCoherenceSnapshot?> ReadCoherenceSnapshotAsync(Repository repository);

    Task WriteCoherenceSnapshotAsync(Repository repository, DecisionSessionCoherenceSnapshot snapshot);

    Task<DecisionSessionLifecycleSnapshot?> ReadLifecyclePolicySnapshotAsync(Repository repository);

    Task WriteLifecyclePolicySnapshotAsync(Repository repository, DecisionSessionLifecycleSnapshot snapshot);

    Task<DecisionSessionTransferEligibilitySnapshot?> ReadTransferEligibilitySnapshotAsync(Repository repository);

    Task WriteTransferEligibilitySnapshotAsync(Repository repository, DecisionSessionTransferEligibilitySnapshot snapshot);

    Task<IReadOnlyList<DecisionSessionContinuityArtifact>> ListContinuityArtifactsAsync(Repository repository);

    Task<DecisionSessionContinuityArtifact?> ReadContinuityArtifactAsync(Repository repository, string artifactId);

    Task WriteContinuityArtifactAsync(Repository repository, DecisionSessionContinuityArtifact artifact);

    Task<IReadOnlyList<DecisionSessionTransfer>> ListTransfersAsync(Repository repository);

    Task WriteTransferAsync(Repository repository, DecisionSessionTransfer transfer);

    Task<IReadOnlyList<DecisionSessionRecoveryResult>> ListRecoveryResultsAsync(Repository repository);

    Task WriteRecoveryResultAsync(Repository repository, DecisionSessionRecoveryResult result);

    Task<IReadOnlyList<DecisionSessionCertificationReport>> ListCertificationReportsAsync(Repository repository);

    Task WriteCertificationReportAsync(Repository repository, DecisionSessionCertificationReport report);
}
