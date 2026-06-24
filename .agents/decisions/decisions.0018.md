# Decisions

## Newly Authorized

- Proceed with the next preparation invocation slice, narrowly limited to:
  - Continuity operational-context preparation.
- Allowed Continuity preparation scope:
  - proposal discovery.
  - proposal linkage.
  - assimilation linkage.
  - reviewable proposal creation.
- Use existing Continuity-owned commands only.
- Preserve the invariant:
  - Workflow invokes.
  - Continuity decides.
  - Workflow records preparation evidence.
  - preparation does not advance workflow stage.
- Add tests proving:
  - open gate means no command is invoked.
  - duplicate proposal evidence means no command is invoked.
  - duplicate assimilation evidence means no command is invoked.
  - duplicate linkage evidence means no command is invoked.
  - successful invocation creates reviewable Continuity evidence.
  - repeating the same fingerprint creates no duplicate artifact.
  - preparation event is persisted.
  - workflow stage remains unchanged.
  - no operational-context promotion occurs.

## Explicitly Deferred

- Do not revisit deeper Decision proposal generation unless Decisions exposes a non-authority review-artifact generation path.
- Do not invoke commit preparation yet.
- Do not enable hosted continuation.
- Do not enable hosted preparation.
- Do not perform background invocation.
- Do not perform any authority action.
- Do not accept or reject handoffs.
- Do not promote decision candidates.
- Do not approve decision proposals.
- Do not resolve, archive, or supersede decisions.
- Do not review, accept, reject, edit, or promote operational context.
- Do not commit.
- Do not push as a workflow authority action.
- Perform a focused architecture review after Continuity and commit preparation are implemented and idempotency-tested, before authorizing hosted continuation/preparation or final certification.
