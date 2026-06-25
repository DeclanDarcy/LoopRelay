# Decisions

## Newly Authorized

- Treat the proposal review `InteractionPatternView` slice as accepted Milestone 9 work because it begins normalizing interaction language, not just consolidating information.
- Preserve `InteractionPatternView` as a presentation abstraction for recurring lifecycle interactions, with a normalized structure of:
  - action subject,
  - expected result,
  - eligibility,
  - supporting evidence,
  - diagnostics.
- Preserve the authority boundary:
  - backend owns lifecycle transitions, eligibility, and transition legality,
  - React owns consistent presentation of authoritative projections only.
- Keep characterization tests focused on normalized interaction presentation rather than duplicating lifecycle-rule validation.
- Continue applying `InteractionPatternView` to remaining decision lifecycle actions in this order:
  - resolved decision supersede and archive,
  - candidate promote, dismiss, expire, and generate proposal.
- Preserve the interaction contract as the pattern is reused:
  - subject is what the action applies to,
  - eligibility is backend-provided only,
  - evidence is backend-provided only,
  - diagnostics are backend-provided only,
  - result is backend-described expected outcome.
- `InteractionPatternView` must not infer blocked reasons, alternative actions, or lifecycle consequences.
- Continue Milestone 9 through three complementary streams:
  - navigation cohesion,
  - presentation cohesion,
  - interaction cohesion.
- After interaction normalization is consistently applied across major lifecycle workflows, continue with:
  - information-density refinement,
  - obsolete duplicate presentation removal,
  - final cohesion audit confirming one primary presentation per capability, one interaction language, consistent navigation, no duplicated semantic detail, and no erosion of backend authority.
