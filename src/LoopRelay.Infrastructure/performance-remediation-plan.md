# LoopRelay.Infrastructure Performance Remediation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cache the one process-invariant part of the artifact-store security walk; everything else in this project passed the audit clean.

**Tech Stack:** .NET 10, xUnit (`tests/LoopRelay.Infrastructure.Tests`).

**Source findings:** PERF-27f (from finding DATA-6) in [production-code-performance-audit.md](../../production-code-performance-audit.md). Line references are against commit `0de6b5a8`.

Audit context: the per-segment symlink-escape walk in `RepositoryArtifactStore` is a **legitimate trust boundary and stays per-operation** (links can appear between calls). Only the repository *root's* resolved physical target is invariant per process and is currently re-resolved on every artifact operation.

## Global Constraints

- The per-segment escape walk (`EnsureExistingPathSegmentsStayWithinRepository`) is security-relevant: do not weaken, reorder, or cache it.
- Escape detection must behave identically for paths that traverse links created after process start.

---

### Task 1: Cache the resolved repository root — PERF-27f

**Files:**
- Modify: `src/LoopRelay.Infrastructure/Services/Artifacts/RepositoryArtifactStore.cs:51-105` (the `ResolveFinalTarget(repositoryRoot)` call performed per operation)
- Test: `tests/LoopRelay.Infrastructure.Tests`

**Interfaces:**
- Consumes/Produces: no public API change. Add a `private readonly Lazy<string> _resolvedRepositoryRoot` (or compute in the constructor) holding the physical target of the repository root; the per-call code reads the cached value instead of re-resolving.

- [ ] **Step 1: Read `RepositoryArtifactStore.cs` fully** — confirm the root-target resolution is the only per-call invariant and that the store is registered with a process lifetime (check construction in `LoopRelayCompositionRoot.CreateProduction`, `src/LoopRelay.Cli/Services/Cli/LoopRelayCompositionRoot.cs`). If the store is constructed per-operation anywhere, hoist the cache to a static keyed-by-root map instead.
- [ ] **Step 2: Write tests first**:
  - `Resolve_RejectsSegmentEscapingViaLink` — existing behavior; create a link escaping the repo in a temp fixture, assert rejection (port/extend the existing escape tests if present — check the test project first).
  - `Resolve_UsesCachedRootTarget` — observability: expose the resolution count via an `internal` counter or verify indirectly by asserting behavior is unchanged and reviewing that the hot path no longer calls `ResolveLinkTarget` on the root (code review is the real gate here; the behavioral tests guard correctness).
- [ ] **Step 3: Implement; run the Infrastructure suite** (`dotnet test tests/LoopRelay.Infrastructure.Tests`). Expected: all pass, escape tests unchanged.
- [ ] **Step 4: Commit** — `perf(infrastructure): cache resolved repository root in artifact store`

---

## Explicitly No Action (audit §14)

Reviewed clean — do not "improve" these while in the area: `InputWaitTurnTracker` (bounded `PeriodicTimer`, cancels on first output), effect executors (failure/effect lifecycles), `GitPorcelain` (43 lines, no process work). The heavy per-open costs this project's stores appear to pay are fixed centrally by LoopRelay.Core Task 1 (PERF-01).
