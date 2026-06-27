# Decisions: 2026-06-27 M1.2 Optionality and Compatibility Wrapper Bridge Direction

These decisions capture only newly authorized direction from the user response after the M1.2 repository-dashboard production-consumer candidate slice.

## Authorized Decisions

1. Treat the current M1.2 pipeline as the active generated-consumer migration architecture.
   - The active chain is Accepted Contract Model -> Governed Schema Metadata -> IR -> Generated Raw Artifact -> Generated Consumer Candidate -> Compatibility Wrapper -> Production Consumers.
   - The generated consumer candidate is first-class evidence, but remains downstream of migration authorization.
   - The compatibility wrapper remains the production boundary until a migration slice proves adoption and rollback.

2. Preserve generation correctness, consumer correctness, and migration authorization as separate verification concepts.
   - Pipeline determinism and freshness verify generation correctness.
   - Structural and semantic/nullability equivalence verify consumer candidate correctness.
   - Production imports require a separate migration authorization slice.

3. Make optional-by-contract the next primary schema gap to resolve.
   - Required, optional, and nullable are independent contract guarantees.
   - `value: T | null` and `value?: T` must not be treated as equivalent.
   - Optionality must come from governed metadata, not fixture observation.

4. Use `src/CommandCenter.UI/src/types/repositories.ts` as a conservative compatibility wrapper bridge.
   - Existing production imports should continue to go through the wrapper during the next slice.
   - The wrapper may alias or mechanically verify against `RepositoryDashboardConsumerCandidateProjection`.
   - Wrapper shrinkage should be incremental and evidence-backed.

5. Distinguish schema deficiency from compatibility obligation in wrapper behavior.
   - Missing metadata is a schema deficiency and requires IR/schema evolution.
   - Temporary bridge behavior because production has not migrated is a compatibility obligation.
   - Semantic transformation in the wrapper caused by missing generator metadata must be recorded as a schema gap, not accepted as compatibility behavior.

6. Publish this decision checkpoint.
   - Rotate `.agents/decisions/decisions.md` to `.agents/decisions/decisions.0073.md`.
   - Create this active decisions checkpoint containing only the newly authorized M1.2 optionality and compatibility-wrapper bridge direction.
   - Stage the completed Slice 0073 work and this decision rotation, excluding unrelated pre-existing dirty files.
   - Commit and push to `origin/dev`.
   - Stop executing after the push.

## Evidence Targets

- `.agents/decisions/decisions.0073.md`
- `.agents/decisions/decisions.md`
- `.agents/handoffs/handoff.md`
- `.agents/handoffs/handoff.0068.md`
- `.agents/milestones/m1.2-generated-contracts.md`
- `.agents/milestones/m1.2-repository-dashboard-production-consumer-candidate-slice-0073.md`
- `docs/contracts.md`
- `docs/architectural-capabilities.md`
- `docs/architectural-mechanisms.md`
- `tests/CommandCenter.Backend.Tests/ContractVerification/ContractGenerationSupport.cs`
- `tests/CommandCenter.Backend.Tests/ContractConsumerVerificationTests.cs`
- `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-dashboard.contract-ir.json`
- `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-dashboard.generated-artifact-freshness.json`
- `src/CommandCenter.UI/src/contracts/generated/repository-dashboard.generated.ts`

## Next Authorized Sequence

1. Stage only the completed Slice 0073 files and this decision rotation.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
