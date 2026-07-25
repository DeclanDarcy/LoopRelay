# LoopRelay.Projections Performance Remediation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop the steady-state projection-manifest rewrite (and its double load) so unchanged loop iterations produce zero manifest disk writes and no artifact-cache churn.

**Architecture:** One task. The manifest is loaded twice and re-saved unconditionally per ensure; in steady state the saved bytes are identical to the loaded bytes, and the rewrite's mtime churn also invalidates `FileSystemArtifactStore`'s signature cache for that path.

**Tech Stack:** .NET 10, xUnit (`tests/LoopRelay.Projections.Tests`).

**Source findings:** PERF-18 (from finding DATA-4) in [production-code-performance-audit.md](../../production-code-performance-audit.md). Line references are against commit `0de6b5a8`.

## Global Constraints

- The manifest MUST still be written when validation fails or freshness changes: both exception paths in `ProjectContextProjectionService` (`:78` and `:84` per the audit) write before throwing — preserve that exactly.
- Regeneration semantics (when stale/invalid) are untouched; `GeneratedAt` preservation for non-regenerated entries is what makes steady-state saves no-ops — keep it.
- Atomic write behavior (temp file + replace) for real saves is untouched.

## Cross-Project Dependencies

- Callers live in LoopRelay.Cli (`DecisionSession.BuildProposalPromptAsync` → `EnsureDecisionProjectionAsync:1278`; `CompositionPromptExecutionOwner.cs:400`) — no changes needed there; verification uses a Cli-driven loop run.

---

### Task 1: Single manifest load per ensure + equality-gated save — PERF-18

**Files:**
- Modify: `src/LoopRelay.Projections/Services/Context/ProjectContextProjectionService.cs:36,93`
- Modify: `src/LoopRelay.Projections/Services/Manifests/ProjectionManifestStore.cs:56-60` (`UpsertAsync` currently re-runs `LoadAsync` then always `SaveAsync`)
- Optional (measure first): `src/LoopRelay.Projections/Services/ProjectionArtifacts/StructuredJsonDocumentStore.cs:15-43` — route `LoadAsync` through the artifact store's cached `ReadAs` if the interface allows; skip if it drags in new coupling.
- Test: `tests/LoopRelay.Projections.Tests`

**Interfaces:**
- Produces: `ProjectionManifestStore.UpsertAsync(ProjectionManifest loaded, ProjectionManifestEntry entry, CancellationToken)` overload (exact type names: read the store first and match them) that (a) uses the caller-supplied manifest instead of re-loading, and (b) skips `SaveAsync` when the post-upsert manifest equals the loaded one. Manifest types are records per the audit — verify record value-equality actually covers all fields (collections inside records compare by reference; if the manifest holds arrays/lists, implement an explicit `ManifestEquals` helper and test it).
- Consumes: the manifest instance `ProjectContextProjectionService.EnsureAsync` already loaded at `:36` — pass it through to the upsert at `:93`.

- [ ] **Step 1: Read all three files fully** (service, manifest store, document store) — confirm the load-twice shape, the exception-path writes, and the manifest type's equality semantics.
- [ ] **Step 2: Write failing tests**:
  - `Upsert_UnchangedEntry_DoesNotRewriteFile`: seed a manifest file, run ensure twice on unchanged inputs, assert the file's last-write time and bytes are identical after the second run.
  - `Upsert_ChangedEntry_Writes`: mutate freshness input, assert the file changes.
  - `Ensure_ValidationFailure_StillWritesBeforeThrow`: drive the validation-failure path (per `:78/:84`), assert the manifest was written and the exception propagates.
  - `ManifestEquality_CoversCollections` (only if an explicit equality helper is needed): two structurally identical manifests with distinct collection instances compare equal.
- [ ] **Step 3: Run tests, verify the first fails** (`dotnet test tests/LoopRelay.Projections.Tests --filter Upsert`).
- [ ] **Step 4: Implement** (overload + equality gate + pass-through; keep the old `UpsertAsync(entry)` delegating to load-then-overload for any other callers — check with `grep -r "UpsertAsync" src/`).
- [ ] **Step 5: Run the full Projections suite; then an end-to-end steady-state check**: two consecutive loop iterations on a quiet workspace show no `projections-manifest.json` mtime change (audit verification for DATA-4).
- [ ] **Step 6: Commit** — `perf(projections): equality-gated manifest save, single load per ensure`

---

## Explicitly No Action (audit §14)

Projections passed the audit well otherwise: `ProjectContextProjectionService` regenerates only when stale/invalid (no history rebuilds); `ProjectionFreshnessEvaluator` is pure; the legacy-markdown manifest path is a correctly retained one-time format migration. Don't restructure while in the area.
