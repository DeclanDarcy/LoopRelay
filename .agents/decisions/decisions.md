# Decisions

## Newly Authorized

- Continue treating late M0 Workstream 0.5 extractions as cognitive-load reductions, not authority moves.
- Favor extractions that preserve a direct `data -> presentation` relationship.
- Treat "caller-provided labels and paths" as the reason `GitPathBucket` is presentation-only and safely inside Workstream 0.5.
- Do not move components or helpers that convert data into workflow meaning before presentation.
- Continue preserving ownership of git meaning, commit readiness, workflow grouping, and review semantics.
- Before selecting the next extraction, classify remaining large `App.tsx` render regions as `Presentation Only`, `Presentation + Formatting`, `Presentation + Workflow`, or `Workflow Only`.
- Extract only `Presentation Only` and `Presentation + Formatting` regions during Workstream 0.5.
- Leave `Presentation + Workflow` and `Workflow Only` regions in place unless separately authorized.
- Use "Can this component be rendered from props alone?" as the classification question for the next extraction.
- Treat presentational execution workspace components as good next candidates when they consume already-derived state only.
- Candidate safe targets include execution session summary, execution context diagnostics panel, execution event timeline, and execution metadata card, provided they do not decide workflow state.
- Do not extract code that answers whether commit is enabled, promotion is enabled, review is valid, or execution can start.
- Evaluate every remaining M0 extraction by whether it reduces cognitive complexity without moving authority.

## Next Authorized Slice

Audit remaining large render regions inside `App.tsx` using the four-category classification above. Then extract one low-risk presentational execution workspace component that can render from props alone and does not own readiness, review validity, promotion, commit, push, or execution-start decisions.
