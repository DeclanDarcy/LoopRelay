# Milestone M0 - Execution Architecture Ratification

## Goal

Establish execution subsystem boundaries, lifecycle, models, and UI architecture without launching sessions.

## Backend Work

- [x] Add `ExecutionSessionState` and `RepositoryExecutionState`.
- [x] Add minimal `ExecutionSession` model.
- [x] Add interfaces:
  - [x] `IExecutionContextService`
  - [x] `IExecutionSessionService`
  - [x] `IExecutionMonitoringService`
  - [x] `IHandoffService`
  - [x] `IExecutionProvider`
  - [x] `IGitService`
- [x] Add no-op or in-memory implementations only where needed to support projections.
- [x] Add execution state fields to dashboard and workspace projections.
- [x] Keep all repositories in `Ready`, `Failed`, or unavailable-derived states based on existing data until real execution exists.

## Documentation Work

- [x] Update `docs/architecture.md` or add `docs/execution-architecture.md`.
- [x] Document disposable sessions, service boundaries, lifecycle, provider strategy, state model, and handoff invariant.

## UI Work

- [x] Display execution state placeholders in dashboard and workspace.
- [x] Do not add a start button yet.

## Tests

- [x] State model serialization tests through HTTP JSON options.
- [x] Projection tests verifying default execution state.
- [x] Service registration smoke test through `Program.CreateApp`.

## Exit Criteria

- [x] Execution boundaries and state model are defined in code and documentation.
- [x] Dashboard and workspace show execution state.
- [x] No execution session can be launched yet.
