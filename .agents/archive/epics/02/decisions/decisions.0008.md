# Decisions

## Newly Authorized Decisions

- M3A.1 is complete as the backend monitoring substrate.
- The monitoring model must remain independent of transport.
- Persisted backend state must be sufficient to answer what happened during an execution session.
- The provider-observer boundary is accepted:
  - providers emit observer callbacks
  - `ExecutionMonitoringService` owns event persistence and status projection
  - providers must not own execution-session persistence
- Zero-exit provider semantics are now locked:
  - non-zero provider exit records `ProviderExited` and transitions the session/repository to `Failed`
  - zero provider exit records `ProviderExited` and transitions `ExecutionSessionState` to `Completed`
  - zero provider exit must not imply success, `AwaitingAcceptance`, or `Ready`
- M3 owns process lifecycle observation only.
- M4 owns handoff validation and decides whether a completed execution is acceptable.
- M3A.2 is authorized before SSE work.
- M3A.2 must certify the JSON backend status/event surface:
  - `GET /api/execution-sessions/{sessionId}/status`
  - `GET /api/execution-sessions/{sessionId}/events`
- M3A.2 endpoint certification must cover event ordering, retention behavior, activity timestamps, failed sessions, completed sessions, and reload persistence.
- M3B.1 follows M3A.2 and should add SSE transport using the existing `ExecutionEvent` model.
- SSE must expose `ExecutionEvent` over a different transport, not introduce separate live/streaming event concepts.
- Dashboard M3B work should add only execution state and `LastActivityAt` indicators.
- Workspace M3B work should render the chronological event feed directly, without interpretation, grouping, or summaries.

## Explicitly Deferred

- No M4 handoff lifecycle work until M3B is complete.
- No acceptance workflow before M4 handoff validation exists.
- No separate `StreamingEvent` or `LiveEvent` model.
