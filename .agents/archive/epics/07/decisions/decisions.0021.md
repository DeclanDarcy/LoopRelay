# Decisions

## Newly Authorized

- Accept the second Milestone 8 persistence slice as correct.
- Treat quality evaluation as historical evidence, not mutable current state.
- Keep quality artifacts as first-class durable artifacts under `.agents/decisions/quality/`.
- Preserve timestamp snapshot semantics for quality assessments, reports, and trends so repeated generation does not overwrite prior evidence.
- Continue ordering Milestone 8 as persist, recover, and verify before serving or displaying through endpoints and UI.
- Continue preserving the advisory model: quality observes workflow outcomes and does not become decision authority.
- Implement service-level quality history operations next:
  - save assessments through quality services
  - list assessments through quality services
  - save reports through quality services
  - list reports through quality services
  - save trends through quality services
  - list trends through quality services
- Generate trends from persisted assessment history rather than recomputing history from current repository state.
- Defer endpoints, reports APIs, and dashboards until persisted quality-history semantics are covered by tests.

## Not Authorized

- Do not introduce endpoints, reports APIs, or dashboards before service-level persisted-history operations and tests.
- Do not make quality assessment block, mutate, or override decision lifecycle state.
- Do not treat trends as a recomputed latest assessment over current repository state.
