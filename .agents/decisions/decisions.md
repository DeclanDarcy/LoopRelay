# Decisions

## Newly Authorized

- Proceed with the first preparation invocation slice, narrowly limited to:
  - Decisions review-artifact preparation only.
- Preserve the invariant:
  - duplicate outcome is diagnostic only.
  - `CanPrepare = false` when equivalent domain evidence exists.
- Before invoking Decisions, require:
  - no open authority gate.
  - no duplicate decision evidence.
  - current workflow stage is `Decision`.
  - preparation fingerprint is stable.
  - existing Decisions command is used directly.
  - workflow records command name and created artifact ids.
  - preparation does not advance workflow stage.
  - preparation does not resolve the decision.
- Add tests proving:
  - open gate means no command is invoked.
  - duplicate evidence means no command is invoked.
  - successful invocation creates a review artifact.
  - repeating the same fingerprint creates no duplicate artifact.
  - preparation event is persisted.
  - workflow stage remains unchanged.
  - decision gate still requires human resolution.

## Explicitly Deferred

- Do not invoke Continuity preparation.
- Do not invoke commit preparation.
- Do not enable hosted continuation.
- Do not perform any authority action.
- Do not accept or reject handoffs.
- Do not resolve, archive, or supersede decisions.
- Do not review, accept, reject, edit, or promote operational context.
- Do not commit.
- Do not push.
