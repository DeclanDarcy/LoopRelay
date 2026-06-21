# CommandCenter.Backend — Architecture & Clean Code Audit

**Auditor:** Principal architecture review (formal)
**Date:** 2026-06-20
**Subject:** `src/CommandCenter.Backend` — single ASP.NET Core (.NET 10) project, 135 `.cs` files, ~8,392 LOC, folder-organized by feature (Execution 58, Continuity 50, Artifacts 10, Projections 6, Repositories 4, Planning 3, Configuration 3). Companion test project `tests/CommandCenter.Backend.Tests` (xUnit, ~192 facts/theories across 20 files).

> **Framing correction.** The audit prompt assumes a multi-project layered solution. This is a **single-assembly modular monolith**: there is no `.sln`, no project references, and the `.csproj` declares no package references. "Layers" and "assembly boundaries" are therefore *folder conventions*, not enforced boundaries. Findings are scored against that reality — folder-as-module is a legitimate choice at this scale and is not itself a defect.

---

## Executive Summary

| Dimension | Score | Justification |
|---|---|---|
| **Overall architecture health** | **7 / 10** | Clean dependency direction, no service-locator, no DI-lifetime traps, consistent idioms, genuine test seams. Held back by two god orchestrators and a single-method composition root. |
| **Maintainability health** | **7 / 10** | Strong, consistent naming and a real 192-test safety net. Undercut by divergent-change hotspots (`ExecutionSessionService`, `Program.cs`) and shotgun-surgery risk in scattered state-transition guards. |
| **Architectural integrity** | **7 / 10** | Implementation matches an intended "modular monolith + DI seams" architecture; the only boundary leak is SSE framing in an endpoint lambda. Folder modules are coherent and cohesive. |
| **Technical debt level** | **4 / 10** *(lower = less debt)* | Debt is **modest and well-contained**, concentrated in ~5 named locations rather than diffuse. Two items (non-atomic persistence, dual state machines) will compound if ignored. |

**One-paragraph verdict.** This is a well-built codebase for its stage: consistent persistence idiom, disciplined test doubles, clean process/git abstraction, immutable models, and a coherent domain vocabulary. Its weaknesses are the classic ones of a maturing monolith — a few services and the composition root have accreted too many responsibilities, and the same *"two parallel enums + ad-hoc `if` guards"* anti-pattern was independently reinvented in both major subsystems. None of this requires a rewrite; every fix below is evolutionary and protected by the existing test suite.

---

## Architecture Findings

Findings are deduplicated across the four sub-audits and assigned global IDs. Each cites `file:line`.

### A1 — Non-atomic, truncate-then-write persistence (durability)
- **Severity:** High · **Category:** Coupling / Persistence / Maintainability
- **Evidence:** All four JSON stores write the full aggregate via truncating writes with no temp-file/rename and no fsync: `FileSystemExecutionSessionStore.SaveAsync` (`File.Create(storePath)` then serialize), `FileSystemArtifactStore.cs:28` (`File.WriteAllTextAsync`), `ApplicationConfigurationStore`, `FileSystemOperationalContextProposalStore`. The execution store keeps **all sessions in one `execution-sessions.json`** and every mutation loads-all/edits-one/saves-all (`ReplaceSessionAsync:459-472`).
- **Impact:** A crash or power loss mid-write corrupts or truncates the *entire* aggregate file — for the session store that is total loss of all sessions. Independently flagged by two sub-audits.
- **Recommendation:** Write to `*.tmp` then `File.Move(tmp, path, overwrite: true)` for atomic replace. Centralize in a shared `JsonFileStore<T>` helper so all four stores inherit the fix. ~1 day, low risk, highest payoff in this report.

### A2 — Dual parallel state machines with scattered guards (recurring anti-pattern)
- **Severity:** High · **Category:** Domain Model / Maintainability
- **Evidence (Execution):** Two enums describe one session — `ExecutionSessionState` (5 values) and `RepositoryExecutionState` (8 values) — both `init` props on `ExecutionSession.cs`, co-mutated by 7 `With*` methods in `file static class ExecutionSessionMutation` (`ExecutionSessionService.cs:515-785`). Legal transitions are enforced by inline `if (session.RepositoryState != X) throw` at `:240`, `:339`, `:418`, and `HandoffService.cs:32-33`.
- **Evidence (Continuity):** `OperationalContextProposalStatus` (6) and `OperationalContextReviewState` (5) overlap on Edited/Accepted/Rejected for the *same* proposal; transition guards duplicated across services (`OperationalContextReviewService` `EnsureReviewable` L143-149; `OperationalContextLifecycleService` `EnsurePromotableLatestAsync` L93-107 checks *both* enums).
- **Impact:** No single source of truth for valid transitions; the two enums can drift out of sync; adding a state is shotgun surgery across multiple files. The fact that the same flaw appears twice signals a missing shared idiom.
- **Recommendation:** Introduce one `StateMachine`/transition-table per subsystem (pure `CanTransitionTo` / `(state, event) -> state`) that every service calls; assert enum-pair consistency in that one place. Keep the enums; centralize the rules.

### A3 — God service: `ExecutionSessionService` (cohesion)
- **Severity:** High · **Category:** Cohesion / Complexity
- **Evidence:** 787 lines, **6 injected collaborators** (`ExecutionSessionService.cs:9-16`), **18 public methods**, mixing persistence (`sessionStore.*` ×16), process control, git (`gitService.*` ×6), event emission (`monitoringService.*` ×6), the state machine, and path validation (`NormalizeSelectedPaths` 479-512). `StartAsync` (132-225, 94 lines, brace depth 5) and `CommitAsync` (331-408, 78 lines) touch all concerns.
- **Impact:** Divergent change — nearly every Execution feature edits this one file; isolation-testing requires standing up six fakes.
- **Recommendation:** Extract per-operation command handlers (Start/Accept/Reject/Commit/Push) over a thin `SessionRepository` facade; move path-safety to a `RepositoryRelativePath` helper. Evolutionary, no rewrite.

### A4 — God composition root: `Program.cs` (cohesion / duplication)
- **Severity:** High · **Category:** Cohesion / Maintainability
- **Evidence:** 713 lines in a single `CreateApp` method: **31 DI registrations** (L21-62) and **35 inline endpoint handlers** (L68-698 = 89% of the file). **65 catch blocks** copy-paste the same exception→HTTP mapping, e.g. `catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }` recurs at L190, L215, L278, L423, L444… (NotFound ×35, BadRequest ×20, Conflict ×15).
- **Impact:** Every feature funnels through one method (merge-conflict surface, no per-feature ownership). The error-envelope shape is duplicated 65× and already drifts (some handlers catch `IOException`, others don't).
- **Recommendation:** (1) Centralize exception mapping via `IExceptionHandler`/`ProblemDetails` middleware → handlers shrink to the happy path; (2) split endpoints into per-feature `Map*Endpoints` extension methods and registrations into `services.AddCommandCenter()`. Removes the bulk of the 632 endpoint lines.

### A5 — God orchestrator + hidden side effect: `OperationalContextGenerationService`
- **Severity:** High · **Category:** Coupling / Complexity
- **Evidence:** **9 injected dependencies** (`OperationalContextGenerationService.cs:10-19`). `GenerateAsync` at **L38** silently calls `proposalStore.SupersedePendingAsync(repository)` — a read-shaped "generate" mutates unrelated pending proposals as a side effect; the sole caller (`Program.cs:274`) cannot opt out.
- **Impact:** Hard-to-test orchestrator; surprising mutation under concurrent use; temporal coupling between generation and supersession.
- **Recommendation:** Extract input assembly (`BuildInputSetAsync` L62-82 + `ReadOptionalAsync`) into an `IOperationalContextInputProvider` (shrinks to ~4 deps); make supersession explicit in the result or a deliberate lifecycle call.

### A6 — Dual-write without atomicity in `PromoteAsync`
- **Severity:** High · **Category:** Coupling / Persistence
- **Evidence:** `OperationalContextLifecycleService.PromoteAsync` L16-79 (63 lines) performs hash re-validation → archival rotation (`RotateCurrentOperationalContextAsync`) → `artifactService.SaveAsync` of the new current context: **two filesystem writes, no atomicity**.
- **Impact:** A crash between rotate and save leaves archived-but-not-replaced state on disk requiring manual recovery.
- **Recommendation:** Stage to temp + atomic rename, or write a "promotion-in-progress" marker enabling resume. Folds into the A1 `JsonFileStore<T>` fix.

### A7 — Coarse global lock serializes unrelated work (concurrency)
- **Severity:** Medium · **Category:** Coupling (runtime)
- **Evidence:** A single `SemaphoreSlim gate = new(1,1)` (`ExecutionSessionService.cs:20`) wraps every public mutation and is **held across long provider awaits** — `RecoverAsync` holds it while awaiting `executionProvider.TryReattachAsync` per session (`:42`); `StartAsync` holds it across `executionProvider.StartAsync` (`:190`). The store adds its own single gate over one file.
- **Impact:** All repositories' operations are globally serialized; one slow process start/reattach blocks unrelated sessions. Correct, not scalable.
- **Recommendation:** Per-repository keyed locking (`ConcurrentDictionary<Guid, SemaphoreSlim>`); release before long provider awaits where state permits.

### A8 — Primitive obsession on IDs / hashes / paths (domain model)
- **Severity:** Medium · **Category:** Domain Model
- **Evidence:** `Guid repositoryId` (~65 sites), `Guid sessionId` (~46), `string proposalId` (~22), and SHA-256 hex content hashes all flow as raw primitives; **zero value-object wrappers** repo-wide. Hash equality is bare `string.Equals(..., Ordinal)` (`PromoteAsync` L24, L31). Magic role string `"CurrentHandoff"` at `ExecutionSessionService.cs:175,177`.
- **Impact:** No compile-time guard against passing a sessionId where a repositoryId is expected; validation isn't centralized. (Counter-example done right: `ArtifactPath.cs:11-23` centralizes the path-traversal invariant.)
- **Recommendation:** `readonly record struct RepositoryId(Guid)` / `SessionId` / `ContentHash` for the top three; promote artifact roles to consts/enum.

### A9 — Stringly-typed English-substring heuristics (determinism / clarity)
- **Severity:** Medium · **Category:** Maintainability / Domain Model
- **Evidence:** Classification logic is hardcoded substring matching: `UnderstandingCompressionService` (`IsTransientExecutionNoise` → "build passed", "recorded with state"; `MeaningfulTokens` stopword set L315-351); `DecisionAnalysisService.Classify` L72-95 (`ContainsAny(statement, "architecture","authority",…)`).
- **Impact:** Brittle to phrasing, locale-bound, semantics buried in control flow. *Mitigant:* the logic is pure/deterministic (no time/IO/random) — good for testing.
- **Recommendation:** Lift keyword sets into named `static readonly` tables (or config) per category so they are reviewable and testable without re-reading branches.

### A10 — Process-start race + unobserved reader tasks
- **Severity:** Medium · **Category:** Complexity / Testability
- **Evidence:** `ProcessRunner.StartAsync` launches stdout/stderr/exit readers via fire-and-forget `Task.Run` (no handle retained), then `await Task.Delay(ImmediateExitProbeDelay /*250ms*/)` and reports `HasExited`. A process exiting at 260 ms is reported as running; reader-task exceptions are swallowed.
- **Impact:** Flaky immediate-failure detection; lost fault diagnostics.
- **Recommendation:** `Task.WhenAny(exitTask, Task.Delay(...))` instead of a fixed delay; retain reader handles to observe faults.

### A11 — SSE transport framing embedded in an endpoint lambda
- **Severity:** Medium · **Category:** Architecture / Coupling
- **Evidence:** `Program.cs:617-648` hand-writes SSE (`ContentType = "text/event-stream"` L629; `WriteAsync($"id: …\n")` / `event:` / `data:` L638-640) and builds a *local* `JsonSerializerOptions` + `JsonStringEnumConverter` (L631-632) duplicating the global config at L64-65.
- **Impact:** Transport concern leaks into the composition root; the duplicated serializer can silently diverge from the app-wide one.
- **Recommendation:** Move framing into an `ExecutionEventSseWriter` and reuse the registered JSON options.

### A12 — Near-duplicate projections / manual re-bucketing
- **Severity:** Medium · **Category:** Complexity / Domain Model
- **Evidence:** `OperationalContextDocument` (Title/Sections/Items) and `OperationalContextProjection` (`OperationalContextProjection.cs:18-32` — 8 hand-written per-kind init lists) re-bucket the *same* `OperationalContextItem`s the Document already holds by `ItemKind`.
- **Impact:** Document→Projection mapping is manual restating; adding an `ItemKind` means editing both.
- **Recommendation:** Derive the per-kind lists from `Document` via a single `GroupBy(ItemKind)` helper.

### A13 — Lower-severity smells (grouped)
- **Severity:** Low · **Category:** Maintainability / Abstraction
- **Evidence & items:**
  - **Interface-per-class:** 25/28 interfaces have exactly one implementation (only `IExecutionProvider` ×3 and `IArtifactStore` ×2 are polymorphic). *Mitigated* — the 192 tests fake many seams, so they aren't purely speculative.
  - No-op `try { … } catch (InvalidOperationException) { throw; }` (`ExecutionSessionService.cs:387-398`) — delete it.
  - Triplicated `new Repository { … Path.GetFileName(...) }` mapping (`:246-251`, `:374-379`, `:423-428`) → add `ExecutionSession.ToRepository()`.
  - `GetRepositoryAsync` helper does load-all + `FirstOrDefault` O(n) lookup in the composition root (`Program.cs:703-706`) → promote to `IRepositoryService.GetByIdAsync`.
  - Markdown parser maps sections by exact heading string; unknown headings silently become `ItemKind.Unknown` (`MarkdownOperationalContextParser.Parse` L22-70) → emit a parse diagnostic.
  - Hardcoded CORS origins as magic strings (`Program.cs:54-58`); no options pattern → bind from config.
  - "Edited" means subtly different things in the two Continuity enums → disambiguate one.
  - DTOs are `class` with 426 `init`/get-only props vs 3 setters (effectively immutable) — idiomatic .NET would prefer `record`; cosmetic only.

---

## Technical Debt Register

| ID | Severity | Description | Long-Term Cost (2+ yr) |
|----|----------|-------------|------------------------|
| A1 | High | Non-atomic truncate-then-write across all 4 JSON stores; session store is one aggregate file | Data-loss incidents as volume grows; erodes trust in persistence |
| A2 | High | Dual parallel state enums + scattered `if` guards in **both** Execution and Continuity | Every new state = multi-file shotgun surgery; drift bugs |
| A3 | High | `ExecutionSessionService` god service (787 LOC, 6 deps, 18 methods) | Divergent change; onboarding friction; merge conflicts |
| A4 | High | `Program.cs` god root (713 LOC, 35 inline handlers, 65 duplicated catch blocks) | Error-policy drift; central conflict point |
| A5 | High | `OperationalContextGenerationService` 9-dep orchestrator with hidden supersede side effect | Hard to test; surprising mutations under concurrency |
| A6 | High | `PromoteAsync` dual filesystem write without atomicity | Inconsistent on-disk state requiring manual recovery |
| A7 | Med | Single global semaphore held across long provider awaits | Throughput ceiling; unrelated sessions block |
| A8 | Med | Primitive-obsessed IDs/hashes/paths; zero value objects | Mis-passed-ID bugs; scattered validation |
| A9 | Med | Hardcoded English-substring classification heuristics | Brittle behavior; locale lock-in; opaque intent |
| A10 | Med | Fixed-delay process-start probe; unobserved reader tasks | Flaky failure detection; lost diagnostics |
| A11 | Med | SSE framing + duplicate serializer in endpoint lambda | Serializer divergence; transport leak |
| A12 | Med | Document/Projection re-bucketing duplication | Add-a-kind edits two places |
| A13 | Low | Interface-per-class, no-op catch, triplicated mapping, O(n) lookup, parser silent-Unknown, hardcoded CORS | Accumulating friction; low individual cost |

---

## Refactoring Opportunities (ranked by ROI)

**1 — Highest ROI · Atomic durable writes (A1, A6).** Effort: ~1 day · Risk: Low · Benefit: eliminates an entire corruption class. Add `JsonFileStore<T>` with temp-write + `File.Move(overwrite:true)`; retrofit all four stores and `PromoteAsync`.

**2 — High ROI · Slim the composition root (A4, A11, A13).** Effort: 1–2 days · Risk: Low · Benefit: deletes ~600 lines of boilerplate, gives per-feature ownership. Central `ProblemDetails` exception mapping + `Map*Endpoints` extensions + `AddCommandCenter()`.

**3 — High ROI · Centralize state transitions (A2).** Effort: 2–3 days · Risk: Medium (touches hot paths, but tests cover) · Benefit: single source of truth for both subsystems' invariants; kills the recurring anti-pattern.

**4 — Medium ROI · Decompose the two god orchestrators (A3, A5).** Effort: 3–5 days · Risk: Medium · Benefit: testable, single-responsibility units; extract input-provider/command-handlers and the hidden side effect.

**5 — Medium ROI · Strongly-typed IDs (A8).** Effort: 2–3 days (mechanical, wide) · Risk: Low · Benefit: compile-time ID safety, centralized validation.

**6 — Low ROI · Per-repo keyed locking, heuristic tables, parser diagnostics (A7, A9, A10, A12).** Effort: small each · Risk: Low · Benefit: incremental scalability/clarity; do opportunistically.

---

## Architectural Strengths (preserve these)

1. **Genuine test architecture.** ~192 xUnit facts/theories across 20 files exercise stores, services, endpoints, git/process/codex. `FakeExecutionProvider`, `NoopExecutionProvider`, and `MemoryArtifactStore` are disciplined **test doubles**, not production fallbacks (DI wires the real implementations). This retroactively justifies most of the one-impl interfaces.
2. **Clean git/process abstraction with correct async.** `GitService` shells out exclusively through `IProcessRunner` and uniformly converts non-zero exits to `InvalidOperationException` (`GitService.cs:28,37,89,98,107,129`); `ProcessRunner.RunAsync` starts both `ReadToEndAsync` tasks *before* `WaitForExitAsync`, avoiding the classic pipe-buffer deadlock.
3. **Clean dependency direction & DI hygiene.** Zero `GetService`/`GetRequiredService` at runtime, no captive-dependency lifetime traps, immutable `init`-only models, and an **optimistic-concurrency check** that re-validates the git status snapshot before commit (`ExecutionSessionService.cs:381-384`).
4. **Consistent persistence idiom & ubiquitous language.** Every store follows the same `IStore` + `FileSystem*`/`Memory*` + JSON + `SemaphoreSlim`/`ConcurrentDictionary` shape; suffix conventions (Service/Store/Result/State/Snapshot/Request) are consistent; `InformationTier`, `DecisionTaxonomy`, and `SemanticChangeType` are real domain concepts, not DTO noise.
5. **Pure, deterministic analytical core.** Compression, diff, and decision analysis have no DateTime/Guid/IO/Random — time and IO are confined to Generation, Lifecycle, Store, and Diagnostics. Excellent for verification.
6. **Centralized path-safety invariant.** `ArtifactPath` enforces repository-root containment (path-traversal guard) in one place (`ArtifactPath.cs:11-23`) — the model for how the primitive-obsession items (A8) should be handled.

---

## Final Assessment

**1. Single most important architectural improvement.** Centralize state transitions (A2). The *"two parallel enums + scattered `if` guards"* pattern was independently reinvented in both major subsystems; a single transition table per subsystem gives one source of truth for invariants and is the prerequisite that makes decomposing the god services (A3, A5) safe.

**2. Largest maintainability risk.** Divergent change concentrated in `ExecutionSessionService` (A3) and `Program.cs` (A4). Almost every Execution feature edits the former; every endpoint and error-policy change edits the latter. These two files are where new contributors will struggle and where merge conflicts will cluster.

**3. What should remain unchanged.** The test architecture and DI seams, the git/process abstraction, the immutable-model + optimistic-concurrency commit path, and the consistent per-feature store idiom. Do **not** introduce an ORM, do **not** collapse the interfaces the tests actually fake, and do **not** "fix" the deterministic analytical core — just harden the writes underneath it.

**4. Debt that becomes expensive after 2+ years.** A1 (non-atomic single-file persistence) and A2 (dual state machines). As session/proposal volume grows, A1 turns latent corruption into real data-loss incidents; as more contributors touch the transition guards, A2 turns into a steady stream of state-drift bugs. Both are cheap to fix now and costly to fix once data and contributors accumulate around them.
