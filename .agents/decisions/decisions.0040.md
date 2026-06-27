# Decisions: 2026-06-26 M0.3 Regression Taxonomy Direction

These decisions capture only newly authorized direction from the user response following Slice 0037.

## Authorized Decisions

1. Accept the M0.3 invariant catalog slice as correctly scoped.
   - The catalog should remain a first-class architectural artifact.
   - The catalog is part of the enforcement pipeline, not passive documentation.
   - Protecting the catalog with executable regression is an accepted architectural strengthening step.

2. Keep `Enforcement strength` as an honest maturity signal.
   - Invariants may be explicitly classified as documentation, inventory, executable regression, or stronger protection.
   - Weak or early-stage protection should not be overstated.
   - Later milestones may strengthen enforcement without redefining the invariant.

3. Treat the next M0.3 slice as the regression taxonomy slice.
   - The taxonomy should classify regression mechanisms rather than merely list them.
   - Initial categories should include structural, contract, consumer, freshness, transport, runtime, and documentation/metadata validation.
   - Later invariants should use this vocabulary when selecting enforcement mechanisms.

4. Add `Preferred execution phase` to the taxonomy.
   - The taxonomy should state where a mechanism normally belongs, such as unit test, UI characterization, integration, or E2E.
   - This metadata should keep expensive regressions out of fast verification layers unless explicitly justified.
   - It should make expected verifier placement clear for later slices.

5. Distinguish `preferred mechanism` from `minimum acceptable mechanism`.
   - Some invariants may temporarily be protected by documentation or inventory before stronger executable mechanisms exist.
   - The taxonomy should make temporary weaker protection explicit instead of implying full enforcement.
   - Future strengthening can move an invariant from minimum acceptable protection toward preferred protection.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0037 plus this decision checkpoint.
2. Stop executing after the push.
