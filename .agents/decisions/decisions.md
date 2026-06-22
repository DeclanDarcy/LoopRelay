# Decisions

## Newly Authorized

- M4 proposal browser slice is accepted as complete.
- The proposal browser architecture is accepted:
  - Backend read model
  - `DecisionProposalBrowserItem`
  - React filtering and selection presentation
- The key invariant remains: selected proposal is UI state, while proposal lifecycle state is backend state.
- The next M4 slice is authorized for a read-only full proposal viewer.
- The full proposal viewer should include context, options, tradeoffs, recommendation, assumptions, diagnostics, review state, notes, revisions, evidence, and source attribution.
- Review, refinement, and resolution mutation controls remain deferred until users can inspect the full proposal, evidence, and review workspace from backend-owned read models.

## Current Milestone Status

- M0 Domain Foundation is complete.
- M1 Context Resolution is complete.
- M2 Discovery is complete.
- M3 Proposal Lifecycle is complete.
- M4 Review Workspace is in progress.

## Newly Authorized Next Slice

- Implement the read-only M4 full proposal viewer:
  1. Load selected proposal review workspace data from backend-owned read models.
  2. Render the complete proposal inspection surface.
  3. Keep evidence and source attribution visible near the content they support.
  4. Do not add lifecycle mutation controls.
