# Decisions

## Newly Authorized

- Accept the candidate duplicate-status rendering slice as architecturally consistent with Milestone 3.
- Treat candidate duplicate rendering as complete when it presents backend-authored lifecycle facts instead of reconstructing duplicate relationships in React.
- Preserve duplicate status ownership in the decision domain through candidate history and source references.
- Render duplicate target identity from the serialized lifecycle record rather than frontend candidate-list matching or heuristics.
- Render duplicate transition reason directly from backend-authored candidate history.
- Continue the projection-first explanation pattern across decision, workflow, governance, execution, reasoning, and continuity surfaces.
- Keep proposal review transparency together in one focused slice.
- Make one proposal review panel the obvious source of truth for:
  - current review state
  - last transition
  - allowed transitions
  - blocked transitions
  - unavailable reasons
- Avoid scattering proposal review semantics across multiple components.
- After proposal review transparency, perform the MVP disposition audit for:
  - review notes
  - revision list
  - revision comparison
  - context snapshot listing
- Record explicit Core MVP, Deferred, Internal, or Remove dispositions for lower-priority lifecycle features instead of leaving them unresolved.
- Treat remaining Milestone 3 work as:
  - proposal review transparency
  - MVP feature disposition audit
  - end-to-end lifecycle characterization
  - milestone exit audit
- After accepted work is complete, rotate decisions, stage, commit, push, and stop executing.
