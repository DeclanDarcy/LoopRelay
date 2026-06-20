# Decisions

## Newly Authorized Decisions

- M3B.1 is complete.
- Streaming must remain a transport over existing `ExecutionEvent` history, not a separate monitoring system.
- JSON and SSE must continue to derive from the same `ExecutionSession.Events` source.
- M3B.2 is authorized as the next slice.
- M3B.2 scope is `EventSource`, workspace feed, and dashboard activity indicators.
- M3B.2 UI should render `ExecutionEvent` directly as an operational log with timestamp, event type, and content.
- M3B.2 UI must avoid interpretation, grouping, summaries, AI-generated explanations, throughput metrics, event counters, and health scores.
- Dashboard activity scope is limited to execution state and last activity.
- Workspace session panel should expose session id, provider, PID, started time, last activity time, session state, and repository state.
- Dashboard and workspace should consume `ExecutionStatus` for state rather than reconstructing state from events.
- The UI should display state, not derive state.
- After M3B.2, proceed to M4A.
- M4A scope is handoff validation, completion processing, and transition to `AwaitingAcceptance`.
- M4 must preserve the distinction between provider completion and execution success.

## Explicitly Deferred

- Do not introduce new monitoring concepts during M3B.2.
- Do not start M4 until React streaming and dashboard activity are complete.
