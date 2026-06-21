# Decisions

## Newly Authorized

- Treat the generated-handoff extraction as further evidence that M0.5 is entering its final phase.
- Preserve the successful M0.5 split: evidence/content display components render caller-owned state, while `App.tsx` retains decisions, mutations, readiness, draft ownership, and workflow coordination.
- For the next Git review audit, ask how much unaudited presentation remains rather than searching for extraction for its own sake.
- Expect the Git review audit to have lower extraction yield than operational-context review; a conclusion that the area is mostly workflow authority is a valid M0.5 outcome.
- Consider M0.5 complete once remaining `App.tsx` responsibilities are audited and classified as workflow authority, draft ownership, navigation ownership, selection reconciliation, readiness evaluation, or mutation coordination, with no significant `props -> render` islands left.
- Do not require zero JSX in `App.tsx` for M0.5 closure; require deliberate presentation ownership and deliberate authority ownership.
