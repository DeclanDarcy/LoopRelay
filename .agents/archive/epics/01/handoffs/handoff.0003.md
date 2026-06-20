# Handoff

## New State This Slice

- M1 backend repository-management slice is implemented.
- `RepositoryService` now registers valid Git repositories, normalizes absolute paths, rejects duplicate paths, creates `.agents/` when missing, persists registrations through `ApplicationConfigurationStore`, and removes registrations without deleting repository files.
- `RegisterRepositoryRequest` was added for backend API registration requests.
- Backend API now exposes:
  - `GET /api/repositories`
  - `POST /api/repositories`
  - `DELETE /api/repositories/{repositoryId}`
- `RepositoryProjectionService.GetDashboardAsync()` now returns registered repositories with `RepositoryAvailability` projected as `Available`, `Missing`, or `AccessDenied` where the filesystem allows that classification.
- `.agents/milestones/m1-repository-management.md` now marks completed backend tasks and completed repository-management tests.
- Previous handoff was archived as `.agents/handoffs/handoff.0002.md`.

## Verification

- `dotnet test CommandCenter.slnx` passes: 18 tests.
- New tests cover successful registration, invalid paths, non-Git directories, duplicate normalized paths, case-difference duplicates, mixed-separator duplicates, `.agents/` creation, preserving existing `.agents/` contents, reload persistence, removal without filesystem deletion, dashboard `Available`, and dashboard `Missing`.

## Immediate Gaps

- M1 UI work has not started.
- `AccessDenied` projection logic exists, but the access-denied test remains unchecked because this environment has not provided a reliable permission-denied directory simulation.
- Repository workspace/details UI and native directory picker are still open.
