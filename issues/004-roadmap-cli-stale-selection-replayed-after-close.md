# Roadmap CLI stale selection replayed after close

## Finding

When a completion route closes an epic, the roadmap state machine transitions back to `SelectNextStrategicInitiative`, but it leaves `.agents/selection.md` in place and marked usable. On the next run, `RoadmapResumePlanner` treats the existing selection artifact as a completed `SelectNextEpic` output and resumes with `ContinueSelectionDecision` instead of asking the planner to choose the next strategic initiative.

## Impact

A completed epic can be selected and processed again. Depending on the stale selection contents, the workflow may recreate or re-audit the just-completed epic, loop over obsolete work, or make decisions from a roadmap context that was already superseded by completion certification.

## Evidence

- `SelectNextInitiativeAsync` writes `.agents/selection.md` and marks it ready.
- `CompletionCertificationRouter` sends `Close Epic` and `Close With Follow-Up` to `SelectNextStrategicInitiative`.
- `RoadmapResumePlanner` resumes `SelectNextStrategicInitiative` with `ContinueSelectionDecision` whenever `.agents/selection.md` is usable.
- The completion tests assert the post-close state, but do not rerun from that state to verify a fresh selection is requested.

## Proposal

Introduce explicit selection artifact freshness instead of using file presence as the resume signal.

A robust approach:

1. Add a selection generation identity to the state document or lifecycle notes. At minimum, record the transition correlation ID or input snapshot hash that produced `.agents/selection.md`.
2. When completion routing returns to `SelectNextStrategicInitiative`, invalidate the old selection by either deleting `.agents/selection.md` or marking it `Completed`/`Superseded` with a lifecycle state that `HasUsableFile` does not accept for selection resume.
3. In `RoadmapResumePlanner`, allow `ContinueSelectionDecision` only when the persisted last transition is actually `SelectNextEpic`, its output is `.agents/selection.md`, and the selection artifact identity matches the persisted transition identity.
4. Add a regression test that runs a successful close route, creates a second state machine from the same repo, and verifies the next runtime call is `SelectNextEpic` rather than a reused `CreateNewEpic`/audit/split path.

This keeps resume behavior deterministic while preserving crash recovery for a genuinely interrupted selection transition.

## Acceptance criteria

- A post-close rerun always performs a fresh `SelectNextEpic`.
- An interrupted `SelectNextEpic` with a durable current selection can still resume without rerunning the prompt.
- Stale selection artifacts remain available as evidence, but are not considered active workflow input.
