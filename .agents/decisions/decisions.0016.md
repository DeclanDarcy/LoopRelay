# Decisions

## Newly Authorized Decisions

- The M5 mock harness is accepted as the right kind of test infrastructure because it validates React state, user interaction, projection consumption, and workspace behavior rather than duplicating backend logic.
- The mock harness should continue consuming production-shaped `RepositoryWorkspaceProjection` and `ArtifactInventory` data to reinforce the backend/UI boundary.
- Browser/mock certification is accepted as meaningful coverage for M5 workspace behavior, selection persistence, selection reconciliation, artifact lifecycle UX, and empty-state behavior.
- Browser/mock certification is not equivalent to native Tauri certification because it does not validate window lifecycle, IPC transport, native dialogs, backend process lifecycle, or platform integration.
- Native Tauri certification is the preferred next slice before automating the mock harness.
- Mock harness automation is valuable as regression protection, but should not be treated as final certification closure for the Tauri path.
- M5 is now primarily in certification and polish territory rather than implementation territory.
- If native Tauri certification passes cleanly, the workspace experience can be considered effectively complete and effort should shift toward remaining Epic 1 acceptance items instead of additional workspace mechanics.

## Authorized Native Certification Scope

- Repository switching.
- Artifact selection restore.
- Artifact edit/save.
- Refresh.
- Handoff rotation.
- Decision rotation.
- Repository removal.
- Restart recovery.
