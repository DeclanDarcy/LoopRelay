# Milestone M4 - Handoff Lifecycle Management

## Goal

Complete execution only when a handoff exists, rotate the previous handoff, and enter review state.

## Backend Work

- [ ] Implement `HandoffService`.
- [ ] Detect provider completion.
- [ ] Validate `.agents/handoffs/handoff.md` exists.
- [ ] Archive previous handoff snapshot to next `.agents/handoffs/handoff.NNNN.md` when appropriate.
- [ ] Associate current handoff with the session.
- [ ] Add completed time and duration to session metadata.
- [ ] Transition from `Executing` to `AwaitingAcceptance` when validation succeeds.
- [ ] Transition to `Failed` when handoff validation or historical archive fails.
- [ ] Refresh repository projections after completion processing.
- [ ] Restore `AwaitingAcceptance` state after restart.

## UI Work

- [ ] Display `Awaiting Acceptance` state in dashboard and workspace.
- [ ] Add handoff review workspace.
- [ ] Display execution summary and complete generated handoff.
- [ ] Do not add accept/reject controls until M5.

## Tests

- [ ] Provider completion plus handoff exists transitions to `AwaitingAcceptance`.
- [ ] Provider completion without handoff transitions to `Failed`.
- [ ] Previous handoff snapshot is archived with next sequence number.
- [ ] No historical handoff is created when no previous handoff existed.
- [ ] Rotation/archive failure transitions to `Failed`.
- [ ] Awaiting acceptance state survives store reload.

## Exit Criteria

- [ ] Execution cannot complete successfully without a current handoff.
- [ ] Generated handoff is visible for review.
- [ ] Prior current handoff is preserved historically.
