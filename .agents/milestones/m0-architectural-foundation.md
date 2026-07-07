# Milestone 0 - Architectural Foundation

## Objective

establish terminology, settings, prompt-policy composition, minimal ledger ownership, and review runner boundaries before implementing the full loop.

## Work
- [ ] Add shared vocabulary types and documented constants. Do not create standalone non-implementation documentation unless the HITL specifically requests it.
- [ ] Add `NonImplementationArtifactPolicyOptions` and settings loader support.
- [ ] Add `ImplementationFirstPromptPolicyComposer` that returns stable guidance text for implementation-first and HITL-request-enabled modes.
- [ ] Add a minimal `NonImplementationReviewLedgerStore` skeleton with schema version, stable path, load/save validation, and empty document support. This exists early so semantic confirmation can safely depend on it.
- [ ] Define `INonImplementationReviewRunner`.
  - [ ] Inputs are bounded prompt payloads and cancellation tokens.
  - [ ] Output is returned as structured text only.
  - [ ] The runner must use a read-only sandbox/profile with no workspace writes, no commits, no pushes, and no mutation-capable scoped artifact operation.
  - [ ] Confirmation and synthesis services must depend on this interface, not on the normal execution agent path.
- [ ] Define review ownership:
  - [ ] slice baseline and changed-file detection live in orchestration primitives
  - [ ] semantic confirmation and synthesis run only through the read-only review runner
  - [ ] main CLI invokes the post-execution identification loop after execution writes and before the `.agents` post-execution publish
  - [ ] epic-completion review runs before completion certification closes the epic
  - [ ] roadmap/planning prompts consume the centralized prompt policy text
- [ ] Add focused tests in `LoopRelay.Orchestration.Primitives.Tests` and `LoopRelay.Permissions.Tests` for settings defaults, ledger skeleton validation, read-only runner contract checks, and policy text selection.

## Acceptance
- [ ] Terms are represented by explicit types or documented constants.
- [ ] `allowHitlRequestedNonImplementationFiles` defaults to implementation-first mode.
- [ ] One prompt-policy composer produces all implementation-first guidance.
- [ ] A minimal ledger store exists before semantic confirmation.
- [ ] Semantic confirmation and synthesis cannot use a mutation-capable runner.
