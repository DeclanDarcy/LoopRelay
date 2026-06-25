# Decisions

## Newly Authorized

- Treat normalized continuity diagnostic categories as a backend-owned semantic taxonomy, not an incidental collection of messages.
- Keep React as a renderer of grouped continuity diagnostics; it must not invent, reinterpret, or assign diagnostic categories.
- Build `OperationalContextEvolutionTimeline` from typed backend semantic events and operational evolution projections.
- Do not parse markdown, infer modification meaning, or rebuild change relationships in React for the evolution timeline.
- Introduce item-level `Merged` or `NoiseRemoved` compression outcomes only if the compression engine performs those operations as distinct backend semantic actions.
- Treat the evolution timeline as the primary remaining user-facing Milestone 7 feature before projection-gap reconciliation and formal exit audit.
