# Decisions

## Newly Authorized

- Classify `mergeExecutionEvents` as stream correctness logic, not display logic and not workflow logic.
- Keep `mergeExecutionEvents` beside `useExecutionEvents` subscription, cleanup, ordering, duplicate replacement, and session isolation behavior.
- Continue treating the retained M0.5 responsibilities as important ownership findings, not merely unextracted code.
- Use the question "If git workflow semantics changed, would this helper still exist?" before extracting git path bucket rendering.
- Extract git path bucket rendering only if classification shows it is presentation, formatting, or grouping.
- Do not extract git path bucket rendering if it embeds staged/unstaged meaning, commit readiness, review meaning, or workflow grouping.
- Continue refusing extraction of commit readiness, proposal readiness, promotion readiness, and execution readiness.
- Treat remaining M0 closure less as an `App.tsx` size target and more as an intentional ownership audit.
- Continue the M0.5 decomposition rule: extract presentation, formatting, and grouping; move stream correctness to stream authority; keep meaning, readiness, and workflow review with their current authority.

## Next Authorized Slice

Continue Workstream 0.5 with classification-first decomposition. The likely next candidate remains git path bucket rendering, but only after confirming it is pure presentation/formatting/grouping rather than workflow meaning or readiness.
