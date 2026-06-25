# Decisions

## Newly Authorized

- Accept the Milestone 3 proposal feature disposition slice.
- Treat Milestone 3 scope as intentionally bounded after classifying the remaining lower-priority proposal features.
- Keep Proposal Actions separate from Proposal Viewer for Milestone 3 because the split separates mutation/user intent from observation/semantic facts.
- Reconsider Proposal Actions consolidation only in Milestone 8 or Milestone 9 if both surfaces render duplicative semantic information.
- Defer proposal review note authoring while retaining read-only projected notes for semantic transparency.
- Treat proposal revision list and revision comparison as diagnostic read models, not lifecycle authorities or editing workflows.
- Keep proposal revision history and comparison read-only.
- Classify standalone context snapshot listing as Internal for Milestone 3 and defer any user-facing snapshot browser until continuity/operational-context work provides interpretive context.
- Treat the next Milestone 3 work as final validation, not new scope expansion.
- Sequence remaining Milestone 3 closure work as:
  - end-to-end lifecycle characterization
  - endpoint coverage audit for shell-reachable lifecycle routes
  - Milestone 3 exit audit
- Keep the end-to-end lifecycle characterization focused on reachability across Discover, Promote Candidate, Generate Proposal, Review, Needs Refinement, Ready, Resolve, Supersede, and Archive.
- During the endpoint audit, verify every Core MVP route is reachable, every reachable route is covered, transport-ready unused routes are identified, and UI actions do not bypass the intended transport.
- During the exit audit, explicitly verify:
  - lifecycle legality comes only from `DecisionLifecycleRules`
  - proposal review semantics come from backend projections
  - React performs no lifecycle inference
  - every Core MVP lifecycle operation is reachable through Backend -> Shell -> API -> Hook -> UI
  - mutations refresh authoritative projections rather than updating local state heuristically
  - no competing lifecycle model exists in the UI
- Stage, commit, and push the accepted documentation slice before beginning remaining Milestone 3 validation work, then stop executing.
