# Decisions

## Newly Authorized

- Treat the Workspace Inspector as a review surface, not an action surface.
- Treat Execution as the action surface for commit, push, execution, and other workflow mutations.
- Continue Workstream 3.7 with navigation-only cross-links.
- Cross-links may change active tab, section target, and selection/navigation state.
- Cross-links must not execute workflow commands, refresh projections, reload events, or increase backend invocation counts.
- Safe Workspace cross-links include navigation to Execution history, Execution commit details, Operational Context, and Execution activity sections.
- Unsafe Workspace cross-links include direct push, commit, proposal application, and execution launch actions.
- The most important 3.7 certification criterion is proving cross-link clicks update shell navigation state without backend mutation or additional projection load.
