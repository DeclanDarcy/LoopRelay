# Implementation-First Review Details

## Source Basis

This file supplements `.agents/plan.md` using `.agents/specs/epic.md` and `.agents/specs/s0.md` through `.agents/specs/s7.md`.

Use `.agents/plan.md` as the current implementation target. The specs are the recovered intent source. Where the specs and plan differ, this file records only cross-milestone guidance so implementers do not silently follow the older shape.

Milestone-specific implementation requirements live in `.agents/milestones/m*.md`.

## Milestone Mapping

The specs use an older milestone order. The current plan intentionally refines that order:

| Spec file | Spec milestone | Current plan milestone |
| --- | --- | --- |
| `s0.md` | Architectural foundation | Milestone 0 |
| `s1.md` | Changed file classification | Milestone 2 |
| `s2.md` | Semantic candidate confirmation | Milestone 4 |
| `s3.md` | Non-implementation review ledger | Milestone 3 |
| `s4.md` | Free-form insight synthesis | Milestone 6 |
| `s5.md` | HITL epic completion review | Milestone 7 |
| `s6.md` | Planning integration and implementation-first guidance | Milestone 1 |
| `s7.md` | Architectural convergence | Milestone 8 |

The current order is useful because prompt policy and request capture need to exist before agents are asked to plan more work, ledger identity should exist before semantic confirmation persists dispositions, and post-execution integration must be proven before synthesis and completion review depend on the ledger.

## Policy Tension To Resolve

There is one meaningful tension between the specs and the current plan.

The specs say default implementation-first mode still allows non-implementation documentation when the HITL specifically requested that documentation or deliverable. The current plan says prompt-time non-implementation file generation requires both an enabled `artifactPolicy.allowHitlRequestedNonImplementationFiles` setting and an explicit HITL request.

Unless the plan is revised, implement the plan's stricter rule for prompt-time generation: enabled setting plus explicit captured HITL request. Still preserve the spec's HITL-authority intent in two ways:

- Always capture explicit HITL request evidence when it exists.
- Always allow completion review to record a human keep decision for a non-implementation file, regardless of whether the file was originally generated under the stricter prompt policy.

If product intent is to follow the original spec instead, update `.agents/plan.md` before Milestone 1 so the setting semantics are not ambiguous.

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
