# Decisions

## Newly Authorized

- Treat proposal-resolution inferred capture as architecturally correct when modeled as `Evidence` / `EvidenceAdded` rather than a reasoning-owned proposal resolution authority.
- Treat the `DerivesFrom` relationship from decision to proposal as the correct explanatory relationship for proposal-resolution capture.
- Preserve the post-authoritative-transition boundary for proposal resolution: successful resolution may append reasoning; failed resolution must append no reasoning.
- Anchor inferred-capture idempotency to source transition identity rather than reasoning narrative text.
- Consider the supersession and proposal-resolution capture slices sufficient architectural proof for the core Milestone 2 inferred-capture pattern.
- Proceed next with decision archival capture as the next inferred-capture slice.
- Avoid adding event types such as `DecisionArchived`, `DecisionRetired`, or `DecisionClosed` if they would mirror the decision lifecycle inside reasoning.
- Evaluate archival capture through the smallest explanatory vocabulary that fits, such as `DecisionEvolution`, `Evidence`, or `ConstraintEvolution`, depending on what rationale is preserved.
- Treat taxonomy drift as the primary emerging risk for upcoming Milestone 2 slices.
- Prefer a small reasoning vocabulary with rich provenance and authoritative references over adding one reasoning event type for every source-domain state transition.
