# Decisions: 2026-06-26 M0.3 Failure UX Direction

These decisions capture only newly authorized direction from the user response following Slice 0040.

## Authorized Decisions

1. Accept the M0.3 architectural drift model slice as correctly scoped.
   - The regression framework should model how architecture fails, not only catalog invariants.
   - Drift classes are architectural failure modes rather than generic implementation bug labels.
   - The drift taxonomy should stay focused on architectural failures such as duplicate authority, semantic leakage, transport responsibility growth, projection impurity, and dependency cycles.

2. Preserve the distinction between detection and evidence.
   - Detection identifies how drift is found.
   - Evidence supports the architectural conclusion produced by the regression.
   - Regressions should not collapse into unstructured "test failed" output.

3. Treat enforcement strength as separate from architectural importance.
   - Release-blocker drift can begin as inventory without changing its architectural severity.
   - Invariants may mature from inventory to documentation, executable regression, and release blocker enforcement.
   - Enforcement strength, severity, and architectural importance must remain distinct concepts.

4. Proceed next with the M0.3 failure UX slice.
   - The framework should define how regressions communicate architectural failure.
   - Failure UX should be consistent across fixture comparison, consumer verification, freshness checks, dependency scans, reflection tests, and source analysis.

5. Model architectural regression failure messages as structured output.
   - Required conceptual fields should include invariant, intent, observed drift, owner, severity, expected evidence, suggested remediation, and escalation.
   - Failure messages should communicate the architectural rule, why it exists, what was detected, what evidence should be reviewed, who owns remediation, and which process applies if unresolved.

6. Introduce detection confidence as a separate metadata concept.
   - Confidence describes confidence in the detection mechanism, not confidence in the architectural decision.
   - Reflection and fixture comparison may be high confidence, source scans may be medium confidence, and documentation inventory may be low confidence.
   - Confidence should complement enforcement strength without replacing severity, ownership, or evidence.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0040 plus this decision checkpoint.
2. Stop executing after the push.
