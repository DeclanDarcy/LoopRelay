# Decisions

## Newly Authorized

- Accept M8 closure as credible.
- Treat the final unresolved M8 architecture question as answered: `App.tsx` is a natural authority boundary, not leftover modernization debt.
- Treat the extraction sequence of `ArtifactWorkspace`, `GitWorkflowPanel`, and `GeneratedHandoffReviewPanel` as the evidence that tested and resolved the remaining `App.tsx` hypothesis.
- Classify the remaining `App.tsx` responsibilities as authority responsibilities: workflow orchestration, draft ownership, readiness ownership, backend dispatch, stream reconciliation, and projection refresh coordination.
- Reject further decomposition based only on file size when it would distribute the same authority across controller, coordinator, manager, or container files with less visibility.
- Treat the frontend modernization program as complete, certified, and the baseline architecture.
- Preserve the final authority map: `types` owns DTO authority, `api` owns transport authority, `hooks` own projection authority, `shellState` owns navigation authority, `features` own presentation authority, and `App.tsx` owns workflow, draft, readiness, and mutation authority.
- Do not reopen M8 unless a new product capability is authorized or a concrete defect invalidates certification assumptions.
- Begin the next planning cycle from product capability and backend surface area, not additional extraction.
