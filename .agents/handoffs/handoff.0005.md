# Handoff

## Slice Summary

Advanced Milestone 0 Workstream 0.3A by extracting `useExecutionContextPreview` only, preserving explicit user-triggered context preview behavior.

## New State

- Rotated prior `.agents/handoffs/handoff.md` to `.agents/handoffs/handoff.0004.md`.
- Added `src/CommandCenter.UI/src/hooks/useExecutionContextPreview.ts`.
- Exported the hook from `src/CommandCenter.UI/src/hooks/index.ts`.
- Updated `src/CommandCenter.UI/src/App.tsx` so execution-context preview data, loading state, error state, and the preview command now come from `useExecutionContextPreview`.
- Preserved `App.tsx` ownership of selected milestone navigation, start-execution gating, workflow actions, and explicit preview invalidation via `setExecutionContext(null)`.
- Added characterization in `src/CommandCenter.UI/src/test/characterization/projectionHooks.test.tsx` proving previews are not auto-built and stale previews remain visible across milestone changes until explicit rebuild or clear.
- Marked `useExecutionContextPreview(repositoryId, milestonePath)` complete in `.agents/milestones/m0-frontend-foundations.md`; Workstream 0.3 remains open.

## Verification

- `npm run lint` passed from `src/CommandCenter.UI`.
- `npm run build` passed from `src/CommandCenter.UI`.
- `npm run test` passed from `src/CommandCenter.UI`.
- `npm run test:e2e` passed from `src/CommandCenter.UI`.
- `dotnet test CommandCenter.slnx` passed from repo root.

## Next Slice

Continue Milestone 0 Workstream 0.3 with M0.3B: extract `useExecutionSession(repositoryId, sessionId)` separately, with characterization around session lifecycle, refresh, reattachment, and recovery before moving behavior.
