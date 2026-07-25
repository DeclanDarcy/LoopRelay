# LoopRelay.Completion Performance Remediation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop completion-phase listings from materializing and hash-validating every evidence body just to enumerate paths, and scope the completion-authority store's reads to the run its consumer actually filters on.

**Architecture:** Two small tasks. The per-open schema-ensure cost `CanonicalCompletionAuthorityStore` pays is fixed centrally by LoopRelay.Core Task 1 (PERF-01) — do not duplicate that here.

**Tech Stack:** .NET 10, xUnit (`tests/LoopRelay.Completion.Tests`).

**Source findings:** PERF-14 (consumer side; store side is LoopRelay.Core Task 4) and the read-scoping half of audit finding AGNT-4 (folded under PERF-01/PERF-14 in the report) in [production-code-performance-audit.md](../../production-code-performance-audit.md). Line references are against commit `0de6b5a8`.

## Global Constraints

- Content consumers of evidence MUST keep per-row hash validation — only path-only consumers switch APIs.
- Evidence ordering by `(stem, sequence)` is observable and must not change.
- Append-only `ON CONFLICT` semantics and ordering in the completion-authority tables are untouchable.
- Schema-compat failure classification (LegacyContinuity/Unknown throws) stays.

## Cross-Project Dependencies

- **Blocked by LoopRelay.Core Task 4** (adds `ListPathsAsync` to `SqliteExecutionEvidenceStore`). Land that first.
- Consumer call sites in LoopRelay.Cli (`CompletionKernelBoundaryObserver.cs:21-24`, `UnifiedCliRunner.cs:240`, `CompositionKernelOwners.cs:214`) do not change — the filtering moves inside this project's store.

---

### Task 1: Path-only evidence listing in CompletionArtifacts — PERF-14 (consumer)

**Files:**
- Modify: `src/LoopRelay.Completion/Services/ArtifactStorage/CompletionArtifacts.cs:37-45` (currently calls the store's full `ListAsync` and keeps only `record.RelativePath`)
- Callers to verify unchanged: `CompletionPromptContextBuilder.cs:47`, `CompletionCertificationService.cs:53` (this project)
- Test: `tests/LoopRelay.Completion.Tests`

**Change contract:** switch the paths-only flow to LoopRelay.Core's `ListPathsAsync(globPattern, ct)` (SQL-side pattern filter, no bodies, no hashes). First enumerate every `CompletionArtifacts` method and its consumers (`grep -rn "CompletionArtifacts" src/`): any consumer that reads `Content` keeps the validating `ListAsync`. Note the fallback store: when the Sqlite store is not injected, `FileBackedExecutionEvidenceStore` serves (audit §14) — give it the same path-only method with equivalent semantics or keep the old path for the fallback only (read the abstraction first; the interface lives wherever `IExecutionEvidenceStore`-equivalent is declared).

- [ ] **Step 1: Read `CompletionArtifacts.cs` and the store abstraction; map consumers** (paths-only vs content).
- [ ] **Step 2: Write test:** path listing equals the old projection on a seeded store (including glob-filtered exclusions), and a corrupted-body row does not fail the paths listing but does fail a content read (proves validation still guards content).
- [ ] **Step 3: Implement; run `dotnet test tests/LoopRelay.Completion.Tests`; commit** — `perf(completion): path-only evidence listing`

### Task 2: Run-scoped completion-authority snapshot reads — AGNT-4 read half

**Files:**
- Modify: `src/LoopRelay.Completion/Services/Authority/CanonicalCompletionAuthorityStore.cs:122-172` (`ReadSnapshotAsync`: unfiltered SELECT of all five append-only tables; the boundary observer then filters to `command.Context.Run` in memory — `CompletionKernelBoundaryObserver.cs:21-24` in LoopRelay.Cli)
- Test: `tests/LoopRelay.Completion.Tests`

**Interfaces:**
- Produces: `ReadSnapshotAsync(RunIdentity rootRun, CancellationToken)` overload (exact identity type: read the store and the observer first) adding `WHERE root_run_id = @run` to each of the five queries. The unfiltered overload stays for status/whole-workspace consumers (`UnifiedCliRunner.cs:240`, status composer) — enumerate them first and leave them on the unfiltered read.

- [ ] **Step 1: Read the store fully;** confirm each of the five tables has a run column (if any lacks one, that table's rows are either run-agnostic — keep unfiltered — or this needs the same schema-addition pattern as Orchestration Task 8c; record which in `## Decisions`).
- [ ] **Step 2: Write parity test:** run-filtered snapshot equals the in-memory-filtered projection of the unfiltered snapshot on a fixture seeded with two runs' worth of rows.
- [ ] **Step 3: Implement; switch the boundary observer** (coordinated one-line change in LoopRelay.Cli — same PR).
- [ ] **Step 4: Run boundary-observer settlement tests; commit** — `perf(completion): run-scoped exit-gate snapshot reads`

---

## Explicitly No Action (audit §14)

Reviewed clean: `CompletionCertificationService` and its parsers (per-epic lifecycle, cost dominated by 2–3 deliberate agent prompts); archive services; numbered-evidence listing within certification. The per-console-message durable evidence writes during completion (PERF-22) are owned by `CompositionPromptExecutionOwner` in the LoopRelay.Cli plan (Task CLI-8), not here.

## Decisions

*(append: Task 2 table/run-column inventory)*
