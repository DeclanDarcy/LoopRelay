# Milestone 9 Obsolete UI Cleanup: Decision Explanations

## Scope

- Audited recommendation, burden, and governance explanation surfaces for duplicate evidence, diagnostics, and fact-chip renderers.
- Removed local recommendation metadata/rationale presentation from `DecisionRecommendationExplanation`.
- Removed local burden fact chips, winning-signal card, and direct diagnostics renderer from `DecisionBurdenExplanation`.
- Confirmed `DecisionGovernanceExplanation` remains a thin grouping/navigation wrapper because finding presentation already delegates to shared diagnostics.

## Result

- Decision recommendation details now render through `DecisionBasis` and `decisionRecommendationToExplanation`.
- Human authoring burden details now render through `DecisionBasis` and the new `humanAuthoringBurdenExplanationToExplanation` adapter.
- Backend-authored burden selection rule, winning signal, source evidence, known/inferred constraints, and diagnostics remain visible through shared explainability components.
- Governance findings retain severity/category grouping and related-proposal navigation while each finding renders through shared `DiagnosticList`.

## Verification

- `npm test -- explainabilityDecisionAdapters.test.ts decisionProposalViewer.test.tsx decisionQualityPanel.test.tsx`
- `npm run build`
