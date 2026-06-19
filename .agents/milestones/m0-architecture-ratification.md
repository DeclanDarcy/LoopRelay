# M0: Architecture Ratification

## Goal

Establish application skeleton, service boundaries, persistence abstractions, IPC, and initial tests.

## Implementation Tasks

- [ ] Create solution layout:
   - [ ] `src/CommandCenter.UI`
   - [ ] `src/CommandCenter.Shell`
   - [ ] `src/CommandCenter.Backend`
   - [ ] `tests/CommandCenter.Backend.Tests`
   - [ ] `docs/architecture.md`
- [ ] Scaffold React/TypeScript UI.
- [ ] Scaffold Tauri shell and wire React to Tauri invoke calls.
- [ ] Scaffold .NET backend sidecar.
- [ ] Add backend dependency injection and service registration.
- [ ] Define `IRepositoryService`, `IArtifactService`, `IArtifactStore`, `IRepositoryProjectionService`, and `IApplicationConfigurationStore`.
- [ ] Implement `MemoryArtifactStore`.
- [ ] Implement `FileSystemArtifactStore`.
- [ ] Implement application configuration load/save with empty repository list support.
- [ ] Add backend ping endpoint returning `Pong`.
- [ ] Add Tauri command that calls backend ping.
- [ ] Add UI button labeled `Ping Backend` and display the returned `Pong`.
- [ ] Document architecture boundaries, service ownership, persistence strategy, artifact store philosophy, manual refresh policy, and the deferred repository snapshot boundary in `docs/architecture.md`.

## Tests

- [ ] Memory store write, read, exists, delete.
- [ ] Filesystem store write, read, exists, delete.
- [ ] Filesystem store persistence after new store instance.
- [ ] Configuration write, reload, and read.
- [ ] Ping endpoint returns `Pong`.

## Acceptance Criteria

- [ ] Application launches.
- [ ] React can call Tauri.
- [ ] Tauri can call .NET backend.
- [ ] UI displays `Pong` from the backend.
- [ ] Artifact stores pass tests.
- [ ] Configuration persistence passes tests.
- [ ] Architecture documentation exists.
