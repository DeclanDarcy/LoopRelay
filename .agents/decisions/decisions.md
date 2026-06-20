# Decisions

## Newly Authorized Decisions

- M8 is authorized as the repeatable execution loop milestone.
- M8 must solve the post-completion question: after a successful execution cycle, what does the user do next?
- M8 must make the transition from `Ready` to the next intentional execution frictionless but not automatic.
- `User selects milestone` remains an explicit act.
- The system may suggest, highlight, and surface history, but it must not choose a milestone.
- Do not introduce system-selected milestones.
- Do not introduce automatic milestone advancement.
- Do not introduce execution chaining.
- Continue using `ExecutionSession` as the primary organizing object for M8.
- The next execution should be understood relative to previous executions through session history.
- M8.1 is authorized as Execution History & Post-Push Continuity.
- M8.1 must prioritize session history before other M8 work.
- M8.1 scope is projection-only:
  - Session history projection.
  - Session summary cards.
  - Last execution visibility.
  - Commit SHA visibility.
  - Push visibility.
  - Duration visibility.
  - Selected milestone visibility.
  - Ready-state continuation UX.
- Session history must use `ExecutionSession` summaries as its source.
- Session history must not become another artifact system.
- M8.2 is authorized conceptually as Next Execution Guidance after history exists.
- M8.2 should answer:
  - What was the last execution?
  - What milestone is currently selected?
  - Can execution start right now?
  - If not, why not?
- M8 must expose state, not make decisions.

## Explicitly Deferred

- Automatic progression.
- Automatic milestone selection.
- Workflow automation.
- System-selected next milestone.
- Loading handoff markdown or repository artifacts as a history authority.
- New workflow state.
- Any mutation without explicit user action.
