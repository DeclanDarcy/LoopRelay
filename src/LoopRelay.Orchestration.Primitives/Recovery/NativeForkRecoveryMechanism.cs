using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Orchestration.Recovery;

public sealed class NativeForkRecoveryMechanism : IRecoveryMechanism
{
    public RecoveryMechanismKey Key { get; } = new("NativeFork", "1");
    public string LineageMechanism => "NativeFork";
    public bool RequiresContextInjection => false;
    public RecoveryActivationStrategy ActivationStrategy => RecoveryActivationStrategy.NativeClone;
    public string ValidationStrategy => "exact-parent-child-and-certified-fidelity.v1";
    public string ReconciliationStrategy => "enumerate-exact-parent-children-by-correlation.v1";

    public RecoveryRuntimeOutcome SuccessOutcome(RecoveryCompleteness completeness) =>
        RecoveryRuntimeOutcome.ReplacementNativeFork;

    public RecoveryMechanismEligibility EvaluateEligibility(RecoveryPlanningInput input)
    {
        SessionOperationSupportDescriptor fork = input.Profile.Operation(SessionContinuityOperation.Fork);
        if (fork.Status != SessionOperationSupport.Supported)
        {
            return Ineligible("ForkSupport is not Supported.");
        }
        if (fork.ResultContract is "unknown" or "none"
            || fork.ReconciliationStrategy is "unknown" or "none")
        {
            return Ineligible("Stable parent/child identity or unknown-response reconciliation is uncertified.");
        }
        if (input.Failure.TurnSubmitted
            || input.Failure.Classification is not ("UnavailableSession" or "CorruptedState"))
        {
            return Ineligible("The structured resume failure is not replacement eligible.");
        }

        return new RecoveryMechanismEligibility(
            true,
            RecoveryCompleteness.Full,
            [
                $"profile={input.Profile.Digest}",
                $"fork-result={fork.ResultContract}",
                $"fork-reconciliation={fork.ReconciliationStrategy}",
            ]);
    }

    public async Task<RecoveryMechanismExecutionResult> ExecuteAsync(
        RecoveryMechanismExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Plan.Mechanism != Key || request.Phase != RecoveryMechanismExecutionPhase.CreateReplacement)
        {
            return KnownFailure("Native fork only handles the persisted CreateReplacement phase.");
        }
        if (request.CreateRequest.Profile.Operation(SessionContinuityOperation.Fork).Status
            != SessionOperationSupport.Supported)
        {
            return KnownFailure("The persisted profile does not support native fork; no provider request was emitted.");
        }

        SessionForkResult fork = await request.ContinuityRuntime.ForkSessionAsync(
            new SessionForkRequest(
                request.CreateRequest.SessionSpec,
                request.Original,
                request.CreateRequest.Profile,
                request.Plan.IdempotencyIdentity,
                request.CreateRequest.Timeout),
            cancellationToken);
        if (!fork.Succeeded || fork.Session is null || fork.Child is null)
        {
            return new RecoveryMechanismExecutionResult(
                fork.Transport?.RequestSubmitted == true
                    ? RecoveryMechanismExecutionStatus.UnknownOutcome
                    : RecoveryMechanismExecutionStatus.KnownFailure,
                null, null, null, null, fork.Failure,
                "Native fork did not produce a verified child.", fork);
        }

        return new RecoveryMechanismExecutionResult(
            RecoveryMechanismExecutionStatus.Succeeded,
            fork.Session,
            fork.Child,
            null,
            null,
            null,
            null,
            fork);
    }

    public async Task<RecoveryMechanismExecutionResult> ReconcileAsync(
        RecoveryMechanismExecutionRequest request,
        RecoveryMechanismExecutionResult previous,
        CancellationToken cancellationToken = default)
    {
        SessionReconcileResult reconciliation = await request.ContinuityRuntime.ReconcileAsync(
            new SessionReconcileRequest(
                SessionContinuityOperation.Fork,
                request.Plan.IdempotencyIdentity,
                request.CreateRequest.Profile,
                request.CreateRequest.SessionSpec,
                request.Original),
            cancellationToken);
        ProviderSessionReference[] candidates = reconciliation.Candidates?.Distinct().ToArray()
            ?? (reconciliation.Session is null ? [] : [reconciliation.Session]);
        return candidates.Length switch
        {
            0 => previous with
            {
                Status = RecoveryMechanismExecutionStatus.KnownFailure,
                Failure = reconciliation.Failure,
                Diagnostic = "Fork reconciliation proved that no child exists.",
            },
            1 => previous with
            {
                Status = RecoveryMechanismExecutionStatus.Succeeded,
                Replacement = candidates[0],
                Failure = reconciliation.Failure,
                Diagnostic = "A unique child was found and must be reopened before activation.",
                Fork = new SessionForkResult(
                    true, null, request.Original, candidates[0], null,
                    HistoryDigest: request.Plan.OperationConstraints.TryGetValue("fork-history-digest", out string? digest)
                        ? digest
                        : null),
            },
            _ => previous with
            {
                Status = RecoveryMechanismExecutionStatus.UnknownOutcome,
                Failure = reconciliation.Failure,
                Diagnostic = "Fork reconciliation found multiple candidate children.",
            },
        };
    }

    public Task<RecoveryMechanismValidationResult> ValidateAsync(
        RecoveryMechanismExecutionRequest request,
        RecoveryMechanismExecutionResult result,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool valid = result.Fork is { Succeeded: true, Child: not null } fork
            && fork.Parent == request.Original
            && fork.Child == result.Replacement
            && result.Session?.ThreadId == result.Replacement?.ThreadId;
        return Task.FromResult(new RecoveryMechanismValidationResult(
            valid,
            result.Status == RecoveryMechanismExecutionStatus.UnknownOutcome,
            valid ? null : "Native fork parent/child identity or certified fidelity evidence did not validate."));
    }

    private static RecoveryMechanismEligibility Ineligible(string evidence) =>
        new(false, RecoveryCompleteness.Unknown, [evidence]);

    private static RecoveryMechanismExecutionResult KnownFailure(string diagnostic) =>
        new(RecoveryMechanismExecutionStatus.KnownFailure, null, null, null, null, null, diagnostic);
}
