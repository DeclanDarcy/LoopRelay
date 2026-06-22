# Handoff

## Slice Summary

Continued Milestone 0 Workstream 0.3 by extracting `useContinuityDiagnostics(repositoryId)` as a read-only projection hook.

## New State

- Rotated prior `.agents/handoffs/handoff.md` to `.agents/handoffs/handoff.0008.md`.
- Added `src/CommandCenter.UI/src/hooks/useContinuityDiagnostics.ts`.
- Exported the hook from `src/CommandCenter.UI/src/hooks/index.ts`.
- Updated `src/CommandCenter.UI/src/App.tsx` so continuity diagnostics data, loading state, error state, automatic load, explicit refresh, and selection clearing are owned by `useContinuityDiagnostics`.
- Preserved `App.tsx` ownership of continuity report generation as an explicit workflow action; report generation still updates diagnostics with the backend-returned report projection.
- Added characterization coverage in `src/CommandCenter.UI/src/test/characterization/projectionHooks.test.tsx` for continuity diagnostics load/refresh and clearing when repository selection is removed.
- Marked `useContinuityDiagnostics(repositoryId)` complete in `.agents/milestones/m0-frontend-foundations.md`.

## Verification

- `npm run lint` passed from `src/CommandCenter.UI`.
- `npm run build` passed from `src/CommandCenter.UI`.
- `npm run test` passed from `src/CommandCenter.UI`.
- `npm run test:e2e` passed from `src/CommandCenter.UI`.
- `dotnet test CommandCenter.slnx` passed from repo root.

## Next Slice

Continue Milestone 0 Workstream 0.3 with an evidence-driven reassessment before extracting more hooks. Prefer `useOperationalContextProposal(repositoryId, proposalId)` only if it can remain projection-loading-only; keep proposal generation, edit, accept, reject, promote, review gating, and current-content comparison in `App.tsx` until a workflow-action extraction is explicitly authorized.
