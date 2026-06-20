# Decisions

## Newly Authorized Decisions

- M2 must implement proposal infrastructure before generator sophistication; persistence, reviewability, determinism, and traceability are higher priority than generated-output quality.
- Current `.agents/operational_context.md` remains the only authoritative operational context; all proposals are non-authoritative artifacts, including the newest proposal.
- Newer proposals must not gain implied authority from recency alone.
- M2 tests must include proposal-level unknown Markdown preservation so unknown operational-context sections survive proposal generation and storage.
- Prompt ordering should continue to keep `OperationalContext` before `CurrentHandoff`; recent activity must not outweigh accumulated understanding in execution context reconstruction.

## Next-Slice Constraints

- Do not build generation quality features before proposal persistence and reviewable traceability exist.
- Do not introduce continuity sessions, proposal authority, or a separate workflow state machine.
