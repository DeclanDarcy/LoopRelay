# Decisions

## Newly Authorized

- The first M5 slice is accepted as a clean opening slice that preserves M0-M4 architectural direction.
- M5 authority separation is approved:
  - `DecisionGenerationService` owns proposal creation.
  - `DecisionRefinementService` owns proposal evolution.
- Refinement must remain separate from review and resolution.
- Stale-base protection is a non-negotiable M5 lifecycle invariant.
- Refinement requests should continue to validate the proposal fingerprint they were based on and reject mismatches.
- Expanded revision metadata is directionally correct and should support future explainability:
  - accepted changes
  - rejected changes
  - diagnostics
  - constraints
  - retired assumptions
  - retired options
  - before/after recommendation rationale
  - attribution
- Proposal authority remains the intended model.
- Revision records are historical artifacts, not the authoritative current proposal state.
- Do not accidentally convert the proposal lifecycle into event sourcing before M6.
- Continue M5 in the sequence:
  - domain
  - persistence
  - read models
  - UI
- React must not compute lifecycle diffs.
- Backend comparison/read models should precede refinement UI mutation controls.
- Add revision chain integrity coverage before UI work, proving each revision references the exact proposal state it evolved from and stale references cannot advance the chain.

## Next Slice Direction

- Build backend revision comparison/read models first.
- Add or shape backend endpoints for revision retrieval and current-versus-previous proposal comparison.
- Expose traceability projections for accepted changes, rejected changes, retired assumptions, and retired options.
- Defer refinement UI controls until reviewers can inspect what changed, why, and compared to what.
