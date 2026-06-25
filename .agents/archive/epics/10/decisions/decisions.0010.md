# Decisions

## Newly Authorized

- Accept Milestone 2 as complete.
- Treat the added `RepositoryDecisionSessionSummary` serialization coverage as the final structural closure item for Milestone 2.
- Preserve the established Milestone 2 authority split:
  - `CommandCenter.DecisionSessions` owns lifecycle, transfer, recovery, and eligibility.
  - Workflow owns operational progression, gates, required actions, health, and certification.
  - React owns presentation, user interaction, and command invocation.
- Preserve transfer recommendation and transfer executability as separate concepts.
- Begin Milestone 3 after committing and pushing the accepted Milestone 2 work.
- Sequence Milestone 3 with reachability first:
  - backend endpoint
  - shell command
  - TypeScript API
  - hook
- Then wire decision lifecycle UI actions including discovery, promote, dismiss, expire, duplicate, and proposal generation.
- Avoid client-side enablement heuristics for decision lifecycle actions.
- Add backend-owned decision lifecycle eligibility before richer UI action availability:
  - current state
  - allowed actions
  - blocked actions
  - blocking reasons
  - required inputs
- Keep React declarative for lifecycle actions by rendering backend-allowed actions, disabling backend-blocked actions, and displaying backend reasons.
