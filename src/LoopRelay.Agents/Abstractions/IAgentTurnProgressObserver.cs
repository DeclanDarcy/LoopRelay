namespace LoopRelay.Agents.Abstractions;

/// <summary>
/// Best-effort observer for transport phases a runtime can see while an agent turn is waiting for output.
/// Implementations must be treated as non-critical; progress faults must never break a turn.
/// </summary>
public interface IAgentTurnProgressObserver
{
    void RequestWriteStarted();

    void RequestSubmitted();

    void RequestAccepted();

    void FirstProtocolEvent();

    void FirstOutput();
}

public static class AgentTurnProgress
{
    private static readonly AsyncLocal<IAgentTurnProgressObserver?> CurrentObserver = new();

    public static IAgentTurnProgressObserver? Current => CurrentObserver.Value;

    public static IDisposable Use(IAgentTurnProgressObserver? observer)
    {
        IAgentTurnProgressObserver? previous = CurrentObserver.Value;
        CurrentObserver.Value = observer;
        return new Scope(previous);
    }

    public static void Notify(Action<IAgentTurnProgressObserver> notification) =>
        Notify(CurrentObserver.Value, notification);

    public static void Notify(
        IAgentTurnProgressObserver? observer,
        Action<IAgentTurnProgressObserver> notification)
    {
        if (observer is null)
        {
            return;
        }

        try
        {
            notification(observer);
        }
        catch
        {
            // Progress is informational. Transport and turn execution must remain fail-open.
        }
    }

    private sealed class Scope(IAgentTurnProgressObserver? previous) : IDisposable
    {
        public void Dispose() => CurrentObserver.Value = previous;
    }
}
