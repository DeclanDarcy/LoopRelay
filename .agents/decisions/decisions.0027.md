# Decisions

## Newly Authorized

- Treat the completed `ExecutionContextValidationList` extraction as conforming to the M0.5 authority boundary because it preserves `string[] -> list` and empty input -> `No validation errors`.
- Keep validation meaning in the backend and validation rendering in the frontend.
- Continue using characterization tests that target provided ordering and empty-state fallback rather than semantic interpretation.
- Treat repository snapshot rendering as the next likely safe M0.5 extraction only if it remains `projection -> JSX`, preserves backend-provided values exactly, uses existing labels and `GitPathBucket`, and preserves existing fallbacks.
- Before extracting repository snapshot rendering, apply this audit gate:
  - If the candidate does more than `projection -> JSX`, stop.
  - If the candidate can answer `Should I care?`, stop.
  - If the candidate can answer `Can execution proceed?`, stop.
  - If the candidate can answer `How risky is this?`, stop.
- Treat labels such as `Modified Files`, `Untracked Files`, `Staged Files`, and `Current Branch` as safe only when rendered directly from existing projection data.
- Treat statements such as `Repository is clean`, `Repository is ready`, `Execution can start`, `Git state is healthy`, and `No blocking changes` as unsafe because they are interpretations.
- For remaining M0.5 work, use the posture `retain unless clearly presentation-only`.
- Treat artifact diagnostics as suspect and require a separate authority review before extraction.
- After a narrow repository snapshot extraction, seriously evaluate whether M0.5 has harvested nearly all presentation-only opportunities inside the execution-context surface.

## Next Authorized Slice

Extract repository snapshot rendering only if it passes the audit gate above; otherwise retain it in `App.tsx` and record why.
