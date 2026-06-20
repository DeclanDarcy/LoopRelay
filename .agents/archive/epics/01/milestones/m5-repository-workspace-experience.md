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

- [x] Build left repository navigation.
- [x] Build repository header.
- [x] Build repository summary.
- [x] Build categorized artifact explorer.
- [x] Build markdown viewer/editor.
- [x] Wire artifact selection to content loading.
- [x] Wire save action to backend.
- [x] Wire refresh action to backend.
- [x] Wire rotation actions to backend.
- [x] Preserve selected artifact per repository where practical.
- [x] Represent missing and empty states explicitly.

## Backend Tasks

- [x] Implement `ArtifactInventory`.
- [x] Implement `RepositoryDashboardProjection`.
- [x] Implement `RepositoryWorkspaceProjection`.
- [x] Implement `IRepositoryProjectionService`.
- [x] Ensure dashboard endpoint returns backend-composed dashboard projections.
- [x] Ensure workspace endpoint returns all summary data in one call.
- [x] Ensure refresh endpoint rebuilds repository state from disk through `IRepositoryProjectionService`.
- [x] Ensure repository dashboard endpoint includes status projections efficiently.

## Tests

- [x] Workspace projection includes repository metadata.
- [x] Workspace projection includes artifact inventory.
- [x] Workspace projection includes current handoff and current decisions status.
- [x] Workspace projection includes plan status, milestone count, and readiness.
- [x] Dashboard projection includes current handoff and current decisions status.
- [x] Missing artifacts are represented.
- [x] Artifact navigation loads correct content.
- [x] Refresh detects external filesystem changes.
- [x] Switching repositories loads the correct workspace.

## Acceptance Criteria

- [x] User can manage repositories from a dashboard.
- [x] User can open a repository workspace.
- [x] User can browse plans, milestones, operational context, handoffs, and decisions.
- [x] User can view current and historical handoffs.
- [x] User can view current and historical decisions.
- [x] User can edit and save markdown artifacts.
- [x] User can rotate current handoff and current decisions.
- [x] User can refresh repository state without restarting.
- [x] User can determine readiness from dashboard and workspace.

## Certification Status

- [x] Native Tauri desktop pass completed with the real shell, native folder picker, backend sidecar, and rendered React workspace.
- [x] Registered a disposable Git repository through the native picker.
- [x] Verified dashboard readiness, workspace summary, artifact explorer, artifact edit/save, manual refresh, handoff rotation twice, decisions rotation twice, restart recovery, and repository removal.
- [x] Restored the pre-existing Command Center configuration after certification.
