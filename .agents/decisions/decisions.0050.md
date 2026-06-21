# Decisions

## Newly Authorized

- Treat Workstream 1.4 as successful because status standardization did not centralize workflow meaning.
- Continue to define `src/lib/status.ts` as presentation-only ownership for labels, tones, and presentation metadata.
- Continue to prohibit `src/lib/status.ts` from owning readiness, promotion eligibility, acceptance logic, execution authority, or workflow decisions.
- Treat the current layering as `Workflow Authority -> Domain Status Values -> status.ts -> StatusBadge`.
- Proceed with Milestone 1 Workstream 1.5 as a render-only primitive adoption pass.
- Treat primitive adoption as a mechanical replacement exercise that preserves existing `onClick`, `disabled`, and visibility conditions exactly.
- Prioritize primitive adoption in this order where mappings are clean: `Panel`, `SectionHeader`, `EmptyState`, `Metric`, `Table`, then `Button`.
- Keep `Button` visual, interaction, and accessibility focused only; do not move workflow semantics, readiness rules, or permission checks into it.
- Certify Workstream 1.5 with the question: can every primitive be replaced with a `div`, `button`, or `table` without changing application behavior?
- Continue treating any proposal that moves workflow decisions into primitives as out of scope for Milestone 1.
