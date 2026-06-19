# Decisions

## Newly Authorized Decisions

- The M2 discovery/load/save slice is accepted as architecturally aligned because artifact resolution remains backend-owned.
- UI-facing artifact operations must continue to use repository-relative paths only; backend services must resolve those paths through registered repository roots before filesystem access.
- Repository-root safety tests are considered high-value certification coverage and should remain part of the artifact infrastructure test suite.
- Early current/historical classification through `ArtifactVersionKind` is accepted as architectural convergence, not M3 milestone leakage.
- M3 should build on the existing current/historical classification and focus on rotation, current resolution, and historical resolution.
- The narrow `.gitignore` unignore for `src/CommandCenter.Backend/Artifacts/` is accepted as the correct fix; the backend `Artifacts` domain directory should not be renamed to satisfy tooling.
- M2 status is approximately 50% complete: discovery, classification, load, save, and repository-root safety are complete; projection integration, workspace endpoints, refresh pipeline, and UI artifact experience remain incomplete.
- `ArtifactInventory` must be the single authoritative artifact projection for M2 and downstream milestones.
- Workspace, explorer, summary, and dashboard behavior must derive from `ArtifactInventory` instead of rediscovering artifacts independently.
- `RepositoryWorkspaceProjection` should be implemented before UI artifact work so it becomes the contract for the rest of Epic 1.
- The next backend slice should add workspace and refresh endpoints, especially `GET /workspace` and `POST /refresh`.
- Refresh must reconstruct state from disk according to the filesystem-authority principle; memory cache remains performance-only.
- Refresh tests should certify both externally added files appearing after refresh and externally deleted files disappearing after refresh.
- Avoid introducing multiple independent inventory DTO shapes such as separate explorer, summary, or dashboard DTOs that rediscover artifacts differently.

## Current Epic Status

- M0 architecture ratification is certified.
- M1 repository management is ready for certification after desktop validation.
- M2 artifact infrastructure has completed discovery/load/save/root-safety foundations and should proceed to projection integration next.
- The next meaningful certification risk is projection and refresh fidelity to filesystem authority.
