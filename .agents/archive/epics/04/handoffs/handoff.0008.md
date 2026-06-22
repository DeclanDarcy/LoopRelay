# Handoff

## Slice Summary

Continued Milestone 0 Workstream 0.3 by extracting `useGitStatus(repositoryId)` as a read-only projection hook and recording the authorized `App.tsx` remaining-responsibility inventory.

## New State

- Rotated prior `.agents/handoffs/handoff.md` to `.agents/handoffs/handoff.0007.md`.
- Added `src/CommandCenter.UI/src/hooks/useGitStatus.ts`.
- Exported the hook from `src/CommandCenter.UI/src/hooks/index.ts`.
- Updated `src/CommandCenter.UI/src/App.tsx` so git status data, loading state, error state, automatic load, explicit refresh, and selection clearing are owned by `useGitStatus`.
- Preserved `App.tsx` ownership of commit preparation, commit readiness, push readiness, commit execution, push execution, and workflow interpretation.
- Added characterization coverage in `src/CommandCenter.UI/src/test/characterization/projectionHooks.test.tsx` for git status load/refresh and clearing when repository selection is removed.
- Marked `useGitStatus(repositoryId)` complete in `.agents/milestones/m0-frontend-foundations.md`.
- Added `.agents/audits/m0-app-responsibility-inventory.md` capturing remaining `App.tsx` navigation, projection, draft, workflow action, workflow gating, and presentation responsibilities.

## Verification

- `npm run lint` passed from `src/CommandCenter.UI`.
- `npm run build` passed from `src/CommandCenter.UI`.
- `npm run test` passed from `src/CommandCenter.UI`.
- `npm run test:e2e` passed from `src/CommandCenter.UI`.
- `dotnet test CommandCenter.slnx` passed from repo root.

## Next Slice

Continue Milestone 0 Workstream 0.3 with `useOperationalContextProposal(repositoryId, proposalId)` or `useContinuityDiagnostics(repositoryId)`. Prefer `useContinuityDiagnostics(repositoryId)` first because it is read-mostly and lower risk; keep continuity report generation in `App.tsx` as an explicit user action until a dedicated mutation hook slice is authorized.
