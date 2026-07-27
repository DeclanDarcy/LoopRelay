# Schema-contract suite re-asserts the v16 fingerprint and re-runs the ensure pipeline across six files

## Classification

- Category: Redundant verification
- Severity: Low-Medium
- Confidence: Medium-High
- Evidence level: Strongly inferred
- Scope: tests/LoopRelay.Core.Tests/Services/: `LoopRelayWorkspaceDatabaseSchemaV9Tests.cs` (34 cases), `...EnsureTests.cs` (7), `...InspectStampedTests.cs` (6), `...RecoveryScopeColumnTests.cs` (2), `...LoopHistoryConvergenceIndexTests.cs` (5), `...Tests.cs` (2)
- Affected tests: 56 cases
- Affected production paths: `LoopRelayWorkspaceDatabase.EnsureSchemaAsync`/`InspectSchemaAsync`
- Primary cost: aggregate runtime (9.3 s run 1 → 27.9 s run 2 for SchemaV9 alone, inside a fully serialized assembly)
- Aggregate cost: ~15–35 s per run across the six files (observed range, both runs)
- Wall-clock impact: minor today (Core.Tests is off the critical path)
- Invocation frequency: every full run; every case re-creates and/or re-verifies fresh or upgraded DBs, several deliberately resetting the per-process verification memo
- Recommended disposition: Consolidate

## Summary

Six files all drive the same `EnsureSchemaAsync`/`InspectSchema` pipeline. The canonical v16 shape fingerprint is re-asserted in three of them (each feature commit added its own "fingerprint moved" proof), and each file independently re-exercises full ensure runs on fresh or migrated DBs. The migration-lineage cases are distinct equivalence classes; the repeated fingerprint assertions and duplicated fresh-DB ensure exercises are the same contract proven through the same helper multiple times.

## Evidence

- Fingerprint re-assertion: `RecoveryScopeColumnTests` ("Canonical_v16_shape_fingerprint_differs_from_v15"), `LoopHistoryConvergenceIndexTests` ("…moved the canonical v16 shape fingerprint"), and SchemaV9's full-shape fingerprint coverage — same constant, three files (history: each added by its own feature commit lineage).
- All six route through the same production helper; 11 `ResetSchemaVerificationCacheForTesting()` sites force full re-verification within a serialized single-threaded assembly.
- Observed: SchemaV9 34 cases 9.3–27.9 s; the whole assembly serial (runner.json), so every re-verification is pure critical-path time for that assembly.

## Current Execution Path

Per case: fresh temp DB (or version-N fixture) → optionally reset memo → `EnsureSchemaAsync` full pipeline (264 statements/~478 ms on creation) → introspection SQL → assertions. Serial within the assembly.

## Intended Purpose

Protect the schema contract: shape inventory, version migrations v9→v16, stamp fast-path classification, repair behavior, busy-timeout configuration. The fingerprint assertions protect against unnoticed shape drift.

## Necessity Analysis

The pipeline suite is the designated owner of creation/verification assurance (other findings *depend* on it staying strong). The redundancy is internal: one canonical fingerprint assertion suffices; migration steps need one ensure run per version transition, not per file. Distinct equivalence classes to preserve: each vN→v16 migration, stamped vs full classification, repair paths, no-write fast path, busy timeout.

## Redundancy Analysis

Partially redundant (three fingerprint assertions = fully redundant duplicates of one contract; repeated fresh-DB ensure exercises across files = partially redundant with SchemaV9's systematic coverage). Migration-lineage and classification cases are intentionally distinct.

## Recommended Remediation

Consolidate ownership (owner: Core.Tests): move the single canonical fingerprint assertion into the schema-contract file; make the recovery-scope and convergence-index files assert their *feature-specific* deltas (column exists, index unique, backfill semantics) against an already-ensured DB rather than re-proving the global fingerprint; audit the six files for duplicate fresh-DB ensure exercises and route shared setup through one helper that creates once per case only where creation is the subject.

## Coverage-Preservation Plan

Shape drift remains protected by the single fingerprint assertion (same constant, same failure mode — three copies fail together today, so consolidation loses no detection, only duplicate noise). Migration and repair coverage untouched. Localization improves: fingerprint drift points at one test.

## Validation Plan

Run the Core.Tests assembly before/after: identical pass/fail behavior on (a) unmodified HEAD, (b) a deliberate local fingerprint-perturbing schema edit (verify exactly one test fails after consolidation, three before) — perform the perturbation as a throwaway working-tree experiment, then revert.

## Risks and Tradeoffs

Small; main risk is accidentally weakening a migration case while de-duplicating setup — mitigated by mapping each retired assertion to its surviving equivalent. Wall-clock gain is modest today; the value is maintenance cost and assertion clarity.

## Final Disposition

Consolidate
