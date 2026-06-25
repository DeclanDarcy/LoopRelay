# Decisions

## Newly Authorized

- Treat endpoint disposition becoming an executable architectural contract as the key Milestone 9 outcome of the backend disposition slice.
- Keep backend route classification protected by tests rather than relying only on milestone documentation.
- Keep `Internal` endpoint disposition precisely bounded to decision-session analysis diagnostics.
- Keep `Compatibility` endpoint disposition precisely bounded to ping and planning readiness.
- Treat the absence of registered `Remove` and `Redirect` routes as evidence that the backend API surface is stable enough for MVP release readiness, assuming the disposition audit remains comprehensive.
- Treat final Milestone 9 cohesion validation as the release gate before Milestone 10.
- Do not begin Milestone 10 until final Milestone 9 cohesion validation is complete.
- Verify no remaining workflow-derived UI helpers reconstruct backend state.
- Verify no duplicate frontend lifecycle derivation remains.
- Verify no duplicate explainability renderers remain where shared components are intended.
- Verify every major capability still has exactly one primary workspace.
- Verify no obsolete navigation paths remain.
- Verify no React-owned semantic state has crept back in during cleanup.
- Produce the final Milestone 9 evidence package before declaring Product Cohesion complete.
- Run one focused backend and frontend verification pass before declaring Product Cohesion complete.
- Treat Milestone 10 as release readiness and certification, not additional architectural cleanup.
