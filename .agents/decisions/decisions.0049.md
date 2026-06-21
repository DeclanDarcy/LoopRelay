# Decisions

## Newly Authorized

- Treat the current Milestone 1 continuation as clean and aligned with the certified Milestone 0 closure conditions.
- Keep Milestone 1 changes constrained to tokens, theme, base styling, render-only primitives, presentation-only status metadata, and CSS tokenization.
- Continue to prohibit movement of workflow authority, navigation authority, draft authority, readiness authority, mutation authority, or projection authority during Milestone 1.
- Audit `src/lib/status.ts` carefully so it remains presentation metadata only.
- Allow status helpers to answer how a status renders: label, tone, badge style, icon, or appearance.
- Do not allow `src/lib/status.ts` or design primitives to answer workflow questions such as readiness for promotion, commit, push, accept, or reject.
- For Workstream 1.4, adopt status metadata and primitives into existing render branches before evaluating broader duplication or refactors.
- Replace existing badges first while leaving readiness and workflow computation exactly where it currently lives.
- Replace panels, cards, and surfaces only as presentation wrappers without changing hierarchy.
- Replace spacing and layout primitives only when the render tree and workflow behavior remain equivalent.
- Do not consolidate repository status, execution status, proposal status, and continuity status into a single workflow-aware status engine during Milestone 1.
- Do not create workflow-aware design components such as proposal or readiness cards that interpret domain state.
- Do not move readiness decisions into buttons or primitives; existing workflow code must continue to provide disabled/readiness inputs.
- Certify Workstream 1.4 with the question: if every design primitive were replaced by plain elements, workflow behavior would remain identical.
