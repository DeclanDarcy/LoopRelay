# Milestone 6 - Free-Form Insight Synthesis

## Objective

produce compact review support from confirmed non-implementation files before human decisions.

## Work
- [ ] Add `SynthesizeNonImplementationInsights.prompt` under `src/LoopRelay.Core/Prompts`.
- [ ] Implement `NonImplementationInsightSynthesizer`.
  - [ ] Input: unresolved confirmed non-implementation ledger entries, semantic rationale, bounded file content, source paths, and entry IDs.
  - [ ] Exclude false positives.
  - [ ] Include semantically uncertain entries only in a separate "uncertain, not synthesized as fact" section if useful.
  - [ ] Output compact free-form Markdown with source path references and ledger entry IDs.
  - [ ] Do not require or produce a structured knowledge schema.
  - [ ] Do not authorize keeping, deleting, or promoting files.
  - [ ] Use only `INonImplementationReviewRunner`.
- [ ] Write synthesis to `.agents/review/non-implementation-synthesis.md` and record its source entry IDs/hashes in the ledger or a small sidecar section.
- [ ] Add tests:
  - [ ] synthesizer is not invoked when no confirmed entries exist
  - [ ] false positives are excluded
  - [ ] synthesis output path is stable
  - [ ] review set links synthesis to source entries
  - [ ] synthesis runner uses read-only review runner only

## Acceptance
- [ ] Confirmed non-implementation files can yield a compact synthesis before HITL review.
- [ ] Synthesis remains free-form and source-linked.
- [ ] Synthesis is review support only.
- [ ] Synthesis cannot mutate repository files.
