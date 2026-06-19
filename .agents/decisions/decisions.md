# Decisions

## Newly Authorized Decisions

- `ArtifactInventory` is the correct authority for artifact state and should remain the single source feeding `RepositoryWorkspaceProjection`.
- The canonical application boundary for workspace page state is `RepositoryWorkspaceProjection`, not raw service or endpoint composition in React.
- Restart should rebuild the artifact inventory cache from the filesystem, and explicit refresh should replace the cache from the filesystem rather than mutating it incrementally.
- `RepositoryWorkspaceProjection.Readiness` is structurally present but semantically provisional until `PlanningService` becomes authoritative in M4.
- M2 is approximately 80-85% complete: backend discovery, classification, load, save, root-safe resolution, artifact inventory, workspace projection, and refresh are complete; workspace UI, artifact editor UI, and manual certification remain.
- Next M2 work should be split into focused slices: Tauri command bridge first, workspace projection rendering second, read-only artifact viewer third, and editing/saving fourth.
- `GET /workspace` should remain the primary workspace endpoint for page composition.
- `GET /artifacts` should be minimized for UI page composition and treated mainly as support for content loading, saving, future rotation operations, or narrower artifact-specific workflows.
- The React workspace must consume projections faithfully and must not accumulate independent artifact-state logic that competes with `RepositoryWorkspaceProjection`.
- Early current/historical classification and centralized inventory generation are accepted as M3 risk reduction because they shrink M3 to current resolution, rotation, and historical numbering rather than inventory redesign.

## Current Epic Status

- M0 is certified.
- M1 is functionally complete and awaiting full desktop certification.
- M2 backend is complete or nearly complete, with UI integration still remaining.
- Epic 1 is tracking cleanly; the largest current M2 risk is preserving projection authority during React workspace implementation.
