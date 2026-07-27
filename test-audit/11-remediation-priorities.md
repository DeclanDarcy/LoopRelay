# Remediation Priorities

Ranked material findings. Impacts are qualitative unless a measured figure exists; no numeric scores are manufactured. All 15 findings counted once — 14 ranked plus one retained item (primary category shown; several are multi-classified in their bodies).

| Rank | Finding | Category | Confidence | Aggregate impact | Wall-clock impact | Assurance value at stake | Reachability of protected failure | Breadth | Arch. significance | Remediation risk | Effort | Disposition | Dependencies | Validation |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | [cli-monolith-classes-serialize-suite-critical-path](findings/cli-monolith-classes-serialize-suite-critical-path.md) | Serialization overhead | High | none (same work) | **Suite wall 244–317 s → est. 90–120 s** | none removed | n/a | whole suite | test organization | Low (latent coupling risk, checked by repeated runs) | Low-Med (file reorg) | Relocate | ranks 2, 3 | timed runs ×2, 5× repeat stability |
| 2 | [env-var-mutation-in-parallel-assembly](findings/env-var-mutation-in-parallel-assembly.md) | Parallelization contention | High | none | none | prevents latent flake | reachable under parallelism | 1 class + neighbors | low | Trivial | Trivial | Relocate | none | 10× parallel repeats |
| 3 | [status-canary-red-at-head](findings/status-canary-red-at-head.md) | Safeguard without decision consumer | High | negligible | none | **gate integrity** | occurring now | suite credibility | medium | Low | Low-Med (drift diagnosis) | Retain with explanation | none — blocks all before/after validation baselines | suite green ×2 + perturbation check |
| 4 | [fresh-database-schema-creation-per-test](findings/fresh-database-schema-creation-per-test.md) | Database initialization | High | **est. 150–240 s/run** (gap #2 refines) | part of critical path | creation coverage stays in Core suite | n/a (setup) | 4 projects | misplaced lifecycle | Medium (empty-DB assumptions per class) | Medium | Cache immutable result | gap #2 sizing; rank 3 | before/after timed runs; Core suite unchanged |
| 5 | [deep-verification-default-on-routine-observations](findings/deep-verification-default-on-routine-observations.md) | Global proof for local behavior | Med-High | material (unmeasured/run) | part of critical path | boundary Deep checks stay | corruption mid-run: boundary-owned | all cycle-driving tests | **production default** | Medium (tier audit at call sites) | Medium | Fix production ownership | gap #1 confirms share; production owner | M3-style census; corrupt-DB tests still fail closed |
| 6 | [composition-root-construction-repeated-per-test](findings/composition-root-construction-repeated-per-test.md) | Repeated setup | Medium | unknown ×90 calls | inside critical path | validation stays (scratch path + validator tests) | n/a | ~90 cases | production seam | Low (measure only) | Low to measure | Measure first | gap #4 | per-call cost report |
| 7 | [store-operation-durability-cost-no-wal-no-pooling](findings/store-operation-durability-cost-no-wal-no-pooling.md) | Production architecture | High (cost) / Med (attribution) | ~58 ms × every store op | floor under critical path | crash-consistency semantics | n/a | all store tests | **deferred ORCH-3 decision** | High if changed casually | Medium | Measure first | ORCH-3 owner; event-sourced direction may supersede | M1 WAL comparison |
| 8 | [perf-canary-seeding-through-production-stores](findings/perf-canary-seeding-through-production-stores.md) | Repeated setup | High | 26–47 s/run class | occasional Orchestration chain-top | canary assertion untouched | n/a | 1 class | low | Low-Med (fixture-shape drift, anchored) | Low-Med | Narrow | none | class timing; broadened-read perturbation |
| 9 | [bounded-workflow-double-coverage-composition-vs-runner](findings/bounded-workflow-double-coverage-composition-vs-runner.md) | Redundant test coverage | Medium | substantial share of 334–394 s | shortens both serial chains | forwarding representatives + surface cases stay | wiring/surface regressions | 2 classes | ADR-0011 boundary | Medium-High (mapping is judgment-heavy) | High | Consolidate | rank 1 lands first; mapping table | mapping-gated retirement; optional coverlet diff |
| 10 | [schema-fingerprint-and-ensure-overlap](findings/schema-fingerprint-and-ensure-overlap.md) | Redundant verification | Med-High | 15–35 s/run | none today | fingerprint + migration lineage stay | shape drift | 6 files | low | Low | Low-Med | Consolidate | none | perturbation: exactly 1 test fails |
| 11 | [assembly-serialization-redundant-with-collections](findings/assembly-serialization-redundant-with-collections.md) | Serialization overhead | High/Med | none | latent (post-rank-1) | collections keep exclusivity | counter/env races | 2 assemblies | low | Low | Trivial | Narrow | verify xUnit exclusivity semantics; after rank 1 | 10× repeats per assembly |
| 12 | [env-gated-noop-tests-report-passed](findings/env-gated-noop-tests-report-passed.md) | Safeguard without decision consumer | High | ~0 | none | signal truthfulness | misread coverage: occurring | ~5 cases | low | Low | Low | Replace with stronger mechanism | mechanism choice (skip support) | trx outcome diff |
| 13 | [compile-enforced-and-triple-asserted-surface-contracts](findings/compile-enforced-and-triple-asserted-surface-contracts.md) | Redundant verification | Med-High | negligible | none | compiler + production verifier remain | reference edit / retirement regrowth | 3 cases | low | Low | Low | Narrow | none | perturbation: verifier alone fails |
| 14 | [wall-clock-dependent-assertions](findings/wall-clock-dependent-assertions.md) | Fixed wait | High | <1 s | none | same behaviors asserted deterministically | load-induced flakes: latent | 4 cases | low | Low | Low | Replace with stronger mechanism | none | 50× repeats under load + perturbation checks |
| — | [test-counters-on-production-database](findings/test-counters-on-production-database.md) | Production architecture | High | negligible | minimal | measurement program consumer | lifecycle regressions | 4 classes | noted | n/a | n/a | Retain with explanation | revisit on storage migration | none |

## Immediate Low-Risk Corrections

Ranks 2, 3, 11, 12, 13, 14 and rank 1's mechanical split. Rank 3 (repairing the red canary) is not optional polish: every validation plan in this audit needs a green baseline, and a permanently red suite erodes the only gate the program has. Rank 1 is the single highest-payoff change in the audit — pure file reorganization converting a 4–5-minute serial chain into parallel chains, estimated 2–3× suite wall reduction on this machine.

## Structural Lifecycle Corrections

Rank 4 (template workspace DB copied per test, invalidation = process lifetime, creation coverage explicitly retained in the Core schema suite) and rank 8 (bulk canary seeding). Both move immutable or assertion-irrelevant work out of per-test lifecycle without adding shared mutable fixtures.

## Test-Coverage Consolidation

Ranks 9, 10, 13 — all deletion/narrowing gated on explicit coverage mapping (finding bodies define the mapping and perturbation checks). Rank 9 is the largest and most judgment-heavy; do it after the split so its wall-clock benefit is visible and its mapping can be validated against a parallel-stable suite.

## Production-Architecture Corrections

Rank 5 (default observation tier → stamped/light; Deep at trust boundaries) is the one production change this audit recommends outright — it implements the target the repository's own prior audit set (M3). Ranks 6–7 are measurement-gated production questions; rank 7 additionally belongs to ORCH-3's owner and may be mooted by the event-sourced storage direction — decide sequencing there, not here.

## Measurement-First Investigations

Ranks 6, 7 plus gaps #1, #2, #5 in [12-measurement-gaps.md](12-measurement-gaps.md). None are safe to act on today; each has a defined minimal experiment.

## Legitimate Cost to Retain

The published-CLI matrix test, real-git suites, junction security tests, torn-file concurrency stress, keyed-read canaries (setup narrowed per rank 8), FullChain live runner, and the opt-in harnesses — inventory and rationale in [10-legitimate-expensive-coverage.md](10-legitimate-expensive-coverage.md).
