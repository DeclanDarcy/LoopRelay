# Command Center Architecture

Command Center is split into a React UI, a thin Tauri shell, and a .NET backend sidecar. The backend owns repository and artifact business logic; React owns presentation state; Tauri owns desktop windowing, native dialogs, sidecar lifecycle, and IPC bridging.

## Filesystem Authority

Repository-owned state remains in each repository under `.agents`. Command Center reads, edits, and organizes those files, but it does not replace them with a private database. Missing artifact files and directories are valid states and must be projected explicitly rather than treated as application failures.

## Persistence Strategy

Application configuration stores local Command Center metadata, starting with registered repositories. Repository artifacts remain in the repository filesystem. In-memory state is a performance cache only and must be rebuildable from configuration plus filesystem scans.

## Service Ownership

Backend behavior is exposed through contracts:

- `IRepositoryService` for repository registration and removal.
- `IArtifactService` for artifact discovery, load, and save.
- `IArtifactStore` for persistence access.
- `IRepositoryProjectionService` for dashboard and workspace projections.
- `IApplicationConfigurationStore` for local application configuration.

The M0 implementation defines these boundaries and implements the artifact stores and configuration store. Repository registration, artifact discovery, rotation, planning readiness, and workspace projection behavior are implemented by later milestones.

## Artifact Store Philosophy

`IArtifactStore` is deliberately low level. It can read, write, delete, list, and check existence, but it does not classify artifacts, validate repositories, determine readiness, or decide lifecycle behavior. Higher-level services own those decisions.

## Manual Refresh Policy

Epic 1 uses explicit refresh. The application must not add filesystem watchers, background polling, or automatic rescans. Refresh rebuilds projections from repository filesystem state when the user asks for it.

## Deferred Snapshot Boundary

`IRepositorySnapshotService` is deferred. Git branch state, modified file counts, and execution-context snapshots belong to a later execution-oriented phase and are intentionally outside M0 and Epic 1 repository artifact management.
