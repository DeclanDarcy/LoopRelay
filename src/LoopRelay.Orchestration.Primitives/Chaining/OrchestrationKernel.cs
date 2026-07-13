using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Chaining;

public readonly record struct KernelDecisionIdentity(string Value)
{
    public static KernelDecisionIdentity New() => new(CausalUlid.NewId("kerneldecision"));
}

public sealed record KernelCommand(
    WorkflowInvocation Invocation,
    RepositoryObservation Observation,
    WorkflowChainDefinition Chain,
    CanonicalWorkflowCatalogSnapshot Catalog,
    WorkflowRunContext Context,
    int ObservationBudget,
    bool Interactive = false,
    TransitionRecoveryPlan? RecoveryPlan = null);

public sealed record KernelDecisionFact(
    KernelDecisionIdentity Identity,
    string CatalogIdentity,
    string SnapshotIdentity,
    RunIdentity RootRun,
    WorkflowInstanceIdentity? WorkflowInstance,
    TransitionRunIdentity? TransitionRun,
    AttemptIdentity? Attempt,
    IReadOnlyList<string> EligibleAlternatives,
    IReadOnlyList<string> RejectedAlternatives,
    string SelectedAction,
    WorkflowStopReason Outcome,
    IReadOnlyList<string> Evidence,
    DateTimeOffset RecordedAt);

public interface IKernelDecisionStore
{
    Task AppendAsync(KernelDecisionFact decision, CancellationToken cancellationToken);
}

public interface IKernelBoundaryObserver
{
    Task ObserveAsync(KernelCommand command, WorkflowChainRunResult boundary,
        CancellationToken cancellationToken);
}

public interface IKernelAttemptAuthorizationSelector
{
    Task<AttemptAuthorization> SelectAsync(KernelCommand command, int cycle,
        CancellationToken cancellationToken);
}

public sealed class DurableKernelAttemptAuthorizationSelector : IKernelAttemptAuthorizationSelector
{
    public Task<AttemptAuthorization> SelectAsync(KernelCommand command, int cycle,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AttemptAuthorization authorization = cycle == 0 && command.RecoveryPlan is not null
            ? new RecoveryAttemptAuthorization(command.RecoveryPlan)
            : FreshAttemptAuthorization.Instance;
        return Task.FromResult(authorization);
    }
}

public sealed record KernelResult(
    RuntimeOutcomeKind Outcome,
    WorkflowStopReason StopReason,
    RunIdentity RootRun,
    WorkflowInstanceIdentity? WorkflowInstance,
    TransitionRunIdentity? TransitionRun,
    AttemptIdentity? Attempt,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> PendingEffects,
    string? RecoveryCase,
    string? InteractionRequest,
    string SnapshotIdentity,
    WorkflowChainRunResult? ChainResult,
    string Explanation);

public sealed class OrchestrationKernel(
    WorkflowChainRunner _chains,
    ICanonicalRepositoryObservationSource _observations,
    IKernelAttemptAuthorizationSelector _authorizations,
    IKernelDecisionStore _decisions,
    IReadOnlyList<IKernelBoundaryObserver>? _boundaryObservers = null)
{
    public async Task<KernelResult> RunAsync(KernelCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.ObservationBudget <= 0)
            throw new ArgumentOutOfRangeException(nameof(command), "Observation budget must be positive.");
        RepositoryObservation observation = command.Observation;
        WorkflowChainRunResult? last = null;
        for (int cycle = 0; cycle < command.ObservationBudget; cycle++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AttemptAuthorization authorization = await _authorizations.SelectAsync(command, cycle, cancellationToken);
            last = await _chains.RunAsync(new WorkflowChainRunRequest(command.Invocation, observation,
                command.Chain, command.Catalog.Workflows, command.Context, authorization, command.Interactive),
                cancellationToken);
            string snapshot = Snapshot(observation, command.Catalog.Identity);
            TransitionEligibility[] alternatives = last.ControllerResult?.Resolution.TransitionEligibility.ToArray() ?? [];
            await _decisions.AppendAsync(new KernelDecisionFact(KernelDecisionIdentity.New(), command.Catalog.Identity,
                snapshot, command.Context.Run, last.Decision.CurrentWorkflowInstance,
                last.ControllerResult?.Transition?.TransitionRun, last.ControllerResult?.Transition?.Attempt,
                alternatives.Where(item => item.State == TransitionEligibilityState.Eligible)
                    .Select(item => item.Transition.Value).ToArray(),
                alternatives.Where(item => item.State != TransitionEligibilityState.Eligible)
                    .Select(item => $"{item.Transition}:{item.State}:{string.Join('|', item.Evidence)}").ToArray(),
                last.ControllerResult?.Transition?.Transition.Value ?? last.StopReason.ToString(),
                last.StopReason, last.Decision.Evidence, DateTimeOffset.UtcNow), cancellationToken);
            foreach (IKernelBoundaryObserver observer in _boundaryObservers ?? [])
                await observer.ObserveAsync(command, last, cancellationToken);
            if (last.StopReason != WorkflowStopReason.TransitionCompleted)
                return Result(last, snapshot);
            observation = await _observations.ObserveAsync(cancellationToken);
        }

        string passiveSnapshot = Snapshot(observation, command.Catalog.Identity);
        return new KernelResult(RuntimeOutcomeKind.Waiting, WorkflowStopReason.Waiting, command.Context.Run,
            last?.Decision.CurrentWorkflowInstance, last?.ControllerResult?.Transition?.TransitionRun,
            last?.ControllerResult?.Transition?.Attempt,
            last?.Decision.Evidence ?? [], [], null, null, passiveSnapshot, last,
            $"Observation budget {command.ObservationBudget} exhausted; next eligible work remains passive.");
    }

    private static KernelResult Result(WorkflowChainRunResult result, string snapshot)
    {
        RuntimeOutcomeKind outcome = result.ControllerResult?.Transition?.Outcome ?? result.StopReason switch
        {
            WorkflowStopReason.ChainCompleted or WorkflowStopReason.BoundedWorkflowCompleted => RuntimeOutcomeKind.Completed,
            WorkflowStopReason.RequiredEffectsPending => RuntimeOutcomeKind.EffectsPending,
            WorkflowStopReason.RecoveryRequired => RuntimeOutcomeKind.RecoveryRequired,
            WorkflowStopReason.WaitingForInteraction => RuntimeOutcomeKind.HumanDecisionRequired,
            WorkflowStopReason.Ambiguous => RuntimeOutcomeKind.Ambiguous,
            WorkflowStopReason.Cancelled => RuntimeOutcomeKind.Cancelled,
            WorkflowStopReason.Failed => RuntimeOutcomeKind.Failed,
            WorkflowStopReason.Stalled => RuntimeOutcomeKind.Stalled,
            WorkflowStopReason.MissingRequiredInput => RuntimeOutcomeKind.MissingRequiredInput,
            _ => RuntimeOutcomeKind.Waiting,
        };
        string[] pending = result.ControllerResult?.EffectCoordination?.Evidence.ToArray() ?? [];
        return new KernelResult(outcome, result.StopReason, result.Decision.Run,
            result.Decision.CurrentWorkflowInstance, result.ControllerResult?.Transition?.TransitionRun,
            result.ControllerResult?.Transition?.Attempt, result.Decision.Evidence, pending,
            result.StopReason == WorkflowStopReason.RecoveryRequired
                ? result.ControllerResult?.Transition?.TransitionRun?.Value : null,
            result.StopReason == WorkflowStopReason.WaitingForInteraction
                ? result.Decision.Evidence.FirstOrDefault() : null,
            snapshot, result, result.Explanation);
    }

    private static string Snapshot(RepositoryObservation observation, string catalogIdentity)
    {
        string json = JsonSerializer.Serialize(new
        {
            catalogIdentity,
            observation.StorageAuthority,
            workflows = observation.WorkflowStates.Select(item => new { item.Workflow, item.State, item.CurrentStage }),
            products = observation.Products.Select(item => new { item.Product.Identity, item.Product.CausalIdentity,
                item.Product.Freshness, item.Product.ValidationState, item.GateUsable }),
            transitions = observation.TransitionRuns.Select(item => new { item.Workflow, item.Stage, item.Transition, item.State }),
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
