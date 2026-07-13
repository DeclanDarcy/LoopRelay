using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Application.ReadModel;
using LoopRelay.Cli.Services.Cli;

namespace LoopRelay.Cli.Services.Application;

internal sealed record CanonicalCliStatusSnapshot(
    string RepositoryPath,
    RepositoryObservation Observation,
    WorkflowResolutionResult Resolution,
    DecisionContinuityStatusSnapshot? Continuity,
    IReadOnlyList<ConsumedInputDrift> InputDrift,
    IReadOnlyList<string> PendingEffects,
    IReadOnlyList<string> PendingDispatches,
    IReadOnlyList<string> PolicyEvaluations,
    IReadOnlyList<string> Compatibility,
    IReadOnlyList<string> RequiredActions,
    CanonicalWorkspaceSnapshot? WorkspaceSnapshot = null);

internal sealed record ApplicationCommandResult(
    LoopRelay.Application.Contracts.ApplicationOutcomeKind Outcome,
    int SuggestedExitCode,
    IReadOnlyList<string> Messages,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> PendingEffects,
    IReadOnlyList<string> RequiredActions,
    CanonicalCliStatusSnapshot? Status = null);
