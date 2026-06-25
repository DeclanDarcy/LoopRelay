# Milestone 9 Obsolete UI Cleanup: Decision Option Evidence

## Scope

- Audited decision influence and proposal-option transparency surfaces for duplicate evidence, diagnostics, and fact-chip renderers.
- Confirmed decision influence trace and explorer surfaces already delegate evidence, diagnostics, and uncertainty to shared explainability components.
- Removed the local `EvidenceSummaries` renderer from `DecisionOptionComparison`.

## Result

- Proposal option comparison evidence now renders through `DecisionEvidenceBlock`.
- `DecisionEvidenceBlock` delegates to `decisionEvidenceToEvidence` and shared `EvidenceList`, preserving backend evidence summaries and source references without local evidence rendering.
- Option comparison retains proposal-specific structure for option rows, benefits, costs, and recommendation state.

## Verification

- `npm test -- decisionOptionComparison.test.tsx`
- `npm test -- explainabilityDecisionAdapters.test.ts decisionOptionComparison.test.tsx`
