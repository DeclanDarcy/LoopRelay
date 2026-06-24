# Decisions

## Newly Authorized

- Accept the Milestone 3 proposal generation slice as architecturally consistent with the roadmap.
- Treat proposal generation UX as the last major architectural gap in proposal generation.
- Preserve proposal generation as backend-driven end to end:
  - backend-owned generation eligibility
  - authoritative generation command response
  - React rendering of returned backend facts
  - centralized decision projection refresh
- Continue exposing `generate_decision_proposal` through lifecycle eligibility rather than allowing React to infer generation availability.
- Continue rendering proposal generation result details from the returned `DecisionProposal` instead of proposal-browser summaries.
- Keep `refreshDecisions()` as the central post-mutation refresh boundary for decision context, candidates, proposals, and lifecycle eligibility.
- Proceed next with candidate duplicate-status rendering before proposal review transparency.
- Candidate duplicate rendering should use backend facts rather than deriving duplicate status in React.
- After duplicate rendering, prioritize proposal review transparency:
  - last transition
  - current review state
  - allowed transitions
  - blocked transitions
  - unavailable reasons
- Perform a proposal review-state placement audit to ensure there is one obvious place for current state, next actions, and unavailable-action reasons.
- Avoid independently rendering review state in multiple competing panels.
- Treat remaining Milestone 3 work as final polishing around established architecture:
  - duplicate candidate rendering
  - proposal review transparency
  - end-to-end lifecycle characterization
  - milestone exit audit
- Maintain the established authority pattern into later transparency milestones:
  - authoritative domain service
  - authoritative projection
  - transport
  - typed client
  - hooks
  - presentation
