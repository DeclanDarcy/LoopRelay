# Decisions: 2026-06-26 M0.3 Regression Architecture Specification Direction

These decisions capture only newly authorized direction from the user response following Slice 0043.

## Authorized Decisions

1. Accept the M0.3 regression lifecycle model slice as correctly scoped.
   - Lifecycle expresses governance maturity.
   - Lifecycle is independent of severity and architectural confidence.
   - A regression may be critical in severity, low in confidence, and still inventory-stage in lifecycle.

2. Keep lifecycle transitions governed.
   - Guarded-or-stronger regressions require explicit decision and evidence before weakening, replacement, retirement, or quarantine.
   - Lifecycle transitions are architectural governance events, not simple implementation edits.

3. Keep accepted baseline protection conditional on explicit revalidation triggers.
   - Accepted baseline protection must not become permanent by default.
   - Revalidation triggers include architecture changes, mechanism replacement, contract evolution, and framework evolution.

4. Proceed next with the M0.3 regression architecture specification slice.
   - The specification should synthesize the invariant catalog, taxonomy, ownership, severity, drift model, failure UX, confidence model, and lifecycle model.
   - The specification should become the architectural reference for expanding executable regression coverage.

5. Distinguish framework metadata from framework implementations in the regression architecture specification.
   - Framework metadata includes taxonomy, ownership, lifecycle, confidence, severity, and escalation.
   - Framework implementations include fixture comparisons, consumer verification, freshness verification, source scans, reflection, and runtime integration.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0043 plus this decision checkpoint.
2. Stop executing after the push.
