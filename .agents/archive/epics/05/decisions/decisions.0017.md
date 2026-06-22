# Decisions

## Newly Authorized

- The M4 read-only proposal viewer slice is accepted as aligned with the roadmap.
- The proposal viewer is confirmed as a projection surface, not a workflow or lifecycle-authority surface.
- M4 remains scoped to inspection, review context, evidence visibility, and source visibility.
- Mutation, resolution, lifecycle authority, operational-context coupling, and execution coupling remain out of scope for the current M4 inspection/navigation work.
- The ownership boundary remains accepted:
  - React may own selected proposal presentation state.
  - Backend decision services own proposal state, review state, lifecycle state, and action availability.
- Evidence and source attribution should remain adjacent to the proposal content they support.
- Candidate browser work is authorized as the next M4 implementation slice because proposal inspection needs upstream candidate context.
- Navigation validation is authorized before introducing mutation actions.
- Evidence/source navigation should be prioritized ahead of mutation controls.
- A dedicated option comparison surface is lower priority unless the embedded proposal viewer becomes difficult to scan.

## Current Milestone Status

- M0 Domain Foundation is complete.
- M1 Context Resolution is complete.
- M2 Discovery is complete.
- M3 Proposal Lifecycle is complete.
- M4 Review Workspace is approaching completion.

## Newly Authorized Next Slice

- Continue M4 with inspection/navigation work:
  1. Add the candidate browser with discovered, promoted, dismissed, expired, and duplicate filters.
  2. Add navigation tests for browser selection and review workspace loading patterns.
  3. Improve evidence/source navigation before adding review, refinement, resolution, discard, or other mutation controls.
  4. Keep option comparison as a lower-priority refinement unless the current embedded viewer proves insufficient.
