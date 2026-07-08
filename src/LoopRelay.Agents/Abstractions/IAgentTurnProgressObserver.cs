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
