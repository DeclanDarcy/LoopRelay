# Decisions

## Newly Authorized

- Treat the Milestone 9 influence trace persistence slice as correct.
- Preserve projection fingerprint plus execution session id as the historical join for execution influence.
- Continue Milestone 9 with backend influence trace retrieval before API, Tauri, or UI expansion.
- Add retrieval for influence trace by execution session id.
- Add retrieval for influence traces by decision id.
- Expose backend endpoints after retrieval contracts are implemented and tested.
- Add Tauri bridge commands after backend endpoints pass tests.
- Keep execution UI influence surfaces deferred until retrieval contracts are stable.
- Keep adherence observations deferred until concrete execution outcome evidence exists.

## Not Authorized

- Do not tie execution influence history to mutable current decision state.
- Do not infer adherence observations without execution outcome evidence.
- Do not expand execution UI influence surfaces before backend retrieval contracts are stable.
