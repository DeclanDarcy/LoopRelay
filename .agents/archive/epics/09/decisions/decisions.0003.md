# Decisions

## Newly Authorized

- Treat Stage 1 as complete.
- Treat Stage 2A as started correctly and continue by finishing metrics snapshot, rebuild, and diagnostics before beginning economics.
- Preserve the Stage 2A boundary: metrics remain live/read-only analysis and must not become lifecycle authority.
- Do not expose reuse score, transfer score, continue/transfer decision, or eligibility status during Stage 2A.
- Add `DecisionSessionEvidenceReader` before completing metrics snapshot persistence/rebuild work.
- Persist metrics snapshots under `.agents/decision-sessions/analysis/metrics/`.
- Rebuild missing or invalid metrics snapshots from authoritative evidence.
- Add determinism tests for identical metrics inputs.
- Add TTL/cache-risk progression tests.
- Expand Stage 2A diagnostics for source counts, byte counts, character counts, missing evidence, TTL assumptions, cache-risk assumptions, and confidence.
- Distinguish `MeasuredAt` from evidence last activity time in snapshot semantics; `MeasuredAt` is analysis runtime, while evidence last activity is when governance evidence last changed.
