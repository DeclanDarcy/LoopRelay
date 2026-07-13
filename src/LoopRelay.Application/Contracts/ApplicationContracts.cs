namespace LoopRelay.Application.Contracts;

public readonly record struct ApplicationCorrelationId(string Value)
{
    public static ApplicationCorrelationId New() => new(Guid.NewGuid().ToString("N"));
}

public sealed record ApplicationRequestContext(
    ApplicationCorrelationId Correlation,
    string WorkspaceIdentity,
    string RepositoryPath,
    IReadOnlyDictionary<string, string> PolicyOverrides,
    int? ObservationBudget = null,
    bool Interactive = false);

public abstract record LoopRelayRequest(ApplicationRequestContext Context);

public enum RunInvocationMode { Default, ForcedTraditional, ForcedEval, BoundedWorkflow }
public sealed record RunWorkflowRequest(
    ApplicationRequestContext Context,
    RunInvocationMode Mode,
    string? Workflow = null) : LoopRelayRequest(Context);
public sealed record CanonicalStatusRequest(
    ApplicationRequestContext Context,
    RunInvocationMode Mode = RunInvocationMode.Default,
    string? Workflow = null) : LoopRelayRequest(Context);

public enum StorageOperationKind { Verify, Initialize, Migrate, Export, Sync }
public sealed record StorageOperationRequest(
    ApplicationRequestContext Context,
    StorageOperationKind Operation,
    string? Target = null) : LoopRelayRequest(Context);

public enum ImportOperationKind { Detect, Preview, Execute, Verify }
public sealed record ImportOperationRequest(
    ApplicationRequestContext Context,
    ImportOperationKind Operation,
    string? ImportIdentity = null) : LoopRelayRequest(Context);

public enum RecoveryOperationKind { Inspect, Plan, Execute }
public sealed record RecoveryOperationRequest(
    ApplicationRequestContext Context,
    RecoveryOperationKind Operation,
    string? RecoveryIdentity = null) : LoopRelayRequest(Context);

public enum InteractionOperationKind { List, Show, Respond, Cancel }
public sealed record InteractionOperationRequest(
    ApplicationRequestContext Context,
    InteractionOperationKind Operation,
    string? RequestIdentity = null,
    string? ResponseDocument = null) : LoopRelayRequest(Context);

public enum CompletionOperationKind { Status, Reconcile }
public sealed record CompletionOperationRequest(
    ApplicationRequestContext Context,
    CompletionOperationKind Operation,
    string? ClosurePlanIdentity = null) : LoopRelayRequest(Context);

public sealed record CapabilityDiagnosticsRequest(
    ApplicationRequestContext Context,
    bool IncludePrerequisites = true) : LoopRelayRequest(Context);

public enum ApplicationOutcomeKind
{
    Completed,
    Waiting,
    EffectsPending,
    RecoveryRequired,
    HumanDecisionRequired,
    MissingRequiredInput,
    DirtyInputSurface,
    UnversionedInputSurface,
    StorageUnusable,
    MissingRuntimePrerequisite,
    UnsupportedProviderCapability,
    CompatibilityImportRequired,
    ConcurrentStateConflict,
    InputInvalidated,
    NoEligibleTransition,
    Ambiguous,
    Stalled,
    Failed,
    Cancelled,
    SpecificCannotProceed,
}

public sealed record LoopRelayResult(
    ApplicationCorrelationId Correlation,
    ApplicationOutcomeKind Outcome,
    string Reason,
    int SuggestedExitCode,
    IReadOnlyList<string> Messages,
    IReadOnlyList<string> Errors,
    IReadOnlyDictionary<string, string> CausalIdentities,
    IReadOnlyList<string> EvidenceIdentities,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> PendingEffects,
    IReadOnlyList<string> RecoveryIdentities,
    IReadOnlyList<string> InteractionIdentities,
    IReadOnlyList<string> RequiredActions,
    string? SnapshotIdentity = null,
    object? Payload = null);

public interface ILoopRelayApplication
{
    Task<LoopRelayResult> ExecuteAsync(
        LoopRelayRequest request,
        CancellationToken cancellationToken = default);
}

public interface IApplicationUseCaseDispatcher
{
    Task<LoopRelayResult> DispatchAsync(
        LoopRelayRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class LoopRelayApplication(IApplicationUseCaseDispatcher _dispatcher)
    : ILoopRelayApplication
{
    public Task<LoopRelayResult> ExecuteAsync(
        LoopRelayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.Context.Correlation.Value) ||
            string.IsNullOrWhiteSpace(request.Context.WorkspaceIdentity) ||
            !Path.IsPathFullyQualified(request.Context.RepositoryPath))
            throw new ArgumentException("Application requests require correlation, workspace, and absolute repository identities.");
        return _dispatcher.DispatchAsync(request, cancellationToken);
    }
}

public sealed record ApplicationStartupFailure(
    IReadOnlyList<string> MissingOwners,
    IReadOnlyList<string> DuplicateOwners,
    IReadOnlyList<string> VersionIncompatibleOwners)
{
    public bool IsValid => MissingOwners.Count == 0 && DuplicateOwners.Count == 0 &&
        VersionIncompatibleOwners.Count == 0;
}

public static class ApplicationCompositionValidator
{
    public static ApplicationStartupFailure Validate(
        IReadOnlyList<(string Owner, string Version)> registrations,
        IReadOnlyDictionary<string, string> required)
    {
        string[] missing = required.Keys.Except(registrations.Select(item => item.Owner), StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        string[] duplicate = registrations.GroupBy(item => item.Owner, StringComparer.Ordinal)
            .Where(group => group.Count() != 1).Select(group => group.Key).Order(StringComparer.Ordinal).ToArray();
        string[] incompatible = registrations.Where(item =>
                required.TryGetValue(item.Owner, out string? version) && version != item.Version)
            .Select(item => $"{item.Owner}:required={required[item.Owner]}:actual={item.Version}")
            .Order(StringComparer.Ordinal).ToArray();
        return new(missing, duplicate, incompatible);
    }
}
