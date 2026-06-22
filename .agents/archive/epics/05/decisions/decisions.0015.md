# Decisions

## Newly Authorized

- M4 UI Phase 1 is accepted as the correct entry point for the review workspace UI.
- The Decisions tab implementation is accepted because it consumes backend contracts instead of inventing a React lifecycle model.
- The governing boundary remains: backend owns lifecycle authority; UI observes lifecycle authority.
- The current layering is accepted:
  - Repository authority
  - Decision services
  - Review workspace
  - Read models
  - Tauri commands
  - API wrappers
  - Hooks
  - UI
- The Tauri bridge scope for decision context, candidates, proposals, review workspace, options, evidence, and sources is accepted because these are read-model concerns.
- The dev Tauri mock extension is accepted as useful for UI development and future characterization tests, provided it preserves backend contract shape.
- Proposal browsing must use `DecisionProposalBrowserItem` only; React must not reconstruct proposal lifecycle status from full proposal payloads.
- Phase 2 is authorized for the M4 proposal browser.
- Proposal browser filters must use backend-driven browser endpoint filtering for generated, viewed, needs-refinement, refined, ready-for-resolution, resolved, and discarded proposals.
- Proposal selection may be local UI state only.
- Review, refinement, and resolution mutation controls remain deferred until proposal viewer, review workspace, and evidence/source navigation exist.
- The missing `rustfmt` component is environment setup only and does not affect roadmap status.

## Current Milestone Status

- M0 Domain Foundation is complete.
- M1 Context Resolution is complete.
- M2 Discovery is complete.
- M3 Proposal Lifecycle is complete.
- M4 Review Workspace is in progress.

## Newly Authorized Next Slice

- Implement the M4 proposal browser UI:
  1. Render browser rows from `DecisionProposalBrowserItem`.
  2. Add backend-driven state filters.
  3. Add selected-proposal local UI state.
  4. Do not add lifecycle mutation controls.
