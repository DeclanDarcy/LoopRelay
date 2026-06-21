# Milestone 5: Operational Context Workspace

## Tracking

- [ ] Milestone complete
- [ ] Workstream 5.1: Current Understanding
- [ ] Workstream 5.2: Proposed Revision
- [ ] Workstream 5.3: Semantic Changes
- [ ] Workstream 5.4: Compression Effects
- [ ] Workstream 5.5: Decision Continuity
- [ ] Workstream 5.6: Review Actions
- [ ] Workstream 5.7: Operational Context Cross-Links
- [ ] Certification complete

Goal: make operational context review a first-class workspace without changing backend lifecycle behavior.

## Workstream 5.1: Current Understanding

Display from `OperationalContextProjection`:

- [ ] Current understanding summary.
- [ ] Architecture.
- [ ] Authority boundaries.
- [ ] Constraints.
- [ ] Stable decisions.
- [ ] Decision rationale.
- [ ] Open questions.
- [ ] Active risks.
- [ ] Recent understanding changes.
- [ ] Continuity warnings.
- [ ] Revision number, revision count, current path, last updated, and last promotion.

Rules:

- [ ] Sections come from projection fields.
- [ ] Do not parse Markdown client-side to reconstruct understanding.

## Workstream 5.2: Proposed Revision

Display from `OperationalContextProposal`:

- [ ] Proposal id.
- [ ] Status.
- [ ] Generated at.
- [ ] Generated content location.
- [ ] Edited content location.
- [ ] Review state.
- [ ] Promotion state.
- [ ] Current vs proposed understanding where practical.

Reviewers should be able to evaluate understanding changes without relying only on raw Markdown. Keep the raw Markdown editor available for edits.

## Workstream 5.3: Semantic Changes

Group `OperationalContextSemanticChange` records by semantic category:

- [ ] Decision added/removed/warning.
- [ ] Constraint added/removed.
- [ ] Question added/resolved/removed.
- [ ] Risk added/retired/removed.
- [ ] Rationale changed/warning.
- [ ] Section added/removed/changed.
- [ ] Preservation warning.

Rules:

- [ ] Show projected semantic changes.
- [ ] Do not compute a new diff in React.

## Workstream 5.4: Compression Effects

Display from `OperationalContextCompressionSummary`:

- [ ] Preserved item count.
- [ ] Added item count.
- [ ] Modified item count.
- [ ] Removed item count.
- [ ] Compressed item count.
- [ ] Permanent/active/historical/noise item counts.
- [ ] Resolved question count.
- [ ] Retired risk count.
- [ ] Warnings.
- [ ] Revision summary.
- [ ] Noise removed indicators.
- [ ] Stable-understanding retention warnings.

Compression is review metadata. It must not block actions unless backend state already does.

## Workstream 5.5: Decision Continuity

Display:

- [ ] Stable decisions.
- [ ] Open decision signals where projected.
- [ ] Decision rationale.
- [ ] Decision warnings from semantic changes and compression warnings.
- [ ] Missing rationale warnings where projected.

Do not turn this workspace into a decision archive viewer.

## Workstream 5.6: Review Actions

Expose existing actions with existing gating:

- [ ] Generate proposal.
- [ ] Load latest proposal.
- [ ] Edit/save proposed content.
- [ ] Accept.
- [ ] Reject.
- [ ] Promote accepted proposal.

Rules:

- [ ] Backend state controls whether actions are enabled.
- [ ] UI does not invent lifecycle transitions.
- [ ] Refresh workspace projection after lifecycle mutations, preserving selected artifact and tab state.

## Workstream 5.7: Operational Context Cross-Links

Add links introduced by the Operational Context workspace:

- [ ] Open questions and active risks navigate to the same section anchors from palette/discovery targets.
- [ ] Continuity warnings navigate to the Continuity tab and warning section.
- [ ] Compression warnings navigate to the Continuity tab and compression section.
- [ ] Decision continuity warnings navigate to the continuity decision-retention section when available.
- [ ] Proposal source paths navigate to artifact content surfaces when the artifact exists.
- [ ] Promotion archive paths navigate to artifact surfaces when the artifact exists.

Rules:

- [ ] Links navigate only.
- [ ] Links do not generate, edit, accept, reject, or promote proposals.
- [ ] Operational context remains current understanding, not a decision archive or execution history browser.

### Certification

- [ ] Proposal lifecycle behavior matches existing behavior.
- [ ] Current understanding remains distinct from proposal, decisions history, execution history, and raw session memory.
- [ ] Operational Context links do not mutate lifecycle state.
