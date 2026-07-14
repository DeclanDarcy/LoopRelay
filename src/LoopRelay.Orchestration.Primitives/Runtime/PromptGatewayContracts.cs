using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Runtime;

/// <summary>
/// Versioned provider-visible rules selected by Policy Authority. The profile remains distinct
/// from invariant template text and is composed before prompt hashing.
/// </summary>
public sealed record PromptPolicyProfile
{
    public PromptPolicyProfile(PromptPolicyProfileIdentity identity, string content)
    {
        if (identity.IsEmpty || string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Prompt policy profiles require an identity and content.");
        }

        Identity = identity;
        Content = content.Trim();
    }

    public PromptPolicyProfileIdentity Identity { get; }
    public string Content { get; }
}

public interface IPromptComposer
{
    PromptComposition Compose(
        PromptTemplateIdentity template,
        string? templateSourceHash,
        PolicyIdentity policy,
        PromptPolicyProfile policyProfile,
        ConsumedInputManifestIdentity consumedInputManifest,
        IReadOnlyList<ConsumedInputFile> consumedInputs,
        IReadOnlyDictionary<string, string> renderingVariables,
        string renderedTemplate);
}

/// <summary>
/// Immutable output of template and policy composition before prompt-fact persistence.
/// Provider Runtime never receives this object or its rendered string.
/// </summary>
public sealed record PromptComposition
{
    public PromptComposition(
        PromptCompositionIdentity identity,
        PromptTemplateIdentity template,
        string? templateSourceHash,
        PolicyIdentity policy,
        PromptPolicyProfileIdentity policyProfile,
        ConsumedInputManifestIdentity consumedInputManifest,
        IReadOnlyList<ConsumedInputFile> consumedInputs,
        IReadOnlyDictionary<string, string> renderingVariables,
        string renderedContent,
        string renderedEncoding = "utf-8")
    {
        if (identity.IsEmpty || template.IsEmpty || policy.IsEmpty || policyProfile.IsEmpty ||
            consumedInputManifest.IsEmpty)
        {
            throw new ArgumentException("Prompt composition identities must not be empty.");
        }

        if (!string.Equals(renderedEncoding, "utf-8", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(renderedContent))
        {
            throw new ArgumentException("Canonical prompt content must be non-empty UTF-8 text.");
        }

        Identity = identity;
        Template = template;
        TemplateSourceHash = templateSourceHash;
        Policy = policy;
        PolicyProfile = policyProfile;
        ConsumedInputManifest = consumedInputManifest;
        ConsumedInputs = consumedInputs.ToArray();
        RenderingVariables = new Dictionary<string, string>(renderingVariables, StringComparer.Ordinal);
        RenderedContent = renderedContent;
        RenderedEncoding = "utf-8";
    }

    public PromptCompositionIdentity Identity { get; }
    public PromptTemplateIdentity Template { get; }
    public string? TemplateSourceHash { get; }
    public PolicyIdentity Policy { get; }
    public PromptPolicyProfileIdentity PolicyProfile { get; }
    public ConsumedInputManifestIdentity ConsumedInputManifest { get; }
    public IReadOnlyList<ConsumedInputFile> ConsumedInputs { get; }
    public IReadOnlyDictionary<string, string> RenderingVariables { get; }
    public string RenderedContent { get; }
    public string RenderedEncoding { get; }
}

public sealed record PromptDispatchAuthorization
{
    public PromptDispatchAuthorization(
        CanonicalCausalContext causality,
        PolicyIdentity policy,
        PromptPolicyProfileIdentity policyProfile,
        RuntimeProfileIdentity runtimeProfile,
        WorkflowTransitionIdentity transition,
        string inputSnapshotHash,
        AgentSessionIdentity? session = null,
        TurnIdentity? turn = null)
    {
        ArgumentNullException.ThrowIfNull(causality);
        if (policy.IsEmpty || policyProfile.IsEmpty || runtimeProfile.IsEmpty || transition.IsEmpty ||
            string.IsNullOrWhiteSpace(inputSnapshotHash))
        {
            throw new ArgumentException("Prompt dispatch authorization must be causally complete.");
        }

        Causality = causality;
        Policy = policy;
        PolicyProfile = policyProfile;
        RuntimeProfile = runtimeProfile;
        Transition = transition;
        InputSnapshotHash = inputSnapshotHash;
        Session = session;
        Turn = turn;
    }

    public CanonicalCausalContext Causality { get; }
    public PolicyIdentity Policy { get; }
    public PromptPolicyProfileIdentity PolicyProfile { get; }
    public RuntimeProfileIdentity RuntimeProfile { get; }
    public WorkflowTransitionIdentity Transition { get; }
    public string InputSnapshotHash { get; }
    public AgentSessionIdentity? Session { get; }
    public TurnIdentity? Turn { get; }
}

public enum PromptDispatchState
{
    Planned,
    Authorized,
    Started,
    Accepted,
    Observed,
    Unknown,
    Failed,
    Cancelled,
}

public sealed record PromptDispatchLifecycleEvent(
    PromptDispatchIdentity Dispatch,
    RenderedPromptFactIdentity Prompt,
    RenderedPromptPersistenceIdentity Persistence,
    CanonicalCausalContext Causality,
    RuntimeProfileIdentity RuntimeProfile,
    AgentSessionIdentity? Session,
    TurnIdentity? Turn,
    PromptDispatchState State,
    DateTimeOffset RecordedAt,
    IReadOnlyList<string> Evidence);

public interface IPromptDispatchLifecycleStore
{
    Task AppendAsync(PromptDispatchLifecycleEvent dispatchEvent, CancellationToken cancellationToken);
}

public interface IRenderedPromptFactReader
{
    Task<PersistedRenderedPromptFact?> ReadAsync(
        RenderedPromptFactIdentity prompt,
        CancellationToken cancellationToken);
}

/// <summary>
/// The only provider-runtime input. It contains immutable identities and authorization, never
/// rendered content. Runtime Authority loads the persisted fact by identity.
/// </summary>
public sealed record AuthorizedPromptDispatch
{
    public AuthorizedPromptDispatch(
        PromptDispatchIdentity dispatch,
        RenderedPromptFactIdentity prompt,
        RenderedPromptPersistenceIdentity persistence,
        PromptDispatchAuthorization authorization)
    {
        if (dispatch.IsEmpty || prompt.IsEmpty || persistence.IsEmpty)
        {
            throw new ArgumentException("Authorized prompt dispatch identities must not be empty.");
        }

        Dispatch = dispatch;
        Prompt = prompt;
        Persistence = persistence;
        Authorization = authorization;
    }

    public PromptDispatchIdentity Dispatch { get; }
    public RenderedPromptFactIdentity Prompt { get; }
    public RenderedPromptPersistenceIdentity Persistence { get; }
    public PromptDispatchAuthorization Authorization { get; }
}

public sealed record PreparedPromptDispatch(
    PersistedRenderedPromptFact Prompt,
    AuthorizedPromptDispatch Dispatch);

public interface IPromptRuntimeDispatcher
{
    Task<PromptExecutionResult> DispatchAsync(
        AuthorizedPromptDispatch dispatch,
        CancellationToken cancellationToken);
}

public interface IPromptDispatchGateway
{
    Task<PreparedPromptDispatch> PrepareAsync(
        PromptComposition composition,
        PromptDispatchAuthorization authorization,
        CancellationToken cancellationToken);

    Task<PromptExecutionResult> DispatchAsync(
        PreparedPromptDispatch prepared,
        CancellationToken cancellationToken);
}

public sealed class PromptDispatchUnknownException(
    AuthorizedPromptDispatch dispatch,
    Exception innerException)
    : Exception("Provider dispatch has an unknown outcome and requires reconciliation.", innerException)
{
    public AuthorizedPromptDispatch Dispatch { get; } = dispatch;
}
