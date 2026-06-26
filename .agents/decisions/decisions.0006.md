# Decisions: 2026-06-26 Slice 0006 Checkpoint and Milestone 0.2 Continuation

These decisions capture only newly authorized direction from the response accepting the Milestone 0.2 Oracle definition and inventory slice.

## Authorized Decisions

1. Treat slice 0006 as the correct direction for Milestone 0.2.
   - Accepted framing: define the Oracle before implementing fixtures or drift mechanisms.
   - Accepted invariant: the Oracle observes backend-owned serialized contracts; it does not become a semantic or contract authority.

2. Keep golden fixtures delayed until contract ownership and consumer mapping are explicit.
   - Required preconditions: semantic owner, serialized-shape owner, consumer set, duplicate representations, and compatibility obligations.
   - Rationale: fixtures must not certify accidental implementation details.

3. Add `Compatibility consumers` as a required field in the endpoint-level contract inventory.
   - Required endpoint-level fields: endpoint or command, backend authority, projection type, serialization authority, contract identity, consumers, parallel truths, compatibility consumers, fixture candidate, and fixture priority.
   - Rationale: this makes the inventory directly useful for Milestones 1.1 and 1.2 without another discovery pass.

4. Define narrow observable serialization rules before selecting the first fixture.
   - Initial focus: identifier representation, enum serialization, null versus omitted fields, empty collections, property naming, date/time representation, ordering guarantees if any, and unknown-field preservation expectations.
   - Deferral: broader versioning and compatibility policy should wait until the actual contract surface is sufficiently observed.

5. Create a checkpoint commit before continuing deeper into Milestone 0.2.
   - Scope: Milestone 0.1 certification and initial mechanisms, shell passivity preparation already present in the working body, Oracle definition, durable docs, handoff, evidence, and decision trail.

## Next Authorized Sequence

1. Endpoint-level contract inventory.
2. Consumer taxonomy.
3. Serialization rules.
4. Parallel truth matrix completion.
5. First carefully selected golden fixture.
6. Drift comparison mechanism.

## Explicit Non-Decisions

- No golden fixture is authorized before endpoint-level inventory and narrow serialization rules.
- No Oracle drift comparison mechanism is authorized before fixture preconditions are satisfied.
- No production migration of Rust mirrors, TypeScript manual types, or dev mock contracts is authorized by this checkpoint.
