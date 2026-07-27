# ~58 ms per store operation: unpooled per-operation connections with rollback-journal durability

## Classification

- Category: Production architecture, Database initialization
- Severity: High
- Confidence: High (cost exists) / Medium (attribution to journal flush — explicitly unmeasured)
- Evidence level: Observed (per-op cost) + Suspected (fsync attribution)
- Scope: all Canonical*/Sqlite* stores (src/LoopRelay.Orchestration.Primitives, src/LoopRelay.Core, src/LoopRelay.Cli telemetry/decisions)
- Affected tests: every store-backed integration test; dominant inside `LoopRelayCompositionRootTests` (18 store ops ≈ 1.05–1.09 s per workflow attempt)
- Affected production paths: `LoopRelayWorkspaceDatabase.OpenReadWriteCreate/OpenReadWrite/OpenReadOnly` (`Pooling=false`), store `OpenAsync` per operation; rollback journal (WAL enabled then deferred: `b300f91d` → `380dc6df` "defer WAL pending ORCH-3")
- Primary cost: aggregate runtime and wall-clock (via the serial Cli.Tests chain)
- Aggregate cost: ~58–60 ms × every store write across ~1,500 tests; measured 1.05–1.09 s per replayed attempt, flat across 100× history growth
- Wall-clock impact: primary constituent of the 5–55 s composition-root tests
- Invocation frequency: every store operation, production and test
- Recommended disposition: Measure first

## Summary

Every store operation opens a fresh unpooled SQLite connection, re-applies two PRAGMAs, re-reads the schema stamp, and commits under rollback-journal durability. Measured steady-state cost: ~58–60 ms per store operation, of which warm `EnsureSchemaAsync` is only ~0.95 ms (~1.6%) — the prior remediation already fixed the ensure path. The deleted audit's own (explicitly unconfirmed) hypothesis attributes the residual to one synchronous journal flush per write transaction. Tests amplify this cost thousands of times; it is production-owned, and the WAL/pooling decision was deliberately deferred pending ORCH-3.

## Evidence

- `21c996d6:production-code-performance-audit.md` §12a M1: steady-state mean 1,049–1,089 ms per 18-store-op attempt across N=10²/10³/10⁴; ensure 0.948 ms warm; "M1's decision threshold is not met on a warm process… Hypothesis, not measured: the remaining ~58 ms is durability — one synchronous journal flush per write transaction."
- `Pooling = false` in all three open helpers (`LoopRelayWorkspaceDatabase.cs:1940-1962`) and in per-feature connection factories (`WorkspaceDatabaseInspection.cs:32-37`, `CompletionArtifacts.cs:172-176`, others).
- History: `b300f91d perf(core): enable WAL and busy_timeout` then `380dc6df perf(core): defer WAL pending … ORCH-3`; `busy_timeout=10000` retained (`LoopRelayWorkspaceDatabase.cs:84`).
- Observed test-side magnitude: composition-root tests 5–55 s each while performing full workflow transitions (multiple attempts × 18 ops × ~58 ms plus observations).

## Current Execution Path

Store method → open unpooled connection (`SqliteConnection.Open`, Windows file open) → `PRAGMA foreign_keys=ON; PRAGMA busy_timeout=10000` → memo-hit ensure (2 stamp SELECTs + legacy-vocabulary EXISTS probe) → operation transaction → commit (journal create/flush/delete under rollback mode) → dispose. Per operation, per store, everywhere.

## Intended Purpose

Unpooled per-op connections give crash-consistent, lock-minimal access for a CLI whose process lifetimes are short; rollback journal is SQLite's conservative durability default. WAL was deliberately deferred as an orchestration-level decision (ORCH-3), not an oversight.

## Necessity Analysis

The behavior tests exercise (store semantics) does not depend on durability mode or pooling; the cost is production architecture leaking into every test. However, the user program has an accepted direction that supersedes further SQLite performance waves (event log in git as source of truth; SQLite becomes a disposable projection), which may moot both WAL and pooling. Changing durability semantics to speed tests up would invert priorities; changing it *for tests only* would diverge test and production behavior on a crash-consistency-relevant axis.

## Redundancy Analysis

The per-open PRAGMA/stamp work is intentionally layered (per-connection semantics require it under the per-op open design). No test-side redundancy to remove without touching the production lifecycle.

## Recommended Remediation

Run the comparison the deleted audit already specified before any change: re-run M1 fixtures with `PRAGMA journal_mode=WAL` (and/or `synchronous=NORMAL`) against the rollback baseline to confirm or refute the flush attribution. If confirmed and the event-sourced direction still keeps SQLite in the hot path, the production decision (WAL + pooling, PERF-10) belongs to ORCH-3's owner; if the event-sourced migration lands first, close this as superseded. No test-side workaround is recommended — tests correctly exercise the production lifecycle.

## Coverage-Preservation Plan

No coverage change — this is a production measurement/decision item. If WAL/pooling later lands, the schema/ensure suites and `Database_UsesBusyTimeout` assertions must be re-baselined deliberately.

## Validation Plan

The M1 WAL-vs-rollback comparison (single machine, same fixtures, warm process, discard first attempt) with per-write-transaction wall times; success criterion per the original audit: measurable drop in persistence wall under identical trace. Suite-level: before/after full-run wall and aggregate.

## Risks and Tradeoffs

WAL changes crash-recovery and cross-process semantics (multiple readers/one writer, checkpointing) — exactly why it was deferred; pooling changes connection-state assumptions (PRAGMAs are per-connection). Any change must be production-motivated, not test-motivated. Doing nothing keeps ~58 ms × N as the floor under every store-backed test.

## Final Disposition

Measure first
