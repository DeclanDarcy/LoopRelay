# Milestone 7: Operational Context Workflow Integration

Objective: make workflow continuity-aware by observing operational-context proposals, reviews, and promotions.

Deliver:

- [ ] `WorkflowOperationalContextProjection` with proposal id, repository id, status, review state, promotion state, created timestamp, reviewed timestamp, promoted timestamp, reviewer, summary, source decision id, and source execution id.
- [ ] `WorkflowOperationalContextStatus` with missing, proposed, under review, accepted, edited, rejected, ready for promotion, promoted, and archived.
- [ ] `IWorkflowOperationalContextService`.
- [ ] context review rules for proposal exists, accepted or edited, rejected, promoted, and no context required.
- [ ] gate integration for operational context review and promotion.
- [ ] `WorkflowOperationalContextDiagnostics`.
- [ ] timeline events: operational context proposed, reviewed, accepted, edited, rejected, promoted, and archived.
- [ ] recovery integration for proposal state, review state, promotion state, gate state, and timeline events.
- [ ] decision-to-context linkage when evidence connects a resolved decision to an assimilation recommendation or context proposal.

Rules:

- [ ] Continuity remains authoritative.
- [ ] Workflow never accepts, edits, rejects, promotes, or mutates operational context.
- [ ] No context proposal required is eligible for commit, but diagnostics must explain why no continuity update is required.

Tests:

- [ ] proposed, accepted, edited, rejected, and promoted context projects correctly.
- [ ] unreviewed proposal opens review gate.
- [ ] accepted or edited unpromoted proposal opens promotion gate.
- [ ] rejected proposal closes context gates and makes commit eligible.
- [ ] promoted proposal closes context gates and makes commit eligible.
- [ ] no-context-required state explains itself.
- [ ] decision-to-context linkage works when evidence exists.
- [ ] recovery rebuilds context workflow state.
- [ ] workflow never mutates operational context.

Exit criteria:

- [ ] operational context projection exists.
- [ ] operational context service exists.
- [ ] review and promotion gate integration works.
- [ ] decision-to-context linkage works.
- [ ] timeline integration exists.
- [ ] recovery integration exists.
- [ ] diagnostics exist.
