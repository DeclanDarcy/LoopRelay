using LoopRelay.Agents.Models;

namespace LoopRelay.Agents.Services;

internal sealed class AgentProcessStateMachine(AgentProcessState initialState = AgentProcessState.Created)
{
    public AgentProcessState State { get; private set; } = initialState;

    public void TransitionTo(AgentProcessState nextState)
    {
        if (!CanTransition(State, nextState))
        {
            throw new InvalidOperationException(
                $"Agent process cannot transition from {State} to {nextState}.");
        }

        State = nextState;
    }

    public bool TryTransitionTo(AgentProcessState nextState)
    {
        if (!CanTransition(State, nextState))
        {
            return false;
        }

        State = nextState;
        return true;
    }

    private static bool CanTransition(AgentProcessState currentState, AgentProcessState nextState)
    {
        if (currentState == nextState)
        {
            return true;
        }

        return currentState switch
        {
            AgentProcessState.Created => nextState is AgentProcessState.Starting or AgentProcessState.Running or AgentProcessState.Disposed,
            AgentProcessState.Starting => nextState is AgentProcessState.Running or AgentProcessState.Failed or AgentProcessState.Canceled or AgentProcessState.Disposed,
            AgentProcessState.Running => nextState is AgentProcessState.Stopping or AgentProcessState.Exited or AgentProcessState.Failed or AgentProcessState.Canceled or AgentProcessState.Disposed,
            AgentProcessState.Stopping => nextState is AgentProcessState.Exited or AgentProcessState.Failed or AgentProcessState.Canceled or AgentProcessState.Disposed,
            AgentProcessState.Exited => nextState is AgentProcessState.Disposed,
            AgentProcessState.Failed => nextState is AgentProcessState.Disposed,
            AgentProcessState.Canceled => nextState is AgentProcessState.Disposed,
            AgentProcessState.Disposed => false,
            _ => false
        };
    }
}
