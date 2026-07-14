namespace LoopRelay.Cli.Services.Planning;

internal sealed record PlanWarmSessionContinuity(
    string ProviderThreadId,
    string SessionIdentity,
    string TurnIdentity,
    string ExactRuntimeProfileIdentity,
    string PromptFactIdentity,
    IReadOnlyList<string> InputReceiptIdentities,
    string InputSnapshotHash,
    string PlanHash,
    string PromptIdentity,
    string CatalogIdentity,
    string RunIdentity,
    string WorkflowInstanceIdentity,
    string TransitionRunIdentity,
    string AttemptIdentity,
    DateTimeOffset RecordedAt);
