# Decisions

## Newly Authorized

- Treat Milestone 6 reconstruction transparency and materialization transparency as complete vertical slices that follow the backend-authority to structured-projection to typed-client to render-only-UI pattern.
- Continue Milestone 6 with capture provenance transparency as the next slice.
- Model capture provenance as a distinct semantic concept rather than a loose collection of optional fields.
- Every reasoning event should expose structured provenance that answers how the reasoning was captured, where it came from, and why it exists.
- Represent manual, assisted, and inferred capture modes explicitly, with subtype-specific details attached beneath the shared capture provenance concept.
- For inferred capture, expose source transition, source artifact, capture reason, captured by, and source timestamp.
- For skipped or deduplicated capture, expose skip reason, duplicate signal, and existing event reference.
- Preserve the established pattern where reasoning projections expose backend-owned semantics and the UI renders those facts without inventing explanations or lifecycle authority.
