# Milestone 9 Evidence: Refinement and Resolution Interaction Normalization

## Scope

- Continued Milestone 9 interaction normalization for the proposal refinement and proposal resolution panels.
- Kept refinement and resolution as phase-specific workspaces because they carry richer context than a plain lifecycle button group.
- Used `InteractionPatternView` as the shared presentation layer for action subject, result, eligibility, evidence, and diagnostics.

## Implementation

- `DecisionLifecycleTab` now passes the selected proposal lifecycle eligibility into `DecisionRefinementPanel` and `DecisionResolutionPanel`.
- `DecisionRefinementPanel` now renders a refinement interaction summary containing:
  - backend lifecycle eligibility when projected,
  - current proposal state,
  - base proposal/package authority evidence,
  - analysis/regeneration result state,
  - refinement plan and regeneration diagnostics.
- `DecisionResolutionPanel` now renders a resolution interaction summary containing:
  - backend lifecycle eligibility when projected,
  - selected and recommended option evidence,
  - reviewed package/proposal fingerprint evidence,
  - recommendation override evidence,
  - package authority conflict diagnostics.
- Existing directive regeneration, compatibility revision, resolution submission, and assimilation recommendation controls remain phase-specific UI and do not expand the base interaction component.

## Verification

- `npm test -- decisionRefinementPanel.test.tsx decisionResolutionPanel.test.tsx`
- `npm test -- decisionRefinementPanel.test.tsx decisionResolutionPanel.test.tsx decisionLifecycleNavigation.test.tsx decisionProposalViewer.test.tsx decisionCandidateBrowser.test.tsx`
- `npm run build`

## Residual Risk

- `npm run build` still reports the existing Vite chunk-size warning for the main bundle.
- The refinement and resolution summaries depend on the selected proposal lifecycle eligibility being available from the parent projection; they degrade to empty action eligibility when that projection is still loading or unavailable.
- Interaction normalization remains incomplete for execution commit/push, recovery, and transfer action families.
