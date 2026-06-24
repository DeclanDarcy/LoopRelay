# Decisions

## Newly Authorized

- Accept the first Milestone 3 slice as architecturally sound and on track.
- Treat Milestone 3 Phase 1 reachability as complete:
  - shell commands
  - typed API
  - hook mutations
  - lifecycle controls
  - proposal diagnostics
- Preserve the sequencing used by Milestones 1 and 2:
  - establish end-to-end reachability first
  - then replace temporary UI behavior with backend-owned semantics
- Treat backend lifecycle eligibility as the highest-priority remaining Milestone 3 architectural task.
- Implement lifecycle eligibility before adding more decision lifecycle UI behavior.
- Keep lifecycle ownership in this direction:
  - `DecisionLifecycleRules`
  - eligibility projection
  - UI rendering
- Make the eligibility projection richer than a simple allow/deny list, including:
  - current state
  - allowed actions
  - blocked actions
  - required inputs
  - allowed next states
  - blocked next states
  - diagnostics
- Include command name, display name, reason, and governing rule where applicable in eligibility action details.
- Replace current temporary action controls with declarative UI behavior after eligibility exists:
  - render allowed actions
  - disable blocked actions
  - display backend reasons
- Treat supersede and archive as transport-ready but product-incomplete until they include:
  - target selection
  - rationale capture
  - lifecycle transition
  - governance refresh
  - execution influence refresh
- Defer supersede/archive polish until after eligibility.
- Sequence remaining Milestone 3 work as:
  1. eligibility projection
  2. UI action availability
  3. supersede dialog
  4. archive dialog
  5. refresh behavior
  6. governance refresh
  7. execution influence refresh
- Do not add backend verification for the completed reachability slice beyond existing checks; add backend verification when the eligibility endpoint is introduced.
