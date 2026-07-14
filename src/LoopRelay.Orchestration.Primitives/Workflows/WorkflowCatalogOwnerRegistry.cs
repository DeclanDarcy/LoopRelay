using LoopRelay.Orchestration.Services;

namespace LoopRelay.Orchestration.Workflows;

public static class WorkflowCatalogOwnerRegistry
{
    public const string ValidatorOwner = "WorkflowCatalog.ValidatorAuthority";

    private static readonly HashSet<string> Capabilities =
    [
        "agent.one-shot-prompt", "agent.persistent-session", "agent.warm-session",
        "agent.decision-session", "agent.read-only-prompt", "artifact.scoped-mutation",
    ];

    private static readonly HashSet<string> Interactions =
    [
        "DirtyInputCommitOffer", "ImportConflict", "RecoveryAmbiguity", "CompletionAmbiguity",
    ];

    private static readonly HashSet<string> RecoveryStrategies =
    [
        "restart", "resume", "rerun",
    ];

    private static readonly HashSet<string> InlinePromptHandlers =
    [
        "ContinueDecisionSession", "CreateArchitecturalCatalog", "CreateEvalDag",
        "CreateEvalDependencyInventory", "CreateEvalHypothesisInventory", "CreateNextEpicImplementationSpec",
        "CreateNextEpicRoadmap", "EvaluateCommit", "EvaluateMilestoneCompletion", "ExecuteImplementationSlice",
        "GenerateDecision", "InterpretCompletionRoute", "PublishRepositoryState", "RunCompletionCertification",
        "RetireEpic", "RunNonImplementationReview", "SeedOperationalContext", "SelectEvaluationIntent", "TransferDecisionSession",
        "UpdateDependencyInventory", "UpdateHypothesisInventory", "UpdateRoadmap", "VerifyExecuteEntryContract",
        "VerifyExecutionReadiness", "VerifyPlanEntryContract", "VerifyWorkflowExitGate",
    ];

    public static bool Owns(ValidatorReference validator) =>
        !validator.Identity.IsEmpty && validator.Owner == ValidatorOwner;

    public static bool Supports(RuntimeCapabilityIdentity capability) =>
        !capability.IsEmpty && Capabilities.Contains(capability.Value);

    public static bool OwnsInteraction(string category) => Interactions.Contains(category);

    public static bool OwnsRecoveryStrategy(string strategy) => RecoveryStrategies.Contains(strategy);

    public static bool OwnsEffect(EffectCategory category) => Enum.IsDefined(category);

    public static bool OwnsPrompt(string identity, string version)
    {
        CanonicalPromptAsset? asset = CanonicalPromptAssetCatalog.Assets
            .SingleOrDefault(item => item.PromptIdentity == identity);
        return asset is not null
            ? string.Equals(asset.SourceHash, version, StringComparison.Ordinal)
            : InlinePromptHandlers.Contains(identity) && version == "catalog-inline-v1";
    }
}
