# Fresh workspace-database creation and full verification paid per test

## Classification

- Category: Database initialization, Repeated setup
- Severity: High
- Confidence: High
- Evidence level: Observed (unit cost) + Strongly inferred (suite-wide multiplication)
- Scope: every test that opens a store against a new temp database path — Cli.Tests composition/runner/telemetry/import tests, Orchestration.Tests persistence/effects/recovery stores, Core.Tests, Completion.Tests
- Affected tests: several hundred cases (store-backed tests across 4 projects)
- Affected production paths: `LoopRelayWorkspaceDatabase.EnsureSchemaAsync` (src/LoopRelay.Core), invoked by every store open
- Primary cost: aggregate runtime
- Aggregate cost: ~478 ms × (number of fresh DBs created per run). Not directly counted per run; at an estimated 300–500 fresh DBs per full pass this is ~150–240 s of the ~767–805 s aggregate (estimate, basis below)
- Wall-clock impact: contributes to the Cli.Tests critical path (each composition-root test pays it at least once)
- Invocation frequency: once per fresh database path per process — effectively once per store-backed test
- Recommended disposition: Cache immutable result

## Summary

Every store-backed test creates a unique temp database path. First contact with a new path runs the full `EnsureSchemaAsync` pipeline: DDL for 79 tables + 148 indexes plus full shape verification — measured at **264 SELECT statements and ~478 ms per database created** (182 `ShapeRequirementProbes`, `FullVerificationRuns` +1 per DB, observed via the repo's own harness). A database file that already carries a valid schema stamp takes the stamped fast path instead: **12 statements, 3.6 ms**. Tests could copy a once-built template file per test and pay the fast path, preserving full isolation.

## Evidence

- Unit costs: `21c996d6:production-code-performance-audit.md` §12a (recorded 2026-07-27 at `fd8065cd`; production source identical at HEAD): first ensure 264 stmts/478 ms; fresh connection to an already-stamped file 12 stmts/3.6 ms; warm open 0.95 ms; probes +182 exactly per database created, 0 per subsequent open.
- Per-test fresh paths: `CanonicalWorkflowPersistenceStoreTests.cs:18-24`, `SqliteRecoveryStoreTests.cs:260-263`, `SqliteExecutionEvidenceStoreListPathsTests.cs:115-129`, `SqliteDecisionSessionResumeStoreTests.cs:12`, `SqliteSessionTelemetrySinkTests.cs:15`, composition-root/runner tests (temp repo + `.agents/looprelay.db` per test).
- Deliberate re-payment: 11 call sites of `ResetSchemaVerificationCacheForTesting()` in Core.Tests (`LoopRelayWorkspaceDatabaseEnsureTests.cs`, `...SchemaV9Tests.cs`, `...LoopHistoryConvergenceIndexTests.cs`) — intentional there, since those tests *test the ensure pipeline itself*.
- Estimate basis: 438 Cli.Tests + ~150 store-backed Orchestration cases create at least one DB each; some create several (import staging). 300–500 × 478 ms → 143–239 s. Labeled estimate; per-run DB-creation count is not instrumented (see [12-measurement-gaps.md](../12-measurement-gaps.md)).

## Current Execution Path

Test constructor/body → store call → `OpenReadWriteCreate` (unpooled) → `EnsureSchemaAsync` → per-process memo miss (new path) → stamp read on empty DB fails → full DDL + 182-probe verification + identity/migration transaction + stamp write → memo add. Repeated for every fresh path in the process.

## Intended Purpose

`EnsureSchemaAsync` guarantees any opened workspace DB has the canonical v16 schema — protection against opening foreign/stale/corrupt workspaces in production. In tests, the *creation* half is necessary scaffolding; the *verification* half re-proves a fact that is identical for every freshly created DB in the same process.

## Necessity Analysis

For production, first-contact verification is load-bearing (arbitrary user workspaces). For tests, each fresh DB is created by the very code being trusted, and verification of the Nth identical creation adds no assurance beyond the 1st. The stamped fast path already exists in production and is itself under test (`LoopRelayWorkspaceDatabaseInspectStampedTests`), so relying on it for test setup uses a *stronger, already-verified* mechanism rather than a test-only shortcut. Core.Tests' deliberate re-verification (memo resets) must be preserved — that is the suite that proves the pipeline.

## Redundancy Analysis

Partially redundant: creation is required, repeated verification is redundant across tests within a run (and across the run pair with production's own stamp mechanism). Intentionally layered exception: Core.Tests schema suite, which exists to exercise exactly this pipeline.

## Recommended Remediation

Introduce a per-assembly template: build one canonical workspace DB in a static lazy initializer (or `ICollectionFixture` at assembly level), then `File.Copy` it into each test's temp repo before first store use. Ownership: test infrastructure (a small helper in each affected test project or the compile-linked TestSupport). Lifetime: one process run. Invalidation: none needed — the template is rebuilt from production DDL every run, so schema changes propagate automatically. Isolation: copies are per-test files; parallel-safe (copy-on-create, no shared handles). Tests that intentionally exercise creation/migration/verification (Core.Tests schema suite, storage-init tests in `UnifiedCliRunnerTests`) keep building from empty.

## Coverage-Preservation Plan

Protected behavior: schema creation/verification correctness — still exercised by the Core.Tests schema suite (48+ cases across six files) and by the template build itself once per run. Store-behavior tests lose nothing: they assert store semantics, not creation. Accepted risk: a store test would no longer catch a creation-path regression incidentally — explicitly covered by the dedicated suite. Localization improves (creation failures surface in one place).

## Validation Plan

Before/after timed full runs (trx): expect aggregate reduction roughly equal to (fresh DBs avoided × ~0.47 s) and a visible drop in Cli.Tests wall. Counter check: run the Core schema suite unchanged; assert `FullVerificationRuns` behavior unchanged there. Parallel-safety: 5 repeated full runs, no new failures.

## Risks and Tradeoffs

Template copy hides creation cost from tests that accidentally relied on empty-DB state — tests asserting "empty workspace" must opt out (grep for assertions on empty tables before migrating each class). Slight test-infra addition (one helper), consistent with the repo's no-fixture style if implemented as a static lazy + copy call. No production change involved.

## Final Disposition

Cache immutable result
