# Decisions

## Newly Authorized

- M1 is functionally complete based on the implemented deterministic `DecisionContext`, stable context fingerprinting, snapshot persistence, source diagnostics, endpoint coverage, and backend verification.
- M1 is not fully closed until a downstream lifecycle service consumes `IDecisionContextService` as its repository-information boundary.
- M2 may begin with a first discovery vertical slice, but that slice must also serve as the dependency-inversion proof required to close M1.
- `IDecisionDiscoveryService` should inject `IDecisionContextService` and must not directly read `.agents/plan.md`, `.agents/operational_context.md`, `.agents/decisions`, `.agents/handoffs`, or milestone files.
- The first discovery slice should build at least one discovery signal from `DecisionContext` and include tests proving context-driven discovery behavior.
- `DecisionContext` must remain an explicit decision-lifecycle context model with source attribution, diagnostics, evidence, and signal-ready structure; it should not degrade into a generic `Dictionary<string,string>` or raw artifact bag.
