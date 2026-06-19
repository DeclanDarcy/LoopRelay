# Decisions

## Newly Authorized Decisions

- M1 backend slice is accepted as complete.
- `GET /api/repositories` returning `RepositoryDashboardProjection[]` is the correct architectural boundary and should be preserved.
- Repository identity remains `Repository.Id`; repository uniqueness is determined by normalized absolute path.
- `RepositoryProjectionService` owns dashboard projection composition so the UI does not derive availability or combine backend state.
- Current milestone sequencing remains M0 foundation, M1 repository lifecycle, M2 artifact infrastructure, M3 artifact lifecycle, M4 planning, and M5 workspace composition.
- Automated M1 certification should cover `Available` and `Missing` repository availability; `AccessDenied` should remain a manual certification case unless filesystem permission failure becomes injectable later.
- M1 UI completion should proceed in this order: Tauri directory picker, add repository flow, repository dashboard, repository details view, remove repository flow.
- The Tauri directory picker should return only the absolute repository path and should not perform validation in the shell.
- Repository validation messages should come directly from backend responses.
- Repository dashboard should stay simple during M1 and display name, availability, and path.
- Repository details view should display name, path, and availability only; artifact-related workspace concerns remain out of M1.
- Decisions rotation is now established alongside handoff rotation.

## Current Milestone Status

- M0 is certified.
- M1 backend is complete.
- M1 UI is in progress.
- M1 overall is approximately 70-75% complete.
