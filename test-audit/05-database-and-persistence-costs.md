# Database and Persistence Costs

All persistence is file-backed SQLite (no `:memory:` anywhere; `Pooling=false` in every open helper). Tests reach the DB exclusively through production helpers (`LoopRelayWorkspaceDatabase.*`, store `OpenAsync`), so tests pay exactly the production lifecycle — which is the point of most of them, and the amplifier for all of them.

Measured unit costs (repo's own harness, recorded 2026-07-27 at `fd8065cd`, production source identical at HEAD — see [02-runtime-profile.md](02-runtime-profile.md)):

| Operation | Cost |
|---|---|
| First `EnsureSchemaAsync` on a new DB (create 79 tables/148 indexes + full verify) | 264 SELECTs, ~478 ms; +182 shape probes and +1 full verification **per DB created** |
| Fresh connection to an already-stamped file | 12 statements, 3.6 ms |
| Warm open (memoized path) | 0.95 ms |
| Steady-state store operation | ~58–60 ms (flat across 100× history) |
| Empty-schema floor | ~1.0 MB per workspace DB |
| `ProjectAsync` | linear in history (4.2 ms @10² → 95.6 ms @10⁴) |

Cost classification per the audit method:

- **Intrinsic to behavior under test**: store semantics against real SQLite (persistence, recovery, migration suites) — the file-backed boundary is the assurance; retained.
- **Required for isolation**: per-test DB *files* — retained; only their *initialization* is misplaced.
- **Misplaced lifecycle**: full creation+verification per fresh path → template-copy remediation, [findings/fresh-database-schema-creation-per-test.md](findings/fresh-database-schema-creation-per-test.md).
- **Redundant assurance**: Deep-tier verification on routine observations → [findings/deep-verification-default-on-routine-observations.md](findings/deep-verification-default-on-routine-observations.md); schema-suite internal duplication → [findings/schema-fingerprint-and-ensure-overlap.md](findings/schema-fingerprint-and-ensure-overlap.md).
- **Inherited from production architecture**: per-operation unpooled opens + rollback-journal flush (~58 ms floor under every write) → [findings/store-operation-durability-cost-no-wal-no-pooling.md](findings/store-operation-durability-cost-no-wal-no-pooling.md) (Measure first; WAL deferred by ORCH-3; the event-sourced storage direction may supersede).
- **Setup amplification**: row-by-row canary seeding → [findings/perf-canary-seeding-through-production-stores.md](findings/perf-canary-seeding-through-production-stores.md).

Schema/transaction guarantees vs repeated checks: the per-process memo + persisted stamp already localize verification to first contact per path; production is *not* re-verifying per operation (prior remediation confirmed effective at 0.95 ms warm). The remaining repeated global proof is the Deep observation tier and the per-test fresh-path first contacts — both addressed above. Foreign-key enforcement is per-connection `PRAGMA foreign_keys=ON` plus boundary `foreign_key_check` at import (`0fa50184`) — correctly boundary-scoped.

No N+1 query patterns, lock contention, or journal-mode conflicts were observed in test runs (busy_timeout=10000 configured; no busy-timeout failures in either run).
