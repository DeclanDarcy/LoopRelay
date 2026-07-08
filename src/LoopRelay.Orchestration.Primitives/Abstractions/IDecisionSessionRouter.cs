using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Primitives;

namespace LoopRelay.Orchestration.Abstractions;

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
