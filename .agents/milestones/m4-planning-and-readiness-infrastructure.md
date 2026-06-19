# M4: Planning and Readiness Infrastructure

## Goal

Add plan detection, milestone inventory, and execution readiness projection without launching execution.

## Planning Authority

Plan authority:

```text
.agents/plan.md
```

Milestone authority:

```text
.agents/milestones/*.md
```

Milestone contents are not interpreted during Epic 1.

## Backend Tasks

- [x] Implement `Milestone`.
- [x] Implement `ExecutionReadiness`.
- [x] Implement `IPlanningService`.
- [x] Detect plan presence.
- [x] Enumerate milestone markdown files.
- [x] Determine readiness from plan and milestone existence.
- [x] Add planning projection endpoint.
- [x] Include readiness in repository dashboard projection.
- [x] Include plan status, milestone count, and readiness in `RepositoryWorkspaceProjection`.

## UI Tasks

- [x] Show readiness on repository dashboard.
- [x] Show plan status in repository workspace.
- [x] Show milestone count in repository workspace.
- [x] Show readiness in repository workspace.
- [x] Add milestone inventory view.
- [x] Update refresh to recompute artifacts, plan status, milestones, and readiness.

## Tests

- [x] Plan present returns plan present.
- [x] Missing plan returns missing plan.
- [x] Milestone files are discovered.
- [x] Missing milestone directory returns zero milestones.
- [x] Empty milestone directory returns zero milestones.
- [x] Corrupt or arbitrary markdown content still counts as a milestone.
- [x] Missing plan returns `MissingPlan`.
- [x] Plan without milestones returns `MissingMilestones`.
- [x] Plan with at least one milestone returns `Ready`.
- [x] Refresh after adding a milestone updates readiness.

## Acceptance Criteria

- [x] Plan detection works.
- [x] Milestone detection works.
- [x] All readiness states are derived correctly.
- [x] Dashboard shows readiness.
- [x] Workspace shows plan status, milestone count, and readiness.
- [x] No execution behavior is introduced.
