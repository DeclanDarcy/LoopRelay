using LoopRelay.Agents.Abstractions;

namespace LoopRelay.Agents.Services.Sessions;
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
