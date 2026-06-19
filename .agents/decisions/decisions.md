# Decisions

## Newly Authorized Decisions

- M2 implementation scope is complete.
- M2 automated verification is complete.
- M2 manual certification remains pending.
- `RepositoryWorkspaceProjection` remains the correct UI page contract for repository workspace state.
- The enforced architecture remains: filesystem authority, `ArtifactService`, `ArtifactInventory`, `RepositoryWorkspaceProjection`, Tauri bridge, then React.
- React must continue not owning artifact discovery, artifact classification, availability derivation, readiness derivation, or path resolution.
- Lightweight markdown previewing is sufficient for Epic 1 because the milestone objective is repository artifact management, not a rich markdown authoring platform.
- Future support for tables, task lists, syntax highlighting, or embedded diagrams should be handled independently if requirements emerge.
- Manual refresh should continue rebuilding state from the filesystem into projections instead of mutating existing UI state.
- M2 certification should verify desktop launch, repository registration, workspace opening, artifact viewing, artifact editing, artifact saving, refresh, and externally added or modified artifacts.
- M3 is ready to start.
- M3 should be sliced as backend rotation service first, rotation endpoints second, inventory refresh integration third, and UI rotation actions last.
- M3 rotation must reuse `ArtifactInventory` as the only source for current and historical handoff and decision classification.
- Rotation should update filesystem state and then rebuild inventory rather than adding a second rotation-specific discovery path.
- Current epic status is M0 certified, M1 awaiting manual certification, M2 awaiting manual certification, and M3 ready to start.
- The largest remaining Epic 1 architectural risk is preserving the `ArtifactInventory` to `RepositoryWorkspaceProjection` single-source-of-truth model as rotation, planning, and readiness are added.
