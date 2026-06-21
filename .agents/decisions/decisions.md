# Decisions

## Newly Authorized

- Treat `SectionHeader.headingLevel` as an appropriate M1 evolution because it keeps `SectionHeader` a flexible render-only presentation primitive.
- Avoid creating domain-specific design-system wrappers such as `ExecutionSectionHeader`, `OperationalContextSectionHeader`, or `ContinuitySectionHeader`.
- Continue using `EmptyState` adoption as a safe Workstream 1.5 target because empty states represent presentation and must not own workflow, navigation, readiness, mutation, or projection authority.
- Continue using the certification question for panel conversions: can this `Panel` be replaced with a `div` without changing behavior?
- Prioritize remaining Workstream 1.5 primitive adoption in this order: `Panel`, `SectionHeader`, `Metric`, then opportunistic `Table`, then careful `Button`.
- Adopt `Table` only where a current table structure already exists; do not invent new table abstractions to force adoption.
- Treat `Button` as the highest-risk primitive because buttons sit on authority boundaries.
- For every `Button` conversion, preserve `type`, `disabled`, `onClick`, and `children` exactly.
- Do not add workflow-oriented props to design primitives, including examples such as `workflow`, `readiness`, `status` carrying workflow meaning, `proposal`, or domain objects.
- Watch for primitive inflation and keep the design-system catalog boring, render-only, and free of domain convenience or workflow knowledge.
- Continue Milestone 1 as presentation modernization only, preserving the M0 authority model.
