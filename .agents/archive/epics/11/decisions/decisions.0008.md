# Decisions: 2026-06-26 Slice 0008 Repository Dashboard Oracle Fixture Authorization

These decisions capture only newly authorized direction from the response accepting the repository dashboard field catalog and authorizing the first fixture/comparison direction.

## Authorized Decisions

1. Treat the repository dashboard field catalog slice as validating the Contract Oracle sequence.
   - Accepted evidence: the Rust `RepositoryDashboardProjection` omits `decisionSessionSummary`, while backend serialization and TypeScript representation include it.
   - Accepted conclusion: this is concrete parallel-truth drift discovered before automation, not a discrepancy to normalize away silently.

2. Preserve backend serialized JSON as the contract authority for the dashboard pilot.
   - Ownership chain: backend projection -> backend serialized JSON -> Oracle observation -> drift identification -> consumer migration.
   - Constraint: downstream Rust, TypeScript, mock, or test representations must not redefine the contract.

3. Continue using repository dashboard as the first Oracle fixture pilot.
   - Rationale: broad enough to exercise realistic serialization, simple enough to reason about, consumed across layers, and now proven to expose a real compatibility discrepancy.

4. Make the first repository dashboard fixture behaviorally representative, not merely typical.
   - Required coverage: explicit null values, intentional omitted-versus-present optional fields where applicable, empty collections, populated collections, nested objects, nested arrays, timestamps, durations, enum values, non-empty execution summary, non-empty execution history, populated `decisionSessionSummary`, and at least one downstream-duplicated field.

5. Treat the Rust dashboard mirror drift as Oracle evidence and later migration input.
   - The drift supports later shell mirror retirement, generation, or verification.
   - It does not authorize treating the Rust mirror as contract authority.

6. Separate recursive comparison outcomes into structural drift and policy drift.
   - Structural drift should fail immediately: missing field, unexpected field where not allowed, type mismatch, null versus object, array versus scalar, and property name changes.
   - Policy drift should require review: new optional field, compatibility field, unordered ordering change, additive metadata, and serializer option changes that preserve semantics.

## Next Authorized Sequence

1. Select or build representative repository dashboard fixture data.
2. Capture the first repository dashboard golden fixture from backend serialized JSON.
3. Add recursive comparison that classifies structural drift separately from policy drift.
4. Keep the Rust mirror discrepancy visible as a compatibility finding until shell mirror retirement, generation, or verification work addresses it.

## Explicit Non-Decisions

- No authorization to treat Rust, TypeScript, mocks, or characterization tests as contract authority.
- No authorization to silently fix or normalize the Rust dashboard mirror drift as part of Milestone 0.2 inventory.
- No authorization to start Milestone 1.2 generated contracts before the repository dashboard Oracle fixture and comparison mechanism exist.
