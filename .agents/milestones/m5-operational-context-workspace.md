# Milestone 5: Operational Context Workspace

## Tracking

- [ ] Milestone complete
- [x] Workstream 5.1: Current Understanding
- [x] Workstream 5.2: Proposed Revision
- [x] Workstream 5.3: Semantic Changes
- [x] Workstream 5.4: Compression Effects
- [x] Workstream 5.5: Decision Continuity
- [x] Workstream 5.6: Review Actions
- [ ] Workstream 5.7: Operational Context Cross-Links
- [ ] Certification complete

Goal: make operational context review a first-class workspace without changing backend lifecycle behavior.

## Workstream 5.1: Current Understanding

Display from `OperationalContextProjection`:

- [x] Current understanding summary.
- [x] Architecture.
- [x] Authority boundaries.
- [x] Constraints.
- [x] Stable decisions.
- [x] Decision rationale.
- [x] Open questions.
- [x] Active risks.
- [x] Recent understanding changes.
- [x] Continuity warnings.
- [x] Revision number, revision count, current path, last updated, and last promotion.

Rules:

- [x] Sections come from projection fields.
- [x] Do not parse Markdown client-side to reconstruct understanding.

## Workstream 5.2: Proposed Revision

Display from `OperationalContextProposal`:

- [x] Proposal id.
- [x] Status.
- [x] Generated at.
- [x] Generated content location.
- [x] Edited content location.
- [x] Review state.
- [x] Promotion state.
- [x] Current vs proposed understanding where practical.

Reviewers should be able to evaluate understanding changes without relying only on raw Markdown. Keep the raw Markdown editor available for edits.

## Workstream 5.3: Semantic Changes

Group `OperationalContextSemanticChange` records by semantic category:

- [x] Decision added/removed/warning.
- [x] Constraint added/removed.
- [x] Question added/resolved/removed.
- [x] Risk added/retired/removed.
- [x] Rationale changed/warning.
- [x] Section added/removed/changed.
- [x] Preservation warning.

Rules:

- [x] Show projected semantic changes.
- [x] Do not compute a new diff in React.

## Workstream 5.4: Compression Effects

Display from `OperationalContextCompressionSummary`:

- [x] Preserved item count.
- [x] Added item count.
- [x] Modified item count.
- [x] Removed item count.
- [x] Compressed item count.
- [x] Permanent/active/historical/noise item counts.
- [x] Resolved question count.
- [x] Retired risk count.
- [x] Warnings.
- [x] Revision summary.
- [x] Noise removed indicators.
- [x] Stable-understanding retention warnings.

Compression is review metadata. It must not block actions unless backend state already does.

## Workstream 5.5: Decision Continuity

Display:

- [x] Stable decisions.
- [x] Open decision signals where projected.
- [x] Decision rationale.
- [x] Decision warnings from semantic changes and compression warnings.
- [x] Missing rationale warnings where projected.

Do not turn this workspace into a decision archive viewer.

## Workstream 5.6: Review Actions

Expose existing actions with existing gating:

- [x] Generate proposal.
- [x] Load latest proposal.
- [x] Edit/save proposed content.
- [x] Accept.
- [x] Reject.
- [x] Promote accepted proposal.

Rules:

- [x] Backend state controls whether actions are enabled.
- [x] UI does not invent lifecycle transitions.
- [x] Refresh workspace projection after lifecycle mutations, preserving selected artifact and tab state.

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
