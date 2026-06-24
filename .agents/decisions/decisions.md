# Decisions

## Newly Authorized

- Treat persisted workflow timeline state as coordination evidence, not
  workflow truth.
- Preserve the invariant that domain projection wins when persisted workflow
  timeline evidence diverges from domain evidence.
- Verify divergence behavior explicitly: corrupt or stale persisted workflow
  stage must be rebuilt or superseded from domain evidence with diagnostics.
- Keep one-step continuation progression as the canonical M9 progression model.
- Split remaining M9 progression implementation into two groups:
  1. `Handoff -> Decision -> OperationalContext`
  2. `OperationalContext -> Commit -> Push -> Completed`
- Validate recovery and gate behavior after the first progression group before
  implementing the second group.
- Add certification or recovery coverage proving corrupt persisted stage does
  not become authority.

## Explicitly Deferred

- Do not authorize preparation until all canonical stage transitions are
  implemented, persisted, recovered, and certified.
- Do not add hosted continuation yet.
- Do not invoke domain commands yet.
