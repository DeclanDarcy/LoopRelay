# Milestone 4: Execution Workspace

## Tracking

- [x] Milestone complete
- [x] Workstream 4.1: Execution Layout
- [x] Workstream 4.2: Full Execution Stream
- [x] Workstream 4.3: Session Panel
- [x] Workstream 4.4: Context Diagnostics
- [x] Workstream 4.5: Execution Diagnostics
- [x] Workstream 4.6: Execution Cross-Links
- [x] Certification complete

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

- [x] Timestamp.
- [x] Event type.
- [x] Provider.
- [x] Status.
- [x] Session id.
- [x] Message.

Rules:

- [x] Use the same execution event hook as Workspace.
- [x] Do not create client replay, client event persistence, or a second polling model.

## Workstream 4.3: Session Panel

Display:

- [x] Provider name.
- [x] Session id.
- [x] Provider process id.
- [x] Provider executable path.
- [x] Started at.
- [x] Completed at.
- [x] Duration.
- [x] Current session state.
- [x] Repository execution state.
- [x] Handoff path.
- [x] Failure reason.

Abort behavior:

- [x] Keep abort hidden or disabled until backend-owned abort exists.
- [x] If abort is later implemented, add backend service, endpoint, Tauri command, tests, projection updates, and UI action together.

## Workstream 4.4: Context Diagnostics

Display from `ExecutionContextDiagnostics`:

- [x] Artifact count.
- [x] Aggregate size.
- [x] Warning threshold.
- [x] Hard limit.
- [x] Validation errors.
- [x] Missing optional artifacts.
- [x] Launch blocked.
- [x] Per-artifact diagnostics.

## Workstream 4.5: Execution Diagnostics

Display from execution projections/events:

- [x] Current state.
- [x] Last activity.
- [x] Recent failures.
- [x] Monitoring warnings where projected.

Do not infer failures from event text.

## Workstream 4.6: Execution Cross-Links

Add links introduced by the Execution workspace:

- [x] Session milestone navigates to the Workspace milestone/context area.
- [x] Context diagnostics navigates to the Workspace execution context panel when users need the broader package view.
- [x] Handoff path navigates to the artifact workspace or handoff review surface.
- [x] Commit and push references navigate to the Workspace inspector commit/push section.
- [x] Failure or warning references navigate only to visible projected diagnostics; do not infer targets from raw event text.

Rules:

- [x] Links navigate only.
- [x] Execution events remain immutable observations.

### Certification

- [x] Workspace execution summary and Execution tab agree for the same session.
- [x] Abort affordance accurately reflects real capability.
- [x] Execution cross-links do not create a second execution model or mutate workflow state.
