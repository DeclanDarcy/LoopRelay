namespace LoopRelay.Core.Models.Identity;

/// <summary>
/// Prefixed-ULID identities forming the causal spine:
/// workspace → run → workflow instance → transition run → attempt → agent session → turn.
/// Values are opaque TEXT; legacy 32-hex ids coexist in existing databases.
/// </summary>
public readonly record struct WorkspaceIdentity(string Value)
{
    public static WorkspaceIdentity New() => new(CausalUlid.NewId("ws"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct RunIdentity(string Value)
{
    public static RunIdentity New() => new(CausalUlid.NewId("run"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct WorkflowInstanceIdentity(string Value)
{
    public static WorkflowInstanceIdentity New() => new(CausalUlid.NewId("wfi"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct TransitionRunIdentity(string Value)
{
    public static TransitionRunIdentity New() => new(CausalUlid.NewId("tr"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct AttemptIdentity(string Value)
{
    public static AttemptIdentity New() => new(CausalUlid.NewId("att"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct AgentSessionIdentity(string Value)
{
    public static AgentSessionIdentity New() => new(CausalUlid.NewId("ses"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct TurnIdentity(string Value)
{
    public static TurnIdentity New() => new(CausalUlid.NewId("turn"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct PolicyIdentity(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct RuntimeProfileIdentity(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct HistoryFactIdentity(string Value)
{
    public static HistoryFactIdentity New() => new(CausalUlid.NewId("hfact"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct HistoryEvidenceSetIdentity(string Value)
{
    public static HistoryEvidenceSetIdentity New() => new(CausalUlid.NewId("evset"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct HistoryEvidenceItemIdentity(string Value)
{
    public static HistoryEvidenceItemIdentity New() => new(CausalUlid.NewId("evi"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct RenderedPromptFactIdentity(string Value)
{
    public static RenderedPromptFactIdentity New() => new(CausalUlid.NewId("rpf"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct RenderedPromptPersistenceIdentity(string Value)
{
    public static RenderedPromptPersistenceIdentity New() => new(CausalUlid.NewId("rpp"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct PromptDispatchIdentity(string Value)
{
    public static PromptDispatchIdentity New() => new(CausalUlid.NewId("dispatch"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct PromptCompositionIdentity(string Value)
{
    public static PromptCompositionIdentity New() => new(CausalUlid.NewId("promptcomp"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct DecisionProductVersionIdentity(string Value)
{
    public static DecisionProductVersionIdentity New() => new(CausalUlid.NewId("decision"));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct ExecutionRecommendationIdentity(string Value)
{
    public static ExecutionRecommendationIdentity New() => new(CausalUlid.NewId("recommendation"));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct RuntimeProfileEvaluationIdentity(string Value)
{
    public static RuntimeProfileEvaluationIdentity New() => new(CausalUlid.NewId("profileeval"));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct ProviderCapabilityEvidenceIdentity(string Value)
{
    public static ProviderCapabilityEvidenceIdentity New() => new(CausalUlid.NewId("capability"));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct ExecutionAuthorizationIdentity(string Value)
{
    public static ExecutionAuthorizationIdentity New() => new(CausalUlid.NewId("execauth"));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct ResumePolicyIdentity(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct ConsumedInputManifestIdentity(string Value)
{
    public static ConsumedInputManifestIdentity New() => new(CausalUlid.NewId("inputs"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct PromptTemplateIdentity(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct PromptPolicyProfileIdentity(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct ContinuityLineageIdentity(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct RecoveryAttemptIdentity(string Value)
{
    public static RecoveryAttemptIdentity New() => new(CausalUlid.NewId("recovery"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct RecoveryCaseIdentity(string Value)
{
    public static RecoveryCaseIdentity New() => new(CausalUlid.NewId("recoverycase"));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct RecoveryClassificationIdentity(string Value)
{
    public static RecoveryClassificationIdentity New() => new(CausalUlid.NewId("recoveryclass"));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct RecoveryPlanIdentity(string Value)
{
    public static RecoveryPlanIdentity New() => new(CausalUlid.NewId("recoveryplan"));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct RecoveryActionIdentity(string Value)
{
    public static RecoveryActionIdentity New() => new(CausalUlid.NewId("recoveryaction"));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct InteractionRequestIdentity(string Value)
{
    public static InteractionRequestIdentity New() => new(CausalUlid.NewId("interaction"));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct InteractionResponseIdentity(string Value)
{
    public static InteractionResponseIdentity New() => new(CausalUlid.NewId("interactionresponse"));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct InteractionEventIdentity(string Value)
{
    public static InteractionEventIdentity New() => new(CausalUlid.NewId("interactionevent"));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct InteractionPolicyEvaluationIdentity(string Value)
{
    public static InteractionPolicyEvaluationIdentity New() => new(CausalUlid.NewId("interactionpolicy"));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}
