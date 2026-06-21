# Decisions

## Newly Authorized

- Treat Milestone 0 as an authority migration with evidence, not as a sequence of refactors.
- Preserve the conclusion: do not extract hooks just to reduce `App.tsx`.
- Classify current M0 status as infrastructure complete, type authority complete, transport authority complete, projection authority substantially complete, authority certification complete, and navigation authority not yet explicit.
- Treat navigation ownership as the highest-leverage remaining M0 ambiguity.
- Proceed next with Workstream 0.4 by making navigation authority explicit.
- Create `src/CommandCenter.UI/src/state/shellState.ts` as navigation authority only.
- Start `shellState.ts` with clearly navigation-owned state:
  - selected repository id
  - selected artifact path by repository
  - selected milestone path by repository
  - active tab
  - command palette open state
- Allow `shellState` to own selection, active view, palette visibility, and navigation history.
- Do not allow `shellState` to own workspace data, execution data, git status, proposal data, draft content, or review state.
- Keep projection retrieval authority in `hooks/`.
- Keep workflow actions, workflow gating, draft state, and view composition in `App.tsx` during the next boundary step.
- Do not move `draftContent`, `commitMessage`, `selectedCommitPaths`, `operationalContextProposalDraft`, or `operationalContextReviewNote` into shell state.
- Treat M0 completion as requiring explicit projection authority, explicit navigation authority, explicit draft authority, and certified boundaries rather than extracting every possible hook.

## Validation Expected For Next Slice

- Implement navigation authority without introducing application-wide state.
- Preserve the separation: navigation state is not projection state, workflow state, or draft state.
- Keep `shellState.ts` narrow enough that it cannot become a dumping ground for backend projections or editor drafts.
