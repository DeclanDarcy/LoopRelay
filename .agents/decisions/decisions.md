# Decisions

## Newly Authorized

- Treat the latest milestone-selection characterization as architectural boundary protection, not ordinary UI behavior coverage.
- Preserve the certified boundary that milestone selection is navigation state while execution-context preview is an explicit backend workflow action.
- Continue prioritizing governance-boundary characterization over further component extraction for the next M0 slices.
- Treat commit preparation, commit scope selection, commit readiness, and push gating as the next highest-risk certification targets.
- Characterize commit workflow boundaries before touching structure.

## Suggested Characterization Sequence

1. Commit preparation trigger:
   - Editing commit message must not prepare commit.
   - Selecting files must not prepare commit.
   - Only the explicit prepare action prepares commit.
2. Commit scope authority:
   - Scope selection remains user driven.
   - Frontend must not silently expand or shrink commit scope.
3. Commit readiness:
   - Readiness must remain tied to existing authority.
   - Presentation helpers must not become readiness authority.
4. Push gating:
   - Push availability follows existing authority.
   - UI state changes alone must not enable push.

## Avoid

- Do not resume extraction-first work immediately.
- Do not move JSX merely to continue decomposition when the higher-value remaining work is authority characterization, boundary certification, and workflow regression protection.
