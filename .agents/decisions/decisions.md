# Decisions

## Newly Authorized

- Accept the Milestone 3 lifecycle eligibility slice as architecturally sound.
- Treat backend decision lifecycle legality as established end to end.
- Treat Milestone 3 Phase 2 backend eligibility as complete.
- Continue preserving this authority chain:
  - `DecisionLifecycleRules`
  - eligibility service
  - eligibility projection
  - endpoint
  - shell
  - TypeScript
  - React
- Keep the repository-level eligibility endpoint as the preferred contract to avoid per-item request amplification and inconsistent UI snapshots.
- Frontend must stop deciding lifecycle legality and should only render backend-owned facts for:
  - whether an action can run
  - whether a transition is valid
  - why an action is disabled or blocked
- The next Milestone 3 slice is authorized as a UI migration:
  - replace always-visible lifecycle buttons with eligibility-driven allowed actions
  - replace UI-assumed disabled states with backend blocked reasons
  - render backend diagnostics and governing rule names directly
- Apply the same eligibility-driven rendering principle to proposal review controls.
- Postpone supersede/archive completion until after the UI consumes lifecycle eligibility.
- Treat remaining Milestone 3 progression as:
  1. UI consumes eligibility
  2. supersede/archive completion
  3. refresh propagation
  4. end-to-end lifecycle characterization
  5. milestone exit audit
