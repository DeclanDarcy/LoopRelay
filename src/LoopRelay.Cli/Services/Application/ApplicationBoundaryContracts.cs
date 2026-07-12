using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Cli.Services.Cli;

namespace LoopRelay.Cli.Services.Application;

internal abstract record ApplicationRequest(UnifiedCliInvocation Invocation);
internal sealed record RunWorkflowCommand(UnifiedCliInvocation Invocation) : ApplicationRequest(Invocation);
internal sealed record StatusQuery(UnifiedCliInvocation Invocation) : ApplicationRequest(Invocation);
internal sealed record StorageVerifyCommand(UnifiedCliInvocation Invocation) : ApplicationRequest(Invocation);
internal sealed record StorageInitCommand(UnifiedCliInvocation Invocation) : ApplicationRequest(Invocation);
internal sealed record StorageImportCommand(UnifiedCliInvocation Invocation) : ApplicationRequest(Invocation);
internal sealed record StorageExportCommand(UnifiedCliInvocation Invocation) : ApplicationRequest(Invocation);
internal sealed record StorageSyncCommand(UnifiedCliInvocation Invocation) : ApplicationRequest(Invocation);
internal sealed record RecoveryResumeCommand(UnifiedCliInvocation Invocation) : ApplicationRequest(Invocation);
internal sealed record RecoveryReconcileCommand(UnifiedCliInvocation Invocation) : ApplicationRequest(Invocation);

internal enum ApplicationOutcome
{
    Completed,
    Waiting,
    Failed,
    Stalled,
    CannotProceed,
    Cancelled,
}

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
    IReadOnlyList<string> RequiredActions);

internal sealed record ApplicationCommandResult(
    ApplicationOutcome Outcome,
    int SuggestedExitCode,
    IReadOnlyList<string> Messages,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> PendingEffects,
    IReadOnlyList<string> RequiredActions,
    CanonicalCliStatusSnapshot? Status = null);

internal interface ILoopRelayApplication
{
    Task<ApplicationCommandResult> ExecuteAsync(
        ApplicationRequest request,
        CancellationToken cancellationToken = default);
}

internal static class ApplicationRequestFactory
{
    public static ApplicationRequest Create(UnifiedCliInvocation invocation) => invocation.Command.Kind switch
    {
        UnifiedCliCommandKind.Status => new StatusQuery(invocation),
        UnifiedCliCommandKind.StorageVerify => new StorageVerifyCommand(invocation),
        UnifiedCliCommandKind.StorageInit => new StorageInitCommand(invocation),
        UnifiedCliCommandKind.StorageImport => new StorageImportCommand(invocation),
        UnifiedCliCommandKind.StorageExport => new StorageExportCommand(invocation),
        UnifiedCliCommandKind.StorageSync => new StorageSyncCommand(invocation),
        _ => new RunWorkflowCommand(invocation),
    };
}
