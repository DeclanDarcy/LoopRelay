# Roadmap Artifact Promotion Boundary

## Current failure points

The roadmap state machine currently treats a completed prompt turn as if the prompt output is already an authoritative artifact. This is incorrect for epic authoring prompts because `CreateNewEpic`, `RealignEpic`, and `ReimagineEpic` can all return either a promotable epic document or an intentional blocked document.

The incorrect coupling exists in `RoadmapStateMachine`:

- `RunPromptTransitionAsync` records the target state as completed when the model turn completes.
- `CreateNewEpicAsync` writes that raw output to `.agents/epic.md` and marks it `Ready`.
- `RewriteActiveEpicAsync` does the same for realign and reimagine output.
- `GenerateMilestonesAndExecutionContextAsync` then assumes `ActiveEpicReady` means `.agents/epic.md` is authoritative.

The authoritative writes are `.agents/epic.md`, `.agents/core/roadmap-completion-context.md`, generated milestone specs, `.agents/operational_context.md`, and `.agents/execution-prompt.md`. This correction only wires active epic promotion, but the boundary must be reusable for later promotion gates.

Lifecycle transitions currently happen too early for epic authoring. `Ready` is assigned immediately after a raw prompt output write. It must instead occur only after classification and validation succeed and the authoritative artifact has been atomically written.

Blocked epic authoring output should be persisted as evidence under `.agents/evidence/blockers`, not written to `.agents/epic.md`. The existing active epic must remain unchanged when blocked, ambiguous, or invalid authoring output is returned.

## Boundary design

Prompt execution, classification, validation, promotion, and lifecycle transition are separate responsibilities:

- Prompt execution means only that the model produced output.
- Output classification decides whether the output is promotable, intentionally blocked, structurally invalid, or ambiguous.
- Artifact validation checks the candidate artifact shape and invariants after classification says it is intended to be an artifact.
- Artifact promotion owns the atomic authoritative write and evidence persistence.
- Lifecycle transition marks the authoritative artifact `Ready` only after promotion succeeds.

## Epic MVP

The first implementation introduces a reusable promotion abstraction and an epic-specific classifier and validator:

- `ArtifactPromotionService` owns the promotion boundary and is artifact-type agnostic.
- `ArtifactPromotionRequest` describes target path, evidence location, classification, validation, and lifecycle behavior.
- `EpicAuthoringOutputClassifier` identifies known blocked epic-authoring documents, valid epic candidates, invalid epic candidates, and ambiguous output.
- `EpicArtifactValidator` validates required active-epic structure.
- `ArtifactPromotionResult` reports whether promotion succeeded or evidence was persisted instead.

Blocked output is a successful domain outcome, not a generic runtime failure. It produces an evidence artifact, a blocked state-machine transition, and no authoritative write.

Ambiguous and invalid output are also non-promotable. They are preserved as evidence with classification metadata so operators can inspect exactly what the model returned.

## State-machine ordering

For epic authoring, the runtime order becomes:

1. Prompt completed.
2. Output classified.
3. Epic candidate validated when classification is promotable.
4. Active epic written only after validation succeeds.
5. Active epic lifecycle marked `Ready` only after the write succeeds.
6. `ActiveEpicReady` transition recorded only after promotion succeeds.

Blocked, ambiguous, and invalid outputs stop before milestone generation. Rewrites leave the previous `.agents/epic.md` untouched unless promotion succeeds.

## Extensibility

The promotion service is not tied to epic prompt wording. Future artifact gates can provide different classifiers and validators while reusing the same promotion result, evidence persistence, lifecycle update, and state-machine transition pattern for split epics, milestone specs, roadmap completion context, audits, and completion evaluations.
