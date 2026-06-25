# Decisions

## Newly Authorized

- Treat the first Milestone 6 reconstruction transparency slice as architecturally correct because it establishes backend reasoning authority before UI presentation.
- Preserve the backend-owned confidence rationale model; the UI must not invent explanations for confidence values.
- Preserve explicit reconstruction scope as an authority projection, including direction, source, target, historical cutoff, reachable evidence, and known unreachable evidence.
- Preserve reachable versus known unreachable evidence as distinct semantic concepts for reconstruction transparency.
- Continue Milestone 6 with UI consumption of the new `confidenceRationale` and `scope` fields.
- Update `ReasoningReconstructionPanel` and `ReasoningQueryPanel` next, with characterization coverage for high versus limited confidence, forward/backward direction, reachable versus unreachable evidence, historical cutoff, and missing evidence branches.
- Keep the UI presentation semantically decomposed rather than collapsing confidence rationale and scope into prose, so Milestone 8 explainability adapters can consume structured fields.
- Treat the full-suite backend and UI order-dependent failures as infrastructure debt that should be addressed before the reasoning test matrix grows substantially, but do not let them block this completed slice.
