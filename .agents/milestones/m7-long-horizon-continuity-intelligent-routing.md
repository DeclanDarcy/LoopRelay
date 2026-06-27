# Phase 7 - Long-Horizon Continuity and Intelligent Routing

Goal: allow repository execution to continue indefinitely while preserving understanding, authority, and governance quality. By the end of this phase, Operational Context has evolved into Repository Understanding: the current, durable, living view of the repository, even before richer knowledge, lineage, and query capabilities are added.

## Implementation

- [ ] Replace deterministic token estimates in runtime routing paths with observed token accounting:
  - prompt tokens
  - output tokens
  - turn growth
  - session utilization
  - context pressure
  - transfer thresholds
- [ ] Keep estimator services only for preflight, tests, and fallback diagnostics.
- [ ] Add repository-aware router decisions:
  - reuse warm Decision Session
  - transfer to new Decision Session
  - fail closed when required understanding cannot be reconstructed
- [ ] Implement warm session reuse when context pressure and health allow.
- [ ] Implement session transfer:
  - produce operational delta
  - update durable operational context
  - create new decision session
  - preserve identity, lineage, evidence, and authority
  - retire old session without preserving live process state
- [ ] Add Operational Delta model:
  - new understanding
  - changed understanding
  - removed understanding
  - compressed understanding
  - evidence references
  - identity/provenance
- [ ] Add context evolution flow through Continuity:
  - current operational context plus operational delta becomes updated Repository Understanding
  - Operational Context becomes the implementation artifact and serialization format behind the living Repository Understanding
  - preserve historical versions and compression history
  - remove temporary noise and completed low-value detail
- [ ] Make Repository Understanding canonical at the continuity boundary:
  - current understanding is no longer treated as an execution supplement
  - planning, execution, decisions, handoffs, transfers, and reasoning all feed the same durable understanding identity
  - runtime projections refer to Repository Understanding rather than exposing Operational Context as a product concept
- [ ] Add repository memory projections:
  - current understanding
  - historical understanding
  - operational history
  - decision history
  - reasoning history
  - transfer history
- [ ] Add transfer/reuse/recovery observability without exposing internal routing mechanics as primary product concepts.
- [ ] Add generated contracts for router decisions, transfer, operational delta, context versions, repository memory, and runtime evolution.

## Certification

- [ ] Long-running conversations transfer sessions without losing repository understanding.
- [ ] Repository Run identity, or the accepted runtime conversation identity, survives session replacement.
- [ ] Continuity remains semantic authority for context evolution and Repository Understanding.
- [ ] Operational Context has become the durable implementation artifact behind Repository Understanding.
- [ ] Decision generation resumes from durable understanding after transfer or restart.
- [ ] Stress tests cover many transfers, large repositories, context rewrites, and recovery after transfer.
