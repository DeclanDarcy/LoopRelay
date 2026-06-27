# Phase 8 - Contracts, Artifacts, and Provenance

Goal: harden the observable contracts and artifact semantics needed by the design. This is not a Repository Knowledge or adaptive intelligence milestone.

## Implementation

- [ ] Add or update backend contract identities for:
  - [ ] plan status;
  - [ ] plan write/revise/execute commands;
  - [ ] planning stream events;
  - [ ] execution stream events used by this flow;
  - [ ] decision stream events;
  - [ ] decision submit;
  - [ ] repository lifecycle state;
  - [ ] prompt provenance records.
- [ ] Generate or update TypeScript consumer types for the new contracts.
- [ ] Add request-boundary tests for command payloads and structured errors.
- [ ] Add stream contract tests for ordering, reconnect/replay behavior where supported, terminal events, and failure events.
- [ ] Add artifact protocol tests for:
  - [ ] `.agents/specs/roadmap.md`;
  - [ ] `.agents/specs/s{n}.md`;
  - [ ] `.agents/plan.md`;
  - [ ] `.agents/operational_context.md`;
  - [ ] `.agents/handoffs/handoff.000N.md`;
  - [ ] `.agents/decisions/decisions.000N.md`;
  - [ ] `.agents/operational_delta.md`.
- [ ] Ensure every generated-prompt turn records prompt name, generated type, `SourceHash`, role, workflow phase, input artifact identities, and output artifact identities.
- [ ] Keep decision output free text for the first implementation unless a canonical structured decision `.prompt` and contract are explicitly added.
- [ ] Do not add knowledge graph, intelligence, query, or recommendation contracts in this phase.

## Certification

- [ ] Contract oracle, consumer verification, generated artifact freshness, generated pipeline, and request-boundary tests pass for touched contract families.
- [ ] Artifact writes are durable and recoverable.
- [ ] Prompt provenance is attached to planning, execution, decision, and transfer turns.
- [ ] No UI type redefines backend-owned contract shapes.
