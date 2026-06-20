# Decisions

## Newly Authorized Decisions

- M5A.1 is authorized as the next backend-only slice.
- M5A.1 is named Backend Accept/Reject Workflow.
- Rejection is a user decision, not a system failure.
- Rejecting an execution from `AwaitingAcceptance` transitions the repository back to `Ready`.
- Rejection must preserve session history, handoff content, events, and metadata.
- Rejection must not delete artifacts, clean up files, roll back changes, or mark execution as `Failed`.
- `Failed` remains reserved for launch failure, monitoring failure, provider failure, handoff validation failure, archive failure, and other execution-system failures.
- Acceptance is valid only from `AwaitingAcceptance`.
- Rejection is valid only from `AwaitingAcceptance`.
- Add backend endpoints `POST /api/execution-sessions/{sessionId}/accept` and `POST /api/execution-sessions/{sessionId}/reject`.
- Add session metadata for `AcceptedAt`, `RejectedAt`, and optional `DecisionNote`.
- Accepting from `AwaitingAcceptance` transitions through user acceptance and records `AcceptedAt`.
- If accepted work has Git changes, repository workflow should proceed to `AwaitingCommit`.
- If accepted work has no Git changes, repository workflow should proceed to `Ready`.
- Invalid accept and reject attempts from non-`AwaitingAcceptance` states must be rejected.
- Accepted and rejected metadata/state must survive session store reload.

## Explicitly Deferred

- Do not implement M5A.2 UI controls in M5A.1.
- Do not add accept or reject buttons until backend certification is complete.
- Do not add confirmation dialogs until M5A.2.
- Do not add Git commit or push controls in M5.
- Do not implement cleanup, rollback, or artifact deletion on rejection.
