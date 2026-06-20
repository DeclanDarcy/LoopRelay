# Decisions

## Newly Authorized Decisions

- M4B is authorized as the next closeout slice before opening M5.
- M4B is named Completion Metadata Certification.
- M4B goal is to certify that once execution completes, validates a handoff, and enters `AwaitingAcceptance`, the operator has a complete backend-projected record of what happened.
- Session summary and status must consistently expose `StartedAt`, `CompletedAt`, `Duration`, provider, PID, repository state, session state, and `HandoffPath`.
- Duration must be computed centrally by the backend and projected everywhere rather than recalculated independently by React.
- Preferred duration invariant is `Duration = CompletedAt - StartedAt`.
- Completion duration may be persisted or centrally projected, but it must survive summary/status/workspace/reload paths consistently.
- M4B certification must verify completed duration appears in `ExecutionStatus`, `ExecutionSessionSummary`, and workspace projection.
- M4B certification must verify `AwaitingAcceptance` survives backend restart.
- M4B certification must verify `HandoffPath`, `CompletedAt`, and duration survive session store reload.
- M4B certification must verify failed handoff validation and archive failure do not incorrectly produce successful-review completion duration metadata.
- M4 should be formally closed only after completion metadata certification.
- M5 acceptance workflow begins only after M4 closeout.
- M5 must preserve the invariant that accept/reject are valid only from `AwaitingAcceptance`.
- M5 must preserve separate workflow states: `Accepted`, `Committed`, and `Pushed` are not equivalent.

## Explicitly Deferred

- Do not begin M5 before M4B is complete.
- Do not add accept controls in M4B.
- Do not add reject controls in M4B.
- Do not add commit controls in M4B.
- Do not add push controls in M4B.
- Rejection semantics for M5 remain undecided.
