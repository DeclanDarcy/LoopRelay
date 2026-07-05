# Roadmap CLI stale selection replayed after close

## Verification status

Confirmed by code audit. The current resume planner can replay `.agents/selection.md` after a completion route has already returned the workflow to `SelectNextStrategicInitiative`.

Targeted tests run:

```text
dotnet test tests\CommandCenter.Roadmap.CLI.Tests\CommandCenter.Roadmap.CLI.Tests.csproj --filter "FullyQualifiedName~RoadmapStateMachineCompletionTests|FullyQualifiedName~RoadmapResumePlannerTests"
```

Result: passed, 24 tests. These tests cover the current completion routes and current resume behavior, but they do not cover a second run after a close route.

## Finding

When a completion certification route closes an epic, the roadmap state machine transitions to `SelectNextStrategicInitiative`, but the previously selected `.agents/selection.md` remains present and lifecycle-usable. On the next CLI run, `RoadmapResumePlanner` treats that file as a completed `SelectNextEpic` output and returns `ContinueSelectionDecision`, so the state machine parses and acts on the old selection instead of asking the planner to choose the next initiative from the updated roadmap completion context.

This is a causal freshness bug: the selection file was produced before completion certification and before the roadmap completion context was updated, but resume planning treats file presence as sufficient proof that the selection belongs to the current selection cycle.

## Impact

A completed epic can be selected and processed again. Depending on stale selection contents, the workflow may:

- recreate or re-audit the just-completed epic,
- run `CreateNewEpic`, `SplitEpic`, or `EpicPreparationAudit` from a selection generated against superseded completion context,
- bypass a fresh `SelectNextEpic` decision after `Close Epic` or `Close With Follow-Up`,
- enter loops where old workflow inputs continue to drive new roadmap cycles.

The bug is especially risky because `.agents/selection.md` is an input to multiple downstream prompt contracts (`EpicPreparationAudit`, `CreateNewEpic`, and `SplitEpic`), so a stale selection can branch into several workflow paths.

## Verified Mechanics

- `SelectNextInitiativeAsync` writes `.agents/selection.md`, writes a numbered selection evidence copy, and marks the active selection artifact `Ready` in lifecycle state. See `src\CommandCenter.Roadmap.CLI\RoadmapStateMachine.cs:268` and `src\CommandCenter.Roadmap.CLI\RoadmapStateMachine.cs:287`.
- `CompletionCertificationRouter` routes both `Close Epic` and `Close With Follow-Up` to `RoadmapState.SelectNextStrategicInitiative` with `NextTransitions: ["SelectNextEpic"]`. See `src\CommandCenter.Roadmap.CLI\CompletionCertificationRouter.cs:8` and `src\CommandCenter.Roadmap.CLI\CompletionCertificationRouter.cs:17`.
- The completion path updates the active epic lifecycle state, then persists the completion route, but it does not delete or supersede `.agents/selection.md`. See `src\CommandCenter.Roadmap.CLI\RoadmapStateMachine.cs:802`, `src\CommandCenter.Roadmap.CLI\RoadmapStateMachine.cs:810`, and `src\CommandCenter.Roadmap.CLI\RoadmapStateMachine.cs:1203`.
- `RoadmapResumePlanner` resumes `SelectNextStrategicInitiative` with `ContinueSelectionDecision` whenever `snapshot.HasUsableFile(RoadmapArtifactPaths.Selection)` is true. See `src\CommandCenter.Roadmap.CLI\RoadmapResumePlanner.cs:105`.
- `HasUsableFile` accepts present files with lifecycle states `Ready`, `Executing`, or `Completed`; missing lifecycle metadata also allows use. See `src\CommandCenter.Roadmap.CLI\RoadmapResumePlanner.cs:642` and `src\CommandCenter.Roadmap.CLI\RoadmapResumePlanner.cs:647`.
- The prompt contract model correctly says `SelectNextEpic` produces `.agents/selection.md`, and downstream selection consumers require it. The missing piece is active-generation ownership, not contract declaration. See `src\CommandCenter.Roadmap.CLI\PromptContractRegistry.cs:13`.
- Existing tests assert the post-close state and current resume behavior independently, but no test reruns the state machine after a close route. See `tests\CommandCenter.Roadmap.CLI.Tests\RoadmapStateMachineCompletionTests.cs:60` and `tests\CommandCenter.Roadmap.CLI.Tests\RoadmapResumePlannerTests.cs:44`.

## Minimal Reproduction Path

1. Start from a repo with roadmap source and roadmap completion context.
2. Run the roadmap CLI through a selection that creates or selects an epic.
3. Let execution complete and make `EvaluateEpicCompletionAndDrift` return `Closure Recommendation = Close Epic` or `Close With Follow-Up`.
4. Observe final persisted state:
   - current state is `SelectNextStrategicInitiative`,
   - last transition is `CompletionCertificationRouting`,
   - next transition is `SelectNextEpic`,
   - `.agents/selection.md` still exists and is lifecycle-usable.
5. Run the CLI again.
6. Expected: run `SelectNextEpic` against the updated completion context.
7. Actual: resume planner returns `ContinueSelectionDecision` and the state machine reuses the old `.agents/selection.md`.

## Solution Options

### Option A: Supersede selection on close route

When `CompletionCertificationRouter` returns a route whose target is `SelectNextStrategicInitiative`, upsert `.agents/selection.md` to `ArtifactLifecycleState.Superseded` before persisting the route. Leave the file and numbered evidence copy in place.

Pros:

- Smallest behavior-preserving change.
- Keeps stale selection visible for audit/evidence.
- Uses existing lifecycle states; `HasUsableFile` already rejects `Superseded`.
- Fits the current model where `.agents/selection.md` is the active selection pointer and numbered evidence preserves history.

Cons:

- Still relies on every state-cycle boundary remembering to supersede active inputs.
- Does not prove freshness for other stale active artifacts.
- Needs tests to make sure genuine interrupted `SelectNextEpic` resumes still work.

Implementation shape:

1. Add a helper such as `SupersedeSelectionAsync(reason)` in `RoadmapStateMachine`.
2. Call it for close routes before `PersistCompletionRouteAsync`.
3. Include the evaluation evidence path or route decision in lifecycle notes.
4. Add a resume planner test where state is `SelectNextStrategicInitiative`, selection exists, lifecycle is `Superseded`, and action is `SelectNextStrategicInitiative`.
5. Add an end-to-end completion rerun test that verifies the next runtime prompt is `SelectNextEpic`.

### Option B: Delete active selection on close route

When a close route returns to `SelectNextStrategicInitiative`, delete `.agents/selection.md`. The numbered evidence copy under `.agents/evidence/selection` remains the durable history.

Pros:

- Very direct: `HasUsableFile` becomes false.
- Avoids lifecycle metadata being out of sync with file presence.
- Easy to reason about in resume planning.

Cons:

- Loses the active pointer content unless callers know to inspect numbered evidence.
- If deletion fails or the file is externally recreated, there is still no provenance guard.
- Slightly less consistent with the existing artifact-lifecycle model.

Implementation shape:

1. Call `artifacts.DeleteAsync(RoadmapArtifactPaths.Selection)` on close routes.
2. Upsert lifecycle to `Superseded` or `Archived` as a tombstone, so the state document explains why active selection is absent.
3. Add the same resume and rerun regression tests as Option A.

### Option C: State-aware resume guard for `ContinueSelectionDecision`

Allow `ContinueSelectionDecision` only when the persisted last transition actually represents a completed or prompt-completed `SelectNextEpic` transition whose output includes `.agents/selection.md`. If the current state is `SelectNextStrategicInitiative` but the last transition is `CompletionCertificationRouting`, run a fresh selection even if `.agents/selection.md` is present.

Pros:

- Directly fixes the observed replay condition.
- Protects against stale files even if lifecycle cleanup is missed.
- Uses state data already persisted in `RoadmapStateDocument`.

Cons:

- Leaves stale active selection marked usable for downstream consumers if another code path reads it directly.
- Needs careful handling of interrupted selection: `Started` or `PromptCompleted` can have different safe-resume semantics than `Completed`.
- Does not solve broader artifact freshness problems by itself.

Implementation shape:

1. Add a predicate such as `CanResumeSelectionDecision(persistedState, snapshot)`.
2. Require:
   - `persistedState.LastTransition.Prompt == "SelectNextEpic"`,
   - `persistedState.LastTransition.Output` includes `.agents/selection.md`,
   - status is a resume-safe status,
   - `snapshot.HasRequiredOutput(.agents/selection.md)` is true.
3. Return `SelectNextStrategicInitiative` when the guard fails.
4. Test the close-route state where `LastTransition.Prompt == "CompletionCertificationRouting"` and selection exists.

### Option D: Selection provenance manifest

Record a selection generation identity and validate it before reuse. Candidate identities include the transition journal correlation ID, `TransitionInputSnapshot.SnapshotHash`, or a dedicated selection manifest with input hashes for completion context, roadmap source, projection, and retired-epic state.

Pros:

- Most robust against stale or externally modified selection artifacts.
- Generalizes to other derived artifacts and aligns with issue 003's provenance direction.
- Can distinguish a valid interrupted `SelectNextEpic` from a selection generated for an older completion context.

Cons:

- Highest implementation cost.
- Requires state/schema migration decisions.
- May duplicate parts of transition journal/projection provenance unless carefully integrated.

Implementation shape:

1. Persist selection provenance when `SelectNextEpic` produces `.agents/selection.md`.
2. Store either in lifecycle notes, a dedicated manifest, or a structured active-artifact provenance table.
3. On resume, require the selection provenance to match the current selection cycle or the last `SelectNextEpic` transition snapshot.
4. Treat mismatch as fresh-selection-required, not evidence-blocked, unless the active file is corrupt.
5. Add tests for context changes, close-route rerun, and interrupted selection resume.

## Recommended Approach

Use Option A plus Option C now, and leave Option D as a broader provenance hardening follow-up.

The combined fix is pragmatic:

- Option A makes the close-route boundary explicit by retiring the active selection artifact.
- Option C makes resume planning robust even if stale `.agents/selection.md` exists from an older run or from pre-fix repositories.
- Numbered selection evidence remains available, so no audit history is lost.

Avoid using `Completed` lifecycle for stale selection invalidation because `LifecycleAllowsUse` currently treats `Completed` as reusable. Use `Superseded` or `Archived` for stale active selection.

## Acceptance Criteria

- After `Close Epic`, a subsequent CLI run performs a fresh `SelectNextEpic`.
- After `Close With Follow-Up`, a subsequent CLI run performs a fresh `SelectNextEpic`.
- Existing numbered selection evidence remains available.
- `.agents/selection.md` is not considered active workflow input after a close route unless a fresh `SelectNextEpic` has produced it.
- A genuinely interrupted `SelectNextEpic` with durable output can still resume without rerunning the prompt.
- A `SelectNextStrategicInitiative` state whose last transition is `CompletionCertificationRouting` cannot return `ContinueSelectionDecision` solely because `.agents/selection.md` exists.

## Regression Tests

- `RoadmapResumePlannerTests`: selection exists, lifecycle is `Superseded`, state is `SelectNextStrategicInitiative`, expected action is `SelectNextStrategicInitiative`.
- `RoadmapResumePlannerTests`: selection exists, last transition is `CompletionCertificationRouting`, state is `SelectNextStrategicInitiative`, expected action is `SelectNextStrategicInitiative`.
- `RoadmapResumePlannerTests`: selection exists, last transition is `SelectNextEpic`, output is `.agents/selection.md`, expected action remains `ContinueSelectionDecision`.
- `RoadmapStateMachineCompletionTests`: run a close route, then construct a new state machine on the same repo and verify the first new runtime prompt is `SelectNextEpic`.
- Repeat the end-to-end rerun assertion for `Close With Follow-Up`.
