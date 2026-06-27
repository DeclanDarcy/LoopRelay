# Decisions: 2026-06-26 M0.3 Confidence Model Direction

These decisions capture only newly authorized direction from the user response following Slice 0041.

## Authorized Decisions

1. Accept the M0.3 regression UX slice as correctly scoped.
   - Architectural regression failures should be treated as decision inputs, not generic test failures.
   - The regression framework should communicate the architectural decision path that follows from detected drift.
   - Structured failure output should connect invariant, drift detection, evidence, and governance.

2. Preserve detection confidence as a detector-specific concept.
   - Detection confidence describes the trustworthiness of the detection mechanism.
   - Detection confidence must not weaken the architectural severity of confirmed drift.
   - Low-confidence heuristic detection can still reveal high-severity architectural drift once confirmed.

3. Proceed next with the M0.3 architectural confidence model slice.
   - Confidence should stay separate from coverage, severity, and detection confidence.
   - Confidence should answer: how trustworthy is the evidence supporting this architectural claim?

4. Define architectural confidence as a function of mechanism quality rather than implementation quantity.
   - Confidence contributors should include mechanism strength, evidence quality, independent corroboration, freshness, and representative coverage breadth.
   - The number of regressions is not itself a confidence measure.
   - The model should avoid drifting toward percentage metrics.

5. Explicitly define what architectural confidence must not represent.
   - Confidence must not mean probability the architecture is correct.
   - Confidence must not mean implementation quality.
   - Confidence must not mean code coverage.
   - Confidence must not mean test pass percentage.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0041 plus this decision checkpoint.
2. Stop executing after the push.
