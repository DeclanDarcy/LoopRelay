# Milestone 6: Decision Resolution

## Goal

transform reviewed and refined proposals into authoritative project decisions through explicit human action.

## Backend Work

- [ ] Add `DecisionResolution`, `ResolveDecisionCommand`, `DecisionResolutionRationale`, `DecisionResolutionHistory`, and recommendation divergence tracking.
- [ ] Implement `IDecisionResolutionService`.
- [ ] Support accept, reject, and defer.
- [ ] Require resolver and rationale.
- [ ] Create or update authoritative `Decision` records only through resolution commands.
- [ ] Record selected option and whether it matched the recommendation.
- [ ] Persist outcome, timestamp, resolver, rationale, state transition, and source proposal.
- [ ] Update `decision.md` and `decisions.md` projections.
- [ ] Support supersede and archive actions with lineage validation.
- [ ] Expose an explicit command to create a decision assimilation recommendation package from a resolved decision and current continuity inputs.
- [ ] Do not decide that operational context must change; package evidence and rationale for the operational-context workflow to review.
- [ ] Do not write `.agents/operational_context.md` from decision resolution.

## UI Work

- [ ] Add resolution panel.
- [ ] Show proposal, refinement history, recommendation, selected outcome, selected option, and rationale before resolution.
- [ ] Require rationale input for resolution actions.
- [ ] Show recommendation override explicitly without treating it as an error.
- [ ] Show any generated operational-context assimilation recommendation as a separate reviewable item.

## Tests

- [ ] Accept/reject/defer tests.
- [ ] Rationale requirement tests.
- [ ] Resolver metadata tests.
- [ ] Recommendation divergence tests.
- [ ] State transition tests.
- [ ] Projection update tests.
- [ ] Supersede/archive tests.
- [ ] Assimilation recommendation tests proving operational context is not mutated and continuity policy is not owned by decision services.

## Exit Criteria

- [ ] Resolution is human-controlled and explicit.
- [ ] Authoritative decisions are repository-backed.
- [ ] Historical proposal, review, refinement, and resolution context remain recoverable.
- [ ] Resolved decisions can be packaged as operational-context assimilation recommendations without becoming operational context automatically.
