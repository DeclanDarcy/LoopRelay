# Cognitive Architecture & Repository Legibility Audit — CommandCenter.Backend

**Auditor:** Repository legibility review (engineer-cognition lens)
**Date:** 2026-06-20
**Scope:** `C:\kernritsu\CommandCenter\src\CommandCenter.Backend` (single-assembly modular monolith; 135 `.cs` files across 7 flat feature folders)
**Question answered:** *How efficiently can a human engineer build, maintain, and update an accurate mental model of this system?*

This audit is deliberately distinct from the clean-code/architecture audit (`ARCHITECTURE_AUDIT.md`). The code may be correct; the question here is whether the **structure teaches the system** or forces engineers to reconstruct it by reading code.

---

## Executive Summary

| Dimension | Score | One-line basis |
|---|:--:|---|
| **Cognitive Architecture** | **5 / 10** | Folders mirror namespaces (real scaffolding), but each is a flat dumping ground hiding 6–8 subsystems; the actual architecture lives *inside* large files, not in structure. Organization is incidental, not intentional. |
| **Discoverability** | **6 / 10** | A *known* type is found instantly (one-type-per-file + consistent naming + namespace=folder). A *subsystem* cannot be discovered — you must already know the filename. |
| **Traceability** | **6 / 10** | Entry point is always trivially findable (everything routes through `Program.cs`), handlers delegate cleanly — but route names carry zero behavioral signal, and state changes split across multiple sites. |
| **Navigability** | **5 / 10** | Flat 58- and 50-file folders plus 700–800-line god files mean you *scroll and search*, not *navigate*. No sub-folders, no `#region`, no in-file map. |
| **Mental Model Clarity** | **5 / 10** | The model the structure implies (clean feature modules) diverges sharply from the model engineers actually build (file-seas + hidden subsystems + scattered state machines). |

**Headline:** The repository is **excellent at location and poor at orientation.** Naming and one-type-per-file granularity make any *named* thing easy to open; but nothing in the structure tells a newcomer *what the subsystems are, where workflows live, or where state transitions are enforced*. The genuine architecture — state-transition models, git-porcelain parsers, information-tier classifiers — is hidden inside a handful of fat service files and two flat mega-folders. An engineer cannot predict structure; they navigate by search and prior knowledge.

---

## Repository Mental Model

### The model the repository *appears to intend*
A clean feature-sliced modular monolith. Seven vertical domains (`Execution`, `Continuity`, `Artifacts`, `Projections`, `Repositories`, `Planning`, `Configuration`), each a self-contained module with namespace = folder. One concept per file. Interfaces (`IFoo`) fronting implementations (`Foo`). A single composition root wiring it together. This is a coherent, guessable intent — a newcomer would correctly predict "git code is under Execution," "proposal logic is under Continuity."

### The model engineers *actually experience*
- **Two of the seven "modules" are file-seas.** `Execution` = 58 files in one flat folder; `Continuity` = 50. Inside each are 6–8 distinct subsystems with no structural separation (session lifecycle, git, process, providers, prompt-building, monitoring, persistence, handoff — all intermixed alphabetically).
- **Behavior is invisible from the route.** All 35 endpoints live inline in one `Program.cs::CreateApp` method; every handler is a thin exception-mapping wrapper that delegates the one meaningful line to a service. The route name tells you nothing — to learn *what happens*, you must open the service every single time.
- **The real architecture is buried inside big files.** The complete execution state-transition model is a `file static class ExecutionSessionMutation` at line 515 of a 787-line file. A full git-porcelain parser hides in `GitService`. An information-tier classification engine hides behind one public method in `UnderstandingCompressionService`. None of these load-bearing concepts has a structural home.
- **State lives in four scattered machines, two of them a parallel pair.** Understanding "what states a session can be in" requires reading 5–6 files; understanding the operational-context lifecycle requires reconciling **two enums** (`Status` + `ReviewState`) maintained in lockstep across 6 files, with no shared validator.

**The gap:** intended *feature modules* vs. experienced *flat file-seas with hidden subsystems and scattered state*. The structure under-describes the system by roughly one level of organization.

---

## Traceability Findings

### C1 — Flat mega-folders hide 6–8 subsystems each
- **Severity:** Critical
- **Category:** Discoverability / Navigation
- **Evidence:** `Execution/` = 58 files, 0 subdirectories; `Continuity/` = 50 files, 0 subdirectories. Clustering by responsibility reveals latent subsystems with no structural home: in `Execution` — session-lifecycle, git, commit, process-runner, providers, execution-context, prompt-building, monitoring, persistence, handoff (~8); in `Continuity` — proposals/lifecycle, generation/model, understanding/compression, parsing, decisions, diagnostics/reporting (~6).
- **Human impact:** To find git code an engineer scans 58 filenames. The folder communicates "Execution stuff" but not the six-to-eight concerns within it. Newcomers cannot form a subsystem map from the tree; they must read filenames linearly and infer clusters themselves — exactly the reconstruction this audit aims to eliminate.
- **Recommendation:** Sub-folder both by latent subsystem (`Execution/{Sessions,Git,Process,Providers,Context,Prompting,Monitoring,Stores,Handoff}`, `Continuity/{Proposals,Generation,Understanding,Parsing,Decisions,Diagnostics}`). Pure file moves; namespaces already isolate concerns enough that this is low-risk. **Highest-leverage single change in the audit.**

### C2 — Route names carry zero behavioral signal; the entire API is one method
- **Severity:** High
- **Category:** Traceability / Workflow Visibility
- **Evidence:** `Program.cs` (713 lines) — all ~35 endpoints + ~28 DI registrations inside one `CreateApp` method (`:15`). Handlers are exception-mapping shells (65 catch blocks) delegating to services, e.g. start session `:495→:502`, commit `:560→:567`. No endpoint grouping; no per-domain routing files.
- **Human impact:** The endpoint is *always* findable (1 file) — but locating *one* route in a 713-line wall means scanning, not navigating, and the handler teaches nothing about behavior. "Where does committing actually happen?" is never answerable from `Program.cs`; you always open the service. The single composition root is also the only place the full interface→impl catalog exists, so wiring questions funnel here too.
- **Recommendation:** Split into per-domain `Map*Endpoints(this WebApplication)` extension files (`RepositoryEndpoints`, `ExecutionEndpoints`, `ContinuityEndpoints`, …). Routes become discoverable by file; the composition root shrinks to registrations.

### C3 — Four state machines, scattered; no transition lives in one home
- **Severity:** High
- **Category:** State Visibility
- **Evidence:** Four lifecycle enums: `ExecutionSessionState` (5 values, `ExecutionSessionState.cs:3`), `RepositoryExecutionState` (8 values, `RepositoryExecutionState.cs:3`), and the parallel pair `OperationalContextProposalStatus` (6) + `OperationalContextReviewState` (5). The first two are a **coupled pair** set together via `ExecutionSession.WithState(...)` but transitioned across **3 services** — `ExecutionSessionService.cs` (12+ guard/write sites incl. `:170,:209,:339-341`), `ExecutionMonitoringService.cs` (`:59-61,:188-221`), `HandoffService.cs` (`:84`). Guards are inline `if (state != X) throw` at 10+ call sites; **no transition table exists.** The OperationalContext pair is coordinated only by hand-written `WithReview`/`WithReviewState` helpers duplicated across `OperationalContextReviewService.cs` and `OperationalContextLifecycleService.cs`, with seeding in the store and generation service.
- **Human impact:** "What are the valid state progressions?" requires reading **5–6 files for execution** and **6 for operational-context**. Because the two OC enums must always agree but are enforced in separate places, an engineer cannot trust either enum alone and cannot see the combined rule anywhere. This is the single hardest thing to reason about in the repo.
- **Recommendation:** Give each machine one home — a transition table or a guarded `CanTransition`/`Transition` method on the aggregate — and collapse the OC dual-enum into one status or a single validated pair. (See `ARCHITECTURE_AUDIT.md` A2.)

### C4 — Load-bearing concepts hidden inside big files
- **Severity:** High
- **Category:** Cognitive Load / Concept Visibility
- **Evidence:** `ExecutionSessionMutation` — the complete immutable state-transition model (6 mutators: `WithState:517`, `WithDecision:567`, `WithCommitPreparation:614`, `WithCommitResult:657`, `WithPushResult:700`, `WithPushFailure:744`) — is a `file static class` buried below the orchestrator at line 515 of a 787-line file. `GitService.cs` hides a full git-porcelain parser/model (`ParseStatus:144`, `ParseBranchHeader:217`, `NormalizeGitPath:286`, private `ParsedGitStatus:404`). `OperationalContextGenerationService.cs` hides 17 private helpers (fingerprinting, decision-signal extraction, hashing). `UnderstandingCompressionService.cs` hides an information-tier classifier (`Classify:360`, `IsTransientExecutionNoise:140`). **Zero `#region` usage** project-wide — these concepts are delimited only by line position.
- **Human impact:** Major domain concepts (a state model, a git parser, a classification engine) have no name in the file tree. An engineer cannot find them by navigating; they discover them by scrolling to the bottom of a file they opened for another reason.
- **Recommendation:** Promote each to its own file/type (`ExecutionSessionMutation.cs`, `GitStatusParser.cs`, `InformationTierClassifier.cs`). Structural promotion, not rewrite.

### C5 — Persistence ownership is ambiguous and variable-depth
- **Severity:** Medium
- **Category:** Ownership
- **Evidence:** Three disk-writing mechanisms: `FileSystemExecutionSessionStore.cs` (raw `File.Create`, **all** sessions in one file), `FileSystemArtifactStore.cs` (behind `IArtifactStore`), `ApplicationConfigurationStore.cs` (raw `File.Create`). But **12 files call `artifactStore.WriteAsync` directly** (ArtifactService, ContinuityReportService, ContinuityDiagnosticsService, `FileSystemOperationalContextProposalStore`, HandoffService, PlanningService, RepositoryProjectionService, `Program.cs`, …). The proposal store indirects through `IArtifactStore`/`ArtifactPath` while the session store writes JSON directly.
- **Human impact:** "Where does X hit disk?" has no consistent answer. A newcomer reasonably starts at `FileSystem*Store.cs` and misses that most writes flow through a shared `IArtifactStore` sink with no per-aggregate ownership. Different engineers pick different starting files for the same question.
- **Recommendation:** Make persistence one-store-per-aggregate, or document the shared-sink pattern explicitly. (Overlaps `ARCHITECTURE_AUDIT.md` persistence findings.)

### C6 — Prefix saturation pushes the distinguishing word ~17 chars in
- **Severity:** Medium
- **Category:** Navigation / Cognitive Load
- **Evidence:** `Execution*` = 24 files; `OperationalContext*` = 23 files; `Commit*` = 7; `Decision*` = 5; `Continuity*` = 5. Two prefixes cover ~47 of the ~108 files in the two big folders — the distinguishing token begins ~17 characters into each filename, in an alphabetically sorted flat list.
- **Human impact:** Visual scanning is slow: a column of `OperationalContextProposal`, `OperationalContextProposalStatus`, `OperationalContextProjection`, `OperationalContextReview`, `OperationalContextReviewState`… reads as near-identical noise. The prefix duplicates the folder name (the namespace already says `Continuity`).
- **Recommendation:** Sub-foldering (C1) lets names shorten (`Proposals/Proposal.cs`, `Proposals/Status.cs`) since the folder restores context. Don't rename in isolation — pair with C1.

### C7 — Path-containment invariant is implemented twice
- **Severity:** Medium
- **Category:** Ownership
- **Evidence:** `Artifacts/ArtifactPath.cs` (`ResolveRepositoryPath` throws on root escape, `:9-22`) is the centralized containment guard, used by 5 callers. But `ExecutionSessionService.cs:492-505` performs its **own** parallel containment checks ("Selected paths must be repository-relative", "escapes the repository") instead of using `ArtifactPath`.
- **Human impact:** A security-relevant invariant has two homes. An engineer hardening path handling finds `ArtifactPath.cs` and may never discover the duplicate logic in `ExecutionSessionService`, risking divergent rules.
- **Recommendation:** Route the `ExecutionSessionService` checks through `ArtifactPath`.

### C8 — No wayfinding inside the backend; authored docs live offstage
- **Severity:** Medium
- **Category:** Discoverability
- **Evidence:** No README/CONTRIBUTING/ARCHITECTURE inside `CommandCenter.Backend` (the only markdown there is generated audit output). **Zero `<summary>` XML doc comments across 135 files.** Genuinely useful authored docs exist but at the **repo root**: `docs/architecture.md` (system split, service-ownership contract) and `docs/operational-context-schema.md`. The `CommandCenter.slnx` references only the Backend (+ tests); the 3-stack relationship (React UI / Rust-Tauri shell / .NET sidecar) is discoverable *only* via `docs/architecture.md`, not the solution or any `.csproj`.
- **Human impact:** An engineer who opens the backend has no in-place map and no doc-comments; cross-stack wiring is found by luck unless they happen on `docs/`. Navigation is by structure for *location*, by search for *behavior*, by luck for *system context*.
- **Recommendation:** Add a one-page `CommandCenter.Backend/README.md` mapping folder→responsibility and linking `docs/architecture.md`; add `<summary>` to the ~8 hotspot services.

### C9 — Single-public-method services mask large private domains
- **Severity:** Low
- **Category:** Concept Visibility
- **Evidence:** `OperationalContextGenerationService` (358 lines, 1 public method, 17 private helpers), `UnderstandingCompressionService` (402 lines, 1 public method, 15 private helpers), `ExecutionMonitoringService` (397 lines, hides nested `ExecutionProviderObserver:373`).
- **Human impact:** The public surface (one method) understates the conceptual weight (an entire heuristic engine). Engineers underestimate these files until they're deep inside them.
- **Recommendation:** Extract cohesive private clusters into named collaborators where they represent a real concept (classifier, fingerprinter, parser).

---

## Cognitive Debt Register

| ID | Severity | Description | Human Cost |
|----|----------|-------------|-----------|
| C1 | Critical | `Execution` (58) & `Continuity` (50) are flat folders hiding 6–8 subsystems each | Subsystem map must be reconstructed by scanning filenames; no newcomer can predict internal structure |
| C2 | High | All 35 endpoints inline in one `Program.cs::CreateApp`; route names carry no behavior | Every behavioral question forces opening the service; routing is scanned, not navigated |
| C3 | High | 4 scattered state machines (incl. a coupled pair + a parallel dual-enum) with no transition home | 5–6 files to understand one lifecycle; combined OC progression visible nowhere |
| C4 | High | State-transition model, git parser, tier classifier hidden inside big files (no `#region`) | Core concepts are undiscoverable by navigation; found only by scrolling |
| C5 | Medium | 3 stores + 12 direct `artifactStore.WriteAsync` callers; variable indirection depth | "Where does it hit disk?" has no consistent answer; engineers start in different places |
| C6 | Medium | Prefix saturation: `Execution*` 24, `OperationalContext*` 23 files | Slow filename scanning; near-identical rows read as noise |
| C7 | Medium | Path containment duplicated (`ArtifactPath` vs `ExecutionSessionService:492-505`) | Security invariant has two homes; one is easily missed |
| C8 | Medium | No in-backend README/doc-comments; system docs offstage at repo root | Behavior found by search, system context by luck |
| C9 | Low | Single-public-method services hiding 15–17 private helpers | Conceptual weight underestimated; surprise depth |

---

## Hidden Subsystem Analysis

### `Execution/` (58 files, flat)
- **Visible subsystem:** "Execution" (one undifferentiated bucket).
- **Hidden subsystems:** Session lifecycle (`ExecutionSession*`, `ExecutionSessionService`, recovery hosted service); Git (`GitService`, `RepositoryGitStatus`, `*DirtyState`, snapshots); Commit (`Commit*`, `Push*` — 9 files); Process (`ProcessRunner`, `ProcessRun/StartResult`); Providers (`*ExecutionProvider`, Codex resolver, Fake/Noop); Execution-context (`ExecutionContext*` — 7 files); Prompt-building (`ExecutionPrompt*`); Monitoring/events (`ExecutionMonitoringService`, `ExecutionEvent*`); Persistence (`FileSystemExecutionSessionStore`); Handoff (`HandoffService`).
- **Recommended structure:** `Execution/{Sessions, Git, Commit, Process, Providers, Context, Prompting, Monitoring, Stores, Handoff}`.

### `Continuity/` (50 files, flat)
- **Visible subsystem:** "Continuity."
- **Hidden subsystems:** Proposals & lifecycle (`OperationalContextProposal*`, `*ReviewService`, `*LifecycleService`, proposal store); Generation & model (`*GenerationService`, `OperationalContextDocument/Item/Section/Tier`); Understanding/compression (`UnderstandingCompressionService`, `UnderstandingDiffService`, ledger, snapshots); Parsing (`MarkdownOperationalContextParser`, semantic-change types); Decisions (`DecisionAnalysisService`, `Decision*`); Diagnostics/reporting (`ContinuityDiagnosticsService`, `ContinuityReportService`).
- **Recommended structure:** `Continuity/{Proposals, Generation, Understanding, Parsing, Decisions, Diagnostics}`.

### `Artifacts/` (10), `Projections/` (6), `Repositories/` (4), `Configuration/` (3), `Planning/` (3)
- **Visible = hidden = actual.** These are correctly sized (≤10 files); the folder name matches the single concept inside. **Leave unchanged** — they are the model the two big folders should aspire to.

**The pattern:** engineers naturally form a *subsystem-level* mental map (sessions vs git vs providers), but the filesystem only offers a *domain-level* map (Execution). The structure is one level coarser than human reasoning, and only in the two largest folders.

---

## Cognitive Hotspots (ranked by reasoning cost)

1. **`Execution/ExecutionSessionService.cs` (787 lines, 18 public methods).** The session orchestrator — engineers land here for *anything* session-related (recover/start/accept/reject/prepare-commit/commit/push). Hidden inside: the entire immutable state-transition model (`ExecutionSessionMutation`, `:515`) and a `SemaphoreSlim` concurrency gate. Why it's expensive: it is both the workflow hub *and* the secret home of the state machine, guarded behind a lock, with validation scattered through it. Elevate `ExecutionSessionMutation` and the per-operation handlers into structure.
2. **`Program.cs` (713 lines, one method).** Composition root *and* full routing table *and* the only complete interface→impl catalog. Every route question and every wiring question converges here, and handlers carry inline error-translation logic. Why it's expensive: two unrelated concerns (wiring + 35 routes across 6 domains) share one method with no internal boundaries.
3. **`Execution/GitService.cs` (414 lines, 6 public + 12 private statics).** Hides a complete git-porcelain parser/model (`ParseStatus`, `ParseBranchHeader`, `NormalizeGitPath`, private `ParsedGitStatus`). Why it's expensive: "git integration" and "git output parsing" are two concepts sharing one file; the parser has no name in the tree.

*Honorable mentions:* `UnderstandingCompressionService` (tier-classification engine behind one method), `OperationalContextGenerationService` (17 private helpers), `ExecutionMonitoringService` (nested provider-observer).

---

## Wayfinding Assessment

- **How engineers navigate today:** by **structure for location** (namespace = folder is a real, reliable aid; a known type is found instantly), by **search for behavior** (route names and one-method services force `grep`/go-to-definition to learn what anything *does*), and by **luck for system context** (the 3-stack architecture is documented only in `docs/architecture.md` at the repo root, reachable by chance).
- **Why navigation succeeds:** one-concept-per-file, consistent suffix grammar (`*Service/*Store/*Result/*State/*Request`), predictable `IFoo→Foo`, namespaces mirroring folders.
- **Why navigation fails:** two flat 50–58-file folders, prefix saturation, god files with no `#region`, state machines with no home, zero in-backend docs, zero doc-comments.
- **Missing cues:** sub-folders inside the big domains; per-domain endpoint files; named homes for the hidden subsystems (state model, git parser, classifier); an in-backend README; `<summary>` on hotspot services; a transition table per state machine.

---

## Final Assessment

**1. Why is it difficult to reason about this repository today?**
Because the structure stops one level short of how engineers actually think. The file tree describes *domains* (Execution, Continuity) but engineers reason in *subsystems* (sessions, git, providers, proposals, compression). The load-bearing concepts — state machines, parsers, classifiers — have no structural home and hide inside 400–800-line files. You can find any *named* thing instantly, but you cannot discover *what the system is made of* without reading it.

**2. What structural decisions create the highest cognitive load?**
(a) Letting two folders grow to 50–58 flat files instead of sub-foldering by subsystem (C1). (b) Putting all 35 endpoints in one method so routes carry no behavioral signal (C2). (c) Scattering four state machines — including a parallel dual-enum — across multiple services with no transition table (C3, C4). These three account for the bulk of the reconstruction cost.

**3. What organizational changes would most improve engineer reasoning?**
In priority order: **(i)** sub-folder `Execution` and `Continuity` by their latent subsystems (C1 — pure file moves, highest leverage); **(ii)** split `Program.cs` into per-domain endpoint modules (C2); **(iii)** give each state machine one home and collapse the OC dual-enum (C3); **(iv)** promote hidden concepts (`ExecutionSessionMutation`, git parser, tier classifier) to named files (C4); **(v)** add a backend `README.md` + `<summary>` on hotspot services (C8). The first three are structural and near-rewrite-free.

**4. What architecture should remain unchanged?**
The feature-folder + namespace-mirrors-folder scheme; one-concept-per-file granularity; consistent suffix naming; the `IFoo→Foo` convention; the clean shell-out boundary (`IProcessRunner`/`IGitService` — the one unambiguous ownership in the repo); centralized path containment in `ArtifactPath`; and the correctly-sized small folders (`Artifacts`, `Projections`, `Repositories`, `Configuration`, `Planning`) — these are the *target* model, not problems. The authored `docs/` files are good; they only need surfacing.

**5. What would an optimal cognitive architecture for this repository look like?**
The same feature-sliced monolith, but with **every folder sized like the good small ones** — each sub-folder a single visible subsystem of ≤~12 files. Routes discoverable per domain; each state machine with one named home and a visible transition table; each hidden engine (mutation model, git parser, tier classifier) promoted to a named file; a one-page backend README mapping folder→responsibility and linking the system docs; `<summary>` on the dozen files that carry real domain weight. In that repository, the **tree itself would teach the system**: open `Execution/`, see its subsystems; open `Sessions/`, see the lifecycle and its state home; never need to scroll an 800-line file to discover that a state machine lives there. The change is almost entirely **re-foldering and promotion, not rewriting** — the concepts already exist; they are simply not yet *visible as structure*.
