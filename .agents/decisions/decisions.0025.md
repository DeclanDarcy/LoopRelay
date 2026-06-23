# Decisions

## Newly Authorized

- Close Milestone 8 as complete.
- Treat the implemented quality platform as satisfying M8 end-to-end across backend, persistence, history, endpoints, Tauri integration, and UI consumption.
- Preserve the M8 quality authority model: quality remains observational only and does not mutate, block, approve, reject, or resolve decisions.
- Keep quality downstream of human resolution: generation produces content, humans resolve, then quality assesses the resolved outcome.
- Keep quality UI centered on human authoring burden and signal categories rather than the overall score.
- Continue treating explicit report and trend generation as persisted evidence, not continuously mutating state.
- Begin Milestone 9 next.
- Sequence M9 by first building the minimal enriched execution-consumption path before influence tracing.
- Start M9 with `ExecutionDecisionContext`, `ExecutionDecisionPriority`, and `ExecutionArchitectureRule`.
- In M9, execution must consume accepted resolved decisions, not recommendations.
- Preserve the projection authority boundary: only accepted, resolved, non-superseded, non-archived, non-deferred, governance-passing decisions may influence execution.
- Add prompt-generation updates for constraints, directives, priorities, and architecture rules before adding decision influence traces.

## Not Authorized

- Do not let M9 consume unresolved recommendations as execution guidance.
- Do not add decision influence tracing before the minimal enriched execution-consumption path is working.
- Do not weaken the observational-only quality model introduced in M8.
