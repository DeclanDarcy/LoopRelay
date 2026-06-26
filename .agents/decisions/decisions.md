# Decisions: 2026-06-26 Slice 0007 Repository Dashboard Pilot Authorization

These decisions capture only newly authorized direction from the response accepting the Milestone 0.2 endpoint catalog slice and repository dashboard pilot sequence.

## Authorized Decisions

1. Treat slice 0007 as a meaningful advancement of Milestone 0.2.
   - Accepted framing: the unit of analysis has moved from contract families to externally observable endpoint contracts.
   - Accepted value: the 177-route catalog is a concrete baseline for future Oracle coverage and bypass detection.

2. Keep compatibility consumers as first-class contract inventory artifacts.
   - Required consumers to track: Rust mirrors, manual TypeScript types, development mocks, characterization tests, and other downstream contract mirrors.
   - Rationale: later migration and compatibility decisions should be based on explicit consumer evidence rather than incidental implementation knowledge.

3. Continue postponing golden fixtures until fixture preconditions are known.
   - Required preconditions: field ownership, serialization behavior, nullability, and compatibility consumers.
   - Rationale: fixtures must observe authoritative backend contracts rather than freezing accidental implementation artifacts.

4. Use the repository dashboard contract as the first pilot fixture candidate.
   - Target: `GET /api/repositories` / repository dashboard projection boundary.
   - Rationale: comparatively stable, high visibility, multi-layer consumption, lower semantic ambiguity, and likely enough shape variety to validate the Oracle workflow.
   - Explicit deferral: workflow, execution, and decision projections should not be first fixtures because they carry more evolving semantics and compatibility obligations.

5. Insert a field ownership catalog before capturing the repository dashboard fixture.
   - Required field-level columns: field, semantic owner, serialization owner, consumer count, compatibility field, required, derived, and notes.
   - Rationale: field provenance makes fixture review easier and creates the template for subsequent Oracle fixtures.

6. Keep serialization confirmation narrowly observational.
   - Required checklist: property naming policy, enum representation, null emission versus omission, empty collections, date/time representation, identifier formatting, numeric formatting when applicable, and intentional ordering guarantees.
   - Constraint: do not introduce broader compatibility policy in this slice; first describe what the backend actually emits.

## Next Authorized Sequence

1. Confirm backend JSON serialization options and observable emitted behavior.
2. Create the field ownership catalog for the repository dashboard contract.
3. Capture the first repository dashboard golden fixture only after field ownership and serialization behavior are explicit.
4. Add the first drift comparison mechanism protecting that pilot fixture.

## Explicit Non-Decisions

- No workflow, execution, or decision fixture is authorized as the first pilot fixture.
- No golden fixture is authorized before the repository dashboard field ownership catalog exists.
- No broad compatibility/versioning policy is authorized by this response.
