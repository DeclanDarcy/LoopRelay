# Cli.Tests monolith classes serialize the entire suite's critical path

## Classification

- Category: Serialization overhead, Overly broad test boundary
- Severity: High
- Confidence: High
- Evidence level: Observed
- Scope: `tests/LoopRelay.Cli.Tests/Services/Cli/LoopRelayCompositionRootTests.cs` (44 cases, 2,737 lines), `UnifiedCliRunnerTests.cs` (41 cases, 1,181 lines)
- Affected tests: 85 cases; the whole suite's completion time
- Affected production paths: none (test organization)
- Primary cost: wall-clock
- Aggregate cost: 394 s / 334 s of per-test duration (runs 1/2) across the two classes
- Wall-clock impact: The suite's total wall clock (317 s / 244 s) equals `LoopRelayCompositionRootTests`' summed duration (313.6 s / 241.3 s) in both observed runs
- Invocation frequency: every full `dotnet test` run
- Recommended disposition: Relocate

## Summary

xUnit places each test class in its own collection and runs collections in parallel, but tests inside one collection serially. All 44 composition-root tests live in one class, so they form a single serial chain whose length equals the entire suite's wall clock on a 32-thread machine — every other assembly and class finishes at least 4× sooner and then idles. `UnifiedCliRunnerTests` (80–92 s) is the second-longest serial chain in the same assembly.

## Evidence

Observed: trx per-test durations, two full runs at `9285dc8c` (see [02-runtime-profile.md](../02-runtime-profile.md)). Run 1: suite wall 317 s, Cli.Tests assembly wall 313 s, `LoopRelayCompositionRootTests` class sum 313.6 s. Run 2: 244 s / 241 s / 241.3 s. Individual tests in the class: 5–55 s each. Cli.Tests has no `xunit.runner.json`, so default parallelism applies; the serialization is purely the one-class-one-collection structure. Each test creates its own temp repo and workspace DB (per-test constructors; no shared fixture — inventory sweep found zero `IClassFixture`/`ICollectionFixture` repo-wide).

## Current Execution Path

`dotnet test` → VSTest → xUnit: collections scheduled in parallel up to `maxParallelThreads` (default = logical CPU count). `LoopRelayCompositionRootTests` = one collection → its 44 tests execute strictly sequentially, each building a full composition root (`CreateForTests`), a fresh temp repo, and a fresh workspace database, then driving complete workflow transitions through the canonical runtime against file-backed SQLite. The class's serial sum bounds the assembly wall, which bounds the suite wall.

## Intended Purpose

The tests protect the composition root's wiring: that every workflow transition family (Plan/Execute/EvalRoadmap/Traditional), handoff/resume, rollback, telemetry, and retirement guards run correctly through the real canonical runtime. The single-class layout is organizational, not an isolation requirement — no shared mutable state between these tests was found (each test owns its temp workspace).

## Necessity Analysis

The *coverage* is necessary and is the repository's deepest integration assurance. The *serial chain* is not: nothing in the tests requires ordering or exclusivity; isolation is per-test temp state. Splitting the class into several classes (e.g., per workflow family: Plan, Execute, EvalRoadmap/Traditional, telemetry/recovery) preserves identical assertions while letting xUnit schedule the parts concurrently. With 4–6 balanced splits, the assembly's wall clock approaches its longest single test (~47–55 s) plus the next-longest chain, an estimated (not measured) suite wall of roughly 90–120 s instead of 244–317 s.

## Redundancy Analysis

Not redundancy — this finding is purely scheduling. Overlap between these two classes' assertions is treated separately in [bounded-workflow-double-coverage-composition-vs-runner.md](bounded-workflow-double-coverage-composition-vs-runner.md); intentionally layered coverage remains.

## Recommended Remediation

Split `LoopRelayCompositionRootTests` into 4–6 classes grouped by workflow family, and `UnifiedCliRunnerTests` into 2–3 (e.g., storage lifecycle vs bounded-workflow runs). Pure file reorganization: move test methods and their private helpers; no assertion changes. Owner: test project. Prerequisite: fix the env-var mutation hazard first ([env-var-mutation-in-parallel-assembly.md](env-var-mutation-in-parallel-assembly.md)) since more Cli.Tests parallelism increases its exposure, and re-check for any hidden static coupling by running the split suite repeatedly.

## Coverage-Preservation Plan

Identical assertions, identical boundaries — only scheduling changes. Failure localization improves slightly (smaller classes). Risk to verify: hidden order/state coupling between tests currently masked by serial execution; proven by running the split assembly 5× consecutively with default parallelism and comparing outcomes to baseline.

## Validation Plan

Before/after: two timed full runs each (`dotnet test LoopRelay.slnx --logger trx`), compare suite wall and Cli.Tests wall; assert identical test counts and outcomes (1,535 cases, same pass/fail set). Success: Cli.Tests wall reduced to near its longest split chain; no new failures across 5 repeated runs.

## Risks and Tradeoffs

More concurrent temp-dir/SQLite work raises transient disk contention (already tolerated elsewhere: Orchestration.Tests runs 573 parallel cases against file SQLite). CPU contention may lengthen individual test durations while shortening total wall (observed variance already shows this effect). No coverage, environment, or assurance change.

## Final Disposition

Relocate
