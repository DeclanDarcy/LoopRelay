# Decisions: 2026-06-27 M1.2 Generated TypeScript Consumer Policy Governance Repair

These decisions capture only newly authorized direction from the user response after M1.2 generated TypeScript consumer policy Slice 0070.

## Authorized Decisions

1. Treat the Slice 0070 TypeScript consumer policy as the correct M1.2 architectural boundary.
   - Single fixture observation is not a production consumer contract.
   - Raw generated aliases remain evidence-only.
   - Production generated aliases require explicit schema facts.
   - `src/CommandCenter.UI/src/types/repositories.ts` remains the repository-dashboard compatibility wrapper until those facts exist.

2. Treat the governance verifier failure as a real blocker.
   - The failing `ReferentialGovernanceClaimsRemainReachable` test must not be ignored.
   - The active M1.2 decision checkpoint must satisfy the established M0.4 reachability rule when durable contract policy changes.
   - The required governance evidence link is `.agents/milestones/m0.4-active-governance-artifact-validation-slice-0053.md`.

3. Authorize a narrow decision-governance repair before schema metadata work.
   - Rotate the prior active decision checkpoint to the next numbered decision file.
   - Create this active decision checkpoint for the M1.2 generated TypeScript consumer policy and governance repair.
   - Re-run `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter FullyQualifiedName~ArchitecturalDecisionGovernanceTests`.
   - Proceed to the repository-dashboard schema metadata pilot only after governance verification passes.

4. Publish the checkpoint.
   - Stage only the completed M1.2 Slice 0070 policy work, the handoff rotation/update, and this decision rotation.
   - Do not stage unrelated `src/CommandCenter.DecisionSessions/CommandCenter.DecisionSessions.csproj` or `refactor-plan.md` changes.
   - Commit and push to `origin/dev`.
   - Stop executing after the push.

## Evidence Targets

- `.agents/milestones/m1.2-generated-typescript-consumer-policy-slice-0070.md`
- `.agents/handoffs/handoff.md`
- `.agents/handoffs/handoff.0066.md`
- `.agents/decisions/decisions.0070.md`
- `.agents/decisions/decisions.md`
- `.agents/milestones/m0.4-active-governance-artifact-validation-slice-0053.md`
- `docs/contracts.md`
- `docs/architectural-capabilities.md`
- `docs/architectural-mechanisms.md`
- `.agents/milestones/m1.2-generated-contracts.md`

## Next Authorized Sequence

1. Re-run the focused governance verifier.
2. Stage only the completed M1.2 Slice 0070 policy work, handoff rotation/update, and decision rotation.
3. Commit and push to `origin/dev`.
4. Stop executing after the push.
