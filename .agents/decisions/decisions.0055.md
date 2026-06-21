# Decisions

## Newly Authorized

- Treat Milestone 2 as implemented but not certified complete.
- Treat the shell as a legitimate new authority-bearing layer only for frontend navigation and application composition.
- Preserve the M0 authority split: `shellState` owns shell navigation state, `components/shell` own navigation presentation, and `App.tsx` continues to own workflow authority, mutation authority, and readiness authority.
- Use the shell authority test during M2 certification: no shell component should perform workflow coordination.
- Keep `activePrimaryTab`, palette visibility, and section target in `shellState`; do not add workflow state such as execution phase, proposal readiness, commit gating, or promotion eligibility to shell state.
- Keep Command Palette v1 limited to navigation, surface discovery, and workspace movement.
- Do not expose execution, proposal review, promotion, commit, push, handoff accept/reject, or other workflow mutations through the command palette.
- Continue omitting sidebar branch, dirty, ahead/behind, or similar git state until backend projections provide that truth.
- Next slice should focus on M2 certification: tab latency, command-palette latency, responsive verification, and a shell-authority audit before closing Milestone 2.
