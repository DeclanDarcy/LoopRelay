# Decisions: 2026-06-26 Workflow Certification and Cross-Family Repeatability

These decisions capture only newly authorized direction from the user response following Slice 0032.

## Authorized Decisions

1. Treat Slice 0032 as the point where the Oracle lifecycle has been locally certified across three independent contract families.
   - Repository dashboard, repository workspace, and primary workflow projection are the certified pilot families.
   - The strongest current claim is repeatability of the same Oracle lifecycle, not success of a single pilot.

2. Frame workflow certification as evidence of Oracle repeatability across a richer semantic contract family.
   - Workflow adds lifecycle, transition, diagnostic, gate, and compatibility-field complexity.
   - This does not mean Milestone 0.2 is complete.

3. Preserve the accepted workflow pilot gaps.
   - Missing dev mock workflow coverage remains a coverage limitation, not a certification blocker.
   - Missing populated `decisionSession` coverage remains a fixture variant limitation, not a certification blocker.

4. Treat serialized .NET verifier execution as a stable operational constraint.
   - Parallel `dotnet test` execution can cause build-output locks.
   - Continue serializing verifier execution and documenting the quarantine until output isolation is implemented and demonstrated.

5. Make the next slice cross-family repeatability evidence.
   - The evidence should prove that the same mechanism set was reused unchanged across dashboard, workspace, and workflow.
   - It should compare field inventory, field-role classification, Oracle fixture, consumer verification, artifact freshness, request-boundary verification, and local certification across the three families.

6. Do not automatically add a fourth contract family.
   - First ask which architectural property remains unvalidated by the first three certified pilots.
   - If no substantial property remains, prefer evidence synthesis over breadth-only coverage.

7. If another family is needed, prefer decision lifecycle eligibility over error envelope.
   - Decision lifecycle eligibility better exercises semantic authority, eligibility rules, compatibility-sensitive booleans, and downstream semantic drift risk.
   - Error envelope remains important, but primarily exercises failure representation rather than semantic authority.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0032 plus this decision checkpoint.
2. Stop executing after the push.
3. In the next work slice, produce cross-family Oracle repeatability evidence and use it to decide whether Milestone 0.2 needs another representative family before milestone-level certification.
