# Decisions

## Newly Authorized

- Accept the opening Milestone 8 slice as correct.
- Continue treating quality evaluation as advisory, non-mutating, and backend-first.
- Preserve the boundary that quality assessment observes decision workflow outcomes and does not become governance authority.
- Treat human-authoring burden as the primary workflow-replacement metric.
- Use the highest-burden evidence as the dominant report classification.
- Keep backend contracts and semantics ahead of persistence, endpoints, and UI.
- Continue Milestone 8 with persisted quality artifacts next.
- Add deterministic JSON and markdown projections for:
  - `.agents/decisions/quality/assessments/`
  - `.agents/decisions/quality/reports/`
  - `.agents/decisions/quality/trends/`
- Add reload/persistence tests before backend endpoints.

## Not Authorized

- Do not build dashboards yet.
- Do not make quality assessment block, mutate, or override decision lifecycle state.
- Do not add endpoints before persisted quality artifacts and reload tests exist.
