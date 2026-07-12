namespace LoopRelay.Core.Models.Identity;

/// <summary>
/// The LoopRelay-owned attempt-level causal spine. Provider identifiers and derived recovery
/// identifiers are evidence attached to this context; they never replace these identities.
/// </summary>
public sealed record CanonicalCausalContext
{
    public CanonicalCausalContext(
        WorkspaceIdentity workspace,
        RunIdentity run,
        WorkflowInstanceIdentity workflowInstance,
        TransitionRunIdentity transitionRun,
        AttemptIdentity attempt,
        AgentSessionIdentity? session = null,
        TurnIdentity? turn = null)
    {
        Require(workspace.IsEmpty, nameof(workspace));
        Require(run.IsEmpty, nameof(run));
        Require(workflowInstance.IsEmpty, nameof(workflowInstance));
        Require(transitionRun.IsEmpty, nameof(transitionRun));
        Require(attempt.IsEmpty, nameof(attempt));
        if (session is { IsEmpty: true })
        {
            throw new ArgumentException("Session identity must not be empty when supplied.", nameof(session));
        }

        if (turn is { IsEmpty: true })
        {
            throw new ArgumentException("Turn identity must not be empty when supplied.", nameof(turn));
        }

        if (turn is not null && session is null)
        {
            throw new ArgumentException("A canonical turn must belong to a canonical session.", nameof(turn));
        }

        Workspace = workspace;
        Run = run;
        WorkflowInstance = workflowInstance;
        TransitionRun = transitionRun;
        Attempt = attempt;
        Session = session;
        Turn = turn;
    }

    public WorkspaceIdentity Workspace { get; }

    public RunIdentity Run { get; }

    public WorkflowInstanceIdentity WorkflowInstance { get; }

    public TransitionRunIdentity TransitionRun { get; }

    public AttemptIdentity Attempt { get; }

    public AgentSessionIdentity? Session { get; }

    public TurnIdentity? Turn { get; }

    public bool BelongsToSameAttempt(CanonicalCausalContext other) =>
        Workspace == other.Workspace &&
        Run == other.Run &&
        WorkflowInstance == other.WorkflowInstance &&
        TransitionRun == other.TransitionRun &&
        Attempt == other.Attempt;

    public void RequireSameAttempt(CanonicalCausalContext other, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!BelongsToSameAttempt(other))
        {
            throw new ArgumentException(
                "Causal identities belong to different transition attempts.",
                parameterName);
        }
    }

    public CanonicalCausalContext WithSession(AgentSessionIdentity session) =>
        new(Workspace, Run, WorkflowInstance, TransitionRun, Attempt, session);

    public CanonicalCausalContext WithTurn(AgentSessionIdentity session, TurnIdentity turn) =>
        new(Workspace, Run, WorkflowInstance, TransitionRun, Attempt, session, turn);

    private static void Require(bool isEmpty, string parameterName)
    {
        if (isEmpty)
        {
            throw new ArgumentException("Canonical identity must not be empty.", parameterName);
        }
    }
}
