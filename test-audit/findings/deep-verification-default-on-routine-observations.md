# Deep storage verification is the default tier on routine observation paths

## Classification

- Category: Global proof for local behavior, Production architecture
- Severity: Medium
- Confidence: Medium-High
- Evidence level: Strongly inferred (suite cost) / Observed (mechanism)
- Scope: `WorkspaceStorageInspector` (src/LoopRelay.Orchestration.Primitives), `StorageContracts` tier enum; consumed by repository observation during workflow cycles
- Affected tests: `WorkspaceStorageVerificationTierTests` (9.8 s/11), `WorkspaceStorageAuthorityTests` (8.9 s/7), and indirectly every composition-root/runner test whose transitions trigger observations
- Affected production paths: `WorkspaceStorageInspector` Deep tier (~182 shape probes + `PRAGMA foreign_key_check`), `RepositoryObserver.ObserveAsync`
- Primary cost: aggregate runtime
- Aggregate cost: Not measured per run; unit cost ≈ the full-verification pipeline per observation at Deep tier
- Wall-clock impact: contributes to the Cli.Tests serial chain
- Invocation frequency: per storage observation wherever the default tier is used
- Recommended disposition: Fix production ownership

## Summary

The Deep verification tier — the ~182-probe shape classification plus a whole-database `PRAGMA foreign_key_check` — is the zero-value (default) member of the tier enum, so routine observation paths pay certification-strength, whole-database proof for local questions. The deleted audit's M3 already set the post-fix target: deep verification absent from routine cycle observations. Tests that drive workflow cycles inherit this cost on every observation.

## Evidence

- Default tier: `StorageContracts.cs:44-62` (Deep = 0, i.e., the default); Deep runs "the ~190-probe classification" plus `foreign_key_check` (`WorkspaceStorageInspector.cs:85-108`, `:261-268`). (Probe count 182 measured; "~190" is the source doc's phrasing.)
- Deliberate boundary use: `CanonicalImportGateway.cs:177-196` calls `ForeignKeyViolationsAsync` on staged DBs before import promotion (commit `0fa50184`) — a correct, intentional Deep-tier consumer at a trust boundary.
- M3 (deleted audit §12): "post-fix target is 2 observations/cycle (cycle + freshness) with deep verification absent from both."
- Tier/authority tests pay it directly: `WorkspaceStorageAuthorityTests.cs:20-23`, `WorkspaceStorageVerificationTierTests.cs:70-73`; observed 8.9–9.8 s aggregates for 7–11 cases.

## Current Execution Path

Workflow transition → repository observation → storage inspection at default (Deep) tier → 182 probes + full `foreign_key_check` against the workspace DB → verdict consumed by gates. Repeated per observation (~4–5/cycle per M3 census), in production and in every test that drives cycles.

## Intended Purpose

Deep verification protects against acting on a structurally foreign or referentially broken workspace — real protection at trust boundaries (import, recovery, first contact). The default-tier placement makes it also run where the workspace was verified moments earlier in the same process.

## Necessity Analysis

First-contact and import-boundary Deep checks have reachable failures (foreign DBs, partial copies) and decision consumers (fail-closed gates). Re-proving the same on every routine observation, after `EnsureSchemaAsync` has already stamped and memoized the same path in-process, is a global proof for a local question; the schema cannot have drifted between two observations of a path this process itself verified, absent external mutation — which import/recovery boundaries already guard.

## Redundancy Analysis

Partially redundant with (a) per-process `VerifiedSchemas` memoization and stamp fast path, (b) import-boundary deep checks (`0fa50184`), and (c) the Core schema suite proving the verifier itself. Intentionally layered at trust boundaries; redundant on routine cycle paths.

## Recommended Remediation

Production change (owner: orchestration storage contracts): make the stamped/light tier the default for routine observations, reserving Deep for import, recovery, explicit repair, and first contact — exactly the M3 target the prior audit set. Tests then inherit the cheaper default automatically; tier tests keep exercising Deep explicitly.

## Coverage-Preservation Plan

Deep-tier behavior remains covered by `WorkspaceStorageVerificationTierTests` (explicit tier selection) and the import-gateway tests. Fail-closed behavior on genuinely broken DBs remains covered by corrupt/foreign-schema tests in `UnifiedCliRunnerTests`. Accepted risk: routine cycles would no longer detect out-of-band DB corruption mid-run; that failure class is bounded by process-external mutation, which the boundary checks own.

## Validation Plan

Before/after M3-style observation census (observations/cycle and per-phase stopwatch) plus full-suite timed runs. Assert tier tests unchanged; corrupt-workspace runner tests still fail closed. Success: deep verification absent from routine cycle observations; measurable drop in composition-root test durations.

## Risks and Tradeoffs

Weakened mid-run detection of external DB tampering (accepted at boundary-guard level); any consumer that silently relied on the default being Deep must be found by explicit tier audit at call sites. Aligns with the program's active perf direction; if the event-sourced migration lands first, re-evaluate — the projection DB may become disposable and verification largely moot.

## Final Disposition

Fix production ownership
