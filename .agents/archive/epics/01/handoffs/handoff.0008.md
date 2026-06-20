# Handoff

## New State This Slice

- Continued M3 artifact lifecycle management.
- Backend artifact discovery now only includes valid rotating artifact names:
  - `.agents/handoffs/handoff.md`
  - `.agents/handoffs/handoff.NNNN.md` where `NNNN` is four digits and greater than zero
  - `.agents/decisions/decisions.md`
  - `.agents/decisions/decisions.NNNN.md` where `NNNN` is four digits and greater than zero
- Added `IArtifactRotationService` and `ArtifactRotationService`.
- Added backend rotation endpoints:
  - `POST /api/repositories/{repositoryId}/artifacts/rotate-current-handoff`
  - `POST /api/repositories/{repositoryId}/artifacts/rotate-current-decisions`
- Rotation archives the current artifact content to the next historical filename, preserves the current artifact, rejects unsupported families, and fails rather than overwriting a detected target.
- Tauri now bridges current handoff/current decisions rotation commands and returns refreshed `RepositoryWorkspaceProjection`.
- React now splits current and historical handoffs/decisions into separate explorer categories.
- React exposes rotate actions only for current handoff/current decisions and refreshes repository/workspace state after rotation.
- M3 checklist is marked complete for implemented code, automated tests, and build verification.
- Previous handoff was archived as `.agents/handoffs/handoff.0007.md`.

## Verification

- `dotnet test CommandCenter.slnx` passes: 33 tests.
- `npm run lint` passes.
- `npm run build` passes.
- `cargo check` passes.
- `cargo fmt` still cannot run because `rustfmt` is not installed for `stable-x86_64-pc-windows-msvc`.

## Immediate Gaps

- Full desktop/manual certification has not been run through the packaged Tauri app.
- M1, M2, and M3 still need manual certification through the desktop path.
- No browser/desktop interaction test was added for the rotate button workflow.
