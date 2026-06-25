# Decisions

## Newly Authorized

- Treat the execution consolidation slice as aligned with Milestone 9 cohesion principles because it keeps execution semantics unchanged while establishing one primary execution presentation.
- Keep `ExecutionTab` as the sole primary home for full execution event streams, complete session history, and detailed execution transparency.
- Treat workspace execution surfaces as contextual overview/dashboard surfaces that may show concise summaries and navigation affordances but must not duplicate detailed execution UI.
- Preserve the regression-test pattern that asserts contextual workspace surfaces do not render primary execution detail rows such as `.execution-event-row` and `.execution-history-row`.
- Continue Milestone 9 with workflow presentation consolidation as the next slice.
- For workflow consolidation:
  - inventory every workflow display,
  - classify each workflow surface as primary, contextual, compatibility, or retire candidate,
  - remove remaining duplicated derivation from `RepositoryExecutionState` where authoritative workflow projection exists,
  - preserve contextual workflow summaries for current stage, blocking gates, health summary, and required action,
  - navigate contextual workflow references into the primary Workflow workspace or owning tab section for the complete experience.
- After workflow consolidation, expect similar Milestone 9 consolidation opportunities in governance summaries, decision summaries, reasoning summaries, and continuity summaries before broader density and terminology work.
