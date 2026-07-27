# Repeated Setup and Lifecycle Ownership

The repository has a deliberate no-fixture test style (zero `IClassFixture`/`ICollectionFixture`/`IAsyncLifetime`; per-test constructors + `IDisposable`). The consequence is that **every** piece of setup runs at per-test lifecycle, including work whose inputs are process-constant. The material repetition clusters, each with a canonical finding:

| Repeated operation | Actual lifecycle | Cheapest sufficient lifecycle | Unit cost (evidence) | Canonical finding |
|---|---|---|---|---|
| Fresh workspace-DB creation + full schema verification | per test (unique temp path) | once per run (template copy takes the stamped fast path) | 264 stmts / ~478 ms per DB created vs 3.6 ms stamped open (Observed, harness §12a) | [fresh-database-schema-creation-per-test.md](findings/fresh-database-schema-creation-per-test.md) |
| Store connection open + PRAGMAs + stamp re-read + journal-flush commit | per store operation | production-owned decision (WAL/pooling deferred by ORCH-3; superseded by event-sourced direction) | ~58–60 ms per store op (Observed) | [store-operation-durability-cost-no-wal-no-pooling.md](findings/store-operation-durability-cost-no-wal-no-pooling.md) |
| Full composition-root construction: DI graph + catalog validation + SHA-256 asset hashing + settings load | per test (~90 cases) | validation/hash of process-constant inputs: once per process — pending measurement | Not measured | [composition-root-construction-repeated-per-test.md](findings/composition-root-construction-repeated-per-test.md) |
| Large-history seeding through production store writes | per canary test | single-connection bulk seed per test | ~58 ms × N rows; 10–29 s observed per canary | [perf-canary-seeding-through-production-stores.md](findings/perf-canary-seeding-through-production-stores.md) |
| Deep-tier storage verification during routine observations | per observation (~4–5/cycle) | trust boundaries only (import/recovery/first contact) | 182 probes + `foreign_key_check` per observation | [deep-verification-default-on-routine-observations.md](findings/deep-verification-default-on-routine-observations.md) |

Deliberate, correct re-payment (not findings): the Core.Tests schema suite resets the per-process verification memo at 11 sites to re-exercise the ensure pipeline — that suite *is* the owner of creation/verification assurance and must keep paying full price. Its internal duplication is a separate, smaller issue ([schema-fingerprint-and-ensure-overlap.md](findings/schema-fingerprint-and-ensure-overlap.md)).

Teardown lifecycle: per-test recursive temp deletes with bounded retry loops (5×50 ms, 10×100 ms) for Windows file-lock release — proportionate cleanup resilience, not flakiness compensation; classified in [07-waits-retries-and-flakiness.md](07-waits-retries-and-flakiness.md).

Ownership ambiguities observed: none structural — ownership is uniformly "the test" today. The audit's lifecycle recommendations move only *immutable* results upward (template DB, validated catalog) with process-lifetime invalidation; both models are defined in the findings. No shared mutable fixtures are recommended anywhere — parallel execution and the repo's isolation-by-construction style are preserved.
