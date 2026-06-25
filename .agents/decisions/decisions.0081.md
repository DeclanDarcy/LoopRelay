# Decisions

## Newly Authorized

- Treat Milestone 8 as complete.
- Stop Milestone 8 feature migration.
- Treat the Milestone 8 audit as having verified both coverage and architectural intent.
- Treat the shared explainability layer as spanning:
  - Workflow,
  - Governance,
  - Decisions,
  - Execution,
  - Reasoning,
  - Continuity / Operational Context.
- Treat the final Milestone 8 migration gaps as resolved because remaining audited bespoke explanation paths were routed through shared primitives without moving domain authority into React.
- Treat the Vite large-chunk warning as an optimization concern, not an architectural or correctness concern.
- Preserve the current semantic layering:
  - Domain Service,
  - Authoritative Projection,
  - Explainability Adapter,
  - Shared Presentation Components,
  - React UI.
- Continue to prohibit React from owning lifecycle decisions, eligibility, taxonomy, quality, burden, confidence, certification, execution outcomes, or continuity semantics.
- Start Milestone 9 as product architecture work rather than semantic architecture work.
- Make the first Milestone 9 slice an audit rather than implementation.
- Structure the first Milestone 9 audit around:
  - navigation,
  - workspace cohesion,
  - information density,
  - interaction consistency,
  - endpoint / projection cleanup.
- For navigation, audit one primary home per capability, contextual links, and duplicated entry points.
- For workspace cohesion, audit Workflow, Governance, Decisions, Execution, Reasoning, and Continuity for duplicated information, repeated panels, inconsistent interaction models, and inconsistent terminology.
- For information density, identify overly tall layouts, repeated evidence sections, related cards that should merge, and summaries that should collapse until expanded.
- Treat the execution history panel as an early likely information-density candidate.
- For interaction consistency, normalize review, approve, reject, recover, retry, promote, archive, and supersede patterns across workspaces.
- For endpoint / projection cleanup, identify obsolete bespoke components, compatibility adapters that can retire, duplicate projections, and classify remaining items as Keep, Compatibility, Internal, or Remove.
- Use Milestone 9 to reduce complexity without changing semantics.
