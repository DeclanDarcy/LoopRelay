# Handoff

## New State From This Slice

- Continued M4 read-only review workspace work.
- Added `DecisionOptionComparison` in `src/CommandCenter.UI/src/features/decisions/DecisionOptionComparison.tsx`.
- Added `DecisionEvidenceSourcePanel` in `src/CommandCenter.UI/src/features/decisions/DecisionEvidenceSourcePanel.tsx`.
- Added hooks for backend-owned option comparison, evidence inspection, and source attribution read models:
  - `useDecisionOptionComparison`
  - `useDecisionEvidenceInspection`
  - `useDecisionSourceAttributions`
- `DecisionLifecycleTab` now loads option comparison, evidence inspection, and source attribution read models for the selected proposal.
- Option comparison renders backend-provided option rows, benefits, costs, recommendation marker, and evidence without recomputing tradeoff summaries in React.
- Evidence/source navigation renders backend-provided evidence rows and uses React presentation state only for selected evidence source.
- `devTauriMock` now handles:
  - `get_decision_option_comparison`
  - `get_decision_evidence_inspection`
  - `list_decision_source_attributions`
- Added characterization tests:
  - `decisionOptionComparison.test.tsx`
  - `decisionEvidenceSourcePanel.test.tsx`
- Updated `decisionLifecycleNavigation.test.tsx` to verify selected proposal propagation to review, comparison, evidence, and source hooks.
- Updated `.agents/milestones/m4-review-workspace.md`; option comparison and evidence/source attribution navigation are now complete.
- Rotated the previous handoff to `.agents/handoffs/handoff.0018.md`.

## Verification

- `npm run test --prefix src/CommandCenter.UI -- decisionOptionComparison decisionEvidenceSourcePanel decisionLifecycleNavigation` passes.
- `npm run lint --prefix src/CommandCenter.UI` passes.
- `npm run test --prefix src/CommandCenter.UI` passes with 42 files and 151 tests.
- `npm run build --prefix src/CommandCenter.UI` succeeds.

## Next Slice

- Finish M4 with a final read-only review-workspace polish pass:
  1. Inspect the composed Decisions tab for duplicated evidence density, layout balance, and mobile wrapping.
  2. Decide whether M4 needs a dedicated review diagnostics panel or whether current diagnostics in the viewer satisfy the read-model requirement.
  3. Run final M4 verification, including UI lint/test/build.
  4. If the polish pass finds no gaps, close M4 and prepare M5 refinement workflow planning.
