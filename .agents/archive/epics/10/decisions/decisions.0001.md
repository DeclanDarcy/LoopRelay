# Decisions

## Newly Authorized

- Milestone 0 is accepted as complete.
- Milestone 1 should begin by consuming the existing workflow backend endpoints rather than creating new workflow authority.
- `src/CommandCenter.UI/src/lib/executionWorkflow.ts` must not be deleted until authoritative workflow transport exists, all consumers are migrated, parity is verified, and the legacy derivation is no longer used.
- The first Milestone 1 slice should stay narrowly focused on workflow transport and foundational models/hooks: shell route exposure, TypeScript workflow models, workflow API client, and foundational workflow hooks.
- UI workflow migration and removal of the legacy execution-derived workflow should be a subsequent Milestone 1 slice.
- The frontend `decisionSessionSummary` repository projection gap should be fixed before Governance workspace integration to avoid temporary compatibility state.

## M1 Risk Guardrails

- Do not introduce UI-owned workflow authority.
- Do not create workflow-specific frontend models that diverge from backend projections.
- Prefer shared shell helper patterns for workflow route bridging instead of duplicating transport logic per command.
- Do not allow temporary derived workflow state to remain after authoritative workflow consumers are migrated.
