# Milestone M0 - Execution Architecture Ratification

## Goal

Establish execution subsystem boundaries, lifecycle, models, and UI architecture without launching sessions.

## Backend Work

- [ ] Add `ExecutionSessionState` and `RepositoryExecutionState`.
- [ ] Add minimal `ExecutionSession` model.
- [ ] Add interfaces:
  - [ ] `IExecutionContextService`
  - [ ] `IExecutionSessionService`
  - [ ] `IExecutionMonitoringService`
  - [ ] `IHandoffService`
  - [ ] `IExecutionProvider`
  - [ ] `IGitService`
- [ ] Add no-op or in-memory implementations only where needed to support projections.
- [ ] Add execution state fields to dashboard and workspace projections.
- [ ] Keep all repositories in `Ready`, `Failed`, or unavailable-derived states based on existing data until real execution exists.

## Documentation Work

- [ ] Update `docs/architecture.md` or add `docs/execution-architecture.md`.
- [ ] Document disposable sessions, service boundaries, lifecycle, provider strategy, state model, and handoff invariant.

## UI Work

- [ ] Display execution state placeholders in dashboard and workspace.
- [ ] Do not add a start button yet.

## Tests

- [ ] State model serialization tests through HTTP JSON options.
- [ ] Projection tests verifying default execution state.
- [ ] Service registration smoke test through `Program.CreateApp`.

## Exit Criteria

- [ ] Execution boundaries and state model are defined in code and documentation.
- [ ] Dashboard and workspace show execution state.
- [ ] No execution session can be launched yet.
