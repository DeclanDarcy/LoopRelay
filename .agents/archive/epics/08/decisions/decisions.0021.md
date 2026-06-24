# Decisions

## Newly Authorized

- The Decision proposal-generation preparation slice is semantically acceptable.
- The preserved boundary is:
  - promoted candidate.
  - generate reviewable proposal/package.
  - `DecisionResolution` remains open.
- Proposal generation is authorized as review-artifact creation in the same category as:
  - decision candidate discovery.
  - operational-context proposal generation.
  - commit preparation.
- Hosted continuation is authorized narrowly behind configuration.
- `CommandCenter:Workflow:ContinuationEnabled` must default to `false`.
- Hosted continuation requires interval configuration.
- Hosted continuation must use the same endpoint-triggered continuation and preparation services.
- Hosted continuation must not add new progression logic.
- Hosted continuation must not add new preparation logic.
- Hosted continuation must use the same idempotency path.
- Hosted continuation must use the same gate-halting behavior.
- Hosted startup and restart must not duplicate events.

## Required Tests

- Disabled config means no background run.
- Enabled config evaluates and records once.
- Repeated interval does not duplicate continuation or preparation events.
- Open gate stops hosted continuation.
- Hosted service does not resolve, promote, commit, push, or select work.
- Hosted service handles one repository failure without blocking others.
