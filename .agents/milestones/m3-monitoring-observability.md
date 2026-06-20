# Milestone M3 - Execution Monitoring and Observability

## Goal

Observe active execution sessions and display live output.

## Backend Work

- [x] Implement `ExecutionMonitoringService`.
- [x] Add `ExecutionEvent` and `ExecutionStatus`.
- [x] Capture provider stdout and stderr as chronological output events.
- [x] Update `LastActivityAt` when output is received.
- [x] Project session state, started time, last activity time, and failure details.
- [ ] Add server-sent events endpoint for live output.
- [x] Retain bounded recent output events for active sessions.
- [x] Apply restart recovery policy to persisted active sessions.
- [ ] Reconnect monitoring only when provider reattach succeeds.
- [x] Mark unrecoverable active sessions failed rather than leaving them executing.
- [x] Reflect provider failure in session and repository execution state.

## UI Work

- [ ] Add live execution stream using `EventSource`.
- [ ] Display session state, started time, last activity time, and output chronologically.
- [ ] Show execution state and last activity on dashboard cards.
- [ ] Preserve visibility if user switches repositories and returns.

## Tests

- [x] Output events are emitted in order.
- [x] `LastActivityAt` updates when output arrives.
- [x] Failure state is projected to session and repository.
- [ ] Cancellation state is projected to session and repository.
- [ ] SSE endpoint streams events.
- [ ] Restart restores active session metadata and resumes monitoring when reattach succeeds.
- [x] Restart marks unrecoverable active sessions failed with explicit orphaned-process reason.

## Exit Criteria

- [ ] Active execution output is visible in real time.
- [ ] Dashboard shows whether execution is running.
- [x] Failures are observable.
- [ ] Restart and orphaned-session behavior is deterministic.
