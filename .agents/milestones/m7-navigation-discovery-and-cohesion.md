# Milestone 7: Navigation, Discovery, and Cohesion

## Tracking

- [ ] Milestone complete
- [x] Workstream 7.1: Command Palette v2
- [x] Workstream 7.2: Cross-Workspace Link Hardening
- [ ] Workstream 7.3: Context Preservation
- [x] Workstream 7.4: Discovery Layer
- [ ] Workstream 7.5: Cohesion Audit
- [ ] Certification complete

Goal: make the application feel like one operational environment rather than separate screens.

## Workstream 7.1: Command Palette v2

Expand navigation targets:

- [x] Repositories.
- [x] Repository Workspace.
- [x] Repository Execution.
- [x] Repository Operational Context.
- [x] Repository Continuity.
- [x] Open questions.
- [x] Active risks.
- [x] Stable decisions.
- [x] Continuity warnings.
- [x] Milestones.
- [x] Execution sessions.
- [x] Inspector sections.

Rules:

- [x] Targets must be built from existing projections.
- [x] Selecting a target only changes navigation state and optional section anchor.
- [x] No workflow mutations.

## Workstream 7.2: Cross-Workspace Link Hardening

Audit and complete the links introduced during Workspace, Execution, Operational Context, and Continuity implementation:

- [x] Operational-context summary -> Operational Context tab.
- [x] Continuity warnings -> Continuity tab and warning section.
- [x] Execution summaries -> Execution tab.
- [x] Milestones -> Workspace milestone section or execution context selection.
- [x] Pending proposal -> Operational Context proposal section.
- [x] Context diagnostics -> Workspace execution context panel.
- [x] Decision/rationale warnings -> relevant Operational Context or Continuity section.
- [x] Report paths -> report/artifact surfaces where projected.

Rules:

- [x] Links navigate only and preserve selected repository context.
- [x] Links do not refresh, mutate, or trigger workflows.
- [x] Broken or unavailable link targets degrade to the nearest valid workspace section.

## Workstream 7.3: Context Preservation

Preserve:

- [x] Selected repository.
- [x] Active tab per repository where useful.
- [x] Selected artifact per repository.
- [x] Selected milestone per repository.
- [ ] Expanded sections.
- [x] Current palette query until close.

### Certification

- [x] Switching tabs does not wipe drafts unless the draft's owning object changes.
- [x] Switching repositories restores that repository's selected artifact and milestone when still valid.

## Workstream 7.4: Discovery Layer

Expose projection-derived shortcuts:

- [x] Pending proposal.
- [x] Current execution.
- [x] Awaiting handoff review.
- [x] Awaiting commit.
- [x] Awaiting push.
- [x] Continuity warnings.
- [x] Open questions.
- [x] Active risks.

Rules:

- [x] Discovery surfaces point to projected information.
- [x] They do not interpret text, score health, or compute product meaning.

## Workstream 7.5: Cohesion Audit

Audit:

- [ ] Status labels.
- [ ] Empty states.
- [ ] Loading states.
- [ ] Error states.
- [ ] Disabled capability states.
- [ ] Layout density.
- [ ] Keyboard behavior.
- [ ] Focus behavior.
- [ ] Responsive behavior.

### Certification

- [ ] Similar concepts behave similarly across Workspace, Execution, Operational Context, and Continuity.
