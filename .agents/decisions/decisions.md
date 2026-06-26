# Decisions: 2026-06-26 Slice 0009 Oracle Mechanism Checkpoint

These decisions capture only newly authorized direction from the response accepting the repository dashboard Oracle fixture slice and authorizing the checkpoint workflow.

## Authorized Decisions

1. Treat Slice 0009 as the point where the Contract Oracle transitions from architectural concept to executable mechanism.
   - Accepted pipeline: backend projection -> backend serialization -> golden fixture -> recursive comparison -> executable drift detection.
   - Accepted status: the repository dashboard fixture establishes the first end-to-end Oracle mechanism, while the broader Oracle remains uncertified.

2. Treat the Rust `RepositoryDashboardProjection` omission of `decisionSessionSummary` as executable architectural evidence.
   - The drift remains a consumer compatibility finding, not a backend contract defect.
   - The Oracle remains authoritative; consumer drift must be surfaced through consumer verification rather than by weakening the backend fixture.

3. Prioritize drift policy classification before expanding fixture coverage.
   - Structural drift should be a hard failure: field removal, field rename, type change, null/object mismatch, array/scalar mismatch, serializer behavior change, or required field missing.
   - Compatibility drift should require review: additive optional field, additive metadata, compatibility alias, unordered ordering change, or serializer configuration expansion.
   - Consumer drift should be visible through consumer verification and must not fail the Oracle itself.

4. Prioritize downstream consumer verification before adding a second fixture.
   - Target chain: backend -> Oracle -> consumer verification.
   - Consumer verification answers how far Rust, TypeScript, mocks, or characterization tests have drifted without treating those consumers as contract authority.

5. Defer the second fixture until after policy drift classification and consumer verification exist.
   - The second fixture should be orthogonal to the dashboard business example.
   - Its purpose should be serialization edge coverage: explicit nulls, omitted fields, empty arrays, empty objects, zero-length history, maximum nesting, and timestamp edge cases.

6. Commit and push Slice 0009 as a single architectural checkpoint.
   - Rationale: Milestone 0.2 moved from documentation into executable enforcement, with implementation, documentation, evidence, and backend verification aligned.

## Current M0.2 Certification Posture

| Capability | Status |
| --- | --- |
| Oracle definition | Complete |
| Boundary taxonomy | Complete |
| Consumer taxonomy | Complete |
| Endpoint inventory | Complete |
| Field ownership | Complete |
| Parallel truth inventory | Complete |
| First executable fixture | Complete |
| Recursive comparison | Complete |
| Drift policy | Pending |
| Consumer verification | Pending |
| Oracle certification | Pending |

## Next Authorized Sequence

1. Add policy-drift classification around the repository dashboard Oracle pilot.
2. Add downstream consumer verification while preserving backend Oracle authority.
3. Add an orthogonal serialization edge-case fixture after consumer verification is in place.
