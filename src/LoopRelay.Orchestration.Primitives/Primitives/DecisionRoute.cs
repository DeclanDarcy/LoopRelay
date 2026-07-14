namespace LoopRelay.Orchestration.Primitives;

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
