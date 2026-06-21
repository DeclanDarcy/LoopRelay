# Decisions

## Newly Authorized

- Treat the `GitWorkflowEvidence` extraction as more important for the ownership conclusion it supports than for the amount of code it moved.
- Use `remove callbacks -> still meaningful` as the operational test for presentation components in this codebase.
- Perform exactly one more deliberate `App.tsx` scan for M0.5.
- Classify remaining `App.tsx` blocks as Presentation, Navigation Authority, Draft Ownership, Workflow Coordination, Workflow Mutation, Selection Reconciliation, or Readiness Evaluation.
- Extract a remaining block only if it is meaningful in isolation and contains no authority.
- Close M0.5 if the final scan finds no additional high-value `props -> render` extraction candidates.
- Do not continue extracting merely because JSX remains in `App.tsx`.
- Avoid fragmenting workflow authority in response to late-stage line-count pressure.
- Consider M0.5 complete when all significant presentation-only islands have been audited, extracted where valuable, and remaining `App.tsx` responsibilities are intentionally centralized authority, draft, readiness, selection, or mutation coordination.
- After M0.5 closure, shift to certification that remaining responsibilities are intentionally centralized and aligned with the M0.0-M0.6 authority model.
