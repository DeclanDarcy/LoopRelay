# Decisions

## Newly Authorized

- M0.3 should continue the bottom-up layering trajectory: test infrastructure, type authority, transport authority, then projection hooks.
- M0.3 should begin with simple projection hooks only: `useRepositories()`, `useRepositoryWorkspace(repositoryId)`, and `useArtifactContent(repositoryId, relativePath)`.
- Projection hooks are authorized to own projection fetch, projection refresh, loading state, error state, projection value, and a boring `refresh` function.
- Projection hooks must not introduce frontend interpretation, derived workflow meaning, recommendations, diagnostics reinterpretation, or domain service behavior.
- Projection hook shape should remain minimal, such as `{ data, isLoading, error, refresh }`.
- Workflow hooks are not yet authorized for early M0.3.
- Do not extract execution session orchestration, operational-context review/proposal workflow, continuity workflow, proposal review, or SSE orchestration during the first M0.3 slice.
- Transport hooks are forbidden; do not create `useInvoke`, `useCommandCenterApi`, `useEventSource`, or similar transport-owning hooks.
- Transport remains owned by `src/api`.
- Add projection-equivalence characterization for M0.3 to prove that hook extraction changes location but not observable behavior.
- Projection-equivalence coverage should focus on the same loading sequence, refresh sequence, error sequence, and rendered result where practical.

## Validation Expected For Next Slice

- Characterization should cover repository projection behavior before and after hook extraction.
- Characterization should cover workspace projection load/refresh equivalence.
- Characterization should cover artifact content load equivalence.
- `npm run lint`
- `npm run build`
- `npm run test`
- `npm run test:e2e`
- `dotnet test CommandCenter.slnx`
