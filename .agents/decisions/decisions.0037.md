# Decisions

## Newly Authorized

- Treat the `OperationalContextCurrentPanel` extraction as satisfying the stricter post-M0.6 standard because it is bounded around `current operational context -> display`, not operational-context workflow.
- Continue requiring remaining M0.5 extraction candidates to pass these tests: all data arrives through props, no backend commands are required, and the component remains meaningful when rendered in isolation.
- Treat `projection -> presentation` regions as the preferred late-M0.5 extraction category.
- Keep workflow transitions, workflow mutations, draft coordination, authority decisions, proposal generation/loading/review/promotion, review notes, and comparison workflows out of presentation components.
- Audit the repository summary region next by distinguishing `repository projection -> display` from selection coordination and repository lifecycle responsibilities.
- Consider repository name, path, metadata, counts, and status text likely safe only when rendered exactly as projected.
- Treat repository selection, selection reconciliation, repository registration, repository removal, and repository refresh orchestration as unsafe for presentation extraction because they participate in state coordination.
- Use this heuristic for repository-summary candidates: if every callback prop were removed, the component should still be useful; otherwise it is probably a coordination component.
- Continue assuming most remaining `App.tsx` code is intentional until a focused audit proves a presentation-only boundary.
