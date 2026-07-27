# Test-only counters and reset hooks live on the production workspace database class

## Classification

- Category: Production architecture, Test-double complexity
- Severity: Low
- Confidence: High
- Evidence level: Observed
- Scope: `LoopRelayWorkspaceDatabase` internal static counters (`FullVerificationRuns`, `RepairTransactionsOpened`, `ShapeRequirementProbes`) and `ResetSchemaVerificationCacheForTesting()` (src/LoopRelay.Core)
- Affected tests: the 4-class `"WorkspaceDatabaseCounters"` collection (Core.Tests); `WorkspaceMagnitudeHarness` (reflection access)
- Affected production paths: `EnsureSchemaAsync` counter increments on every open/verification
- Primary cost: maintenance/serialization coupling (runtime cost negligible)
- Aggregate cost: negligible CPU; forces exclusive execution of the counter tests
- Wall-clock impact: minimal
- Invocation frequency: counters increment on every DB open in every process
- Recommended disposition: Retain with explanation

## Summary

Production code carries process-wide static test instrumentation: three counters incremented inside `EnsureSchemaAsync` and a test-only cache-reset hook. This is test machinery in production, and it is the direct cause of the serialized `"WorkspaceDatabaseCounters"` collection (any concurrent DB open moves the counters). It is retained because it currently has real decision consumers.

## Evidence

- Counters and reset hook on the production class; increments at verification/probe sites (`LoopRelayWorkspaceDatabase.cs:376`, `:1840`); 11 reset call sites across Core.Tests schema files.
- Consumers: the deterministic ensure-pipeline tests (e.g., probes "+182 per database, 0 per open" assertions), the opt-in magnitude harness (reflection at `WorkspaceMagnitudeHarness.cs:205-217`), and perf-canary commits (`07114860` "measure real connection opens per settled effect", `dd406015` statement counts) — an active measurement program.
- Serialization consequence: `WorkspaceDatabaseCountersCollection.cs` rationale comment.

## Current Execution Path

Every `EnsureSchemaAsync` increments counters (interlocked/static). Tests read/assert deltas; the harness reflects into them. The collection serializes readers against all other collections.

## Intended Purpose

Give the perf program observable, deterministic proof of lifecycle behavior ("probes run per DB created, not per open") without a connection-level seam — which SQLitePCLRaw cannot provide externally (documented in the deleted audit: the authorizer is per-connection and stores expose no handle).

## Necessity Analysis

The counters are the only existing mechanism proving verification-frequency invariants — the exact invariants several of this audit's findings rely on. The failure they detect (verification lifecycle regressions, e.g., probes re-running per open) is reachable — precisely what past PERF work changed. Decision consumer: the measurement program and its canary tests. A cleaner seam (injectable observer) exists as an alternative but is a production change with its own cost; the deleted audit deliberately declined to add the connection-observer seam mid-measurement.

## Redundancy Analysis

Not redundant — no other mechanism observes verification frequency. Intentionally layered with the stamp/memo design it verifies.

## Recommended Remediation

Retain as-is for now. If the event-sourced storage direction proceeds and the SQLite projection becomes disposable, retire counters together with the verification machinery they observe rather than investing in an observer seam first. If a store-level connection observer is ever added for other reasons (the deleted audit names `CanonicalEffectWorkStore.ConnectionObserverForTesting` as the pattern), migrate the counter assertions onto it and remove the statics then.

## Coverage-Preservation Plan

No change proposed now; the collection serialization stays as the documented price. Revisit trigger: storage-architecture migration or a new observer seam.

## Validation Plan

None required (no change). If later removed: the ensure-frequency assertions must move to the replacement seam before deletion, proven by running the schema suite against both mechanisms once.

## Risks and Tradeoffs

Keeping statics on a production class is a purity cost and a standing parallelization constraint for 4 test classes; both are currently proportionate to the measurement value. The reset hook is test-only API surface on production — acceptable at pre-MVP stage per program stance.

## Final Disposition

Retain with explanation
