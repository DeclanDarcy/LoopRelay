# Decisions

## Newly Authorized Decisions

- The repository removal selection-memory cleanup is accepted as the correct M5 fix.
- The M5 workspace certification mock should cover all readiness states: `Ready`, `MissingPlan`, and `MissingMilestones`.
- `PlanOnlyRepo -> MissingMilestones` is accepted as useful browser certification coverage.
- The current authority chain remains correct: filesystem state flows through backend services into `ArtifactInventory`, then `RepositoryWorkspaceProjection`, then React.
- There is no current evidence of UI-derived readiness, artifact state, or lifecycle state.
- The browser certification harness now covers the core M5 behavioral surface sufficiently for workflow-level confidence.
- Remaining M5 uncertainty is platform-specific native desktop behavior, not workspace logic.
- The next and final M5 certification slice should be a single uninterrupted native Tauri desktop pass covering repository registration, switching, selection restore, artifact edit/save/refresh persistence, handoff and decision rotation, repository removal cleanup, quit/restart, and workspace recovery.
- Do not add additional workspace mechanics unless final native desktop certification uncovers a concrete defect.
