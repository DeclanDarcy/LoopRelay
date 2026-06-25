# Milestone 9 Obsolete UI Cleanup: Execution Artifact Diagnostics

## Scope

- Removed the duplicate local artifact diagnostics renderer from `ExecutionContextArtifactDiagnosticsList`.
- Kept the existing shared explainability adapter, `executionArtifactDiagnosticsToExplanation`, as the only presentation source for artifact path, size, threshold, tone, and evidence facts.
- Updated the component characterization test to assert shared `DiagnosticList` output rather than the obsolete `.diagnostic-item` markup.

## Disposition

- `ExecutionContextArtifactDiagnosticsList`: `Contextual`. Retained as the execution-context surface wrapper.
- Local `.diagnostic-item` artifact-size renderer: `Duplicate`. Removed.
- `executionArtifactDiagnosticsToExplanation`: `Primary`. Retained as the presentation adapter for execution artifact diagnostics.
- `DiagnosticList`: `Primary`. Retained as the shared diagnostics renderer.

## Verification

- `npm test -- executionContextArtifactDiagnosticsList.test.tsx`
- `npm test -- workflowAuthority.test.ts`
- `npm run build`

## Notes

- `npm run build` still reports the existing Vite chunk-size warning for the main bundle.
- The obsolete `src/CommandCenter.UI/src/lib/executionWorkflow.ts` target remains absent and guarded by `workflowAuthority.test.ts`.
