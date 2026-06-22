# Decisions

## Newly Authorized

- Pause automatic hook extraction after the current committed slice and reassess M0 through the responsibility inventory.
- Treat `.agents/audits/m0-app-responsibility-inventory.md` as a strategic M0 artifact for deciding what should be extracted, not just what can be extracted.
- Classify remaining `App.tsx` responsibilities before further M0 work as Projection, Workflow Action, Workflow Authority, Navigation, or Draft.
- Continue extracting only remaining read-mostly projection loading concerns in M0.
- Do not extract workflow actions into projection hooks.
- Do not extract workflow authority or workflow gating into hooks during M0.
- `useContinuityDiagnostics(repositoryId)` is an authorized next slice if it remains read-only and projection-only.
- `useContinuityDiagnostics(repositoryId)` may own load, refresh, loading state, error state, and the diagnostics projection.
- `useContinuityDiagnostics(repositoryId)` must not own continuity report generation, continuity meaning, continuity evaluation, or workflow implications.
- After `useContinuityDiagnostics(repositoryId)`, evaluate `useOperationalContextProposal(...)` and generated handoff content loading as potential final M0 projection extractions.
- Treat execution start, artifact rotation, accept/reject, commit, push, proposal promotion, and continuity generation as workflow actions, not M0 projection hook work.
- Treat execution readiness, commit readiness, push readiness, review readiness, and promotion readiness as workflow authority/gating, not M0 projection hook work.
- Add `.agents/audits/m0-projection-authority-certification.md` before declaring M0 complete.
- The projection-authority certification must prove every read-oriented projection has one frontend authority and no duplicate load paths.
- Re-evaluate whether M0 is effectively complete after remaining read-mostly projections and the projection-authority certification artifact.

## Validation Expected For Next Slice

- Keep the next extraction selective and evidence-driven.
- Preserve observable behavior.
- `npm run lint`
- `npm run build`
- `npm run test`
- `npm run test:e2e`
- `dotnet test CommandCenter.slnx`
