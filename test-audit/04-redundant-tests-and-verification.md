# Semantic Test Redundancy, Repeated Verification, and Boundary Proportionality

Comparisons are by observable contract, not by name or code similarity. Three material redundancy clusters, each with a canonical finding, plus intentionally layered coverage that is explicitly **not** flagged.

## Material redundancy

1. **Bounded-workflow behavior at two adjacent boundaries** — the composition root and the unified CLI runner both drive the workflow matrix through the canonical runtime against real storage; ADR-0011 makes the runner a thin forward/render shell, so much of the runner's workflow matrix is a strict subset of composition-root coverage. Runner storage fail-closed cases also re-assert Core schema-suite behavior at a third boundary. → [findings/bounded-workflow-double-coverage-composition-vs-runner.md](findings/bounded-workflow-double-coverage-composition-vs-runner.md) (Consolidate, with an assertion-level mapping table as the guard).
2. **Schema-contract internal duplication** — the canonical v16 shape fingerprint asserted in three files; fresh-DB ensure exercises repeated across six files that all drive the same production helper. Migration-lineage and classification cases are distinct equivalence classes and stay. → [findings/schema-fingerprint-and-ensure-overlap.md](findings/schema-fingerprint-and-ensure-overlap.md) (Consolidate).
3. **Compile-enforced and triple-asserted surface contracts** — a reflection test that mirrors the project-reference graph (unreachable failure at assembly level), and the retired-Plan/Roadmap invariant asserted by the production convergence verifier, again in the composition-root tests, and again in prose. → [findings/compile-enforced-and-triple-asserted-surface-contracts.md](findings/compile-enforced-and-triple-asserted-surface-contracts.md) (Narrow; the production verifier stays as single authority).

## Intentionally layered — retained, not redundant

- **Completion certification service unit tests vs the live `completion-closure` certification fixture**: docs/certification.md explicitly frames component tests as prerequisite, not substitute, for the operator-run campaign. Distinct execution boundaries and consumers.
- **Architecture source-text scans** (no `Console.*` in orchestration namespaces; no SQL in `UnifiedCliRunner`/`RepositoryObserver`): not compiler-enforceable; they are the only enforcement of those seams. Retained.
- **Storage fail-closed at unit + entry + certification tiers**: layering is deliberate; only the entry tier's full matrix (rather than representatives) is flagged, inside finding 1.
- **Perf canaries vs the opt-in magnitude harness**: every-run property guards vs opt-in scale measurement — different frequencies, same program, complementary.

## Boundary proportionality

Broad boundaries that earn their breadth (real git, spawned published CLI, real junctions, torn-file concurrency) are catalogued in [10-legitimate-expensive-coverage.md](10-legitimate-expensive-coverage.md). The one disproportionate breadth pattern is procedural rather than semantic — the 44-case and 41-case monolith classes whose serial execution, not their assertions, is the problem ([findings/cli-monolith-classes-serialize-suite-critical-path.md](findings/cli-monolith-classes-serialize-suite-critical-path.md)).

No tests were found coupled solely to private structure or line exercise; no parameter matrices without distinct equivalence classes were identified in the inspected population (bounded non-observation: theory inputs were reviewed by sweep, not exhaustively per-case).
