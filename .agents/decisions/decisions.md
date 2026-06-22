# Decisions

## Newly Authorized

- M4 is complete.
- Diagnostics should remain in the proposal viewer instead of moving to a dedicated diagnostics panel.
- The diagnostics requirement is visibility, traceability, and contextual placement, not a specific panel count.
- Keeping diagnostics adjacent to proposal inspection is the preferred review-workspace UX.
- Responsive layout hardening for evidence grids, source attribution, diagnostics, option comparison, and review surfaces is accepted as appropriate M4 closure work.
- M4 maintains the intended architecture: backend-owned lifecycle authority, review state, and read models with React limited to presentation.
- No operational-context coupling should be introduced by M4.
- No execution coupling should be introduced by M4.

## M5 Start Authorization

- M5 refinement workflow is ready to start.
- M5 should proceed backend-first.
- Implement refinement domain models as first-class models:
  - `DecisionRefinementRequest`
  - `DecisionProposalRevision`
  - `DecisionConstraint`
  - `DecisionAssumptionRevision`
  - `DecisionOptionRevision`
  - `DecisionTradeoffRevision`
- Create a dedicated `IDecisionRefinementService`.
- Do not grow `DecisionGenerationService` into the refinement lifecycle engine.
- Persist `REV-*.json` and `REV-*.md` as authoritative refinement artifacts.
- Preserve all historical revisions and never overwrite prior revisions.
- Stale-base protection is the highest-leverage M5 concern.
- Refinement requests must reference a base proposal fingerprint or equivalent revision identity.
- Reject refinement against stale proposal state.
- Create revision read models before UI mutation controls:
  - revision history
  - revision comparison
  - current versus previous proposal state

## Program Status

- M0 Domain Foundation is complete.
- M1 Context Resolution is complete.
- M2 Discovery is complete.
- M3 Proposal Lifecycle is complete.
- M4 Review Workspace is complete.
- M5 Refinement Workflow is ready to start.
