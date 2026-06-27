# Phase 2 - Repository Runtime

Goal: make each registered repository a living runtime with explicit lifecycle, memory, reconstruction, and coordination ownership.

## Implementation

- [ ] Add `src/CommandCenter.RepositoryRuntime` to the solution.
- [ ] Add `RepositoryRuntime`, `IRepositoryRuntimeRegistry`, `IRepositoryRuntimeSupervisor`, and runtime command/event models.
- [ ] The registry maps repository id to exactly one runtime and owns creation, lookup, disposal, reconstruction, enumeration, and health.
- [ ] Runtime lifecycle states:
  - `Uninitialized`
  - `Loading`
  - `Ready`
  - `Running`
  - `Stopping`
  - `Disposed`
- [ ] Runtime memory is explicit and reconstructable:
  - active planning session id
  - active run id
  - active decision session id
  - current streams
  - current lifecycle
  - transient metadata
- [ ] Runtime reconstruction reads repository registration, repository artifacts, execution/decision/workflow records, operational context, run journals, and lifecycle records. It never reconstructs live processes.
- [ ] Repository Runtime composes, but does not interpret, Agents, Execution, DecisionSessions, Decisions, Workflow, Continuity, Reasoning, and Middle projections.
- [ ] Move orchestration entry points for planning, execution, decision, continuation, streaming, and recovery behind runtime commands while retaining existing endpoint compatibility.
- [ ] Add runtime projections for lifecycle, readiness, activity, health, ownership, and diagnostics without exposing process handles or registry internals.
- [ ] Extend generated contracts for repository runtime lifecycle, readiness, health, state, and command responses.

## Certification

- [ ] Duplicate runtimes for a repository are impossible.
- [ ] Runtime lifecycle transitions are centralized and tested.
- [ ] Runtime disposal releases live sessions and streams.
- [ ] Runtime reconstruction produces the same durable projection after application restart.
- [ ] Existing endpoints continue to work through compatibility paths.
