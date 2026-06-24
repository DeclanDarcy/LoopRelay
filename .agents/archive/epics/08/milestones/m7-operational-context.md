# Milestone 7: Operational Context Workflow Integration

Objective: make workflow continuity-aware by observing operational-context proposals, reviews, and promotions.

Deliver:

- [x] `WorkflowOperationalContextProjection` with proposal id, repository id, status, review state, promotion state, created timestamp, reviewed timestamp, promoted timestamp, reviewer, summary, source decision id, and source execution id.
- [x] `WorkflowOperationalContextStatus` with missing, proposed, under review, accepted, edited, rejected, ready for promotion, promoted, and archived.
- [x] `IWorkflowOperationalContextService`.
- [x] context review rules for proposal exists, accepted or edited, rejected, promoted, and no context required.
- [x] gate integration for operational context review and promotion.
- [x] `WorkflowOperationalContextDiagnostics`.
- [x] timeline events: operational context proposed, reviewed, accepted, edited, rejected, promoted, and archived.
- [x] recovery integration for proposal state, review state, promotion state, gate state, and timeline events.
- [x] decision-to-context linkage when evidence connects a resolved decision to an assimilation recommendation or context proposal.

Rules:

- [x] Continuity remains authoritative.
- [x] Workflow never accepts, edits, rejects, promotes, or mutates operational context.
- [x] No context proposal required is eligible for commit, but diagnostics must explain why no continuity update is required.

Tests:

- [x] proposed, accepted, edited, rejected, and promoted context projects correctly.
- [x] unreviewed proposal opens review gate.
- [x] accepted or edited unpromoted proposal opens promotion gate.
- [x] rejected proposal closes context gates and makes commit eligible.
- [x] promoted proposal closes context gates and makes commit eligible.
- [x] no-context-required state explains itself.
- [x] decision-to-context linkage works when evidence exists.
- [x] recovery rebuilds context workflow state.
- [x] workflow never mutates operational context.

Exit criteria:

- [x] operational context projection exists.
- [x] operational context service exists.
- [x] review and promotion gate integration works.
- [x] decision-to-context linkage works.
- [x] timeline integration exists.
- [x] recovery integration exists.
- [x] diagnostics exist.
