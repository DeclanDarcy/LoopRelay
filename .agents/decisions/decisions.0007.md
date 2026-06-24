# Decisions

## Newly Authorized

- Proceed with Milestone 7 operational-context workflow integration.
- Preserve the core workflow boundary: Continuity owns operational-context
  review, edit, rejection, acceptance, and promotion authority; workflow only
  consumes and reports Continuity-owned state.
- Operational-context workflow projection must not derive review or promotion
  authority from artifact inspection. If Continuity says a proposal is
  accepted, workflow reports accepted; workflow must not independently infer
  accepted status.
- Model operational-context review and promotion as distinct authority gates:
  `OperationalContextReview` and `OperationalContextPromotion`.
- Accepted operational-context proposals must remain blocked on promotion until
  Continuity records promotion authority. Workflow must not treat acceptance as
  promotion.
- Represent "No Context Required" as a first-class eligible projection outcome,
  not as a missing or incomplete context proposal.
- M7 should project operational-context states such as proposed, under review,
  accepted, edited, rejected, ready for promotion, promoted, and no context
  required only from Continuity-owned evidence.
- Keep the M6 invariant active for M7: workflow remains explanation and
  coordination, not authority.

## Explicitly Deferred

- Do not start M7 implementation in this slice; this response triggers staging,
  commit, push, and stop.
