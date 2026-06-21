# Milestone 4: Execution Workspace

## Tracking

- [ ] Milestone complete
- [ ] Workstream 4.1: Execution Layout
- [ ] Workstream 4.2: Full Execution Stream
- [ ] Workstream 4.3: Session Panel
- [ ] Workstream 4.4: Context Diagnostics
- [ ] Workstream 4.5: Execution Diagnostics
- [ ] Workstream 4.6: Execution Cross-Links
- [ ] Certification complete

Goal: create a dedicated execution inspection workspace.

## Workstream 4.1: Execution Layout

Implement `features/execution/ExecutionTab.tsx`.

Target structure:

```text
Main stream panel
Right rail
  Session panel
  Context diagnostics panel
  Execution diagnostics panel
  Launch readiness panel
```

The execution stream gets primary visual weight.

## Workstream 4.2: Full Execution Stream

Display:

- [ ] Timestamp.
- [ ] Event type.
- [ ] Provider.
- [ ] Status.
- [ ] Session id.
- [ ] Message.

Rules:

- [ ] Use the same execution event hook as Workspace.
- [ ] Do not create client replay, client event persistence, or a second polling model.

## Workstream 4.3: Session Panel

Display:

- [ ] Provider name.
- [ ] Session id.
- [ ] Provider process id.
- [ ] Provider executable path.
- [ ] Started at.
- [ ] Completed at.
- [ ] Duration.
- [ ] Current session state.
- [ ] Repository execution state.
- [ ] Handoff path.
- [ ] Failure reason.

Abort behavior:

- [ ] Keep abort hidden or disabled until backend-owned abort exists.
- [ ] If abort is later implemented, add backend service, endpoint, Tauri command, tests, projection updates, and UI action together.

## Workstream 4.4: Context Diagnostics

Display from `ExecutionContextDiagnostics`:

- [ ] Artifact count.
- [ ] Aggregate size.
- [ ] Warning threshold.
- [ ] Hard limit.
- [ ] Validation errors.
- [ ] Missing optional artifacts.
- [ ] Launch blocked.
- [ ] Per-artifact diagnostics.

## Workstream 4.5: Execution Diagnostics

Display from execution projections/events:

- [ ] Current state.
- [ ] Last activity.
- [ ] Recent failures.
- [ ] Monitoring warnings where projected.

Do not infer failures from event text.

## Workstream 4.6: Execution Cross-Links

Add links introduced by the Execution workspace:

- [ ] Session milestone navigates to the Workspace milestone/context area.
- [ ] Context diagnostics navigates to the Workspace execution context panel when users need the broader package view.
- [ ] Handoff path navigates to the artifact workspace or handoff review surface.
- [ ] Commit and push references navigate to the Workspace inspector commit/push section.
- [ ] Failure or warning references navigate only to visible projected diagnostics; do not infer targets from raw event text.

Rules:

- [ ] Links navigate only.
- [ ] Execution events remain immutable observations.

### Certification

- [ ] Workspace execution summary and Execution tab agree for the same session.
- [ ] Abort affordance accurately reflects real capability.
- [ ] Execution cross-links do not create a second execution model or mutate workflow state.
