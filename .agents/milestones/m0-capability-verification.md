## Milestone 0: Capability Verification and Consolidation

### Objective

Create a single implementation baseline before exposing more product surface. This milestone should be short, engineering-oriented, and limited to the information needed to sequence implementation safely.

### Implementation

- [x] Build a capability inventory covering Core, Workflow, Decision Sessions, Decisions, Reasoning, Continuity, Execution, Middle, Backend, Shell, and UI.
- [x] For each capability record the minimum useful facts: name, owning project, authority, entry points, consumers, current reachability, and current completion.
- [x] Assign exactly one disposition: `Core MVP`, `Deferred`, `Infrastructure`, `Extension Point`, `Experimental`, or `Retire`.
- [x] Perform an authority review for workflow lifecycle, execution lifecycle, decision lifecycle, decision-session lifecycle, reasoning, operational context, certification, recovery, observability, repository summaries, health, and diagnostics.
- [x] Record only actionable plan adjustments: sequencing corrections, duplicate concepts to consolidate, routes or clients to add, and capabilities to defer or retire.
- [x] Freeze the MVP boundary so later milestones focus on integration and surfacing, not rediscovery.

### Deliverables

- [x] Capability inventory: `m0-capability-inventory.md`.
- [x] Capability disposition register: `m0-capability-disposition-register.md`.
- [x] Authority review: `m0-authority-review.md`.
- [x] MVP adjustment log: `m0-mvp-adjustment-log.md`.

### Exit Criteria

- [x] Every implemented capability is inventoried.
- [x] Every capability has one disposition.
- [x] Every semantic concept has exactly one authority.
- [x] Every duplicate or ambiguity discovered during the review has a concrete adjustment, deferral, or retirement decision.
- [x] Every Core MVP capability has an implementation path.
- [x] Milestone 0 produces enough engineering direction to start Milestone 1 without turning into a documentation project.
