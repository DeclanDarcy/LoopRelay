# Milestone M3 - Execution Monitoring and Observability

## Goal

Observe active execution sessions and display live output.

## Backend Work

- [ ] Implement `ExecutionMonitoringService`.
- [ ] Add `ExecutionEvent` and `ExecutionStatus`.
- [ ] Capture provider stdout and stderr as chronological output events.
- [ ] Update `LastActivityAt` when output is received.
- [ ] Project session state, started time, last activity time, and failure or cancellation details.
- [ ] Add server-sent events endpoint for live output.
- [ ] Retain bounded recent output events for active sessions.
- [ ] Apply restart recovery policy to persisted active sessions.
- [ ] Reconnect monitoring only when provider reattach succeeds.
- [ ] Mark unrecoverable active sessions failed rather than leaving them executing.
- [ ] Reflect provider failure and cancellation in session and repository execution state.

## UI Work

- [ ] Add live execution stream using `EventSource`.
- [ ] Display session state, started time, last activity time, and output chronologically.
- [ ] Show execution state and last activity on dashboard cards.
- [ ] Preserve visibility if user switches repositories and returns.

## Tests

- [ ] Output events are emitted in order.
- [ ] `LastActivityAt` updates when output arrives.
- [ ] Failure state is projected to session and repository.
- [ ] Cancellation state is projected to session and repository.
- [ ] SSE endpoint streams events.
- [ ] Restart restores active session metadata and resumes monitoring when reattach succeeds.
- [ ] Restart marks unrecoverable active sessions failed with explicit orphaned-process reason.

## Exit Criteria

- [ ] Active execution output is visible in real time.
- [ ] Dashboard shows whether execution is running.
- [ ] Failures and cancellations are observable.
- [ ] Restart and orphaned-session behavior is deterministic.
