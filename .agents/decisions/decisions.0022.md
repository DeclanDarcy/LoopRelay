# Decisions

## Newly Authorized Decisions

- M6 Git Lifecycle is complete.
- The Git lifecycle is accepted as `Observe -> Prepare -> Review -> Validate -> Commit -> Push`.
- Backend-owned authority at every mutation boundary remains the required model.
- `Generate -> Persist -> Review -> Validate -> Mutate` is accepted as a core architectural primitive for Execution Context, Handoff, and Commit Preparation flows.
- M7 is authorized as Workspace Consolidation, not a feature milestone.
- M7 must not introduce major new capabilities.
- M7 must solve workflow fragmentation across Context, Execution, Monitoring, Handoff, Acceptance, Commit, Push, and Git Status surfaces.
- M7 must build a projection of existing workflow state machines, not a new workflow or state machine.
- M7 should organize primarily around the Execution Session as the object that answers what happened, what is happening, and what must happen next.
- M7 should progressively reveal `Context -> Execution -> Handoff -> Acceptance -> Commit -> Push` based on existing state.
- M7 must preserve distinct authority boundaries, especially separate Execution State and Repository Workflow State.
- M7 success means a user can understand and complete Launch, Monitor, Review Handoff, Accept, Prepare Commit, Commit, and Push from a single screen.
- M7 should optimize for making M8 easy rather than introducing new workflow concepts.
- M8 remains the repeatable execution loop: Execute, Review, Accept, Commit, Push, Select Next Milestone, Execute Again.
- Proceed with M7.1 Workspace Consolidation under these constraints:
  - no new workflow state;
  - no new authority boundaries;
  - no duplicated lifecycle logic;
  - only projection and orchestration of existing certified behavior.

## Explicitly Deferred

- M8 Repeatable Execution Loop remains after M7 workspace consolidation.
