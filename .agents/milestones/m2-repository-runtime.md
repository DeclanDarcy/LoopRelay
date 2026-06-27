# Phase 2 - Repository Orchestrator

Goal: add the per-repository orchestrator that holds live processes and run state across HTTP requests.

## Implementation

- [ ] Add a singleton registry keyed by repository id. It returns exactly one active orchestrator per repository.
- [ ] Place the orchestrator in Backend or a composition-root project such as `CommandCenter.Orchestration`.
- [ ] The orchestrator owns transient state only:
  - [ ] open plan-authoring process;
  - [ ] open decision-session process;
  - [ ] cached plan text;
  - [ ] current handoff;
  - [ ] current decisions;
  - [ ] run-scoped iteration counter;
  - [ ] router inputs;
  - [ ] planning, execution, and decision SSE channels.
- [ ] Add repository lifecycle projection for `PlanAuthoring` and `ExecutingPlan`, or equivalent state surfaced by plan status.
- [ ] Add `GET /api/repositories/{id}/plan/status` returning `{ planExists, state }`.
- [ ] Gate Plan Authoring primarily on `!File.Exists(.agents/plan.md)`.
- [ ] Add `services.AddMemoryCache()` and reserve key `{repositoryId}:Plan` for the active execution run.
- [ ] Route artifact reads/writes through `IArtifactStore` or existing repository artifact abstractions.
- [ ] Add orchestrator disposal on cancel, failure, repository deselection, app shutdown, and repository runtime teardown.
- [ ] Keep the orchestrator compositional. It may call Agents, Execution, DecisionSessions, Decisions, Continuity, Git, and artifact services, but it must not become semantic authority for those domains.

## Certification

- [ ] Duplicate active orchestrators for one repository are impossible.
- [ ] Plan status correctly reports missing and existing `.agents/plan.md`.
- [ ] Live process handles are not stored as durable state.
- [ ] Restart reconstructs durable projection from repository artifacts and local records, not from live processes.
- [ ] Architecture tests preserve the DecisionSessions/Execution separation.
