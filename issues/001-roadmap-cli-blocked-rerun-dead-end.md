# Roadmap CLI blocked rerun dead end

## Severity

Medium

## Finding

Blocked states tell the user to resolve the blocker and rerun the CLI, but startup planning treats blocked and failed states as report-only. A rerun does not perform preflight, does not inspect whether the blocker was resolved, and does not resume workflow execution.

Affected code:

- `src/CommandCenter.Roadmap.CLI/RoadmapStartupPlanner.cs`
- `src/CommandCenter.Roadmap.CLI/RoadmapWorkflowStateClassifier.cs`
- `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs`

The tests explicitly assert this behavior: existing `EvidenceBlocked`, terminal pause states, and `Failed` skip preflight and perform zero runtime calls.

## Impact

The persisted instructions are operationally misleading. Users can repair the artifact named in the blocker, rerun the CLI, and still remain stuck because the state machine only reports the blocked state.

This creates a recovery dead end for:

- Artifact promotion blockers.
- Split epic blockers.
- Malformed execution disposition.
- Generic transition failures.
- Preflight blockers in fresh repositories.
- Invalid completion certification.

Recovery then requires undocumented manual edits to `.agents/state.md` or deleting state, both of which can lose provenance or bypass safety checks.

## Proposal

Add an explicit unblock/resume workflow instead of relying on ambiguous reruns.

The robust design is:

- Extend CLI arguments with a command shape such as:
  - `roadmap <repo> run`
  - `roadmap <repo> status`
  - `roadmap <repo> unblock`
- Keep plain `run` report-only for unresolved blocked states.
- Make `unblock` perform deterministic checks based on `TransitionIntent`:
  - Verify referenced evidence/artifacts still exist.
  - Re-run relevant validators.
  - Confirm project context preflight.
  - Reclassify to the safest resumable state.
- Record an `UnblockReviewed` journal event with checked artifacts and hashes.
- Update blocker text to say either "run `unblock` after repair" or list the exact state/artifact mutation required.

For the current single-command interface, a smaller but still robust alternative is to let `RunAsync` detect a blocked state with `TransitionIntent` and call a dedicated `TryUnblockAsync` planner before returning report-only.

## Acceptance Criteria

- A repaired blocked promotion can resume without manual state edits.
- A repaired malformed execution disposition can resume or re-enter execution safely.
- A preflight-blocked fresh repository resumes after project context is added.
- Rerun/status text clearly distinguishes unresolved blocked states from recoverable repaired states.
- Tests assert that blocker evidence and transition hashes are recorded during unblock.
