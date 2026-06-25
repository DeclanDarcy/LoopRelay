# Milestone 9 Obsolete UI Cleanup: Decision and Execution Diagnostics/Evidence

## Scope

- Removed duplicate decision proposal generation validation and command diagnostic list rendering from `DecisionLifecycleTab`.
- Routed proposal generation diagnostics through shared `DiagnosticList` while preserving existing accessibility labels and backend-facing text.
- Removed duplicate decision proposal lineage diagnostic rendering from `DecisionRevisionHistory`.
- Routed proposal lineage diagnostics through shared `DiagnosticList`.
- Removed duplicate decision source attribution list rendering from revision history.
- Routed revision history source attribution through shared `EvidenceList` using the decision source-reference adapter.
- Removed duplicate execution governed-conflict evidence list rendering from `ExecutionContextValidationList`.
- Routed governed-conflict evidence through shared `EvidenceList` while keeping the conflict detail card as domain composition.

## Retained

- Decision lifecycle summaries, generated proposal counters, revision selection, revision comparisons, and lineage event sequencing remain domain-specific presentation.
- Execution governed-conflict detail cards remain domain-specific presentation because they expose affected context, affected prompt section, resolution path, and authority.
- Accessibility labels used by characterization tests remain as thin wrappers around shared explainability components.

## Verification

- `npm test -- decisionLifecycleNavigation.test.tsx decisionCandidateBrowser.test.tsx`
- `npm test -- executionContextValidationList.test.tsx executionEventFeed.test.tsx`
- `npm run build`

## Notes

- `npm run build` still reports the existing Vite chunk-size warning for the main bundle.
