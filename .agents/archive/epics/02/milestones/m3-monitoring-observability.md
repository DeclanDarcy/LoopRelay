# Milestone M3 - Execution Monitoring and Observability

## Goal

Observe active execution sessions and display live output.

## Backend Work

- [x] Implement `ExecutionMonitoringService`.
- [x] Add `ExecutionEvent` and `ExecutionStatus`.
- [x] Capture provider stdout and stderr as chronological output events.
- [x] Update `LastActivityAt` when output is received.
- [x] Project session state, started time, last activity time, and failure details.
- [x] Add server-sent events endpoint for live output.
- [x] Retain bounded recent output events for active sessions.
- [x] Apply restart recovery policy to persisted active sessions.
- [x] Reconnect monitoring only when provider reattach succeeds.
- [x] Mark unrecoverable active sessions failed rather than leaving them executing.
- [x] Reflect provider failure in session and repository execution state.
- [x] Certify JSON status and retained-events endpoints before SSE transport work.

## UI Work

- [x] Add live execution stream using `EventSource`.
- [x] Display session state, started time, last activity time, and output chronologically.
- [x] Show execution state and last activity on dashboard cards.
- [x] Preserve visibility if user switches repositories and returns.

## Tests

- [x] Output events are emitted in order.
- [x] `LastActivityAt` updates when output arrives.
- [x] Failure state is projected to session and repository.
- [x] Completed provider-exit state is projected without implying acceptance or readiness.
- [x] Cancellation state is projected to session and repository.
- [x] JSON status and events endpoints return persisted event history after store reload.
- [x] JSON status and events endpoints return not found for unknown sessions.
- [x] SSE endpoint streams events.
- [x] Restart restores active session metadata and resumes monitoring when reattach succeeds.
- [x] Restart marks unrecoverable active sessions failed with explicit orphaned-process reason.

## Exit Criteria

- [x] Active execution output is visible in real time.
- [x] Dashboard shows whether execution is running.
- [x] Failures are observable.
- [x] Restart and orphaned-session behavior is deterministic.
