# Phase 4 - Repository Run Boundary and Operational Execution Runtime

Goal: stop treating execution as a disconnected provider invocation and introduce the narrowest durable coordination object that can own execution progress, iteration, journal, and lifecycle. The initial implementation should model this as Repository Run, while preserving the option to collapse it into Repository Runtime if an independent run lifecycle proves unnecessary.

## Implementation

- [ ] Add an explicit architecture decision checkpoint for Repository Run before broad adoption:
  - why it needs a distinct identity from Repository Runtime
  - what lifecycle it owns independently
  - which durable records it owns
  - what would collapse into Repository Runtime if the boundary is not justified
  - which contracts and migration path would preserve external behavior if it collapses
- [ ] Add Repository Run models under `CommandCenter.RepositoryRuntime`:
  - run id
  - repository id
  - lifecycle
  - current phase
  - iteration
  - current owner
  - current operational session id
  - current handoff
  - execution metadata
  - run journal
- [ ] Run lifecycle states:
  - `Created`
  - `Preparing`
  - `Executing`
  - `Waiting`
  - `Completed`
  - `Cancelled`
  - `Failed`
- [ ] Add append-only run journal persistence with plan reference, handoff references, execution metadata, lifecycle events, turn events, and milestone/progress records.
- [ ] Route Start Execution through:
  - endpoint
  - Repository Runtime
  - Repository Run
  - Execution
  - Agent Runtime operational session
- [ ] Execution remains the authority for context building, Git, operational prompts, provider interaction, handoffs, commit, and push.
- [ ] Repository Run owns sequencing, iteration, lifecycle, current phase, and durable progress only if those responsibilities remain distinct from Repository Runtime coordination.
- [ ] Align execution streams with Repository Run events while preserving existing SSE/resource behavior.
- [ ] Connect repository lifecycle transitions:
  - `PlanReady` -> `ExecutingPlan`
  - `ExecutingPlan` -> `Completed`
  - failure and cancellation states remain explicit and recoverable.
- [ ] Add run recovery from journal and execution records. Live processes are never recovered.
- [ ] Add generated contracts for repository run, run lifecycle, run journal, execution readiness, execution metadata, and stream events.

## Certification

- [ ] One active run or active runtime conversation per repository is enforced, depending on the accepted Repository Run boundary.
- [ ] Execution starts and advances through Repository Runtime.
- [ ] Existing execution semantics remain intact.
- [ ] Handoffs become run artifacts with provenance.
- [ ] Run recovery restores durable run state and projections.
- [ ] Repository Run has explicit justification, or the implementation records a governed collapse plan into Repository Runtime before downstream phases depend on it.
