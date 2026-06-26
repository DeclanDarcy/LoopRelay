# Decisions: 2026-06-26 Slice 0011 Consumer Verification Checkpoint

These decisions capture only newly authorized direction from the response accepting the consumer verification pilot and authorizing the checkpoint workflow.

## Authorized Decisions

1. Treat Slice 0011 as the point where contract authority and consumer conformance become architecturally separate executable mechanisms.
   - The Contract Oracle owns backend serialized contract truth.
   - Consumer verification owns downstream conformance.
   - Downstream Rust, TypeScript, mocks, and fixtures must not become alternative sources of contract authority.

2. Classify the repository dashboard Rust mirror finding as executable consumer drift.
   - The known `$[].decisionSessionSummary` omission has progressed from observation to documented parallel truth to continuously observable consumer verification evidence.
   - The drift remains a downstream conformance issue, not a backend contract defect.

3. Evolve consumer verification by levels rather than by technology-specific one-offs.
   - Level 1: structural surface checks such as missing fields, extra fields, type mismatches, and property names.
   - Level 2: nested shape checks including recursive objects, arrays, optional values, and null semantics.
   - Level 3: consumer inventory across Rust mirrors, manual TypeScript types, development mocks, and characterization fixtures.
   - Level 4: semantic consumer verification for downstream eligibility, severity, lifecycle, recommendation, or other semantic computation.

4. Keep semantic consumer verification separate from structural consumer drift.
   - Semantic verification should wait until later authority restoration work.
   - Semantic reinterpretation is a different class of drift from structural mismatch and must remain separately classified.

5. Generalize consumer verification before adding more consumers.
   - Introduce a reusable abstraction with verifier name, source, comparison strategy, and exclusions.
   - Plug the Rust repository dashboard verifier into that framework first.
   - Add TypeScript as the second consumer and manual mocks as the third.

6. Treat Milestone 0.2 as architecturally mature in concept but not yet certified.
   - Remaining work is breadth, framework generalization, additional consumers, broader contract catalog coverage, and certification.

7. Commit and push Slice 0011 as an architectural checkpoint.
   - Rationale: the Oracle/conformance distinction is now executable, documented, and tested as a coherent architectural capability.

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
| Recursive comparison | Complete |
| Drift classification | Complete for repository dashboard pilot |
| Consumer verification | Complete as pilot |
| Oracle certification | Remaining |

## Next Authorized Sequence

1. Generalize the consumer verification framework with nested recursive comparison and reusable verifier abstraction.
2. Plug the Rust repository dashboard verifier into that framework.
3. Add TypeScript repository dashboard verification.
4. Add manual mock repository dashboard verification.
