# Decisions: 2026-06-27 Governance-Link Certification Repair

These decisions capture only newly authorized direction from the user response pausing Phase 0 implementation until the governance-link failure is repaired.

## Evidence

- `.agents/milestones/m0.4-referential-governance-validation-slice-0054.md`

## Authorized Decisions

1. Pause Phase 0 implementation until the governance-link failure is repaired.
   - Do not proceed to stream/event primitives while the backend suite is blocked by governance reachability.
   - Treat the next work as certification repair only, not new architectural scope.

2. Repair `ArchitecturalDecisionGovernanceTests.ReferentialGovernanceClaimsRemainReachable`.
   - Restore active decision governance reachability by making `.agents/decisions/decisions.md` cite reachable M0.4 governance evidence.
   - The repair must preserve the existing governance mechanism rather than weakening or bypassing it.

3. Accept the Phase 0 agent process supervision slice subject to certification repair.
   - Supervision remains scoped to one process lifecycle.
   - Agents must not accumulate registry, routing, retry, repository, or independent lifecycle-stream semantics.
   - `IProcessRunner` compatibility remains intact.

4. Sequence the next implementation slice after certification repair.
   - After the full backend suite is green, continue with stream/event primitives projected from supervisor lifecycle facts.
   - Streams must not define independent lifecycle semantics.

## Next Authorized Sequence

1. Rerun the full backend suite.
2. Stage the supervision slice, governance repair, decision rotation, and handoff rotation.
3. Commit on `dev`.
4. Push to `origin/dev`.
5. Stop executing after the push.
