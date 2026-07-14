# Completed main CLI rerun no longer short-circuits

## Severity

High

## Finding

Successful completion certification removes the live milestone files that `LoopRunner` uses to detect an already completed epic.

Affected code:

- `src/LoopRelay.Cli/LoopRunner.cs`
- `src/LoopRelay.Cli/MilestoneGate.cs`
- `src/LoopRelay.Cli/ExecutionStep.cs`
- `src/LoopRelay.Completion/CompletedEpicArchiveService.cs`

`LoopRunner` checks `MilestoneGate.IsEpicCompleteAsync()` at the top of each loop and only enters completion certification when the live `.agents/milestones/m*.md` files contain at least one checkbox and all checkboxes are checked.

After the new close path succeeds, `CompletedEpicArchiveService` moves `.agents/milestones`, `.agents/plan.md`, and related execution files into `.agents/archive/epics/{index}/`. A later invocation against the same repository no longer satisfies the milestone gate and can fall through to normal execution with a missing or null plan.

This contradicts the nearby `LoopRunner` comment that clearing resume state is idempotent on every rerun against a completed epic.

## Impact

An operator rerunning the main CLI after a completed close can accidentally start execution against an archived, completed epic rather than receiving an immediate completed result. That can generate noisy handoffs, reopen work that should be closed, or fail in confusing ways because the plan and milestones were intentionally moved.

## Proposal

Add an explicit completed-state gate that survives archive cleanup.

The robust shape is one of:

- Leave enough live completion marker data for `MilestoneGate` to remain true after archive.
- Write a durable completion marker and check it before ordinary execution.
- Teach `LoopRunner` to recognize the active epic lifecycle or latest completed archive synthesis before opening an execution session.
- Add a regression test that runs `LoopRunner` twice after a successful certification/archive.

## Acceptance Criteria

- A second main CLI invocation after successful certification returns completed or a clear already-closed outcome without opening execution.
- The completion check does not depend solely on live milestone files that the archive step removes.
- Tests cover rerun behavior after archive success.
