# Milestone 4 - Semantic Candidate Confirmation

## Objective

confirm routed candidates with a mutation-impossible read-only agent workflow.

## Work
- [ ] Add `ConfirmNonImplementationCandidate.prompt` under `src/LoopRelay.Core/Prompts`.
- [ ] Prompt requirements:
  - [ ] input includes candidate path, deterministic evidence, slice ID, baseline status, post status, reviewed content hash, and bounded content excerpt or instructions to inspect the file read-only
  - [ ] output is strict JSON or an exact Markdown field table parsed into:
    - [ ] ledger entry ID
    - [ ] candidate path
    - [ ] reviewed content hash
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
  - [ ] parser rejects mismatched entry ID, path, or content hash
  - [ ] service skips only valid exact ledger identities
  - [ ] service confirms candidates and records rationale
  - [ ] service does not process deterministic exclusions
  - [ ] service cannot be constructed with a mutation-capable runner adapter

## Acceptance
- [ ] Every routed candidate receives a durable semantic disposition or is skipped by a valid exact ledger identity.
- [ ] False positives and semantic uncertainty are first-class outcomes.
- [ ] Semantic confirmation does not decide retention or deletion.
- [ ] Semantic confirmation cannot mutate repository files.
