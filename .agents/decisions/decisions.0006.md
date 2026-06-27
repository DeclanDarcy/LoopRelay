# Decisions: 2026-06-27 Phase 0 Certification Repair Completion

These decisions capture only newly authorized direction for the current certification-repair slice.

## Evidence

- `.agents/milestones/m0.4-referential-governance-validation-slice-0054.md`

## Authorized Decisions

1. Treat the governance-link certification repair as complete after backend verification.
   - `ArchitecturalDecisionGovernanceTests.ReferentialGovernanceClaimsRemainReachable` remains protected by reachable M0.4 evidence.
   - The governance mechanism stays intact; no verifier weakening, bypass, or compatibility exception was introduced.

2. Resume Phase 0 implementation after committing and pushing this certification-repair state.
   - The next implementation target remains stream/event primitives in `CommandCenter.Agents`.
   - Those primitives must project supervisor lifecycle facts and must not define independent lifecycle semantics.

3. Preserve the current process-supervision authority boundaries.
   - `CommandCenter.Agents` owns role-agnostic process lifecycle primitives only.
   - Execution keeps operational semantics, provider contracts, Git, handoffs, prompts, and operational evidence.
   - `IProcessRunner` compatibility remains intact.

4. Stop executing after staging, committing, and pushing this slice.
   - Further Phase 0 feature implementation should happen in the next work slice.

## Next Authorized Sequence

1. Stage the certification-repair decision rotation and handoff rotation.
2. Commit on `dev`.
3. Push to `origin/dev`.
4. Stop executing after the push.
