# Milestone 8: Capability Gaps, Cleanup, and Final Validation

## Tracking

- [ ] Milestone complete
- [x] Workstream 8.1: Capability Gap Closure
- [x] Workstream 8.2: Abort Execution Decision
- [x] Workstream 8.3: Global Navigation Decisions
- [x] Workstream 8.4: Repository Summary Completion
- [x] Workstream 8.5: Notifications Strategy
- [x] Workstream 8.6: Deviation Ledger
- [ ] Workstream 8.7: Remove Legacy Structure
- [ ] Workstream 8.8: Final UX Validation
- [ ] Certification complete

Goal: resolve remaining discrepancies between visible UX and real capability, then remove legacy migration scaffolding.

## Workstream 8.1: Capability Gap Closure

Classify every unresolved target affordance as:

- [ ] Implemented.
- [x] Deferred.
- [ ] Rejected.

Known gaps to resolve:

- [x] User-invokable abort execution.
- [x] Global Overview.
- [x] Global Executions.
- [x] Insights.
- [x] Notifications.
- [x] Dashboard/sidebar branch and dirty state for all repositories.
- [x] Ahead/behind counts outside selected repository git status.
- [x] Milestone criteria progress.
- [x] Cross-repository execution views.
- [x] Cross-repository continuity/insight rollups.

Rules:

- [x] Implement only with backend projection or backend capability.
- [x] Deferred and rejected items must be explicit in product UI or docs.

## Workstream 8.2: Abort Execution Decision

Option A: implement abort.

Required backend work:

- [ ] Add service contract method for abort/cancel.
- [ ] Add execution-session state transition tests.
- [ ] Add provider/process cancellation behavior.
- [ ] Add monitoring event.
- [ ] Add endpoint.
- [ ] Add Tauri command.
- [ ] Update workspace/session projections.
- [ ] Add UI action and disabled/error states.

Option B: omit or disable abort.

Required UI work:

- [x] Remove active abort affordance.
- [x] Show a restrained disabled state only if useful.
- [x] Ensure no palette abort command exists.

## Workstream 8.3: Global Navigation Decisions

Overview, Executions, and Insights need backend-backed behavior before they become functional product surfaces.

Possible outcomes:

- [ ] Overview becomes a repository landing page using dashboard projections only.
- [ ] Executions becomes a cross-repository execution projection after backend support.
- [ ] Insights becomes a continuity/operational insight projection after backend support.
- [x] Any of these can remain disabled/deferred until backend authority exists.

## Workstream 8.4: Repository Summary Completion

If the final sidebar/header requires branch, dirty count, ahead, behind, criteria progress, or rollups for every repository, add them to backend projections rather than calling git repeatedly from React.

Potential backend additions:

- [ ] Extend `RepositoryDashboardProjection` with branch, dirty count, ahead count, behind count, and captured timestamp.
- [x] Add milestone criteria projection only if criteria parsing is a real backend capability.
- [ ] Add tests in `RepositoryProjectionServiceTests` and `GitServiceTests`.
- [ ] Update Tauri DTOs and frontend shared types.

## Workstream 8.5: Notifications Strategy

Choose one:

- [ ] Implement a backend-backed notification projection.
- [x] Keep notification icon as disabled placement.
- [ ] Remove notification UI.

Do not show fake notification counts.

## Workstream 8.6: Deviation Ledger

Create `docs/frontend-modernization-deviations.md` before final validation.

Record every intentional difference between the target UX and the real product:

- [x] Description.
- [x] Location or surface.
- [x] Reason.
- [x] Category:
  - [x] Capability.
  - [ ] Product decision.
  - [ ] Technical constraint.
- [x] Outcome:
  - [ ] Implemented differently.
  - [x] Deferred.
  - [ ] Rejected.
- [x] Required backend projection or capability, if any.
- [x] Follow-up owner or issue reference, if known.

Rules:

- [x] A missing capability is not a defect if it is recorded and represented honestly in the UI.
- [x] An unrecorded mismatch found during final validation is a defect.
- [x] The ledger must be self-contained and explain each difference directly.

## Workstream 8.7: Remove Legacy Structure

- [ ] Delete unused CSS from the old layout.
- [ ] Delete duplicate DTOs.
- [ ] Delete obsolete helpers after feature modules own them.
- [ ] Remove any temporary migration adapters.
- [ ] Keep `devTauriMock.ts` aligned with shared types and current visible states.
- [ ] Ensure `App.tsx` is only composition.

## Workstream 8.8: Final UX Validation

Validate:

- [ ] Layout.
- [ ] Navigation.
- [ ] Interaction.
- [ ] Workspace structure.
- [ ] Information density.
- [ ] Projection ownership.
- [ ] Workflow ownership.
- [ ] Responsive behavior.
- [ ] Keyboard behavior.
- [ ] Error/loading/empty states.

Every remaining deviation must be classified as:

- [ ] Intentional product decision.
- [ ] Capability-based deferral.
- [ ] Defect to fix.

### Certification

- [ ] Every intentional or capability-based deviation is recorded in `docs/frontend-modernization-deviations.md`.
- [ ] Every defect is fixed or converted into an explicit deferred/rejected product decision before completion.
