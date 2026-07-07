# Milestone 0 - Architectural Foundation

## Objective

establish terminology, settings, prompt-policy composition, minimal ledger ownership, and review runner boundaries before implementing the full loop.

## Work
- [x] Add shared vocabulary types and documented constants. Do not create standalone non-implementation documentation unless the HITL specifically requests it.
- [x] Add `NonImplementationArtifactPolicyOptions` and settings loader support.
- [x] Add `ImplementationFirstPromptPolicyComposer` that returns stable guidance text for implementation-first and HITL-request-enabled modes.
- [x] Add a minimal `NonImplementationReviewLedgerStore` skeleton with schema version, stable path, load/save validation, and empty document support. This exists early so semantic confirmation can safely depend on it.
- [x] Define `INonImplementationReviewRunner`.
  - [x] Inputs are bounded prompt payloads and cancellation tokens.
  - [x] Output is returned as structured text only.
  - [x] The runner must use a read-only sandbox/profile with no workspace writes, no commits, no pushes, and no mutation-capable scoped artifact operation.
  - [x] Confirmation and synthesis services must depend on this interface, not on the normal execution agent path.
- [x] Define review ownership:
  - [x] slice baseline and changed-file detection live in orchestration primitives
  - [x] semantic confirmation and synthesis run only through the read-only review runner
  - [x] main CLI invokes the post-execution identification loop after execution writes and before the `.agents` post-execution publish
  - [x] epic-completion review runs before final completion evaluation closes the epic
  - [x] roadmap/planning prompts consume the centralized prompt policy text
- [x] Add focused tests in `LoopRelay.Orchestration.Primitives.Tests` and `LoopRelay.Permissions.Tests` for settings defaults, ledger skeleton validation, read-only runner contract checks, and policy text selection.

## Detail Notes

Vocabulary types or constants must cover all shared review states from `.agents/details.md`: deterministic routes, semantic dispositions, ledger resolution states, and HITL provenance kinds. `AmbiguousForSemanticReview` is a deterministic route and must not be modeled as the final semantic disposition `Uncertain`.

Add canonical review path constants to `OrchestrationArtifactPaths`:

- `.agents/review/non-implementation-ledger.json`
- `.agents/review/non-implementation-review.md`
- `.agents/review/non-implementation-decisions.md`
- `.agents/review/non-implementation-synthesis.md`
- `.agents/evidence/non-implementation/`

`CompletionArtifactPaths` may keep compatibility members later, but shared `.agents` literals should delegate to the orchestration constants instead of introducing duplicate strings.

Extend the current settings loader rather than replacing it. The existing `LoadPermissionPolicy()` compatibility helper must keep returning only `PermissionPolicyOptions`. The broader load result should include permissions, artifact policy, source path, and default-template state without changing `permissions` validation.

Add default settings support for:

```json
"artifactPolicy": {
  "allowHitlRequestedNonImplementationFiles": false
}
```

Missing `artifactPolicy` and missing `allowHitlRequestedNonImplementationFiles` both default to implementation-first mode. The setting controls prompt guidance only; it never disables post-execution review.

The read-only review runner is a shared boundary. It accepts bounded prompt payloads and cancellation tokens, returns structured text only, has no workspace writes, has no commits or pushes, has no mutation-capable scoped artifact operation, and may read only the repository context needed for review. Shared primitives should not depend on CLI-specific agent specs.

## Acceptance
- [x] Terms are represented by explicit types or documented constants.
- [x] `allowHitlRequestedNonImplementationFiles` defaults to implementation-first mode.
- [x] One prompt-policy composer produces all implementation-first guidance.
- [x] A minimal ledger store exists before semantic confirmation.
- [x] Semantic confirmation and synthesis cannot use a mutation-capable runner.
