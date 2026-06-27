# Decisions: 2026-06-26 M0.4 Unguarded Change Detection Direction

These decisions capture only newly authorized direction from the user response following M0.4 Referential Governance Validation Slice 0054.

## Authorized Decisions

1. Accept Slice 0054 as a correct M0.4 governance-strengthening step.
   - Governance should protect relationships between architectural claims and supporting evidence.
   - Capability and mechanism claims should require reachable evidence packages.
   - Active governance checkpoints should remain linked to relevant governance evidence.

2. Preserve the limited certification claim for Slice 0054.
   - The guard proves reachable governance references, active checkpoint linkage, and capability/mechanism traceability.
   - The guard does not prove decision correctness, evidence sufficiency, or complete historical graph validation.

3. Continue M0.4 with narrow unguarded architectural change detection.
   - Prioritize new authority/projection-like names before compatibility-field detection.
   - New authority/projection-like artifacts should not appear silently without corresponding governance.
   - The detector should remain heuristic, scoped, and explicitly limited.
   - Evidence target: `.agents/milestones/m0.4-authority-projection-watchlist-slice-0055.md`.

4. Follow authority/projection detection with compatibility-field governance.
   - New compatibility fields should require reachable governance before acceptance.
   - The guard should protect against long-lived architectural debt without judging whether the compatibility field is correct.

5. Require explicit false-positive boundaries for heuristic governance detectors.
   - Each detector should document detection scope.
   - Each detector should document exclusions.
   - Each detector should inventory accepted exceptions.
   - Each detector should state what it intentionally does not claim.

## Next Authorized Sequence

1. Stage Slice 0054 changes, handoff rotation, decision rotation, and this decision checkpoint.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
