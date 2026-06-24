# Decisions

## Newly Authorized

- Proceed with Milestone 8 git workflow integration.
- Preserve the core workflow boundary: Git and Execution own commit
  preparation, commit execution, push execution, and any explicit push-skip
  authority; workflow only consumes and reports their evidence.
- Workflow must project git authority outcomes, not create git authority.
- M8 may project states such as awaiting commit, committed, awaiting push,
  pushed, push skipped, no changes produced, and diagnostics.
- M8 must not introduce workflow-owned concepts such as commit approved, push
  approved, should push, or ready to push as authority decisions.
- Completion evaluation must remain evidence-driven. Safe completion evidence
  includes executed push evidence, explicit push-skip evidence, or explicit
  no-changes-produced evidence.
- No-change workflows must be represented as a first-class completion outcome,
  not as missing commit evidence.
- Keep monitoring the transient Windows file-lock signal from backend recovery
  tests during M8 through M10, especially as continuation and preparation add
  artifact activity.
- Keep the M7 invariant active for M8: domains own truth, authority, and
  actions; workflow owns coordination, projection, explanation, and recovery
  evidence.

## Explicitly Deferred

- Do not start M8 implementation in this slice; this response triggers staging,
  commit, push, and stop.
