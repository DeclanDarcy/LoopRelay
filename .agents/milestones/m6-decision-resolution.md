# Milestone 6: Decision Resolution

## Goal

transform reviewed and refined proposals into authoritative project decisions through explicit human action.

## Backend Work

- [ ] Add `DecisionResolution`, `ResolveDecisionCommand`, `DecisionResolutionRationale`, `DecisionResolutionHistory`, and recommendation divergence tracking.
- [x] Implement `IDecisionResolutionService`.
- [x] Support accept, reject, and defer.
- [x] Require resolver and rationale.
- [ ] Create or update authoritative `Decision` records only through resolution commands.
- [x] Record selected option and whether it matched the recommendation.
- [x] Persist outcome, timestamp, resolver, rationale, state transition, and source proposal.
- [x] Update `decision.md` and `decisions.md` projections.
- [x] Support supersede and archive actions with lineage validation.
- [x] Expose an explicit command to create a decision assimilation recommendation package from a resolved decision and current continuity inputs.
- [x] Do not decide that operational context must change; package evidence and rationale for the operational-context workflow to review.
- [x] Do not write `.agents/operational_context.md` from decision resolution.

## UI Work

- [ ] Add resolution panel.
- [ ] Show proposal, refinement history, recommendation, selected outcome, selected option, and rationale before resolution.
- [ ] Require rationale input for resolution actions.
- [ ] Show recommendation override explicitly without treating it as an error.
- [ ] Show any generated operational-context assimilation recommendation as a separate reviewable item.

## Tests

- [x] Accept/reject/defer tests.
- [x] Rationale requirement tests.
- [x] Resolver metadata tests.
- [x] Recommendation divergence tests.
- [x] State transition tests.
- [x] Projection update tests.
- [x] Supersede/archive tests.
- [x] Assimilation recommendation tests proving operational context is not mutated and continuity policy is not owned by decision services.

## Exit Criteria

- [ ] Resolution is human-controlled and explicit.
- [ ] Authoritative decisions are repository-backed.
- [ ] Historical proposal, review, refinement, and resolution context remain recoverable.
- [ ] Resolved decisions can be packaged as operational-context assimilation recommendations without becoming operational context automatically.

## Progress Notes

- Resolution now captures an immutable source proposal snapshot inside `DecisionResolution`, including the pre-resolution proposal fingerprint, proposal state, proposal content, history, and proposal revisions.
- Resolution now runs through `IDecisionResolutionService` and `DecisionResolutionService`; generation no longer owns proposal resolution commands and the resolve endpoint uses the resolution service boundary.
- Resolution outcomes now have one backend interpretation through `DecisionLifecycleRules`: accepted decisions become `Resolved`, rejected decisions become `Archived`, and deferred decisions become `UnderReview`, while the source proposal becomes `Resolved` for all explicit resolution outcomes.
- Assimilation recommendation packages are now generated through `IDecisionOperationalContextAssimilationService` and persisted under `.agents/decisions/assimilation/{DEC-id}` with source decision and decision context snapshot lineage.
- Assimilation recommendation generation is advisory only: it creates `recommendation.json`, `recommendation.md`, and a context snapshot, but does not mutate `.agents/operational_context.md` or own continuity merge, review, acceptance, or promotion policy.
