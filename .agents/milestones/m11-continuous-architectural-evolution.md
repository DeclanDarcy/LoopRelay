# Phase 11 - Governance and Documentation

Goal: align architecture documentation, governance evidence, and rollback paths with the implemented design.

## Implementation

- [ ] Update architecture documentation for:
  - [ ] `CommandCenter.Agents` as shared role-agnostic process runtime;
  - [ ] Operational vs Decision session roles;
  - [ ] generated prompt authority;
  - [ ] repository-scoped orchestrator ownership;
  - [ ] plan authoring lifecycle;
  - [ ] handoff/decision artifact rotation;
  - [ ] router reuse/transfer behavior.
- [ ] Update contract documentation for all new endpoints, stream events, and structured errors.
- [ ] Update prompt architecture documentation for the 11 canonical prompts and generated signatures.
- [ ] Record governance evidence for the intentional divergence from current `HandoffService` behavior and `AwaitingAcceptance`.
- [ ] Record rollback paths:
  - [ ] disable Plan Authoring screen;
  - [ ] disable persistent planning;
  - [ ] disable Decision reuse and force transfer-only;
  - [ ] disable automatic commit/push;
  - [ ] return to existing execution/session endpoints.
- [ ] Document compatibility impact for existing execution sessions, generated TypeScript artifacts, UI hooks, and tests.
- [ ] Add or update architectural mechanism docs for prompt provenance and no-literal-prompt enforcement.

## Certification

- [ ] Documentation matches implemented behavior.
- [ ] Every architecture-affecting change has invariant, owner, evidence, compatibility impact, and rollback path.
- [ ] Governance tests protect the new boundaries.
- [ ] Known fallback behavior is explicit and does not masquerade as the full design.
