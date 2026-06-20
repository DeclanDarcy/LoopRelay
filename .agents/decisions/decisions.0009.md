# Decisions

## Newly Authorized Decisions

- M3A is complete as the backend monitoring substrate.
- Provider exit must transition to `ExecutionSessionState.Completed`, not execution success.
- Process lifecycle and execution lifecycle are separate concerns.
- M3 remains concerned with provider/process lifecycle observation.
- M4 begins connecting provider completion to repository execution lifecycle through handoff validation.
- The effective state-machine split is:
  - provider/session state: `Created`, `Executing`, `Completed`, `Failed`, `Cancelled`
  - repository state: `Ready`, `Executing`, `AwaitingAcceptance`, `AwaitingCommit`, `AwaitingPush`, `Failed`, `Cancelled`
- M3B.1 is authorized as the next slice.
- M3B.1 must add `GET /api/execution-sessions/{sessionId}/events/stream`.
- The SSE endpoint must use `text/event-stream`.
- The SSE endpoint must stream existing `ExecutionEvent` instances directly.
- No new DTOs, event types, or streaming-specific monitoring model should be introduced for M3B.1.
- JSON events and SSE events must derive from the same `ExecutionSession.Events` source.
- SSE certification must cover event ordering, retained plus live event consumption, disconnect safety, and multiple simultaneous consumers.

## Explicitly Deferred

- Do not start React `EventSource` integration until SSE transport is certified.
- Do not add additional monitoring concepts for M3B.
- Do not begin M4 until M3B streaming transport is complete.
