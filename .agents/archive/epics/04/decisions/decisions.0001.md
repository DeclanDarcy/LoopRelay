# Decisions

## Newly Authorized

- M0 Workstream 0.1 slice is authorized as: centralize frontend contract types without changing behavior.
- The slice goal is to move DTO/type authority out of `App.tsx` and `devTauriMock.ts` into `src/types/` while preserving current runtime behavior.
- `src/types/` should distinguish backend-facing contracts from frontend-only helper/view contracts.
- Type names should reflect backend authority rather than UI interpretation.
- `App.tsx` and `devTauriMock.ts` should import the same shared DTO types after extraction.
- The slice must not introduce projection logic, workflow state, UI layout changes, styling changes, or behavior changes.
- Characterization coverage should be added or updated before moving behavior-sensitive types.
- The work should stay as contract ownership cleanup, not frontend domain modeling or projection interpretation.

## Validation Expected For Next Slice

- `npm run lint`
- `npm run build`
- `npm run test`
- `npm run test:e2e`
