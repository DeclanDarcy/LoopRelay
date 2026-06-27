# Decisions: 2026-06-26 M0.3 Frontend Regression Skeleton Direction

These decisions capture only newly authorized direction from the user response following Slice 0044.

## Authorized Decisions

1. Accept the M0.3 regression architecture specification slice as a good synthesis slice.
   - The regression framework model is now largely defined.
   - Subsequent M0.3 work should increasingly focus on expanding enforcement rather than inventing new governance concepts.

2. Preserve the separation between regression framework metadata and verifier implementations.
   - Metadata includes invariants, taxonomy, ownership, severity, drift, confidence, lifecycle, UX, and governance.
   - Implementations include reflection, fixture comparison, consumer verification, freshness, request-boundary checks, and future mechanisms.
   - The metadata layer is the stable governance layer that future regression implementations plug into.

3. Keep certification bounded by evidence.
   - A regression cannot certify beyond its evidence, coverage breadth, confidence level, and lifecycle state.
   - Strong severity, many tests, or broad-looking coverage do not imply architectural certainty by themselves.

4. Keep regression evolution governed.
   - Changes to scope, mechanism, owner, severity, lifecycle, confidence, evidence obligations, or certification use require lifecycle governance.

5. Proceed next with a minimal frontend regression skeleton.
   - The first frontend slice should establish location, discovery, naming, organization, and registration.
   - It should intentionally avoid introducing many frontend architecture rules.
   - It should mirror the backend framework's incremental start.

6. Make the frontend skeleton conceptually parallel to the backend architecture-test structure.
   - Backend architecture tests and frontend architecture tests should have uniform governance, even when their implementations differ.
   - Later rules should be reasoned about consistently across implementation languages.

7. Classify natural ownership of future regression categories.
   - Backend: contract authority, projection purity, serialization, and Oracle.
   - Frontend: presentation purity, controller and workspace boundaries, consumer verification, and resource ownership.
   - Cross-layer: transport, generated artifacts, and contract consistency.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0044 plus this decision checkpoint.
2. Stop executing after the push.
