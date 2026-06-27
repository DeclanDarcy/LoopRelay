# Decisions: 2026-06-26 Slice 0012 Recursive Consumer Verification Checkpoint

These decisions capture only newly authorized direction from the response accepting recursive consumer verification and authorizing the checkpoint workflow.

## Authorized Decisions

1. Treat Slice 0012 as the point where consumer verification becomes a framework-oriented mechanism rather than a consumer-specific test.
   - Recursive parsing is valuable because it advances the architecture toward reusable consumer conformance verification.
   - The verifier should continue evolving around consumer shape, recursive comparison, and drift reporting rather than one-off downstream technology checks.

2. Preserve layered consumer verification as the M0.2 progression model.
   - Level 1: surface field verification is established.
   - Level 2: recursive structural verification is established for the Rust repository dashboard pilot.
   - Level 3: multiple consumer verification is next.
   - Level 4: semantic consumer verification remains future authority-restoration work.

3. Add TypeScript repository dashboard verification next, but keep it symmetric with Rust.
   - Do not build a standalone TypeScript-only verifier.
   - Implement TypeScript as another shape provider feeding the same recursive comparison and drift report mechanism.

4. Split consumer verification conceptually into extraction and comparison before adding TypeScript.
   - Consumer shape extraction should produce a canonical consumer shape.
   - Recursive comparison should remain language-agnostic.
   - Rust, TypeScript, mock, and future generated-contract extractors should all feed the same intermediate representation.

5. Treat the transient execution-session test failure as an observation, not an architectural decision.
   - It was observed once, passed in isolation, and passed again in the full backend suite.
   - No M0.2 scope expansion is authorized unless the failure becomes reproducible.
   - Monitor for recurrence.

6. Commit and push Slice 0012 as an architectural checkpoint.
   - Rationale: recursive consumer verification materially strengthens the Oracle ecosystem by generalizing consumer conformance beyond a one-off Rust comparison.

## Current M0.2 Certification Posture

| Capability | Status |
| --- | --- |
| Oracle definition | Complete |
| Boundary taxonomy | Complete |
| Endpoint inventory | Complete |
| Consumer taxonomy | Complete |
| Field catalog | Complete for repository dashboard pilot |
| Serialization observations | Complete for repository dashboard pilot |
| First executable fixture | Complete |
| Recursive Oracle comparison | Complete for repository dashboard pilot |
| Drift classification | Complete for repository dashboard pilot |
| Consumer verification | Complete as Rust pilot |
| Recursive consumer verification | Complete as Rust pilot |
| Multiple consumer verification | Remaining |
| Oracle certification | Remaining |

## Next Authorized Sequence

1. Refactor consumer verification around canonical consumer shape extraction and language-agnostic recursive comparison.
2. Keep the Rust repository dashboard extractor plugged into that pipeline.
3. Add TypeScript repository dashboard shape extraction and verification.
4. Add manual mock repository dashboard verification after TypeScript.
