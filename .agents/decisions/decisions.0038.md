# Decisions

## Newly Authorized

- Treat `RepositoryDashboardItemContent` as an appropriate late-M0.5 extraction because it isolates repository item body rendering while retaining selection, selected styling, registration, removal, loading, empty-state coordination, and selection reconciliation in `App.tsx`.
- Continue distinguishing repository projected facts from repository selection behavior; repository card display and repository selection are separate responsibilities.
- Treat the successful repository dashboard item extraction as consistent with M0.5 decomposition and M0.6 authority certification because it remains `backend projection -> display` with no embedded workflow authority.
- Proceed next with a more aggressive selected-repository summary audit, assuming the likely outcome may be partial extraction rather than full panel extraction.
- Consider repository name, path, metadata, workspace statistics, and other read-only projected facts potentially extractable as a `SelectedRepositorySummary` only if the component can receive props only and remain useful without callbacks.
- Treat regions owning `onRefresh`, `onRemove`, `onGenerate`, `onPromote`, `onExecute`, `onCommit`, or `onPush` as workflow coordination surfaces unless a focused audit proves otherwise.
- Use the late-M0.5 callback-removal heuristic: if a component remains meaningful with every callback removed, it is probably presentation; if not, it is probably participating in coordination.
- Assume remaining M0.5 opportunities are small display islands inside larger authority-bearing surfaces, not broad region extractions.
- Accept that a focused audit may conclude a surface should remain in `App.tsx`; at this stage, retaining authority-bearing surfaces is as valuable as extracting safe display-only components.
