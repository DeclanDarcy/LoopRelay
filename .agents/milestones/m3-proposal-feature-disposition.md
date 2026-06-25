# Milestone 3 Proposal Feature Disposition

## Scope

This audit covers the remaining lower-priority proposal lifecycle features called out by Milestone 3:

- proposal review notes
- proposal revision list
- revision comparison
- context snapshot listing
- Proposal Actions panel placement

## Current Placement

- `DecisionProposalViewer` renders the active backend `DecisionReviewWorkspace`, including proposal content, review status, review notes, compact revision metadata, diagnostics, and lifecycle eligibility passed from `DecisionLifecycleTab`.
- `DecisionRevisionHistory` renders the richer backend `DecisionProposalLineage`, including current proposal authority, lineage events, revision list, backend revision comparisons, and source attribution.
- `DecisionLifecycleTab` keeps mutation controls in a separate Proposal Actions panel. The panel consumes backend lifecycle eligibility and invokes backend-owned lifecycle commands through hooks.

## Dispositions

| Feature | Disposition | Rationale |
| --- | --- | --- |
| Proposal review notes | Deferred for creation/editing UI; retain read-only display where already projected | Backend note persistence and endpoints exist, and notes participate in lineage/certification evidence. Milestone 3 does not require a note-authoring workflow to complete the lifecycle path. |
| Proposal revision list | Diagnostic | Revisions explain proposal evolution and preserve authority lineage, but the current proposal remains authoritative. The list belongs in lineage/diagnostic inspection, not primary lifecycle action flow. |
| Revision comparison | Diagnostic | Backend comparison output is useful for refinement provenance and authority audits, but it is not required to advance proposal lifecycle state. Keep it read-only and backend-projected. |
| Context snapshot listing | Internal for Milestone 3 | Proposal review already renders the current decision context and evidence needed for lifecycle review. A separate context snapshot browser would duplicate operational-context authority and should wait for Milestone 7 if needed. |
| Proposal Actions panel | Retain for Milestone 3 | The panel owns lifecycle mutations and eligibility controls. The viewer owns semantic facts. There is duplicated eligibility text, but not duplicated authority; consolidation can be evaluated during product cohesion. |

## Follow-Up

- Keep all retained revision and note surfaces read-only unless a later milestone explicitly adds authoring commands to the shell and TypeScript client.
- Treat any future context snapshot browser as a Milestone 7 continuity/operational-context concern, not a Milestone 3 decision lifecycle requirement.
- Revisit Proposal Actions panel consolidation in Milestone 9 if product cohesion work finds the current split creates user confusion.
