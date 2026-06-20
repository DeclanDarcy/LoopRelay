# Decisions

## Newly Authorized Decisions

- M3 is functionally complete except for cancellation projection and provider reattach capability coverage.
- M3B.2 preserved the intended single source of truth: provider output flows through the monitoring service into `ExecutionStatus` and `ExecutionEvent`, then through JSON/SSE into dashboard and workspace UI.
- `ExecutionEvent` remains the unit of observation in the UI.
- The UI must continue consuming the monitoring model rather than creating a separate UI monitoring model.
- M3 must retain raw observability and avoid interpretation.
- Before opening M4, finish M3C: cancellation projection plus reattach capability certification.
- Cancellation is its own terminal state and must not be treated as failed or completed.
- Cancellation requirements: session state `Cancelled`, repository state `Cancelled`, cancellation event emitted, `LastActivityAt` updated, status endpoint reflects cancellation, and SSE reflects cancellation.
- Add an explicit provider reattach capability contract such as `SupportsReattach` plus `TryReattachAsync`, or an equivalent abstraction.
- Codex provider behavior is authorized as `SupportsReattach = false`.
- Reattach certification must cover both provider-supported recovery and provider-unsupported orphan failure.
- Successful reattach may be fake-provider-only for now; the important outcome is an architecturally certified recovery branch.
- M4 must not start until M3C closes the monitoring lifecycle edge cases.
- After M3C, begin M4A with handoff validation and transition to `AwaitingAcceptance`.
- Preferred M4 order is M4A.1 handoff validation/completion processing, M4A.2 historical handoff preservation, then M4A.3 `AwaitingAcceptance` projection/restart restoration/handoff review.

## Explicitly Deferred

- Do not begin M4 before cancellation projection and reattach capability coverage are implemented and certified.
- Do not implement real Codex reattach unless it can be guaranteed; provider-unsupported orphan failure remains valid for Codex.
