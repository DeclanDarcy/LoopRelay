# Decisions

## Newly Authorized

- The M5 UI transition is accepted as correct because React consumes backend lineage rather than reconstructing lineage from proposal, revision, and comparison artifacts.
- The proposal authority boundary should remain explicit in the UI:
  - current proposal is authoritative
  - revision history is historical
  - comparison models are explanatory
  - lineage is navigation
- M5 mutation UI should remain thin and request/response oriented.
- React must submit structured refinement requests to the backend and must not perform local proposal mutation.
- Backend services continue owning refinement validation, stale-base protection, revision creation, comparison generation, and lineage generation.
- After successful refinement, the UI should refresh proposal and lineage projections from the backend instead of locally patching proposal, revision, comparison, or lineage state.
- Mutation UI tests should cover form state, submission success/failure, stale-base rejection, proposal reload, lineage reload, comparison visibility, and preservation of authority boundaries.
- M6 preparation should preserve the M5 decomposition:
  - proposal is authority
  - revision is history
  - comparison is explanation
  - lineage is navigation
- M5 remaining work is mutation UI plus certification; backend domain, revision infrastructure, comparison models, lineage models, and read-only UI are considered complete.
