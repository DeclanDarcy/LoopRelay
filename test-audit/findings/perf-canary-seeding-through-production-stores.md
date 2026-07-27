# Performance canaries seed large histories row-by-row through production stores

## Classification

- Category: Repeated setup, Legitimate expensive assurance
- Severity: Medium
- Confidence: High
- Evidence level: Observed
- Scope: `CanonicalTransitionPersistenceStoresTests` (tests/LoopRelay.Orchestration.Primitives.Tests/Persistence/, 12 cases)
- Affected tests: primarily `ReadTransitionRunAsync_reads_exactly_one_row_no_matter_how_large_the_history_table_is` (10.1 s run 1 / 28.8 s run 2) and `PersistStateAsync_updates_the_correct_existing_run_when_history_has_many_rows` (4.9 s)
- Affected production paths: none defective — the canaries protect PERF-02 keyed-read behavior
- Primary cost: aggregate runtime, high variance under load
- Aggregate cost: class total 26.7 s / 47.4 s per run
- Wall-clock impact: within Orchestration.Tests' parallel budget (26–47 s wall) — occasionally its longest chain
- Invocation frequency: every run
- Recommended disposition: Narrow

## Summary

The keyed-read canaries build "large history" preconditions by inserting rows one at a time through the production store — each insert paying the ~58 ms per-operation open/flush cost — so the *setup* costs tens of seconds to prove a read that then takes milliseconds. The magnitude harness deliberately replays through production stores for fixture realism, but these unit-level canaries assert read-scoping only; the history table's provenance is irrelevant to the assertion. Bulk-seeding inside one connection/transaction preserves the proof at a fraction of the cost.

## Evidence

Observed durations above (with 3× run-to-run variance — the row-by-row seeding is contention-sensitive). Store insert cost: ~58–60 ms/op (M1). The assertion subject is "reads exactly one row no matter how large the history table is" — a property of the SELECT, not of how rows arrived. Related instrumented canaries (`07114860` connection opens per settled effect, `dd406015` statement counts in plan hydration) count real operations and *do* need production-path fidelity.

## Current Execution Path

Test → loop of store-level inserts (each: open unpooled connection, PRAGMAs, ensure fast-path, insert txn, journal flush, dispose) × N rows → single keyed read → assert row count/read behavior.

## Intended Purpose

Regression protection for PERF-02: keyed reads must stay O(1) as history grows. Decision consumer: the active perf program (these are its canaries).

## Necessity Analysis

The canary itself is legitimate and should remain every-run. Only the seeding lifecycle is wrong: the precondition ("a large history exists") does not require the production write path; the write path's own behavior is covered by the store's other cases and by the opt-in harness at realistic scales.

## Redundancy Analysis

Setup is partially redundant with production-write coverage elsewhere; the assertion is unique (keep). Intentional layering with the magnitude harness: the harness proves magnitudes at scale opt-in; the canary guards the property every run.

## Recommended Remediation

Owner: the test class. Seed via a single connection/transaction (raw inserts or a store-provided bulk path used only in setup), sized to the same N; keep the assertion untouched. Where a canary's subject *is* operation counts (the authorizer/observer canaries), leave seeding as-is.

## Coverage-Preservation Plan

The keyed-read property remains asserted identically. Production insert behavior remains covered by the store's behavioral cases. Accepted risk: seeded rows bypass store-level invariants — mitigate by asserting the seeded table shape matches store-written shape once (single store-written row alongside bulk rows, as the read target).

## Validation Plan

Before/after class timings (expect ~26–47 s → low single digits); canary still fails when the keyed read is deliberately broadened (throwaway local perturbation of the SELECT, reverted).

## Risks and Tradeoffs

Bulk-seeded fixtures can drift from real row shapes if the schema changes — anchored by keeping one store-written row in the fixture and asserting shape equality. Variance shrinks as a side benefit.

## Final Disposition

Narrow
