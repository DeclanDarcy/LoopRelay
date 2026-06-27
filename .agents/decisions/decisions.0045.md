# Decisions: 2026-06-26 M0.3 Regression Lifecycle Direction

These decisions capture only newly authorized direction from the user response following Slice 0042.

## Authorized Decisions

1. Accept the M0.3 architectural confidence model slice as correctly scoped.
   - Architectural confidence is evidence quality for a scoped architecture claim.
   - Confidence must remain tied to what can actually be proven.
   - High confidence for a narrow executable invariant is valid.
   - Broad inventory coverage may still be low confidence.
   - Pass percentage must not be treated as architectural confidence.

2. Proceed next with the M0.3 regression lifecycle model slice.
   - The lifecycle model is the next layer after the invariant catalog, taxonomy, ownership, severity, drift model, failure UX, and confidence model.
   - The model should govern how architectural regressions evolve over time.

3. Make lifecycle transitions explicit.
   - Primary progression should be: Inventory -> Advisory -> Guarded -> Corroborated -> Certified -> Accepted.
   - Exceptional or terminal transitions should include: Guarded -> Quarantined, Guarded -> Weakened, Guarded -> Replaced, and Guarded -> Retired.

4. Require evidence and an explicit decision path for regression weakening, retirement, or replacement.
   - A regression may weaken, retire, or be replaced only with evidence.
   - The decision path must be explicit before the transition is accepted.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0042 plus this decision checkpoint.
2. Stop executing after the push.
