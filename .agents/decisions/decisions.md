# Decisions

## Newly Authorized

- Accept the reported Milestone 2 governance workspace progression as architecturally sound.
- Treat Milestone 2 as functionally complete, pending final verification and exit audit.
- Preserve the established authority split:
  - `CommandCenter.DecisionSessions` owns governance lifecycle.
  - Workflow owns operational timeline, gates, and required human action.
  - React owns presentation only.
- Keep Governance as a first-class workspace for visibility, not authority.
- Keep UI product language as `Governance` while preserving `DecisionSession` code-facing contracts where they mirror backend authority.
- Keep workflow context adjacent to governance facts as Workflow Gate plus Required Human Action, not as a separate Governance Workflow.
- Keep transfer recommendation distinct from transfer executability.
- Complete Milestone 2 closure with:
  - `RepositoryDecisionSessionSummary` serialization/projection coverage
  - explicit exit audit of authority, mutation, projection, and workflow boundaries
  - backend tests, UI tests, and build
- Do not add more Milestone 2 integration scope unless the serialization coverage or exit audit finds a gap.
- After Milestone 2 closure passes, transition to Milestone 3 focused on decision lifecycle reachability.
