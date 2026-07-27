# Whole-assembly serialization is broader than the shared state it protects

## Classification

- Category: Serialization overhead
- Severity: Low
- Confidence: High (mechanism) / Medium (xUnit scheduling semantics nuance)
- Evidence level: Observed
- Scope: [tests/LoopRelay.Core.Tests/xunit.runner.json](../../tests/LoopRelay.Core.Tests/xunit.runner.json), [tests/LoopRelay.Agents.Tests/xunit.runner.json](../../tests/LoopRelay.Agents.Tests/xunit.runner.json) — both `{parallelizeAssembly:false, maxParallelThreads:1}`
- Affected tests: 103 Core.Tests + 154 Agents.Tests cases
- Affected production paths: none
- Primary cost: wall-clock within those assemblies
- Aggregate cost: none added (same work); wall: Core.Tests 25–45 s serial vs an estimated 5–10 s parallel; Agents.Tests 8–9 s serial vs ~2–3 s
- Wall-clock impact: currently none on the suite (both assemblies finish far before Cli.Tests); becomes material once the Cli.Tests critical path is fixed
- Invocation frequency: every run
- Recommended disposition: Narrow

## Summary

Both assemblies force single-threaded execution globally, yet the shared mutable state that motivates serialization is already isolated behind `[CollectionDefinition(..., DisableParallelization = true)]` collections: `"WorkspaceDatabaseCounters"` (4 Core classes reading process-wide static counters on `LoopRelayWorkspaceDatabase`) and `"ProcessEnvironment"` (process-global env/CWD/child-process tests in Agents.Tests). Under xUnit 2.8+ semantics, non-parallel collections run exclusively after parallel collections complete, so the runner.json is belt-and-braces over the collections — at the price of serializing ~250 unrelated cases.

## Evidence

- Both runner.json files verbatim `parallelizeAssembly:false, maxParallelThreads:1` (only two in the repo).
- Collections with rationale comments: `WorkspaceDatabaseCountersCollection.cs:16` ("static test-only counters … would be moved by concurrent DB opens"), `ProcessEnvironmentCollection.cs:10`; applied at `LoopRelayWorkspaceDatabaseEnsureTests.cs:14`, `...InspectStampedTests.cs:14`, `...RecoveryScopeColumnTests.cs:19`, `...LoopHistoryConvergenceIndexTests.cs:21`, `ProcessRunnerStderrDrainTests.cs:6`.
- Observed serial cost: Core.Tests aggregate ≈ wall in both runs (25/45 s); Orchestration.Tests demonstrates the same store/DB patterns run safely at high parallelism.
- Introducing commit not identifiable by path log (folded into a merge) — historical motivation Unknown.

## Current Execution Path

xUnit reads runner.json at assembly load; every collection runs on one thread sequentially. The `DisableParallelization` collection flags are inert under this configuration (nothing runs in parallel anyway).

## Intended Purpose

Protect the counter-reading tests from concurrent DB opens (any Core test opening a workspace DB bumps the process-wide counters) and process-global env mutation. The counters concern is real: collection-level exclusion alone must guarantee no *other* collection runs concurrently — which `DisableParallelization = true` provides (exclusive execution), making assembly-wide serialization unnecessary for correctness.

## Necessity Analysis

The protected failure (counter interference / env races) is reachable only for the ~19 cases inside the two collections. The remaining ~240 cases have per-test temp state and no shared mutability (sweep found none). The narrow mechanism already exists and is documented in-code; the broad one adds only wall time.

## Redundancy Analysis

Fully redundant pairing: runner.json ⊃ collection flags for the shared-state concern. Keep the collections (precise, self-documenting); drop the global serialization.

## Recommended Remediation

Delete both `xunit.runner.json` files (owner: respective test projects). Before deleting, verify the xUnit 2.9.3 scheduling guarantee with a quick targeted run (assert the counter tests still pass under default parallelism, repeatedly), since the exclusivity semantics of `DisableParallelization` are the load-bearing assumption.

## Coverage-Preservation Plan

No assertions change. Counter tests remain exclusive via their collection; env tests likewise. Risk: any latent shared-state coupling among the other ~240 cases currently masked by serialization — surfaced by repeated parallel runs before adopting.

## Validation Plan

10 consecutive `dotnet test` runs of each assembly with runner.json removed; success = identical outcomes every run plus reduced assembly wall (expect Core.Tests 25–45 s → single digits). If flakes appear, bisect to the class and add it to a serialized collection instead of restoring global serialization.

## Risks and Tradeoffs

Low: worst case is discovering real hidden coupling (which is worth knowing) and re-serializing narrowly. Benefit today is latent; it becomes real the moment the Cli.Tests critical path shortens.

## Final Disposition

Narrow
