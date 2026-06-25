# Milestone 9 Evidence: Proposal Review Interaction Normalization

## Scope

- Continued Milestone 9 interaction normalization with the proposal review lifecycle family.
- Added a reusable `InteractionPatternView` explainability component for action, eligibility, evidence, result, and diagnostics presentation.
- Replaced the proposal lifecycle action panel's local eligibility block with the shared interaction summary while preserving backend-owned lifecycle rules and existing mutation buttons.

## Authoritative Inputs

- Proposal lifecycle eligibility remains sourced from `DecisionLifecycleEligibilityProjection`.
- Action availability remains adapted through `decisionLifecycleEligibilityToActions`.
- Diagnostics remain adapted through `decisionDiagnosticsToExplanation`.
- Current state, allowed/blocked next states, review reason, and last transition are rendered from backend proposal review workspace and lifecycle eligibility data.

## Verification

- `npm test -- decisionLifecycleNavigation.test.tsx decisionProposalViewer.test.tsx`
- `npm run build`

## Residual Risk

- This slice normalized only the proposal review action family. Candidate generation/promote/dismiss/expire, decision supersede/archive, refinement, resolution, execution commit/push, recovery, and transfer actions still need the same interaction summary pattern.
- `npm run build` still reports the existing Vite chunk-size warning for the main bundle.
