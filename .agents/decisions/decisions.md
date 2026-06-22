# Decisions

## Newly Authorized

- M4 Review Workspace is in progress.
- The first M4 backend slice is accepted as aligned with the roadmap.
- Review information must remain a first-class repository-backed layer rather than being folded into proposals or decisions.
- The review layer must remain observational rather than authoritative.
- Review notes must persist as their own review layer in `notes.json`.
- Review notes must not mutate `proposal.json`, proposal revisions, decisions, candidates, operational context, or execution context.
- Review transition endpoints returning `DecisionReviewWorkspace` is accepted as the correct contract direction because backend review state should own the authoritative projection for UI consumption.
- The implementation should continue to preserve the lifecycle ordering: decision context, candidate, proposal, revision history, review workspace, and decision authority.
- Proposal browser, option comparison, evidence inspection, and source attribution must remain backend-owned projections/read models and must not become alternate sources of authority.
- The safe read-model flow is repository authority to review workspace models to read models to UI.
- Backend contracts should lead the UI; the Decisions UI should wait until M4 read models stabilize.

## Current Milestone Status

- M0 Domain Foundation is complete.
- M1 Context Resolution is complete.
- M2 Discovery is complete.
- M3 Proposal Lifecycle is complete.
- M4 Review Workspace is in progress.

## Newly Authorized Next Slice

- Continue M4 by completing backend read models before Decisions UI work.
- Priority order:
  1. Proposal browser read model with state, classification, priority, created, updated, review status, and resolution status.
  2. Backend proposal filtering for generated, viewed, needs-refinement, refined, ready-for-resolution, resolved, and discarded states.
  3. Dedicated option comparison projection.
  4. Evidence inspection model with evidence, signals, diagnostics, and attribution.
  5. Source attribution navigation exposing source kind, relative path, section, and excerpt.
  6. UI workspace only after backend contracts stabilize.
