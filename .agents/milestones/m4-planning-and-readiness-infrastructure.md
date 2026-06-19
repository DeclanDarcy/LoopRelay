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

- [ ] Implement `Milestone`.
- [ ] Implement `ExecutionReadiness`.
- [ ] Implement `IPlanningService`.
- [ ] Detect plan presence.
- [ ] Enumerate milestone markdown files.
- [ ] Determine readiness from plan and milestone existence.
- [ ] Add planning projection endpoint.
- [ ] Include readiness in repository dashboard projection.
- [ ] Include plan status, milestone count, and readiness in `RepositoryWorkspaceProjection`.

## UI Tasks

- [ ] Show readiness on repository dashboard.
- [ ] Show plan status in repository workspace.
- [ ] Show milestone count in repository workspace.
- [ ] Show readiness in repository workspace.
- [ ] Add milestone inventory view.
- [ ] Update refresh to recompute artifacts, plan status, milestones, and readiness.

## Tests

- [ ] Plan present returns plan present.
- [ ] Missing plan returns missing plan.
- [ ] Milestone files are discovered.
- [ ] Missing milestone directory returns zero milestones.
- [ ] Empty milestone directory returns zero milestones.
- [ ] Corrupt or arbitrary markdown content still counts as a milestone.
- [ ] Missing plan returns `MissingPlan`.
- [ ] Plan without milestones returns `MissingMilestones`.
- [ ] Plan with at least one milestone returns `Ready`.
- [ ] Refresh after adding a milestone updates readiness.

## Acceptance Criteria

- [ ] Plan detection works.
- [ ] Milestone detection works.
- [ ] All readiness states are derived correctly.
- [ ] Dashboard shows readiness.
- [ ] Workspace shows plan status, milestone count, and readiness.
- [ ] No execution behavior is introduced.
