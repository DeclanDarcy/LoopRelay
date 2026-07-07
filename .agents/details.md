# Implementation-First Review Details

## Source Basis

This file supplements `.agents/plan.md` using the HITL design discussion excerpts as the controlling intent source.

Use `.agents/plan.md` as the current implementation target. Do not use older `.agents/specs/*` material as current authority for this work; if historical spec material exists, treat it only as derived planning that must be reconciled against the HITL excerpts and the current plan.

Milestone-specific implementation requirements live in `.agents/milestones/m*.md`.

## Milestone Order Rationale

The current order is useful because prompt policy and request capture need to exist before agents are asked to plan more work, ledger identity should exist before semantic confirmation persists dispositions, and post-execution integration must be proven before synthesis and completion review depend on the ledger.

The milestone sequence should be understood as implementation ordering, not a change to product intent:

- Milestone 0 establishes terminology, settings, prompt-policy composition, minimal ledger ownership, and read-only review-runner boundaries.
- Milestone 1 injects implementation-first prompt guidance and captures explicit HITL requests before new plans can prescribe non-implementation deliverables.
- Milestones 2 through 5 implement the post-execution detection, classification, ledger, confirmation, and CLI invocation loop.
- Milestones 6 and 7 add free-form synthesis and HITL completion review after the ledgered review loop exists.
- Milestone 8 converges terminology and ownership only after implementation evidence shows duplication or friction.

## Policy Semantics To Preserve

Prompt-time non-implementation file generation requires both `artifactPolicy.allowHitlRequestedNonImplementationFiles = true` and explicit captured HITL request evidence. Enabled mode never authorizes autonomous documentation, documentation-centric milestones, or theory-protection artifacts.

Preserve HITL authority in two ways:

- Always capture explicit HITL request evidence when it exists.
- Always allow completion review to record a human keep decision for a non-implementation file, regardless of whether the file was originally generated under the stricter prompt policy.

Request evidence explains why a non-implementation deliverable may be legitimate. It is not a keep/delete decision and does not bypass HITL completion review.

## Shared Vocabulary Contract

Use one vocabulary across model names, JSON fields, prompt text, tests, evidence files, and review output.

Deterministic classification routes:

- `ExcludedImplementationArtifact`
- `ExcludedMachineRequiredArtifact`
- `ExcludedSanctionedOperationalArtifact`
- `SemanticReviewCandidate`
- `AmbiguousForSemanticReview`

Semantic dispositions:

- `ConfirmedNonImplementation`
- `FalsePositive`
- `Uncertain`

Ledger resolution states:

- `Unresolved`
- `HitlKept`
- `HitlDeleted`
- `HitlFalsePositive`
- `HitlDeferred`

HITL provenance kinds:

- `None`
- `HitlRequested`
- `HitlKept`

Do not use `UncertainCandidate` for deterministic routing. `AmbiguousForSemanticReview` means "send this file to semantic confirmation." `Uncertain` is only a semantic confirmation outcome.

## Artifact Paths

Canonical review paths:

- `.agents/review/non-implementation-ledger.json`
- `.agents/review/non-implementation-review.md`
- `.agents/review/non-implementation-decisions.md`
- `.agents/review/non-implementation-synthesis.md`
- `.agents/evidence/non-implementation/`

Add these paths to `OrchestrationArtifactPaths`. `CompletionArtifactPaths` should delegate to the orchestration constants for shared `.agents` paths instead of duplicating new literals. `.agents/review` and `.agents/evidence/non-implementation` are sanctioned operational artifacts and must be excluded from semantic review.

## Non-Goals To Preserve

Do not broaden the implementation into:

- structured insight synthesis
- semantic deduplication
- repository knowledge projection
- preservation metrics
- repository health analysis
- documentation debt analysis
- semantic garbage collection
- repository mutation acceptance
- commit gating
- publication gating
- repository certification
- broad relay/runtime redesign
