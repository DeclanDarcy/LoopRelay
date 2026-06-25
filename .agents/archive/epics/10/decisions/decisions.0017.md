# Decisions

## Newly Authorized

- Accept the proposal review transparency slice as architecturally complete for the proposal-review portion of Milestone 3.
- Treat proposal review as projection-driven when the UI renders backend-owned proposal history, review state, and lifecycle eligibility rather than interpreting lifecycle meaning in React.
- Use `proposal.history` as the authority for proposal last transition and transition reason.
- Keep `DecisionLifecycleRules -> Eligibility Projection -> Proposal Viewer` as the lifecycle semantics path for proposal review.
- Treat review state, lifecycle state, last transition, review reason, transition reason, allowed transitions, allowed actions, blocked transitions, and governing rules as semantic facts supplied by backend projections.
- Defer the Proposal Actions panel consolidation decision until the proposal feature disposition audit.
- Evaluate the Proposal Actions panel by semantic duplication, not by component count.
- Classify lower-priority proposal features against whether they are necessary for the decision lifecycle MVP and whether they add unique semantic value.
- Use the initial disposition expectations:
  - proposal review notes: Core MVP only if they participate in lifecycle or governance; otherwise Deferred
  - proposal revision list: Deferred unless already integrated into current review flow
  - revision comparison: Deferred
  - context snapshot listing: Internal or Deferred unless required to explain review decisions
- Treat remaining Milestone 3 work as proposal feature disposition audit, Proposal Actions panel consolidation decision, end-to-end lifecycle characterization, and Milestone 3 exit audit.
- Stage, commit, and push the accepted proposal review transparency slice before continuing Milestone 3 closure work.
