# Decisions

## Newly Authorized

- Proceed with the next preparation invocation slice, narrowly limited to:
  - commit preparation.
- Allowed commit preparation scope:
  - invoke the existing Execution-owned commit preparation command.
  - create reviewable commit preparation evidence.
  - persist workflow preparation events for commit preparation.
  - detect duplicate preparation snapshots and prepared-commit evidence before invocation.
  - prove restart/recovery idempotency for existing commit preparation evidence.
- Preserve the invariant:
  - Workflow invokes preparation.
  - Execution owns commit preparation semantics.
  - Workflow records preparation evidence.
  - preparation does not approve or execute commit.
  - preparation does not approve or execute push.
  - preparation does not advance workflow stage.
- Add tests proving:
  - open gate means no commit preparation command is invoked.
  - duplicate prepared commit evidence means no new preparation is invoked.
  - duplicate preparation snapshot evidence means no new preparation is invoked.
  - successful invocation creates reviewable commit preparation evidence.
  - preparation event is persisted.
  - workflow stage remains unchanged.
  - commit gate remains open after preparation.
  - no commit execution occurs.
  - no push execution occurs.
  - restart does not duplicate commit preparation.
  - recovery detects existing preparation evidence.

## Explicitly Deferred

- Do not enable hosted continuation.
- Do not enable hosted preparation.
- Do not perform background invocation.
- Do not perform any authority action.
- Do not accept or reject handoffs.
- Do not resolve, archive, or supersede decisions.
- Do not review, accept, reject, edit, or promote operational context.
- Do not approve commits.
- Do not execute commits.
- Do not approve pushes.
- Do not execute pushes.
- Do not select work.
- After commit preparation is implemented and tested, pause for a focused review of the full Milestone 9 implementation before authorizing hosted/background behavior.
