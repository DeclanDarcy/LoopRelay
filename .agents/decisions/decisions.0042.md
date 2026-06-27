# Decisions: 2026-06-26 M0.3 Drift Model Direction

These decisions capture only newly authorized direction from the user response following Slice 0039.

## Authorized Decisions

1. Accept the M0.3 ownership and severity slice as correctly scoped.
   - Regression category determines the protection mechanism.
   - Ownership determines accountability and escalation.
   - Category, ownership, severity, and escalation should remain orthogonal framework concerns.

2. Preserve the separation between architectural impact and execution behavior.
   - Severity describes architectural importance and risk.
   - Local, CI, and release behavior describe current enforcement policy.
   - A critical invariant may temporarily begin as documentation or inventory protection without redefining its architectural impact.

3. Treat explicit escalation rules as required regression governance metadata.
   - Regressions should identify what failed, who owns resolution, how serious the failure is, and which process should be followed.
   - Escalation metadata is part of the architectural governance system, not merely test documentation.

4. Proceed next with the M0.3 architectural drift model slice.
   - Drift classes should include new authorities, duplicate authorities, transport responsibility growth, projection impurity, contract replication, state duplication, composition growth, dependency cycles, and semantic leakage.
   - These should be modeled as architectural drift classes rather than ordinary implementation defects.

5. Model each drift class with separate detection and evidence dimensions.
   - Detection should identify how drift is found, such as reflection, fixture comparison, source scan, consumer verification, or runtime observation.
   - Evidence should identify what architectural proof the mechanism produces, such as contract diff, dependency graph, ownership matrix, consumer report, or drift report.
   - The drift model should make clear that a regression is a mechanism that produces evidence supporting an architectural conclusion.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0039 plus this decision checkpoint.
2. Stop executing after the push.
