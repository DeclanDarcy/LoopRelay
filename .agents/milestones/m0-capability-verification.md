## Milestone 0: Capability Verification and Consolidation

### Objective

Create a single implementation baseline before exposing more product surface. This milestone should be short, engineering-oriented, and limited to the information needed to sequence implementation safely.

### Implementation

- [ ] Build a capability inventory covering Core, Workflow, Decision Sessions, Decisions, Reasoning, Continuity, Execution, Middle, Backend, Shell, and UI.
- [ ] For each capability record the minimum useful facts: name, owning project, authority, entry points, consumers, current reachability, and current completion.
- [ ] Assign exactly one disposition: `Core MVP`, `Deferred`, `Infrastructure`, `Extension Point`, `Experimental`, or `Retire`.
- [ ] Perform an authority review for workflow lifecycle, execution lifecycle, decision lifecycle, decision-session lifecycle, reasoning, operational context, certification, recovery, observability, repository summaries, health, and diagnostics.
- [ ] Record only actionable plan adjustments: sequencing corrections, duplicate concepts to consolidate, routes or clients to add, and capabilities to defer or retire.
- [ ] Freeze the MVP boundary so later milestones focus on integration and surfacing, not rediscovery.

### Deliverables

- [ ] Capability inventory.
- [ ] Capability disposition register.
- [ ] Authority review.
- [ ] MVP adjustment log.

### Exit Criteria

- [ ] Every implemented capability is inventoried.
- [ ] Every capability has one disposition.
- [ ] Every semantic concept has exactly one authority.
- [ ] Every duplicate or ambiguity discovered during the review has a concrete adjustment, deferral, or retirement decision.
- [ ] Every Core MVP capability has an implementation path.
- [ ] Milestone 0 produces enough engineering direction to start Milestone 1 without turning into a documentation project.
