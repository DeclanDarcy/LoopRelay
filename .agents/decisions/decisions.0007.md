# Decisions

## Newly Authorized

- Accept the Milestone 3A lifecycle policy implementation as architecturally aligned.
- Treat Milestone 3A policy as complete.
- Treat Milestone 3B transfer eligibility as ready to begin in a future slice.
- Transfer eligibility must be modeled as operational feasibility, not as an alternative policy engine.
- Eligibility should answer whether a policy-directed transfer can safely happen right now.
- Eligibility must not answer whether transfer is a good idea, whether transfer score is too low, or whether reuse should win; those remain policy responsibilities.
- Keep the roadmap's four transfer eligibility statuses: `NotApplicable`, `Eligible`, `Blocked`, and `Deferred`.
- Avoid adding more eligibility statuses unless implementation reality reveals a genuine need.
- Add a policy/eligibility separation test before transfer execution exists: when policy says `Transfer` and eligibility is `Blocked`, policy must remain `Transfer`.
- Eligibility may prevent execution, but it must never rewrite policy.
