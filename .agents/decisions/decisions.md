# Decisions: 2026-06-26 Slice 0013 TypeScript Consumer Verification Checkpoint

These decisions capture only newly authorized direction from the response accepting TypeScript consumer verification and authorizing the checkpoint workflow.

## Authorized Decisions

1. Treat Slice 0013 as validation that consumer verification is language-agnostic.
   - The same recursive comparison engine now verifies both Rust and TypeScript downstream representations.
   - Language-specific code should remain isolated to extraction, while comparison remains canonical and shared.

2. Formalize the extractor concept as `Consumer Shape Extractor`.
   - Rust extractor, TypeScript extractor, mock extractor, and future generated-contract extractor should all produce the same canonical structural representation.
   - Avoid framing the mechanism as separate language-specific verifier implementations.

3. Preserve TypeScript as a downstream compatibility consumer.
   - Backend serialization defines the contract.
   - The Oracle observes and protects backend serialized truth.
   - Consumer verification measures downstream conformance.
   - Manual TypeScript types remain consumers, even when they are important compile-time surfaces.

4. Add repository dashboard dev mock verification next.
   - `devTauriMock.ts` should become the third repository dashboard consumer checked against the same Oracle fixture.
   - The mock is a development/test consumer and must not become a contract authority.

5. Classify consumers separately in reporting.
   - Rust mirror: runtime consumer.
   - TypeScript types: compile-time consumer.
   - `devTauriMock`: development/test consumer.
   - This categorization should be retained for future generated artifacts, SDKs, documentation, and other downstream surfaces.

6. Treat current M0.2 work as having crossed from architectural proof into coverage and certification work.
   - The architecture of the Oracle and consumer verification model is now demonstrated by Rust and TypeScript consumers.
   - Remaining M0.2 work should focus on coverage, certification, evidence, and acceptance rather than reproving the core model.

7. Commit and push Slice 0013 as an architectural checkpoint.
   - Rationale: multi-consumer verification materially strengthens the Oracle by proving a singular backend fixture can govern multiple downstream consumer classes through shared recursive comparison.

## Current M0.2 Certification Posture

| Capability | Status |
| --- | --- |
| Oracle definition | Complete |
| Boundary taxonomy | Complete |
| Contract inventory | Complete |
| Endpoint catalog | Complete |
| Field ownership | Complete for repository dashboard pilot |
| Serialization observations | Complete for repository dashboard pilot |
| Executable fixture | Complete for repository dashboard pilot |
| Recursive Oracle comparison | Complete for repository dashboard pilot |
| Drift classification | Complete for repository dashboard pilot |
| Rust consumer verification | Complete for repository dashboard pilot |
| TypeScript consumer verification | Complete for repository dashboard pilot |
| Dev mock consumer verification | Remaining |
| Generated artifact freshness | Remaining |
| Oracle certification | Remaining |

## Next Authorized Sequence

1. Add repository dashboard `devTauriMock.ts` verification against the Oracle fixture.
2. Report consumer category alongside consumer name.
3. Keep the mock classified as a development/test consumer and downstream compatibility surface.
4. Continue using the canonical recursive comparison model fed by consumer shape extractors.
