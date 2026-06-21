# Decisions

## Newly Authorized

- Treat the `ExecutionSessionPanel` extraction as a stronger authority-boundary validation than the event-feed extraction because it sits closer to workflow concerns.
- Treat the successful `ExecutionSessionPanel` shape as a positive signal only because it remained `props -> render` and did not absorb readiness, execution authority, workflow transitions, or review logic.
- Recognize that remaining `App.tsx` responsibilities are increasingly concentrated in workflow actions, workflow gating, draft state, review state, and composition.
- Before extracting execution history, audit remaining `App.tsx` render regions and classify them as pure presentation, presentation plus formatting, presentation plus interpretation, or workflow.
- Extract only pure presentation and presentation plus formatting regions during the next Workstream 0.5 slices.
- Use execution history only if it is genuinely `session list -> props -> render`; leave it in `App.tsx` if it contains grouping, prioritization, relevance, current-session inference, or other interpretation concerns.
- Prefer skipping an extraction over moving ownership-sensitive logic into a component.

## Next Authorized Slice

Audit execution history first. Extract it only if sorting, grouping, selection, relevance, and current-session decisions already happen elsewhere. If not, leave it in `App.tsx` and select a smaller presentational target such as diagnostic cards, metadata summaries, status displays, or read-only history rows.
