# Decisions

## Newly Authorized

- Proceed with Milestone 6 decision workflow integration.
- Preserve the core workflow boundary: workflow explains authority; it does not
  reconstruct or replace authority.
- Decision workflow progression eligibility must be based on authoritative
  decision resolution state, not recommendation output, governance
  recommendations, quality recommendations, or certification recommendations.
- Treat decision recommendations, governance findings, quality analysis, and
  certification output as observability and diagnostics unless the Decisions
  domain has converted them into authoritative state.
- Model the decision-domain boundary as:
  recommendation equals observability; resolution equals authority.
- Governance-blocked decision states may be projected by workflow only when the
  Decisions domain determines the blocked status.
- Workflow may report and explain governance-blocked states, but must not
  independently calculate governance health or duplicate decision governance
  logic.
- Supersession handling in M6 must follow decision authority lineage.
- When a decision is superseded by another decision, workflow should project
  according to the successor decision's authoritative state rather than anchoring
  progression to obsolete evidence.
- Continue preserving the central invariant: workflow remains observer,
  coordinator, explainer, and recovery layer, not scheduler, lifecycle owner,
  approval engine, or authority source.

## Explicitly Deferred

- Do not start M6 implementation in this slice; this response triggers staging,
  commit, push, and stop.
