# Milestone 9 Evidence: Decision and Continuity Explainability Cleanup

## Scope

- Continued obsolete UI cleanup for shared explainability presentation.
- Replaced manually rendered decision option evaluation recommendation evidence with shared `EvidenceList`.
- Added `decisionRecommendationEvidenceToEvidence` so recommendation evidence is adapted once and reused by proposal-level and option-evaluation surfaces.
- Replaced option and analyzed-option diagnostic chips in `DecisionProposalViewer` with shared `DiagnosticList`.
- Replaced operational-context proposal modification supporting-evidence lists with shared `EvidenceList`.
- Added `operationalContextSemanticChangeSupportingEvidenceToEvidence` for supporting evidence only, preserving modification metadata as domain comparison content.
- Confirmed `ReasoningReconstructionPanel` already uses shared `EvidenceList`, `DiagnosticList`, and `UncertaintyView`; no migration needed this slice.

## Preserved Domain Composition

- Decision option strengths, weaknesses, risks, constraints, tradeoffs, analyzed facts, and comparison facts remain domain-owned.
- Operational-context modification review still owns previous/current state, identity basis, reason, and side-by-side markdown comparison.
- Reasoning reconstruction metadata, grouped sections, trace counts, and horizon narrative remain domain-owned.

## Verification

- `npm test -- decisionLifecycleNavigation.test.tsx operationalContextProposalComparison.test.tsx`
- `npm run build`

## Residual Risk

- `npm run build` still reports the existing Vite chunk-size warning for the main bundle.
- Milestone 9 cleanup is still not complete; remaining likely targets are duplicate health/certification renderers and final terminology alignment.
