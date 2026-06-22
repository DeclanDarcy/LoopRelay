# Decisions

## Newly Authorized

- Proceed next with `useGitStatus(repositoryId)` before touching workflow-related extractions.
- `useGitStatus(repositoryId)` is a projection hook only.
- `useGitStatus(repositoryId)` may own status loading, explicit refresh, loading state, error state, and the git status projection.
- `useGitStatus(repositoryId)` must not own commit readiness, push readiness, workflow readiness, ahead/behind interpretation, or commit/push workflow meaning.
- After the Git hook extraction, perform a deliberate `App.tsx` remaining-responsibility inventory without refactoring.
- The `App.tsx` inventory should classify remaining responsibilities as navigation state, draft state, workflow actions, workflow gating, view composition, and presentation.
- The `App.tsx` inventory should map remaining state variables into navigation, projection, draft, and workflow categories.
- Add a Projection Ownership Audit as an M0 exit certification artifact.
- The Projection Ownership Audit should prove repository, workspace, artifact, execution preview, execution session, execution events, and git status projections are hook-owned.
- The Projection Ownership Audit should verify no duplicate projection loading paths remain inside `App.tsx`.

## Validation Expected For Next Slice

- Keep `useGitStatus(repositoryId)` read-only except for explicit status refresh.
- Keep commit preparation, commit, push, and workflow gating in `App.tsx` until dedicated slices.
- Preserve observable behavior.
- `npm run lint`
- `npm run build`
- `npm run test`
- `npm run test:e2e`
- `dotnet test CommandCenter.slnx`
