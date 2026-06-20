# Decisions

## Newly Authorized Decisions

- M2 is complete and certifiable.
- The latest-session summary addition is accepted as the final M2 visibility piece.
- M3 should be split into M3A and M3B.
- M3A owns backend observability substrate only:
  - event model
  - status projection
  - output capture
  - bounded event retention
- M3B owns transport and UI streaming:
  - SSE endpoint
  - EventSource integration
  - streaming workspace UI
  - dashboard activity indicators
- The next authorized slice is M3A.1.
- M3A.1 must add `ExecutionEvent`, `ExecutionEventType`, and `ExecutionStatus`.
- M3A.1 must implement backend execution monitoring before SSE or React streaming.
- Monitoring event types should remain observational: `Info`, `StdOut`, `StdErr`, `ProviderStarted`, `ProviderExited`, `Failure`, and `Recovery`.
- Monitoring must avoid interpretive event types such as thinking, planning, confused, warning, or healthy.
- Persisted session event history must be bounded by explicit maximum event count and maximum byte policy.
- M3A.1 must support retained backend history sufficient to answer what happened during an execution session from backend state alone.

## Certification Required

- Output ordering:
  - stdout and stderr events are retained in chronological order.
- Activity tracking:
  - received output updates `LastActivityAt`.
- Failure tracking:
  - provider exit or failure records a failure event.
- Retention limits:
  - event history remains bounded and does not grow without limit.
- Restart behavior:
  - retained events survive session store reload.

## Explicitly Deferred

- No SSE in M3A.1.
- No EventSource in M3A.1.
- No React streaming UI in M3A.1.
- No live workspace feed in M3A.1.
- No dashboard activity indicators until M3B.

## Next Authorized Slice

- Proceed with M3A.1: execution event/status models, monitoring service, stdout/stderr capture, bounded event retention, `LastActivityAt` projection, and backend certification.
