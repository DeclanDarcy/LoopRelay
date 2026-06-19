# Handoff

## New State This Slice

- Continued M4 planning and readiness infrastructure.
- `PlanningService` now derives plan presence from `.agents/plan.md`.
- `PlanningService` now enumerates `.agents/milestones/*.md` as milestone inventory without parsing milestone content.
- Readiness is now authoritative:
  - missing plan -> `MissingPlan`
  - plan with no milestone markdown files -> `MissingMilestones`
  - plan with at least one milestone markdown file -> `Ready`
- Added `PlanningProjection` and `GET /api/repositories/{repositoryId}/planning`.
- Repository dashboard and workspace projections now receive real planning readiness from the file-backed planning service.
- Added backend tests for plan detection, missing/empty milestone directories, milestone discovery, arbitrary milestone markdown content, all readiness states, and refresh updating readiness after a milestone is added.
- M4 checklist is marked complete for backend tasks, UI projection rendering already present, tests, and acceptance criteria.
- Previous handoff was archived as `.agents/handoffs/handoff.0008.md`.

## Verification

- `dotnet test CommandCenter.slnx` passes: 42 tests.
- `npm run lint` passes.
- `npm run build` passes.
- `cargo check` passes.

## Immediate Gaps

- Full desktop/manual certification still has not been run through the packaged Tauri app.
- M1, M2, M3, and M4 still need manual certification through the desktop path.
- No dedicated UI interaction test was added for readiness and milestone inventory rendering.
