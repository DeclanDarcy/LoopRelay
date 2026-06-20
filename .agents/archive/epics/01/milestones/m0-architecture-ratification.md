# M0: Architecture Ratification

## Goal

Establish application skeleton, service boundaries, persistence abstractions, IPC, and initial tests.

## Implementation Tasks

- [x] Create solution layout:
   - [x] `src/CommandCenter.UI`
   - [x] `src/CommandCenter.Shell`
   - [x] `src/CommandCenter.Backend`
   - [x] `tests/CommandCenter.Backend.Tests`
   - [x] `docs/architecture.md`
- [x] Scaffold React/TypeScript UI.
- [x] Scaffold Tauri shell and wire React to Tauri invoke calls.
- [x] Scaffold .NET backend sidecar.
- [x] Add backend dependency injection and service registration.
- [x] Define `IRepositoryService`, `IArtifactService`, `IArtifactStore`, `IRepositoryProjectionService`, and `IApplicationConfigurationStore`.
- [x] Implement `MemoryArtifactStore`.
- [x] Implement `FileSystemArtifactStore`.
- [x] Implement application configuration load/save with empty repository list support.
- [x] Add backend ping endpoint returning `Pong`.
- [x] Add Tauri command that calls backend ping.
- [x] Add UI button labeled `Ping Backend` and display the returned `Pong`.
- [x] Document architecture boundaries, service ownership, persistence strategy, artifact store philosophy, manual refresh policy, and the deferred repository snapshot boundary in `docs/architecture.md`.

## Tests

- [x] Memory store write, read, exists, delete.
- [x] Filesystem store write, read, exists, delete.
- [x] Filesystem store persistence after new store instance.
- [x] Configuration write, reload, and read.
- [x] Ping endpoint returns `Pong`.

## Acceptance Criteria

- [x] Application launches.
- [x] React can call Tauri.
- [x] Tauri can call .NET backend.
- [x] UI displays `Pong` from the backend.
- [x] Artifact stores pass tests.
- [x] Configuration persistence passes tests.
- [x] Architecture documentation exists.
