# Decisions

## Newly Authorized

- M5 Refinement Workflow is complete and can be treated as the logical closure point for the milestone.
- The final M5 UI slice is accepted because it exposes backend-established lifecycle state without introducing new lifecycle authority.
- Refinement authority remains backend- and repository-owned; React, Tauri, and client hooks remain presentation, transport, and submission layers only.
- Successful refinement must continue to follow:
  - submit structured request
  - let backend rebuild authoritative state
  - reload backend projections
  - avoid local proposal, revision, lineage, comparison, evidence, or source patching
- The M5 lifecycle decomposition is accepted:
  - proposal is current authority
  - revision is historical evolution
  - comparison is change explanation
  - lineage is history navigation
  - review is review state
  - resolution is human authority
- React may own selection, input, navigation, expansion, and submission, but must not own lifecycle validity, revision meaning, comparison meaning, lineage construction, or decision authority.
- Before building M6 resolution UI, the next slice must audit the existing resolution path, especially:
  - `ResolveDecisionCommand`
  - decision records
  - resolution metadata
  - decision authority creation
- M6 should verify whether current resolution artifacts satisfy human resolution, governance traceability, and operational adoption preparation before adding new UI.
- M6 should inspect whether resolution captures the proposal ID, proposal fingerprint, and revision context at resolution time.
- If resolution does not immutably capture what was resolved, introduce a resolution snapshot or equivalent rather than requiring future reconstruction by walking proposal history.
