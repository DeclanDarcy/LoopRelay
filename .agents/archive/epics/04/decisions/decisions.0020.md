# Decisions

## Newly Authorized

- Continue using "Can it render entirely from props?" as the strongest safety test for Workstream 0.5 extraction candidates.
- Treat `ExecutionEventFeed` as correctly classified when it remains `events -> render`, without interpreting execution meaning.
- Preserve the ownership boundary where `useExecutionEvents` owns subscription, cleanup, ordering, duplicate replacement, and merge behavior, while `ExecutionEventFeed` remains props-to-presentation only.
- Continue adding characterization coverage for event-feed presentation behavior such as ordering, empty states, timestamps, event formatting, and possible future grouping before moving behavior.
- Prioritize `ExecutionSessionSummary` over execution history as the next Workstream 0.5 extraction candidate.
- Prefer session-summary extraction because it is more likely to remain `session -> display`, while history rendering can conceal grouping, aggregation, or interpretation.
- Keep extracting presentation, formatting, and display models during Workstream 0.5.
- Do not extract workflow meaning during Workstream 0.5.
- Classify safe extraction candidates as props-driven renderers that may format props but do not derive authority or make decisions.
- Keep candidates that determine readiness, health, promotion state, workflow transitions, or review outcomes in `App.tsx`.
- Treat the current M0 risk as presentation components accidentally absorbing workflow logic, and guard against that explicitly during each extraction.

## Next Authorized Slice

Extract an `ExecutionSessionSummary` presentation component if it can be kept as a props-only session display. Leave readiness, handoff review, commit, push, promotion, and execution-start decisions in `App.tsx`.
