using LoopRelay.Completion.Models.Certification;
using LoopRelay.Orchestration.Models.RepositorySlices;

namespace LoopRelay.Cli.Services.Execution;

internal sealed record ExecutionWarmSessionContinuity(
    string ProviderThreadId,
    string InputSnapshotHash,
    IReadOnlyList<string> ChangedPaths,
    int UncheckedMilestonesBefore,
    int UncheckedMilestonesAfter,
    RepositorySliceBaseline SliceBaseline,
    bool HandoffCompleted,
    DateTimeOffset RecordedAt);

internal sealed record CompletionCertificationCheckpoint(
    string CompletionDecisionIdentity,
    string? CompletionCertificateIdentity,
    string? CompletionClosurePlanIdentity,
    CompletionCertificationResult Result,
    IReadOnlyList<string> RecoveryEvidencePaths,
    DateTimeOffset RecordedAt);
