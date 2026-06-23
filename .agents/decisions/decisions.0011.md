# Decisions

## Newly Authorized

- Treat template-gated manual capture as the approved manual capture shape for Milestone 2; the backend owns the capture vocabulary, not the UI or arbitrary client payloads.
- Treat `UserSupplied` provenance as an approved source kind for human-observed reasoning that exists before an authoritative artifact exists.
- Continue preserving manual captures only as immutable reasoning events, not as first-class hypothesis, alternative, contradiction, direction, assumption, or constraint records.
- Treat Milestone 2 as functionally complete from an architecture perspective: inferred capture and manual capture are both operational while preserving provenance, idempotency where applicable, and authority separation.
- Treat remaining Milestone 2 work as projection and usability work rather than core capture-architecture proof.
- Make workspace/dashboard reasoning summaries the next highest-value backend slice.
- Reasoning summaries must remain descriptive and read-only: counts, family counts, relationship/thread counts, and latest activity are acceptable.
- Do not introduce evaluative reasoning summaries such as reasoning score, reasoning health, reasoning quality, reasoning maturity, or similar aggregate judgments.
