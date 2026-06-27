# Decisions: 2026-06-27 M1.2 Governance Repair and Wrapper Bridge Publication

These decisions capture only newly authorized direction from the user response after the M1.2 repository-dashboard compatibility-wrapper bridge slice.

## Authorized Decisions

1. Treat the active governance traceability failure as the immediate blocker for M1.2 acceptance.
   - The repository-dashboard compatibility-wrapper bridge may not be accepted while `ArchitecturalDecisionGovernanceTests.ReferentialGovernanceClaimsRemainReachable` fails.
   - M1.2 implementation work should not continue to the dev Tauri mock bridge until governance traceability is repaired.

2. Repair the active decision checkpoint by creating a new narrow decision record with reachable M0.4 governance evidence.
   - The governance repair is supported by `.agents/milestones/m0.4-referential-governance-validation-slice-0054.md`.
   - The prior active decision checkpoint is rotated to `.agents/decisions/decisions.0074.md`.
   - This checkpoint supersedes only the active governance-traceability state; it does not redefine the generated-consumer migration architecture.

3. Publish the completed M1.2 repository-dashboard compatibility-wrapper bridge after governance repair.
   - Stage the Slice 0074 wrapper bridge, generated artifacts, evidence, docs, handoff rotation, and this decision rotation.
   - Exclude unrelated pre-existing dirty files, including `src/CommandCenter.DecisionSessions/CommandCenter.DecisionSessions.csproj`, `design.md`, and `refactor-readiness.md`.
   - Commit and push to `origin/dev`.
   - Stop executing after the push.

## Evidence Targets

- `.agents/decisions/decisions.0074.md`
- `.agents/decisions/decisions.md`
- `.agents/handoffs/handoff.md`
- `.agents/handoffs/handoff.0069.md`
- `.agents/milestones/m1.2-generated-contracts.md`
- `.agents/milestones/m1.2-repository-dashboard-compatibility-wrapper-bridge-slice-0074.md`
- `.agents/milestones/m0.4-referential-governance-validation-slice-0054.md`
- `docs/contracts.md`
- `docs/architectural-capabilities.md`
- `docs/architectural-mechanisms.md`
- `tests/CommandCenter.Backend.Tests/ContractVerification/ContractGenerationSupport.cs`
- `tests/CommandCenter.Backend.Tests/ContractConsumerVerificationTests.cs`
- `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-dashboard.contract-ir.json`
- `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-dashboard.generated-artifact-freshness.json`
- `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-dashboard.artifact-freshness.json`
- `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-workspace.artifact-freshness.json`
- `src/CommandCenter.UI/src/contracts/generated/repository-dashboard.generated.ts`
- `src/CommandCenter.UI/src/types/repositories.ts`

## Next Authorized Sequence

1. Re-run the architectural decision governance guard.
2. Stage only the completed Slice 0074 files, handoff rotation, and this decision rotation.
3. Commit and push to `origin/dev`.
4. Stop executing after the push.
