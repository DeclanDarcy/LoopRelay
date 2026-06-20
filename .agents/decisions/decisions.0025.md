# Decisions

## Newly Authorized Decisions

- M8.2 is authorized as Repeatable Execution Certification.
- M8.2 should focus on proving the loop works twice, not expanding the loop.
- M8.2 certification must prove:
  - Execution A.
  - Acceptance.
  - Commit.
  - Push.
  - Ready.
  - Execution B on the same repository.
- M8.2 must include a milestone-change certification:
  - Execution A.
  - Push.
  - Ready.
  - Select a different milestone.
  - Execution B.
- Milestone-change certification must verify:
  - Context rebuilt.
  - Prompt rebuilt.
  - Selected milestone changed.
  - Execution launched successfully.
- M8.2 must inspect handoff rotation integrity across repeated executions.
- Handoff rotation certification must verify:
  - Prior handoff was archived.
  - Historical numbering incremented correctly.
  - Latest handoff was preserved.
  - Execution history remained visible.
- M8.2 must include restart-between-executions certification:
  - Execution A.
  - Push.
  - Ready.
  - Application restart.
  - Execution B.
- Restart certification must verify:
  - History survives.
  - Latest summary survives.
  - Selected milestone can be changed.
  - New execution launches.
- If repeatable execution, history preservation, context rebuild, milestone selection, handoff rotation, and restart restoration pass, Epic 2 is probably functionally complete.

## Explicitly Deferred

- Expanding the execution loop beyond current Epic 2 scope.
- Automatic milestone progression.
- Automatic milestone selection.
- Execution chaining.
- Treating execution history as repository artifacts.
