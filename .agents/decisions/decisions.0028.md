# Decisions

## Newly Authorized

- Accept the Milestone 4 governance transparency slice as architecturally correct.
- Treat governance transparency as composition from existing authoritative sources rather than a new consolidated governance explanation authority.
- Preserve the governance composition chain as:
  - governance report
  - review workspace
  - lifecycle eligibility
  - resolved decision
- Keep `DecisionLifecycleTab` as the decision lifecycle presentation composition layer for selected review workspace, proposal eligibility, decision eligibility, and resolved decision response.
- Add a projection only when a semantic fact is missing, not when presentation becomes inconvenient.
- Keep recommendation divergence backend-owned; React must not infer divergence from selected option and recommendation.
- Proceed next with execution influence transparency using backend-owned reason categories.
- Structure execution influence rendering around authoritative backend categories: included, excluded, superseded, conflicted, ignored, and blocked.
- Characterize execution influence UI by verifying included, excluded, superseded, conflict, ignored, and blocked reasons render without frontend categorization.
- After committing and pushing the accepted governance slice, stop executing before continuing remaining Milestone 4 work.
