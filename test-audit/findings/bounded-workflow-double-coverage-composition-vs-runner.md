# Bounded-workflow behavior is proven twice: composition root and unified runner

## Classification

- Category: Redundant test coverage, Overly broad test boundary
- Severity: Medium
- Confidence: Medium
- Evidence level: Strongly inferred
- Scope: `LoopRelayCompositionRootTests` (44 cases) vs `UnifiedCliRunnerTests` (41 cases), tests/LoopRelay.Cli.Tests/Services/Cli/
- Affected tests: the "bounded plan/execute/eval/traditional … through canonical runtime" families in both classes; storage-init/corrupt-schema cases in `UnifiedCliRunnerTests`
- Affected production paths: `LoopRelayCompositionRoot`, `UnifiedCliRunner.RunAsync`
- Primary cost: aggregate runtime and wall-clock (both classes are serial chains)
- Aggregate cost: the overlapping families are a substantial share of the two classes' 394 s / 334 s combined aggregate (per-family split not measured)
- Wall-clock impact: both classes bound the suite's critical path
- Invocation frequency: every full run
- Recommended disposition: Consolidate

## Summary

Both classes drive workflow transitions "through canonical runtime" against real composition and real storage: the composition-root tests from the wiring side, the runner tests from `RunAsync` entry. The distinct value of each boundary is real (wiring correctness vs CLI-entry contract: exit codes, typed failures, rendering), but several cases prove the same observable workflow behavior twice at nearly the same boundary — the runner sits one thin layer above the composition root (ADR-0011 asserts the runner only forwards/renders). Additionally `UnifiedCliRunnerTests` re-asserts schema fail-closed/corrupt/future-version behaviors that the Core schema suite and storage-init tests already prove at their own boundary.

## Evidence

- `UnifiedCliRunnerTests.cs` (1,181 lines): `RunAsync_bounded_eval_verifies_existing_eval_roadmap_products_through_canonical_runtime` (16.0 s), `RunAsync_bounded_traditional_verifies_existing_roadmap_products_through_canonical_runtime` (12.4 s), `RunAsync_bounded_eval_selects_evaluation_intent_through_canonical_runtime` (12.7 s), `RunAsync_bounded_plan_verifies_existing_execution_artifacts_through_canonical_runtime` (6.4 s); storage init/corrupt/future-version at `:258-352`.
- `LoopRelayCompositionRootTests.cs` (2,737 lines): `Plan_workflow_transitions_run_through_canonical_runtime` (47–55 s), `Execute_…` (31–34 s), `EvalRoadmap_…` (30 s), plus per-family behavioral variants.
- Boundary thinness: `ApplicationBoundaryTests.cs:18` asserts the runner is a forward/render/exit-code shell (ADR-0011) — i.e., the two boundaries are one layer apart by design.
- Schema fail-closed overlap: Core.Tests schema suite (48+ cases) and `UnifiedCliRunnerTests:258-352` both prove unsupported/corrupt/future-version handling; the live `persistence-lifecycle` certification fixture covers it a third time at campaign level (docs/certification.md).

## Current Execution Path

Both classes: fresh temp repo + fresh workspace DB per test → full composition → drive transitions → assert DB rows/products/outcomes. The runner class additionally parses/renders and asserts exit codes.

## Intended Purpose

Composition-root tests: the graph is wired correctly and transitions execute end-to-end. Runner tests: the published entry point maps requests to those transitions and reports typed results. Both exist because each caught different regression classes historically (wiring vs surface).

## Necessity Analysis

Keeping both boundaries is right; running the *full workflow matrix* at both is not. One representative bounded-workflow path through `RunAsync` per workflow family proves the forwarding contract; the behavioral matrix (variants, rollback, resume, recovery markers) needs to live at exactly one boundary — the composition root, where localization is better. The schema fail-closed cases at the runner boundary add distinct value only for exit-code/rendering semantics; one representative case suffices there.

## Redundancy Analysis

Partially redundant: workflow-behavior families overlap across the two classes (same observable contract, adjacent boundaries — ADR-0011 makes the delta thin by construction). Intentionally layered: parser/renderer/exit-code assertions; storage fail-closed at three tiers is layered by design (unit / entry / certification) but the entry tier needs only representatives.

## Recommended Remediation

Coverage-mapping consolidation (owner: Cli.Tests): (1) enumerate runner bounded-workflow cases; keep one per workflow family as the forwarding representative, retire cases whose assertions are a strict subset of a composition-root case; (2) keep all runner cases whose assertions are surface-specific (exit codes, typed errors, rendering, cancellation); (3) reduce runner storage-lifecycle cases to representatives per outcome class. Do this after (or with) the class split, so wall-clock benefits compound.

## Coverage-Preservation Plan

For each retired case, record the composition-root case that asserts the same observable outcome (workflow product rows, gate results). Surface semantics keep dedicated coverage. Failure localization: wiring regressions localize to composition-root tests; surface regressions to the remaining runner tests. Accepted risk: a regression precisely in the forwarding of a non-representative family variant would surface at the composition-root test plus the family representative, not a dedicated runner case.

## Validation Plan

Produce the mapping table first (case → covering case); retire only mapped cases; run the full suite twice; confirm no coverage-visible change (same production lines exercised on the retained paths — spot-check via coverlet on the two classes before/after if desired) and reduced runner-class chain length.

## Risks and Tradeoffs

Consolidation is judgment-heavy: the classes are 3,900 lines combined and case names overstate similarity — mapping must read assertions, not names. Wrongly retiring a surface-semantics case would lose real coverage; the mapping step is the guard.

## Final Disposition

Consolidate
