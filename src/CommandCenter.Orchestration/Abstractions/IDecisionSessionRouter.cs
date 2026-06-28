using CommandCenter.Orchestration.Models;

namespace CommandCenter.Orchestration.Abstractions;

/// <summary>
/// The route for the next decision turn after an operational continuation (m7). This is the token-pressure
/// verdict; the orchestrator separately applies its own transfer-eligibility gate (a primed Decision process
/// + operational context must exist) before honouring a <see cref="Transfer"/>, so it only ever acts on a
/// route it is safe to execute.
/// </summary>
public enum DecisionRoute
{
    /// <summary>Reuse the warm Decision process and propose the next decisions against it.</summary>
    Continue,

    /// <summary>Recycle the Decision process: extract an operational delta, rewrite operational context,
    /// close the old process, and seed a fresh one from the rewritten context before proposing.</summary>
    Transfer
}

/// <summary>
/// The decision-session lifecycle router the continuation loop consults after every handoff rotation (m7).
/// It is deliberately registry-free: it routes on the loop's OWN decision-session token pressure
/// (<see cref="RouterInputs"/> — observed accounting where available, a deterministic estimate as the
/// fallback) rather than on the registry-backed DecisionSessions lifecycle policy, which is part of the
/// separate, registry-keyed subsystem the Plan-Authoring loop does not populate. Pure and synchronous: the
/// orchestrator owns the inputs and the eligibility gate, so routing can never throw or strand the loop.
/// </summary>
public interface IDecisionSessionRouter
{
    /// <summary>
    /// Returns <see cref="DecisionRoute.Transfer"/> when decision-session token pressure has crossed the
    /// configured transfer threshold, otherwise <see cref="DecisionRoute.Continue"/>.
    /// </summary>
    DecisionRoute Evaluate(RouterInputs inputs);
}
