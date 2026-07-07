# Milestone 4 - Semantic Candidate Confirmation

## Objective

confirm routed candidates with a mutation-impossible read-only agent workflow.

## Work
- [ ] Add `ConfirmNonImplementationCandidate.prompt` under `src/LoopRelay.Core/Prompts`.
- [ ] Prompt requirements:
  - [ ] input includes ledger entry ID, candidate path, deterministic evidence, slice ID, baseline status, post status, reviewed content hash or deleted-reviewed identity, and bounded content excerpt or instructions to inspect the file read-only
  - [ ] output is strict JSON or an exact Markdown field table parsed into:
    - [ ] ledger entry ID
    - [ ] candidate path
    - [ ] reviewed content hash or deleted-reviewed identity
    - [ ] disposition: `ConfirmedNonImplementation`, `FalsePositive`, or `Uncertain`
    - [ ] concise rationale
    - [ ] evidence excerpts or path facts
    - [ ] uncertainty note when applicable
  - [ ] prompt explicitly forbids keep/delete decisions
- [ ] Implement parser and validation for the structured output.
- [ ] Implement `NonImplementationSemanticConfirmer`.
  - [ ] Consume only `SemanticReviewCandidate` and `AmbiguousForSemanticReview` routes from deterministic classification.
  - [ ] Ask `NonImplementationReviewLedgerStore` whether a valid semantic disposition already exists for the exact path/hash/classifier/prompt identity.
  - [ ] Treat false positives as normal outcomes.
  - [ ] Preserve semantic uncertainty instead of forcing a binary answer.
  - [ ] Update ledger entries with semantic disposition and rationale.
- [ ] Host composition:
  - [ ] main CLI and roadmap/completion tests pass an `INonImplementationReviewRunner`
  - [ ] shared primitives do not depend on `LoopRelay.Cli.AgentSpecs`
  - [ ] tests assert review services receive read-only runner calls and never open operational or scoped mutation specs
- [ ] Add tests:
  - [ ] parser accepts each valid disposition
  - [ ] parser rejects missing/unknown disposition
  - [ ] parser rejects mismatched entry ID, path, content hash, or reviewed status
  - [ ] service skips only valid exact ledger identities
  - [ ] service confirms candidates and records rationale
  - [ ] service does not process deterministic exclusions
  - [ ] ambiguous deterministic routes are semantically confirmed instead of treated as final uncertainty
  - [ ] service cannot be constructed with a mutation-capable runner adapter

## Detail Notes

The confirmation prompt should include:

- ledger entry ID
- candidate path
- route: `SemanticReviewCandidate` or `AmbiguousForSemanticReview`
- deterministic evidence
- execution slice ID or discovery context
- baseline status
- post status
- reviewed content hash or deleted-reviewed identity
- bounded content excerpt, or read-only inspection instructions

Output should be strict JSON or an exact parseable field table with:

- ledger entry ID
- candidate path
- reviewed content hash or deleted-reviewed identity
- disposition: `ConfirmedNonImplementation`, `FalsePositive`, or `Uncertain`
- concise rationale
- evidence excerpts or path facts
- uncertainty note when applicable

The parser must reject missing dispositions, unknown dispositions, mismatched entry ID, mismatched path, mismatched reviewed hash/status, and malformed output. Parser failure is review infrastructure failure, not semantic uncertainty.

The prompt must forbid keep/delete decisions. False positives are expected outcomes, not errors. Semantic uncertainty must remain durable instead of being forced into confirmed or false-positive categories.

Semantic confirmation must depend on `INonImplementationReviewRunner`, not on the normal mutation-capable execution path. The runner accepts bounded prompt payloads and cancellation tokens, returns structured text only, has no workspace writes, has no commits or pushes, has no mutation-capable scoped artifact operation, and may read only the repository context needed for review.

Shared primitives should not depend on CLI-specific agent specs. CLI, roadmap, and completion hosts can provide adapters, but tests must prove review services use the read-only runner and cannot be constructed with a mutation-capable runner adapter.

## Acceptance
- [ ] Every routed candidate receives a durable semantic disposition or is skipped by a valid exact ledger identity.
- [ ] False positives and semantic uncertainty are first-class outcomes.
- [ ] Semantic confirmation does not decide retention or deletion.
- [ ] Semantic confirmation cannot mutate repository files.
