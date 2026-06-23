# Decisions

## Newly Authorized

- Treat Milestone 5 as complete from an architecture perspective.
- Treat the current full UI suite failure as test infrastructure fragility, not a reasoning feature regression.
- Restore full UI regression reliability before opening Milestone 6.
- Prefer fixing the smoke tests by making selectors explicit:
  - scope queries with `within(activePanel)`
  - use labels, placeholders, or named roles where possible
- Consider hiding inactive tabs from accessibility queries only if the application intends inactive panels to be inaccessible.
- Avoid test updates that merely tolerate duplicate controls without clarifying intent.
- Before starting Milestone 6, verify that every M5 recommendation can be ignored without affecting repository correctness.
- Keep the ownership boundary intact:
  - Reasoning identifies materialization pressure.
  - Decision Lifecycle decides authoritative action.
