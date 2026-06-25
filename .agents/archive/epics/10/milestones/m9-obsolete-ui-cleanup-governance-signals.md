# Milestone 9 Obsolete UI Cleanup: Governance Signals

## Scope

- Continued Milestone 9 obsolete UI cleanup for governance lifecycle and analysis presentation.
- Removed local list rendering for lifecycle contributing factors and analysis warnings in `GovernanceWorkspace`.
- Preserved transfer lineage and continuity artifact lists as thin domain summaries rather than explainability diagnostics.

## Consolidation

- Added `governancePolicyFactorsToEvidence` to map lifecycle contributing factors into shared `ExplanationEvidence`.
- Added `governanceAnalysisWarningsToDiagnostics` to map analysis warnings into shared `ExplanationDiagnostic`.
- Changed lifecycle contributing factors to render through `EvidenceList`.
- Changed governance analysis warnings to render through `DiagnosticList`.

## Verification

- `npm test -- governanceWorkspace.test.tsx explainabilityGovernanceAdapters.test.ts`
- `npm run build`

## Notes

- `npm run build` still reports the existing Vite chunk-size warning for the main bundle.
- This slice intentionally did not replace recent lineage or continuity artifact lists because those are compact domain navigation/status summaries, not duplicate diagnostic or evidence renderers.
