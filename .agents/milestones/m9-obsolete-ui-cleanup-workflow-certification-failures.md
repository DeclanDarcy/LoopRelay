# Milestone 9 Obsolete UI Cleanup: Workflow Certification Failures

## Scope

- Continued Milestone 9 duplicate renderer cleanup for workflow certification failure presentation.
- Kept workflow certification authority in backend projections and added only a narrow presentation adapter.

## Changes

- Added `workflowCertificationFailuresToDiagnostics` in `src/CommandCenter.UI/src/lib/explainability/workflow.ts`.
- Changed `WorkflowCertificationPanel` to render `certification.failures` through shared `DiagnosticList` instead of a local workflow-specific list block.
- Added characterization coverage for the new workflow certification failure adapter.

## Verification

- `npm test -- workflowPanels.test.tsx explainabilityWorkflowAdapters.test.ts`
- `npm run build`

## Notes

- The UI build still reports the existing Vite chunk-size warning for the main bundle.
