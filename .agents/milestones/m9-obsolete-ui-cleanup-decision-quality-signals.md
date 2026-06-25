# Milestone 9 Obsolete UI Cleanup: Decision Quality Signals

## Scope

- Audited decision recommendation, quality, burden, and governance explanation surfaces for duplicate local diagnostics and evidence rendering.
- Classified recommendation, burden, and governance explanation components as thin wrappers over shared explainability components with domain-specific framing retained.
- Removed the duplicate local priority quality signal card renderer from `DecisionQualityPanel`.
- Priority quality signals now render through `decisionQualitySignalsToDiagnostics` and shared `DiagnosticList`.

## Disposition

- `DecisionQualityPanel` priority signal card renderer: `Duplicate`. Removed.
- `decisionQualitySignalsToDiagnostics`: `Primary`. Retained as the quality signal presentation adapter.
- `DiagnosticList`: `Primary`. Retained as the shared diagnostics renderer for priority quality signals.
- `DecisionRecommendationExplanation`: `Thin wrapper over shared components`. Retained.
- `DecisionBurdenExplanation`: `Thin wrapper over shared components`. Retained because it frames the selected burden signal while delegating signal evidence and burden diagnostics.
- `DecisionGovernanceExplanation`: `Thin wrapper over shared components`. Retained because it groups governance findings for navigation while rendering finding details through shared diagnostics.

## Verification

- `npm test -- decisionQualityPanel.test.tsx`

## Notes

- This cleanup preserves the Decisions workspace primary functionality and removes only the redundant quality signal presentation path.
