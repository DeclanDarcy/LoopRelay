# Executive Summary — Test Performance, Redundancy, and Necessity Audit

Revision `9285dc8c` (branch `improve-performance`, clean tree), audited 2026-07-27. Full artifact map at the end; every material claim has a canonical finding under [findings/](findings/) with evidence levels.

## Shape of the test system

.NET 10 / xUnit 2.9.3 solution with 11 test projects, **1,535 test cases**, no CI (deliberate — local `dotnet test LoopRelay.slnx` is the only gate), no mocking framework, no fixtures (per-test constructor/dispose everywhere), and file-backed SQLite through production open paths as the dominant resource. Two projects force whole-assembly serial execution; live/measurement paths are env-var-gated. A separate operator-run certification campaign is correctly outside the routine pass. Details: [01-test-system-inventory.md](01-test-system-inventory.md).

## Where the time goes (measured)

Two full timed runs: suite wall **317 s / 244 s**; aggregate per-test time **767 s / 805 s**; incremental build 15 s. The decisive fact, stable across both runs: **the suite's wall clock equals the summed duration of one serial test class** — `LoopRelayCompositionRootTests` (44 cases, 313.6 s and 241.3 s; one xUnit class = one serial collection) — while a 32-thread machine idles. The 34 tests ≥5 s carry 56% of all aggregate cost; 851 tests under 10 ms carry 0.2%. Unit costs from the repository's own instrumented harness (recorded the same day, identical production source): fresh workspace-DB creation+verification **~478 ms** (264 statements, 182 probes — paid per test-created DB), warm open 0.95 ms, steady-state store operation **~58–60 ms** (unpooled connection + rollback-journal flush). Full profile: [02-runtime-profile.md](02-runtime-profile.md).

## Highest-amplification repeated operations

1. Fresh-DB creation+full verification per store-backed test (est. 150–240 s/run aggregate; template-copy remediation uses the production stamp fast path at ~4 ms).
2. The ~58 ms per-store-operation floor (production architecture: `Pooling=false`, WAL deferred by ORCH-3) multiplied by every write in every integration test.
3. Deep-tier (~182 probes + `foreign_key_check`) storage verification as the *default* on routine observations — certification-strength proof for local questions; the repo's prior audit already set its removal target.
4. Full composition-root construction (catalog validation + SHA-256 asset hashing + settings load) ~90× per run — share unmeasured, measurement-gated.
5. Row-by-row canary seeding through production stores (26–47 s/run for one class).

## Most significant redundancy and lifecycle findings

Bounded-workflow behavior proven at two adjacent boundaries (composition root and thin CLI runner — consolidation with explicit coverage mapping); the v16 schema fingerprint asserted in three files; a reflection test that mirrors the compile-enforced reference graph; retirement of Plan/Roadmap asserted by three authorities; whole-assembly serialization that duplicates narrower, already-correct collection-level exclusivity. One latent parallel hazard (process-env mutation in a parallel assembly) and four wall-clock-dependent assertions in an otherwise deterministic suite — no retry machinery, no flakiness debt, near-zero fixed waiting ([07](07-waits-retries-and-flakiness.md)).

## Suite integrity

One test **fails deterministically at HEAD** (`StatusCanaryTests.CoverageLedgerIsProductionDerivedAndKeepsUncoveredSetVisible` — certification coverage-ledger drift). With no CI, a permanently red suite erodes the only gate; repairing it precedes every before/after validation in this audit. Opt-in tests report Passed when they no-op, overstating live-certification coverage; recommendation is honest Skip reporting.

## Legitimate expensive coverage retained

The published-CLI full command matrix (19–22 s), real-git effect/observation suites, Windows junction security denials, the torn-file concurrency stress, keyed-read perf canaries (assertions kept; seeding narrowed), the FullChain live runner, and both opt-in harnesses — rationale per item in [10-legitimate-expensive-coverage.md](10-legitimate-expensive-coverage.md), which also records eight investigated-and-rejected candidates.

## Remediation sequence (full table: [11-remediation-priorities.md](11-remediation-priorities.md))

1. Fix the env-mutation hazard, repair the red canary (green baseline).
2. **Split the two monolith Cli.Tests classes** — pure reorganization; estimated suite wall 244–317 s → ~90–120 s (estimate, validated by timed runs).
3. Template workspace-DB per test (est. 150–240 s aggregate reduction; sized precisely by gap #2).
4. Production: default observation tier down from Deep (implements the prior audit's M3 target); measurement-gated WAL/pooling and composition-construction questions per [12-measurement-gaps.md](12-measurement-gaps.md).
5. Coverage consolidation (runner vs composition root; schema suite; surface contracts) — mapping-gated.

No savings figure above is presented as measured unless labeled; the two split/template estimates carry explicit bases and validation plans.

## Major gaps

Per-test decomposition inside composition-root tests (no store connection observer seam — a constraint the prior audit documented), exact fresh-DB count per run, WAL-vs-rollback residual, `CreateForTests` per-call cost, post-split contention — each with a minimal experiment in [12-measurement-gaps.md](12-measurement-gaps.md). Run-to-run wall variance is ±25–30%; all single figures are two-sample observations on one machine (i9-14900K/NVMe/Debug).

## Artifact map

[01 inventory](01-test-system-inventory.md) · [02 runtime profile](02-runtime-profile.md) · [03 lifecycle](03-repeated-setup-and-lifecycle.md) · [04 redundancy](04-redundant-tests-and-verification.md) · [05 database](05-database-and-persistence-costs.md) · [06 filesystem/process/CLI](06-filesystem-process-and-cli-costs.md) · [07 waits/flakiness](07-waits-retries-and-flakiness.md) · [08 parallelization](08-parallelization-and-contention.md) · [09 production architecture](09-production-architecture-findings.md) · [10 legitimate coverage + rejected](10-legitimate-expensive-coverage.md) · [11 priorities](11-remediation-priorities.md) · [12 measurement gaps](12-measurement-gaps.md) · findings/ (15 canonical findings)
