# Decisions

## Newly Authorized

- Treat the resolved decision supersede/archive `InteractionPatternView` slice as accepted Milestone 9 work because it extends the normalized interaction language into the resolved decision phase without broadening the abstraction prematurely.
- Continue expanding interaction normalization by action family rather than attempting a wholesale lifecycle refactor.
- Preserve the authority boundary:
  - backend owns lifecycle legality, eligibility, transition semantics, evidence, and diagnostics,
  - `InteractionPatternView` owns presentation of subject, expected result, eligibility, evidence, and diagnostics only.
- Keep characterization tests focused on normalized interaction language and presentation consistency rather than duplicating lifecycle-rule validation.
- Migrate candidate lifecycle actions together as the next slice:
  - promote,
  - dismiss,
  - expire,
  - duplicate,
  - generate proposal.
- Evaluate refinement and resolution separately after candidate lifecycle normalization.
- If refinement or resolution require additional concepts such as revision history, comparison, or consequence preview, introduce a small wrapper around `InteractionPatternView` rather than expanding the base component.
- Preserve the base interaction vocabulary as stable while allowing more complex lifecycle phases to compose additional presentation where needed.
