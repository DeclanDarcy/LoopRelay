# M5: Repository Workspace Experience

## Goal

Unify repository management, artifact navigation, artifact lifecycle, planning state, and readiness into a coherent repository-centric workspace.

## Navigation Model

Primary navigation:

```text
Repository Dashboard
```

Secondary navigation:

```text
Repository Workspace
```

Tertiary navigation:

```text
Artifact Explorer
```

## Workspace Layout

Repository workspace must include:

- Repository header
- Repository summary
- Artifact explorer
- Artifact viewer/editor
- Refresh action

Repository header displays:

- Repository name
- Repository path
- Availability
- Readiness status

Repository summary displays:

- Plan present or missing
- Operational context present or missing
- Milestone count
- Current handoff present or missing
- Current decisions present or missing
- Readiness status

Artifact explorer displays:

- Plan
- Operational Context
- Milestones
- Current Handoff
- Historical Handoffs
- Current Decisions
- Historical Decisions

## Dashboard Projection

Repository dashboard should display:

- Repository name
- Repository path
- Availability
- Readiness
- Milestone count
- Current handoff status
- Current decisions status

## Missing Artifact Experience

If a repository contains only `.agents/`, the workspace still loads and displays:

```text
Plan: Missing
Operational Context: Missing
Milestones: 0
Current Handoff: Missing
Current Decisions: Missing
Readiness: Missing Plan
```

If a repository contains `plan.md` and no milestones, display:

```text
Plan: Present
Milestones: 0
Readiness: Missing Milestones
```

## Refresh Pipeline

`IRepositoryProjectionService` owns the refresh pipeline.

Refresh performs:

```text
Artifact discovery
Artifact classification
Plan resolution
Milestone resolution
Readiness resolution
Dashboard and workspace projection rebuild
UI update
```

## Workspace State

When switching repositories, the workspace must load the selected repository projection.

When returning to a repository, restore the previously selected artifact where practical. If the artifact no longer exists, clear the selection and show the current repository summary.

## UI Tasks

- [ ] Build left repository navigation.
- [ ] Build repository header.
- [ ] Build repository summary.
- [ ] Build categorized artifact explorer.
- [ ] Build markdown viewer/editor.
- [ ] Wire artifact selection to content loading.
- [ ] Wire save action to backend.
- [ ] Wire refresh action to backend.
- [ ] Wire rotation actions to backend.
- [ ] Preserve selected artifact per repository where practical.
- [ ] Represent missing and empty states explicitly.

## Backend Tasks

- [ ] Implement `ArtifactInventory`.
- [ ] Implement `RepositoryDashboardProjection`.
- [ ] Implement `RepositoryWorkspaceProjection`.
- [ ] Implement `IRepositoryProjectionService`.
- [ ] Ensure dashboard endpoint returns backend-composed dashboard projections.
- [ ] Ensure workspace endpoint returns all summary data in one call.
- [ ] Ensure refresh endpoint rebuilds repository state from disk through `IRepositoryProjectionService`.
- [ ] Ensure repository dashboard endpoint includes status projections efficiently.

## Tests

- [ ] Workspace projection includes repository metadata.
- [ ] Workspace projection includes artifact inventory.
- [ ] Workspace projection includes current handoff and current decisions status.
- [ ] Workspace projection includes plan status, milestone count, and readiness.
- [ ] Dashboard projection includes current handoff and current decisions status.
- [ ] Missing artifacts are represented.
- [ ] Artifact navigation loads correct content.
- [ ] Refresh detects external filesystem changes.
- [ ] Switching repositories loads the correct workspace.

## Acceptance Criteria

- [ ] User can manage repositories from a dashboard.
- [ ] User can open a repository workspace.
- [ ] User can browse plans, milestones, operational context, handoffs, and decisions.
- [ ] User can view current and historical handoffs.
- [ ] User can view current and historical decisions.
- [ ] User can edit and save markdown artifacts.
- [ ] User can rotate current handoff and current decisions.
- [ ] User can refresh repository state without restarting.
- [ ] User can determine readiness from dashboard and workspace.
