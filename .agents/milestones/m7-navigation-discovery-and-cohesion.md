# Milestone 7: Navigation, Discovery, and Cohesion

## Tracking

- [ ] Milestone complete
- [ ] Workstream 7.1: Command Palette v2
- [ ] Workstream 7.2: Cross-Workspace Link Hardening
- [ ] Workstream 7.3: Context Preservation
- [ ] Workstream 7.4: Discovery Layer
- [ ] Workstream 7.5: Cohesion Audit
- [ ] Certification complete

Goal: make the application feel like one operational environment rather than separate screens.

## Workstream 7.1: Command Palette v2

Expand navigation targets:

- [ ] Repositories.
- [ ] Repository Workspace.
- [ ] Repository Execution.
- [ ] Repository Operational Context.
- [ ] Repository Continuity.
- [ ] Open questions.
- [ ] Active risks.
- [ ] Stable decisions.
- [ ] Continuity warnings.
- [ ] Milestones.
- [ ] Execution sessions.
- [ ] Inspector sections.

Rules:

- [ ] Targets must be built from existing projections.
- [ ] Selecting a target only changes navigation state and optional section anchor.
- [ ] No workflow mutations.

## Workstream 7.2: Cross-Workspace Link Hardening

Audit and complete the links introduced during Workspace, Execution, Operational Context, and Continuity implementation:

- [ ] Operational-context summary -> Operational Context tab.
- [ ] Continuity warnings -> Continuity tab and warning section.
- [ ] Execution summaries -> Execution tab.
- [ ] Milestones -> Workspace milestone section or execution context selection.
- [ ] Pending proposal -> Operational Context proposal section.
- [ ] Context diagnostics -> Workspace execution context panel.
- [ ] Decision/rationale warnings -> relevant Operational Context or Continuity section.
- [ ] Report paths -> report/artifact surfaces where projected.

Rules:

- [ ] Links navigate only and preserve selected repository context.
- [ ] Links do not refresh, mutate, or trigger workflows.
- [ ] Broken or unavailable link targets degrade to the nearest valid workspace section.

## Workstream 7.3: Context Preservation

Preserve:

- [ ] Selected repository.
- [ ] Active tab per repository where useful.
- [ ] Selected artifact per repository.
- [ ] Selected milestone per repository.
- [ ] Expanded sections.
- [ ] Current palette query until close.

### Certification

- [ ] Switching tabs does not wipe drafts unless the draft's owning object changes.
- [ ] Switching repositories restores that repository's selected artifact and milestone when still valid.

## Workstream 7.4: Discovery Layer

Expose projection-derived shortcuts:

- [ ] Pending proposal.
- [ ] Current execution.
- [ ] Awaiting handoff review.
- [ ] Awaiting commit.
- [ ] Awaiting push.
- [ ] Continuity warnings.
- [ ] Open questions.
- [ ] Active risks.

Rules:

- [ ] Discovery surfaces point to projected information.
- [ ] They do not interpret text, score health, or compute product meaning.

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
