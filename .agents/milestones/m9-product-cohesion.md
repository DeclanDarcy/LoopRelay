## Milestone 9: Product Cohesion

### Objective

Make the application feel unified, not merely smaller. Remove fragmentation so every semantic concept has one authority, one projection, one primary navigation path, and one primary presentation.

### Implementation

- [ ] Audit navigation for workflow, decision sessions, decisions, execution, reasoning, operational context, repository, health, diagnostics, and certification.
- [ ] Define one primary home and allowed contextual links for each capability.
- [ ] Consolidate duplicate workflow displays, governance summaries, execution monitoring views, reasoning confidence displays, continuity evolution summaries, health widgets, and certification summaries.
- [ ] Review backend endpoints and classify each as `Keep`, `Redirect`, `Internal`, or `Remove`.
- [ ] Review projections and classify each as authoritative, derived consumer, compatibility, or retire.
- [ ] Review frontend state and classify each state value as authoritative view state, derived display state, disposable UI state, or duplicate domain state.
- [ ] Normalize interaction patterns for review, accept, reject, transfer, recover, generate, refine, commit, push, promote, archive, and supersede:
   - [ ] action
   - [ ] eligibility
   - [ ] evidence
   - [ ] result
   - [ ] diagnostics
- [ ] Build or update a unified operational dashboard that summarizes:
   - [ ] workflow
   - [ ] governance
   - [ ] execution
   - [ ] operational context
   - [ ] reasoning
   - [ ] repository
   - [ ] health
   - [ ] certification
   - [ ] diagnostics
- [ ] Delete obsolete UI components, old workflow derivation, duplicate panels, temporary views, deprecated widgets, obsolete summaries, and unused client functions after replacements are tested.
- [ ] Align terminology across statuses, health, diagnostics, recovery, certification, governance, execution, and explainability.

### Likely Cleanup Targets

- [ ] `src/CommandCenter.UI/src/lib/executionWorkflow.ts` after workflow projection integration.
- [ ] Any rail or status component that still consumes `RepositoryExecutionState` as a workflow source.
- [ ] Duplicate decision recommendation, quality, governance, and influence summaries replaced by explainability components.
- [ ] Duplicate health renderers replaced by shared `HealthView`.
- [ ] Duplicate diagnostics renderers replaced by shared `DiagnosticList`.

### Tests

- [ ] Navigation characterization tests.
- [ ] UI tests proving primary surfaces remain reachable.
- [ ] Static or unit tests for removed duplicate helpers where practical.
- [ ] Backend endpoint disposition tests for retained routes.

### Exit Criteria

- [ ] Every major capability has one obvious primary navigation path.
- [ ] Every semantic concept has one authoritative projection and one primary presentation.
- [ ] Duplicate endpoints, projections, views, and components are removed or intentionally retained with documented purpose.
- [ ] Interaction patterns are consistent across the product.
- [ ] The dashboard gives a coherent overview without replacing detailed workspaces.
