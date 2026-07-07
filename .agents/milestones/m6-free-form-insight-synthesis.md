# Milestone 6 - Free-Form Insight Synthesis

## Objective

produce compact review support from confirmed non-implementation files before human decisions.

## Work
- [ ] Add `SynthesizeNonImplementationInsights.prompt` under `src/LoopRelay.Core/Prompts`.
- [ ] Implement `NonImplementationInsightSynthesizer`.
  - [ ] Input: unresolved confirmed non-implementation ledger entries, semantic rationale, bounded file content, source paths, entry IDs, and reviewed hashes.
  - [ ] Exclude false positives.
  - [ ] Include semantically uncertain entries only in a separate "uncertain, not synthesized as fact" section if useful.
  - [ ] Output compact free-form Markdown with source path references and ledger entry IDs.
  - [ ] Do not require or produce a structured knowledge schema.
  - [ ] Do not authorize keeping, deleting, promoting, or retaining files.
  - [ ] Use only `INonImplementationReviewRunner`.
- [ ] Write synthesis to `.agents/review/non-implementation-synthesis.md` and record its source entry IDs/hashes in the ledger or a small sidecar section.
- [ ] Treat synthesis as stale unless its source entry IDs, reviewed hashes, and synthesis prompt source hash still match.
- [ ] Add tests:
  - [ ] synthesizer is not invoked when no confirmed entries exist
  - [ ] false positives are excluded
  - [ ] synthesis output path is stable
  - [ ] review set links synthesis to source entries
  - [ ] stale source entry IDs, hashes, or prompt hash require regeneration
  - [ ] synthesis runner uses read-only review runner only

## Detail Notes

Synthesis is optional review support generated before HITL keep/delete decisions.

Inputs:

- unresolved confirmed non-implementation ledger entries
- semantic rationale
- bounded file content
- source paths
- entry IDs and reviewed hashes

Rules:

- Exclude false positives.
- Do not synthesize semantically uncertain entries as fact. If useful, include them in a separate "uncertain, not synthesized as fact" section.
- Keep output compact and free-form Markdown.
- Include source path references and ledger entry IDs.
- Write to `.agents/review/non-implementation-synthesis.md`.
- Record source entry IDs and reviewed hashes in the ledger or a sidecar section.
- Treat synthesis as stale unless its source entry IDs, reviewed hashes, and synthesis prompt source hash still match.

Synthesis must not authorize keeping, deleting, promoting, or retaining any source file. It is not a structured knowledge system.

`NonImplementationInsightSynthesizer` must depend on `INonImplementationReviewRunner`, not on the normal mutation-capable execution path.

## Acceptance
- [ ] Confirmed non-implementation files can yield a compact synthesis before HITL review.
- [ ] Synthesis remains free-form and source-linked.
- [ ] Synthesis is review support only.
- [ ] Synthesis cannot mutate repository files.
