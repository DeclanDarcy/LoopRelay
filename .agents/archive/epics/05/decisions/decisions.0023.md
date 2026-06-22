# Decisions

## Newly Authorized

- The priority-adjustment implementation direction is accepted as preserving the key M5 authority boundary.
- Priority adjustments must remain refinement reasoning metadata, not proposal mutations.
- The authority model remains a certification-level invariant:
  - proposal is authority
  - revision is history
  - comparison is explanation
  - review is review state
  - decision is resolution authority
- Refinement evidence should remain separated into distinct classes:
  - structural changes such as option, assumption, and constraint changes
  - analytical changes such as tradeoff, evidence, and rationale expansion
  - preference changes such as priority adjustment
- Priority adjustment answers what became more important; it must not imply what became true or what changed structurally in the proposal.
- Revision records should remain self-describing so future consumers do not reconstruct intent from raw diffs.
- Proposal lineage projection should precede UI work.
- Proposal lineage should be treated as a foundational read model for M5 UI and likely later M6 resolution integration, M7 governance, and M10 operational adoption.
- Revision UI must preserve a hard boundary between current proposal and historical revision:
  - current proposal is authoritative, active, and current recommendation state
  - historical revision is read-only and explanatory
- M6 should consider an immutable resolution-time proposal snapshot so decision authority records what proposal was actually resolved without relying on the latest proposal lookup.

## Next Slice Direction

- Build proposal lineage projection before UI work.
- Then continue M5 read-only UI work:
  - revision history read models
  - revision comparison UI
  - revision history UI
- Defer refinement mutation UI until lineage and read-only history surfaces are in place.
