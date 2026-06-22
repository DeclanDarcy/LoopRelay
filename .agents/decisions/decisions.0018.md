# Decisions

## Newly Authorized

- The M4 candidate browser slice is accepted as complete.
- Candidate browser coverage of all candidate states is accepted as aligned with M4 inspection goals.
- Candidate filtering and selection remain React presentation state only.
- Proposal selection navigation characterization coverage is accepted.
- M4 remains read-only.
- Backend read models remain authoritative.
- No lifecycle authority should move into React.

## Current UI Ownership Boundary

React owns:

- selected candidate
- selected proposal
- active filters
- expanded panels

Backend owns:

- candidate state
- proposal state
- review state
- lifecycle transitions
- read-model authority

## Newly Authorized Next Slice

- Proceed with remaining M4 inspection surfaces:
  1. Add evidence/source attribution navigation.
  2. Keep evidence/source navigation read-only.
  3. Add tests proving evidence/source links render and select the expected source.
  4. Add an option comparison surface using the existing backend option-comparison read model.
  5. Do not recompute tradeoff summaries in React.
  6. Add characterization tests for option rows, tradeoffs, recommendation, and evidence adjacency.
- After these surfaces, M4 should be near closure or ready for a final read-only review-workspace polish pass.
