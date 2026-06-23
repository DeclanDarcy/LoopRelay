# Decisions

## Newly Authorized

- Preserve the current Milestone 1 implementation order: documentation, models, persistence, validation, services, endpoints, then UI. UI requirements must not drive reasoning ontology decisions.
- Keep Milestone 1 API exposure limited to reasoning events, threads, and relationships. Do not add hypothesis, alternative, contradiction, or direction endpoints before the materialization gate.
- Keep conflict translation at the service/API boundary while preserving repository validation as persistence-safety logic.
- During UI work, keep events, threads, and decisions visually and behaviorally distinct: events are immutable historical records, threads are grouping/navigation/summarization aids, and decisions remain the authoritative decision workflow.
- Avoid hidden reasoning-owned mutation workflows in Milestone 1 UI/API work, including thread resolution, hypothesis acceptance, direction selection, contradiction archival, or similar lifecycle authority.
- Avoid persisted service-layer derived statuses such as thread, hypothesis, alternative, or direction status before materialization review. Derived display status is acceptable only as non-authoritative presentation.
