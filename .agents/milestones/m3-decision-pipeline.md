## Milestone 3: Decision Pipeline Completion

### Objective

Make the existing decision lifecycle operational end to end from product entrypoints:

```text
Discovery -> Candidate -> Proposal -> Review -> Refinement -> Resolution -> Supersession -> Archive
```

### Backend and Shell

- [x] Inventory all decision lifecycle routes already mapped in `DecisionEndpoints.cs`.
- [ ] Add or expose any missing request/response models in TypeScript for:
   - [x] `DecisionDiscoveryResult`
   - [x] `DecisionProposalTransitionRequest`
   - [x] `CreateDecisionProposalCommand` or existing generation request type
   - [x] `SupersedeDecisionCommand`
   - [x] `ArchiveDecisionCommand`
   - [x] proposal generation diagnostics
- [ ] Add a backend read-only lifecycle eligibility projection over `DecisionLifecycleRules`:
   - [ ] current state
   - [ ] allowed next states
   - [ ] allowed actions
   - [ ] blocked actions
   - [ ] blocking reasons
   - [ ] required request fields
   - [ ] action command name
- [ ] Prefer a single route returning candidate, proposal, and decision eligibility for a repository:
   - [ ] `GET /api/repositories/{repositoryId}/decisions/lifecycle/eligibility`
- [ ] If a single route becomes too large, split by entity while keeping the rule evaluation in one backend service.
- [ ] Add shell commands for Core MVP lifecycle operations:
   - [x] `discover_decisions`
   - [x] `promote_decision_candidate`
   - [x] `dismiss_decision_candidate`
   - [x] `expire_decision_candidate`
   - [x] `mark_decision_candidate_duplicate`
   - [x] `generate_decision_proposal`
   - [x] `expire_decision_proposal`
   - [x] `discard_decision_proposal`
   - [x] `mark_decision_proposal_viewed`
   - [x] `mark_decision_proposal_needs_refinement`
   - [x] `mark_decision_proposal_ready_for_resolution`
   - [x] `supersede_decision`
   - [x] `archive_decision`
   - [ ] `get_decision_lifecycle_eligibility`
- [x] Shell commands must call backend endpoints and return backend domain responses directly.

### UI

- [x] Expand `src/CommandCenter.UI/src/api/decisions.ts` with typed functions for all Core MVP lifecycle operations.
- [x] Expand `useDecisionDiscovery`, `useDecisionProposals`, `useDecisionProposalReview`, and related hooks with action methods and refresh behavior.
- [ ] Update `DecisionCandidateBrowser` to show:
   - [ ] state
   - [ ] signals
   - [ ] evidence
   - [ ] duplicate status
   - [ ] allowed actions
   - [ ] unavailable action reasons
- [ ] Add candidate actions:
   - [x] discover
   - [x] promote
   - [x] dismiss
   - [x] expire
   - [x] mark duplicate
   - [x] generate proposal
- [ ] Proposal generation flow must refresh candidates, refresh proposals, navigate to the generated proposal where appropriate, and display generation diagnostics, generated proposal id, generation mode, accepted option count, rejected option count, deduplicated option count, and validation diagnostics.
- [ ] Update proposal viewer/review panels to render review state, allowed transitions, unavailable reasons, last transition, and transition controls.
- [ ] Add supersede and archive actions for resolved decisions, including target decision selection, rationale, resulting state, relationships, governance impact, and execution projection refresh.
- [ ] Classify lower-priority lifecycle features as Core MVP, Deferred, Internal, or Remove:
   - [ ] proposal review notes
   - [ ] proposal revision list
   - [ ] revision comparison
   - [ ] context snapshot listing
- [ ] Deferred features may remain reachable only if intentionally placed in an advanced or diagnostic view.

### Tests

- [ ] Backend tests for lifecycle eligibility projection.
- [ ] Endpoint tests for shell-reachable lifecycle routes.
- [ ] UI tests for candidate actions, proposal generation, proposal review transitions, supersede, archive, and refresh behavior.
- [ ] End-to-end test path:
  - [ ] discover candidate
  - [ ] promote candidate
  - [ ] generate proposal
  - [ ] mark viewed
  - [ ] mark needs refinement
  - [ ] refine proposal
  - [ ] mark ready for resolution
  - [ ] resolve decision
  - [ ] supersede decision
  - [ ] archive superseded decision

### Exit Criteria

- [ ] Every Core MVP decision lifecycle operation is reachable from the product.
- [ ] UI action availability comes from backend eligibility, not client guesses.
- [ ] Proposal generation feeds live review, refinement, and resolution panels.
- [ ] Supersede and archive update decision governance and execution influence projections.
- [ ] Deferred lifecycle endpoints have explicit dispositions.
