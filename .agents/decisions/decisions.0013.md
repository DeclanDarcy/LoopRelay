# Decisions

## Newly Authorized

- Accept the Milestone 3 eligibility-driven UI slice as architecturally sound.
- Treat the decision UI transition from heuristic lifecycle legality to projection-driven lifecycle rendering as complete for candidate/proposal controls.
- Preserve this authority chain:
  - `DecisionLifecycleRules`
  - lifecycle eligibility projection
  - React rendering of backend facts
- Continue rendering backend-owned lifecycle facts in the UI:
  - allowed actions
  - blocked actions
  - blocked reasons
  - allowed next states
  - governing rules
- Keep repository-level eligibility refresh after lifecycle mutations so React does not predict post-mutation legality.
- Treat dev Tauri mock parity for `get_decision_lifecycle_eligibility` as required for frontend transport alignment.
- Commit and push the accepted eligibility slice before continuing Milestone 3 work.
- The next Milestone 3 slice is authorized as supersede/archive completion:
  - supersede target selection
  - supersede rationale capture
  - archive rationale capture where applicable
  - backend invocation
  - lifecycle eligibility refresh
  - governance refresh
  - execution influence refresh where applicable
- Keep refresh propagation centralized rather than scattering projection refresh policy across individual components.
- After supersede/archive are complete, add an end-to-end decision lifecycle characterization path covering discovery through archive.
