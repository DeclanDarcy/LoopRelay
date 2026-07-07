# Main CLI Completion Transition Plan

## Goal

When `LoopRelay.Cli` reaches plan completion, it should not stop at `LoopOutcome.EpicCompleted` immediately. It should transition into the existing roadmap completion-certification flow:

1. Evaluate whether the completed implementation actually satisfies the active epic and milestone intent.
2. Route the certification decision through the completion policy.
3. Archive the completed execution workspace and synthesize a compact completed-epic record.
4. Update `.agents/core/roadmap-completion-context.md` for close-worthy outcomes using that synthesis.
5. Persist completion evidence before returning success to the operator.

The completed milestone checkbox gate remains the trigger, but it becomes a claim that requires certification rather than the final roadmap-domain meaning.

## Current Behavior

Main CLI completion is detected in `src/LoopRelay.Cli/LoopRunner.cs`.

At the top of each loop iteration:

- `MilestoneGate.IsEpicCompleteAsync()` reads `.agents/milestones/m*.md`.
- Completion means at least one strict checkbox exists and every checkbox is checked.
- If complete, `LoopRunner` clears the persisted decision-session resume state and returns `LoopOutcome.EpicCompleted`.
- `Program.cs` prints `Epic completed. Press any key to exit.` and exits `0`.

Roadmap completion logic already exists, but it is owned by `LoopRelay.Roadmap.Cli` internals:

- `EvaluateEpicCompletionAndDrift`
- `CompletionEvaluationParser`
- `CompletionCertificationPolicy`
- `CompletionCertificationRouter`
- `UpdateRoadmapCompletionContext`
- transition journaling, decision ledger writes, and evidence persistence

Roadmap CLI can reach that logic from a persisted `EpicCompletionDetected` state or unblock repair path, but normal Roadmap CLI planning now stops at `MilestoneSpecsReady`. Main CLI does not call this completion path.

## Design

Extract the reusable completion-certification behavior out of `LoopRelay.Roadmap.Cli` into a shared implementation that both Roadmap CLI and Main CLI can call.

Recommended shape:

- Add a library such as `LoopRelay.Completion`.
- Reference only shared dependencies: `LoopRelay.Core`, `LoopRelay.Agents`, `LoopRelay.Infrastructure`, `LoopRelay.Orchestration.Primitives`, and `LoopRelay.Projections`.
- Do not make `LoopRelay.Cli` reference the `LoopRelay.Roadmap.Cli` executable project.
- Move or adapt completion-specific types from Roadmap CLI into the shared library:
  - completion evaluation parser and DTOs
  - completion certification vocabulary, policy, and router
  - completion evidence writer
  - completed-epic archive and synthesis service
  - completion prompt context builder
  - completion prompt runner
  - completion artifact path constants that are not CLI-specific

Roadmap CLI should then delegate its existing private completion path to the shared service. Main CLI should call the same service when the milestone gate reports completion.

Also update the prompt catalog/contract for `SynthesizeCompletedEpic.prompt`:

- register the runtime prompt in the shared completion prompt runner or the existing prompt catalog used by the completion service
- pass the archive index as the prompt's `label` input
- make the prompt text consistently refer to `.agents/archive/epics/{label}/` as the input directory and `.agents/archive/epics/{label}.md` as the output file

## Shared Service Contract

Introduce a service shaped roughly like:

```csharp
public interface ICompletionCertificationService
{
    Task<CompletionCertificationResult> CertifyPlanCompletionAsync(
        CompletionCertificationRequest request,
        CancellationToken cancellationToken);
}
```

The request should include:

- repository identity
- active epic path, normally `.agents/epic.md`
- roadmap completion context path, `.agents/core/roadmap-completion-context.md`
- execution plan path, `.agents/plan.md`
- optional details path, `.agents/details.md`
- executed milestone directory, `.agents/milestones`
- completion trigger metadata, for example `MainCliMilestoneGate`
- completed epic archive root, `.agents/archive/epics`

The result should include:

- certification decision
- route intent
- evidence paths written
- completed epic archive index
- completed epic synthesis path
- whether roadmap completion context changed
- whether Main CLI should return success, blocked, failed, or continue

## Artifact Contract

Main CLI plan completion is based on `.agents/milestones/m*.md`, so the shared completion service must treat those files as the executed milestone evidence and intent source for Main CLI completion.

Do not require Main CLI completion to depend on Roadmap-only `.agents/specs/*.md` provenance. Those files may exist from Roadmap planning, but Plan CLI owns `.agents/plan.md`, `.agents/details.md`, `.agents/operational_context.md`, and `.agents/milestones/m*.md` for execution.

The completion context sent to `EvaluateEpicCompletionAndDrift` should include:

- projection content from `ProjectionForEvaluateEpicCompletionAndDrift`
- `.agents/epic.md` when present
- `.agents/plan.md`
- `.agents/details.md` when present
- every `.agents/milestones/m*.md`
- latest handoff and historical handoff references when useful
- a generated execution evidence artifact explaining that Main CLI reached the milestone-complete gate
- read-only repository inspection instructions

The update context sent to `UpdateRoadmapCompletionContext` should include:

- projection content from `ProjectionForUpdateRoadmapCompletionContext`
- current `.agents/core/roadmap-completion-context.md`
- completed epic synthesis read from `.agents/archive/epics/{INDEX}.md`
- latest completion evaluation evidence
- read-only repository inspection instructions

Before `UpdateRoadmapCompletionContext` runs, the completion service must archive and synthesize the completed epic workspace:

1. Count existing directories directly under `.agents/archive/epics/`.
2. Compute `INDEX = directory_count + 1`.
3. Create `.agents/archive/epics/{INDEX}/`.
4. Move these execution artifacts into the new directory:
   - `.agents/decisions/`
   - `.agents/deltas/`
   - `.agents/handoffs/`
   - `.agents/milestones/`
   - `.agents/details.md`
   - `.agents/operational_context.md`
   - `.agents/plan.md`
5. Run `SynthesizeCompletedEpic.prompt` with `label = INDEX`.
6. Verify that `.agents/archive/epics/{INDEX}.md` was written.
7. Read `.agents/archive/epics/{INDEX}.md`.
8. Pass that synthesis as the `completedEpic` secondary input to `UpdateRoadmapCompletionContext.prompt`.

The move and synthesis step is part of the close-worthy completion route, not a separate post-success cleanup. If it fails, Main CLI must not report `EpicCompleted`.

## Main CLI Flow

Change the top-of-loop completion branch in `LoopRunner.RunAsync`:

1. `MilestoneGate.IsEpicCompleteAsync()` returns true.
2. Main CLI enters a new phase: `Completion Certification`.
3. `LoopRunner` calls the shared completion service.
4. The service runs `EvaluateEpicCompletionAndDrift`.
5. The service parses and validates the completion decision.
6. If the route is `Close Epic` or `Close With Follow-Up`, the service archives the completed execution workspace under `.agents/archive/epics/{INDEX}/`.
7. The service runs `SynthesizeCompletedEpic` with `label = INDEX`.
8. The service reads `.agents/archive/epics/{INDEX}.md` and passes it as `completedEpic` to `UpdateRoadmapCompletionContext`.
9. Main CLI publishes the `.agents` submodule with a new message such as `Orchestration loop: completion certification`.
10. Main CLI clears decision-session resume state only after archive, synthesis, and completion-context update all succeed.
11. Main CLI returns `LoopOutcome.EpicCompleted`.

If certification does not close the epic:

- `Continue Epic`, `Reopen Epic`, and `Gather More Evidence` should not return `EpicCompleted`.
- Main CLI cannot simply continue the loop because the checkbox gate will still report complete.
- Persist a clear evidence/blocker artifact and return a new outcome such as `LoopOutcome.CompletionBlocked`.
- `Program.cs` should map that to a nonzero exit code and tell the operator where the certification evidence is.

## Roadmap CLI Flow

Keep Roadmap CLI behavior equivalent, but replace duplicated private logic with the shared service:

- `RunCompletionCertificationAsync` becomes a thin adapter around the shared service.
- `RecoverCompletionCertificationAsync` keeps using the shared parser, policy, router, and update flow.
- `UpdateRoadmapCompletionContextAsync` is not called directly for close-worthy routes until completed-epic archive and synthesis have succeeded.
- Existing Roadmap state/journal semantics remain unchanged.

This avoids two divergent implementations of completion certification.

## Persistence

Completion certification should write durable artifacts before Main CLI returns:

- `.agents/evidence/execution/*` for the Main CLI completion claim
- `.agents/evidence/evaluations/*` for `EvaluateEpicCompletionAndDrift`
- `.agents/archive/epics/{INDEX}/` containing the completed execution workspace
- `.agents/archive/epics/{INDEX}.md` containing the synthesized completed epic
- `.agents/evidence/evaluations/*` for the roadmap completion update output
- `.agents/core/roadmap-completion-context.md` when updated
- projection manifest entries for evaluation and update projections

If shared state/journal files remain Roadmap-owned, Main CLI should not fake Roadmap state transitions. Prefer one of these:

1. Move transition journal and decision ledger writing into shared completion services with neutral event names.
2. Keep Main CLI completion evidence file based and let Roadmap CLI state remain a planning workflow state only.

The first option gives better audit consistency; the second option is smaller and avoids overloading Roadmap state with Main CLI execution ownership.

## Edge Cases

- Missing `.agents/core/roadmap-completion-context.md`: run `CreateRoadmapCompletionContext` first, then evaluate completion.
- Missing `.agents/epic.md`: block completion certification with clear evidence; Main CLI cannot certify roadmap completion without an active epic.
- No milestone files: this should already be impossible when `MilestoneGate` returns true, but treat it as a hard certification failure.
- Archive index collision after counting directories: fail before moving files; never merge into an existing archive directory.
- Failure while moving completion artifacts: preserve a blocker/evidence record and do not run synthesis or update roadmap completion context.
- `SynthesizeCompletedEpic` fails or does not write `.agents/archive/epics/{INDEX}.md`: preserve the archive directory and return blocked/failed, not `EpicCompleted`.
- Certification says `Continue Epic`: persist a blocker because all milestone boxes are checked and the loop has no remaining executable milestone signal.
- Certification prompt fails or returns malformed markdown: persist failed evaluation evidence and return `CompletionBlocked` or `Failed`, not `EpicCompleted`.
- Completion context update fails after successful evaluation: preserve evaluation evidence and return blocked; do not report success until context update is written.

## Tests

Main CLI tests:

- completed milestone gate calls completion certification before returning `EpicCompleted`
- successful `Close Epic` runs evaluation, updates roadmap completion context, publishes `.agents`, clears decision resume state, and exits success
- successful `Close With Follow-Up` follows the same close path
- close-worthy certification creates `.agents/archive/epics/{INDEX}/`, moves the completed execution artifacts into it, runs synthesis, and uses `.agents/archive/epics/{INDEX}.md` as `completedEpic`
- `Continue Epic` returns `CompletionBlocked` and does not clear decision resume state
- malformed completion evaluation returns blocked/failed and preserves evidence
- archive move or synthesis failure does not call `UpdateRoadmapCompletionContext` and does not return `EpicCompleted`
- completion context update failure does not return `EpicCompleted`
- completion certification publish happens after completion artifacts are written

Shared completion tests:

- builds evaluation context from `.agents/epic.md`, `.agents/plan.md`, `.agents/details.md`, and `.agents/milestones/m*.md`
- parses and routes all allowed completion recommendations
- computes archive index from existing directories under `.agents/archive/epics/`
- moves `.agents/decisions/`, `.agents/deltas/`, `.agents/handoffs/`, `.agents/milestones/`, `.agents/details.md`, `.agents/operational_context.md`, and `.agents/plan.md` into `.agents/archive/epics/{INDEX}/`
- renders `SynthesizeCompletedEpic` with `label = INDEX`
- reads `.agents/archive/epics/{INDEX}.md` and passes it as the `completedEpic` secondary input to `UpdateRoadmapCompletionContext`
- updates roadmap completion context only for `Close Epic` and `Close With Follow-Up`
- creates initial roadmap completion context when missing
- preserves projection provenance and freshness behavior

Roadmap CLI regression tests:

- existing completion certification tests still pass through the shared service
- invalid completion certification unblock still routes through the same policy
- Roadmap CLI still stops at `MilestoneSpecsReady` during planning and does not resume execution preparation

## Acceptance Criteria

- Main CLI no longer treats checked milestone boxes as final success by themselves.
- A completed plan triggers completion certification before process success.
- Close-worthy certification archives and synthesizes the completed epic before roadmap completion context is updated.
- Close-worthy certification updates `.agents/core/roadmap-completion-context.md`.
- `UpdateRoadmapCompletionContext.prompt` receives the synthesized `.agents/archive/epics/{INDEX}.md` content as `completedEpic`.
- Non-close certification exits with explicit evidence instead of looping indefinitely or falsely reporting success.
- Main CLI and Roadmap CLI use one shared completion policy, parser, and router.
- No dependency is introduced from Main CLI to the Roadmap CLI executable.
