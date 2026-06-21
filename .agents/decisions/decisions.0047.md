# Decisions

## Newly Authorized

- Treat `ExecutionWorkflowRail` as the final valid M0.5 extraction because it remains meaningful after removing workflow authority.
- Consider M0.5 complete because the final scan found no additional high-value presentation-only islands.
- Do not reopen M0.5 or continue searching for presentation decomposition.
- Treat the remaining `App.tsx` responsibilities as intentionally centralized when they own navigation authority, draft ownership, workflow coordination, readiness evaluation, or mutation authority.
- Shift the next work to final Milestone 0 certification rather than further decomposition.
- Reconcile M0.3 projection authority, M0.4 navigation authority, M0.5 presentation decomposition, and M0.6 authority certification against the current implementation.
- Classify every remaining responsibility as Projection, Navigation, Presentation, Draft Ownership, Workflow Coordination, or Workflow Mutation and verify that its current owner is intentional.
- Explicitly document deferred centralized boundaries such as decision continuity review, proposal acceptance workflow, Git readiness workflow, handoff acceptance workflow, and artifact mutation workflow.
- For each deferred boundary, document why it remains centralized, why extraction was rejected, and which authority it owns.
- Add a Milestone 0 closure note if checklist items remain technically open, making clear that authority objectives are achieved and further extraction would risk fragmenting workflow authority.
- Move to Milestone 1 only after final M0 certification and accepted centralized boundaries are documented.
