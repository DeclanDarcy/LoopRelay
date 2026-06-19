# Decisions

## Newly Authorized Decisions

- M1 implementation is accepted as architecturally consistent, with manual desktop certification remaining as the final certification step.
- The React/Tauri/backend boundary should remain:
  - React renders projections and invokes commands.
  - Tauri picks directories and proxies requests.
  - Backend validates repositories, manages registrations, derives availability, and composes projections.
- Repository validation must remain backend-owned; UI and Tauri should display or transport backend results rather than duplicate validation rules.
- `GET /api/repositories -> RepositoryDashboardProjection[]` remains the correct dashboard contract and should continue as readiness, milestones, artifact inventories, current handoff status, and current decisions status expand.
- API enum serialization as names such as `Available` is accepted as the correct desktop-app contract.
- M1 can be considered complete if manual certification passes for native directory picking, repository registration, dashboard refresh, repository selection, details view, repository removal, `.agents` creation, and non-destructive removal.
- M2 should begin after M1 manual certification.
- M2 should proceed in focused slices:
  - Slice 1: artifact domain models and discovery only.
  - Slice 2: artifact inventory projection.
  - Slice 3: read-only artifact content loading and viewer support.
  - Slice 4: artifact content saving.
  - Slice 5: manual refresh pipeline and cache rebuilding.
- Artifact discovery should classify artifacts around `ArtifactFamily` immediately so M3 lifecycle behavior does not require retrofitting.

## Current Epic Status

- M0 architecture ratification is certified.
- M1 repository management implementation is complete and pending manual certification.
- M2 artifact infrastructure is ready to start after M1 certification.
