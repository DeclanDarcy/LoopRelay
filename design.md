# Design: Plan Authoring → Execution → Decision-Loop UI

Status: proposed
Owner: declandarcy
Companion: `refactor-plan.md` — the migration that extracts the shared `CommandCenter.Agents` runtime. Its load-bearing content (the revised invariant and the build order) is **inlined below**, so this document is self-contained and does not require reading it.
Prompt source of truth: `C:\claude-repos\Temp\CommandCenter\CommandCenter.App` PoC + the `Lib.Prompts` generator at `C:\kernritsu\dotnet-libraries\Lib.Prompts`

---

## 1. What this builds

When a repository has **no `.agents/plan.md`**, the repository view shows a **Plan Authoring** screen instead of the normal dashboard. From that one screen the user drives a full lifecycle without leaving it:

1. Author a **Roadmap** + any number of **Specs**, then **Write Plan** (against an existing codebase, or for a new one).
2. Watch codex stream the plan, **Revise** it via feedback as many times as needed.
3. **Execute Plan** — which closes the planning session, extracts milestones, commits/pushes, starts execution, and then enters a **decision loop**: every operational handoff is fed to a held-open, zero-permission **Decision Session** codex process that proposes decisions; the user edits and **Submits**; execution continues; the **router** decides whether the next decision turn **reuses** the warm decision process or **transfers** operational context into a fresh one.

This realizes the architecture's governing invariant. Stated in full so this document stands alone:

> **Revised invariant.** A Decision Session and an Operational (Execution) session are distinct session *roles*, but **both are backed by a real codex process.**
> - **DecisionSession ≠ Operational.** The Operational session is where coding work is done (edit, commit, push). A Decision Session is a *separate* codex process that reasons over the questions/issues an operational session raised in its handoff and emits decisions. It does no coding, commit, or push work.
> - **A Decision Session MUST involve a codex process.** It is not a deterministic template engine and not a read-only telemetry layer.
> - **Separation is preserved by a shared runtime, not by isolation.** `CommandCenter.DecisionSessions` must not reference Execution's *operational orchestration* (its `ExecutionSessionService`, git/commit/push lifecycle). Both roles instead depend on a shared, role-agnostic agent runtime — `CommandCenter.Agents` — that owns codex spawning, streaming, and lifecycle.
> - **The router governs the real process.** The lifecycle policy decides, based on token count, whether the decision session **reuses the active warm codex process** or **transfers operational context** into a fresh one.

In one line: the two session roles differ by **sandbox profile** (§4), not by being prevented from running codex.

---

## 2. Grounding inventory — exists vs new

| Concern | Today | This design needs |
|---|---|---|
| Prompt classes | `Lib.Prompts` generator exists; **no prompts in CommandCenter.Core** (agent confirmed `CommandCenter.Core.Prompts` is absent) | Add the 11 PoC `.prompt` files under `src/CommandCenter.Core/Prompts/` + wire `Lib.Prompts` (§3) |
| Codex spawn | `ProcessRunner.StartAsync` one-shot; stdin **closed** after first write (`ProcessRunner.cs:97-102`); `codex exec --cd <repo> -`; `SupportsReattach => false` (`CodexExecutionProvider.cs:13`) | **Persistent/interactive** process that accepts multiple stdin turns + **effort** + **sandbox profile** knobs (§4) |
| Effort / sandbox | none — no `model_reasoning_effort`, no `--sandbox`/approval flags anywhere | Two sandbox profiles + two effort tiers, passed as codex `-c` config (§4) |
| Streaming | observer → `ExecutionMonitoringService` → `Channel` → SSE `GET /api/execution-sessions/{id}/events` (`ExecutionSessionsEndpoints.cs`) | Reuse the SSE pattern for 3 stream kinds: planning, execution, decision (§10) |
| Repo state | no `Repository.State`; `RepositoryExecutionState { Ready, Executing, AwaitingAcceptance, AwaitingCommit, AwaitingPush, Failed, Cancelled }` on `ExecutionSession` | Add `PlanAuthoring` + `ExecutingPlan` repo-level states (§5) |
| `.agents` IO | `HandoffService` owns `handoff.md` + 4-digit rotation via `IArtifactStore`; `operational_context.md` referenced in `DecisionReasoningCaptureService.cs:206` | New writes: `specs/roadmap.md`, `specs/s{n}.md`, `operational_delta.md`, run-scoped handoff/decisions rotation (§7) |
| Router | `IDecisionSessionLifecyclePolicy.EvaluateAsync(repoId)` → `Continue`\|`Transfer` from `ReuseScore`/`TransferScore`; `DeterministicTokenEstimator.EstimateTokenCount` `(len+3)/4` (no real tokens) | Feed the **active decision session's** token count in; act on `Continue`→reuse, `Transfer`→new-session sequence (§9) |
| Memory cache | **absent** — no `IMemoryCache`/`AddMemoryCache` anywhere | Add `services.AddMemoryCache()` + `RepositoryId:Plan` entry (§8) |
| UI | React 19 + TS + Tauri 2.9 + Vite; `SelectedRepositorySummary.tsx`; `useExecutionEvents` consumes SSE; conditional-by-state rendering exists | New `features/planning/PlanAuthoringScreen.tsx` + a reusable stream view + clipboard button (§11) |
| Orchestrator | none — endpoints are stateless request/response | New **singleton, per-repository orchestrator** holding the open processes + run state across HTTP calls (§6) |

### Build order — the shared-runtime extraction this rests on

This design adds a UI and an orchestrator on top of a shared codex runtime (`CommandCenter.Agents`) that does **not exist yet**. That runtime and its decision-session consumer are extracted by the companion migration; the steps are summarized here so this document stands alone, and each names where this design uses it. The flow cannot land before its prerequisite steps:

| Step | Deliverable (summarized) | Where this design uses it |
|---|---|---|
| **0 — ratify** | Governance record of the reference-architecture change (the invariant above) | The screen + decision loop are the *concrete reason* for it; ratify first |
| **1 — extract `CommandCenter.Agents`** | role-agnostic `IAgentProvider` / `IProcessRunner` / `AgentSessionSpec{role}`, moved out of `Execution` with **no behavior change** | Every codex call in §12 routes through it; the orchestrator depends on it, **never** on Execution↔DecisionSessions directly |
| **2 — persistent/resumable codex** | a held-open process accepting multiple stdin turns + real token accounting | The plan-authoring and decision-session processes (§4) require it — **highest-risk prerequisite** |
| **3 — decision session drives codex** | a decision-session runner that reads the operational handoff and runs codex; **codex becomes the decision authority** and the deterministic `CommandCenter.Decisions` template/scoring services demote to an **offline fallback** | §12 steps 11–13 *are* this runner; `GetNextDecisions` output supersedes the template engine (§6) |
| **4 — wire router to the process** | `Continue`→reuse the warm process; `Transfer`→inject the continuity artifact as the new process's **seed**; real token counts replace estimates | §9 / §12 step 17 |
| **5 — realign observability/docs** | DecisionSessions telemetry reads real session+process state | The streams in §10 carry real per-turn telemetry |

---

## 3. Prompt library (`Lib.Prompts`) — canonical setup

The PoC proves the intended mechanism. `Lib.Prompts` is a Roslyn **incremental source generator** referenced as an analyzer (`OutputItemType="Analyzer" ReferenceOutputAssembly="false"`, or as `PackageReference … PrivateAssets="all"`). Its auto-wiring (`build/Lib.Prompts.props`) feeds `Prompts\**\*.prompt` as `<AdditionalFiles>` and emits one class per file.

**Wiring into `CommandCenter.Core` (one of):**
- `<PackageReference Include="Lib.Prompts" Version="…" PrivateAssets="all" />` (props auto-import), **or**
- the PoC's source-tree form: `ProjectReference … OutputItemType="Analyzer" ReferenceOutputAssembly="false"` + `<Import>` of `Lib.Prompts.props`/`.targets`.

**Generation rules** (from `PromptSourceGenerator.cs`):
- Namespace = `$(PromptRootNamespace|RootNamespace)` + folder path under `Prompts/`. With `CommandCenter.Core`'s root namespace and files in `Prompts/`, classes land in **`CommandCenter.Core.Prompts`** — exactly what the design references.
- Class name = PascalCased file name. Each class is `public static partial class` with `const string Template`, `const string SourceHash`.
- **No `{placeholder}`** → `public const string Text` (+ `Render()`).
- **Has `{placeholder}`** → `public static string Render(string? p1, …)`, parameters in **first-appearance order**, `null` → empty string.
- Syntax: `{name}` (valid C# identifier), `{{`/`}}` = literal braces. Malformed templates are **build errors** (`PROMPT001-004`).

**The 11 prompts (verified in the PoC) and their generated members:**

| Prompt file | Generated member | Placeholders (in order) |
|---|---|---|
| `WritePlanForNewCodebase.prompt` | `.Text` | — |
| `WritePlanAgainstCodebase.prompt` | `.Text` | — |
| `ExtractMilestones.prompt` | `.Text` | — |
| `ProduceOperationalDelta.prompt` | `.Text` | — |
| `UpdateOperationalContext.prompt` | `.Text` | — |
| `RevisePlan.prompt` | `.Render(feedback)` | `{feedback}` |
| `StartExecution.prompt` | `.Render(plan)` | `{plan}` |
| `GetNextDecisions.prompt` | `.Render(handoff)` | `{handoff}` |
| `StartDecisionSession.prompt` | `.Render(operationalContext)` | `{operationalContext}` |
| `StartDecisionSessionFromTransfer.prompt` | `.Render(operationalContext)` | `{operationalContext}` |
| `ContinueExecution.prompt` | `.Render(plan, handoff, decisions)` | `{plan}`, `{handoff}`, `{decisions}` |

**Two reconciliations between the design prose and the canonical `.prompt` files** (the `.prompt` files win — they are the source of truth the user pointed at):
- Design said `RevisePlan.Text`; canonical is **`RevisePlan.Render(feedback)`** — pass the **Feedback** textarea text. (Natural: the Revise button is already gated on that textarea.)
- Design said `StartExecution.Text`; canonical is **`StartExecution.Render(plan)`** — pass the cached plan text.

These are recorded here so implementation uses the generated signatures, not the prose shorthand.

---

## 4. Codex process model

Each prompt is delivered to a codex process under one of **two sandbox profiles** and one of **two effort tiers**:

| Profile | Sandbox intent | Used by |
|---|---|---|
| **Operational** (can edit repo + `.agents/`) | codex `workspace-write`, approvals off | WritePlan/Revise, ExtractMilestones, StartExecution, ContinueExecution, UpdateOperationalContext |
| **Decision** (zero permissions, stdin↔stdout only, no tools) | most-restrictive codex sandbox (`read-only`, approvals `never`, no MCP/tools) | StartDecisionSession(+FromTransfer), GetNextDecisions, ProduceOperationalDelta |

| Effort | codex config | Used by |
|---|---|---|
| **ExtraHigh** | `-c model_reasoning_effort="xhigh"` *(verify exact token vs installed codex)* | WritePlan/Revise, ExtractMilestones, UpdateOperationalContext, StartDecisionSession(+FromTransfer) |
| **Medium** | `-c model_reasoning_effort="medium"` | StartExecution, ContinueExecution |

**Lifetime — two kinds:**
- **One-shot** (today's model): prompt in → stream → exit. Used by ExtractMilestones, StartExecution, each ContinueExecution, UpdateOperationalContext. The existing `ProcessRunner.StartAsync` already streams; it just needs the effort/sandbox args.
- **Persistent / interactive** (NEW — the load-bearing gap): a held-open process that accepts **multiple sequential prompts on stdin**, streaming each turn's output until a turn-complete signal, without closing stdin. Used by the **plan-authoring** process (Write → Revise* → Execute closes it) and the **decision session** process (Start → GetNextDecisions → … → reuse writes another GetNextDecisions). 

This persistent mode **cannot** be `codex exec --cd <repo> -` (it closes stdin at `ProcessRunner.cs:101`, and `exec` is non-interactive one-shot). It requires codex's interactive/app-server protocol (`codex proto` / app-server / MCP — **must be validated**; this is the highest-risk prerequisite, build-order step 2, and a codex **MCP/server session owned by `CommandCenter.Agents` is the preferred mechanism**). Concretely this design needs, in `CommandCenter.Agents`:
- `IAgentProcess` with `WritePromptAsync(text)`, a per-turn stdout stream, `TurnCompleted`, and `Dispose`/close — keyed by session id in a process registry.
- `ProcessRunner` change: do **not** close stdin in persistent mode; expose `WriteAsync` for subsequent turns.
- `AgentSessionSpec { Role (Operational|Decision), Effort, SandboxProfile, WorkingDirectory, SessionId }` (the shared-runtime spec from build-order step 1, extended here with `Effort` + `SandboxProfile`).

If persistence proves infeasible, degrade: planning Revise re-runs a one-shot seeded with the current plan + feedback; decision sessions become per-turn one-shots and the router is forced to **transfer-only**. Functional, lower fidelity.

---

## 5. Repository lifecycle state

There is no repository-level state field today; state is derived on `ExecutionSession` via `RepositoryExecutionState`. This pre-execution flow precedes any `ExecutionSession`, so add a repository-level lifecycle owned by the orchestrator:

- `PlanAuthoring` — `!File.Exists(.agents/plan.md)` (or plan exists but not yet executed). Drives showing the screen.
- `ExecutingPlan` — set after Execute Plan's commit/push (the design's literal "set repository state to ExecutingPlan").

Add both to `RepositoryExecutionState` (or a new `RepositoryLifecycleState` if mixing with session states is undesirable) and persist via the orchestrator's store. The screen's visibility gate is **`!File.Exists(.agents/plan.md)`**, surfaced by a new `GET /api/repositories/{id}/plan/status` returning `{ planExists, state }`.

---

## 6. Server-side orchestrator

The flow holds processes open **across multiple HTTP requests** (Write → Revise → Execute → Submit → …) and pauses for human edits. That cannot be stateless. Introduce a **singleton, per-repository orchestrator** (a registry keyed by `repositoryId`) that owns:

- the **open plan-authoring** process and the **open decision-session** process (handles, not re-spawned per request);
- run state: cached `plan`, current `handoff`, current `decisions`, the **iteration counter** (handoff/decisions sequence), router inputs;
- the SSE channels for planning / execution / decision streams.

**Placement (preserves the invariant):** the orchestrator is a **composition-root** concern (Backend, or a new `CommandCenter.Orchestration` project). It depends on `CommandCenter.Agents` (processes), `IArtifactStore`/handoff rotation (`.agents` IO), `IDecisionSessionLifecyclePolicy` + token estimator (router), git, and the memory cache. **`CommandCenter.DecisionSessions` and `CommandCenter.Execution` still never reference each other** — both reach codex only through `CommandCenter.Agents`, and the orchestrator composes them. This is allowed: Backend already composes all contexts.

The orchestrator's decision-session handling (§12 steps 11–13 and 17) **is** the decision-session runner: codex output (`GetNextDecisions`) is the **authority** for the decisions the user reviews, while the deterministic `CommandCenter.Decisions` services (option templates, weighted-sum scoring) are kept only as an **offline/fallback** path — not the live source.

It is effectively a hosted singleton (process handles must outlive a request and be disposed on cancel/shutdown — extends today's process orphan/recovery handling).

---

## 7. `.agents` filesystem writes

All writes go through `IArtifactStore` (`Path.Combine(repoPath, rel.Replace('/', sep))`), matching `HandoffService`.

| Path | When | Notes |
|---|---|---|
| `.agents/specs/roadmap.md` | Write Plan | from Roadmap textarea |
| `.agents/specs/s{n}.md` | Write Plan | `n` = spec index in the group (new dir) |
| `.agents/plan.md` | written **by codex** | orchestrator **verifies existence** after the planning turn |
| `.agents/operational_context.md` | Execute Plan | copy of `plan.md`; also cached (§8); rewritten by UpdateOperationalContext on transfer |
| `.agents/handoff.md` | written by each Operational turn | verified, read into `handoff`, then **moved** to history |
| `.agents/handoffs/handoff.{NNNN:0000}.md` | after each Operational turn | `0001` after StartExecution, `0002` after first ContinueExecution, … |
| `.agents/decisions/decisions.{NNNN:0000}.md` | on Submit | `0001`, `0002`, … per loop iteration |
| `.agents/operational_delta.md` | router → transfer | captured stdout of ProduceOperationalDelta |

**Divergence from `HandoffService` (flag explicitly):** the existing `ProcessProviderCompletionAsync` archives the *previous* handoff, keeps `handoff.md` in place, and transitions to **`AwaitingAcceptance`** (a human gate). This design instead **moves** the current `handoff.md` into `handoffs/handoff.000N.md` and proceeds automatically into the decision loop — **no `AwaitingAcceptance` gate**; the human gate moves to the **decision Submit** step. The orchestrator owns a **run-scoped monotonic counter** (`0001…`) rather than `HandoffService`'s next-free-sequence scan; reuse the 4-digit `{n:0000}` format so artifacts coexist. Decide whether to bypass or reuse `HandoffService` here (recommend a dedicated orchestrator rotation to avoid the `AwaitingAcceptance` side effect).

---

## 8. Memory cache

Add `services.AddMemoryCache()` in `Program.cs`. At Execute Plan, store the plan text under key **`{repositoryId}:Plan`**. `ContinueExecution.Render(plan, handoff, decisions)` reads `MemoryCache.Get("{repositoryId}:Plan")` for `plan` each iteration (avoids re-reading `operational_context.md`/`plan.md` and survives plan-file mutation). Entry lifetime = the execution run; evict on run completion/cancel.

---

## 9. Router wiring (reuse vs transfer)

After each ContinueExecution turn + handoff rotation, the orchestrator calls **`IDecisionSessionLifecyclePolicy.EvaluateAsync(repositoryId)`**, feeding the **current active decision session's token count** (the design's "token count of current active decisions session"). Today that count comes from `DeterministicTokenEstimator.EstimateTokenCount(string?)` over the accumulated decision-session transcript; **real token accounting from the live process is a later upgrade (build-order step 4)** — the deterministic estimate stays as the fallback.

- **`Continue` → reuse:** write `GetNextDecisions.Render(handoff)` to the **still-open** decision process's stdin → stream → §12 decision sub-flow.
- **`Transfer` → new session:** the transfer sequence in §12. The concrete **continuity artifact** (grounding `DecisionSessionContinuityArtifact`) is the pair: `operational_delta.md` (captured) + the rewritten `operational_context.md` (the transfer **payload**, not audit-only). `IDecisionSessionTransferEligibilityService.CheckAsync` (`Eligible`/`Blocked`/`Deferred`) gates whether the transfer is allowed before spawning.

---

## 10. Backend endpoints (new) + SSE

Mirror the existing `repositories/{id}/…` + SSE conventions:

- `GET  /api/repositories/{id}/plan/status` → `{ planExists, state }` (screen gate).
- `POST /api/repositories/{id}/plan/write` → body `{ roadmap, specs[], newCodebase }`. Writes specs, opens planning process, returns a `planSessionId`.
- `POST /api/repositories/{id}/plan/revise` → body `{ feedback }`. Writes to the open planning process.
- `GET  /api/repositories/{id}/plan/stream` → SSE planning output (reuse `ExecutionSessionsEndpoints` SSE shape).
- `POST /api/repositories/{id}/plan/execute` → closes planning process, runs the Execute pipeline (§12), returns an `executionRunId`.
- `GET  /api/repositories/{id}/execution/stream` → SSE operational-turn output (existing pattern).
- `GET  /api/repositories/{id}/decision/stream` → SSE decision-session output.
- `POST /api/repositories/{id}/decision/submit` → body `{ decisions }`. Persists + continues the loop (§12).

All three streams are the existing `text/event-stream` + `data: {json}\n\n` shape from `ExecutionSessionsEndpoints.cs:81-103`, consumed UI-side like `useExecutionEvents`.

---

## 11. UI — the Plan Authoring screen

React + TS, new `src/CommandCenter.UI/src/features/planning/PlanAuthoringScreen.tsx`, shown when `plan/status.planExists === false` (the host already renders conditionally by state). API calls added to `src/api/` (`planning.ts`), streams via a hook modeled on `useExecutionEvents`.

**Layout & controls:**
- **Roadmap** textarea → `specs/roadmap.md`.
- **Specs** group: N textareas, each → `specs/s{index}.md`; an **Add Spec** control for unbounded specs.
- Bottom bar: **New Codebase** checkbox (default **unchecked**) + **Write Plan** button.
  - **Write Plan disabled until Roadmap has non-empty text.**
  - On click: persist spec files, then `WritePlanForNewCodebase.Text` (checked) or `WritePlanAgainstCodebase.Text` (unchecked) → planning process (ExtraHigh). Stream to the display.
- After the planning turn: verify `plan.md`, render it, and a **Copy** button using a **copy icon (no text label)** (Tauri clipboard / `navigator.clipboard`).
- **Feedback** textarea: when non-empty, enables **Revise Plan** → `RevisePlan.Render(feedback)` to the open planning process; stream.
- **Execute Plan** button → the Execute pipeline (§12).
- Once executing: the same screen hosts the **decision loop** — a read-only live stream that, on turn completion, **swaps into an editable textarea** seeded with the streamed `decisions`, plus a **Submit** button.

**State machine (UI):** `Authoring → Planning(streaming) → PlanReady → (Revising… → PlanReady)* → Executing(milestones→commit→startExec, streaming) → DecisionStreaming → DecisionEditable → (Submit → ContinueStreaming → router → DecisionStreaming)* ` — interruptible.

---

## 12. Canonical orchestration sequence (the spec, grounded)

Legend: **[O]** = Operational sandbox, **[D]** = Decision sandbox (zero-perm); **XH**/**Med** = effort; **1-shot** vs **held-open**.

**A. Write Plan** (button enabled only when Roadmap non-empty)
1. Write Roadmap → `specs/roadmap.md`; each Spec → `specs/s{index}.md`.
2. **[O] XH held-open:** `WritePlanForNewCodebase.Text` if *New Codebase* checked, else `WritePlanAgainstCodebase.Text` → planning process. **Stream** to display.
3. On turn complete: **verify `.agents/plan.md` exists**; render it + **Copy icon** button. **Hold the process open.**

**B. Revise Plan** (enabled only when Feedback non-empty)
4. **[O] XH (same held-open process):** `RevisePlan.Render(feedback)`. **Stream.** Repeatable.

**C. Execute Plan**
5. **Close** the planning process.
6. Copy `plan.md` → `.agents/operational_context.md`; store plan text in memory cache `{id}:Plan`.
7. **[O] XH 1-shot:** `ExtractMilestones.Text`. **Stream**, close on finish.
8. **git commit -A && push**; set repo state **`ExecutingPlan`**.
9. **[O] Med 1-shot:** `StartExecution.Render(plan)`. **Stream**, close on finish.
10. On finish: **verify `.agents/handoff.md`**; read → `handoff`; **move** to `handoffs/handoff.0001.md`.
11. **[D] XH held-open**, zero-perm/no-tools: `StartDecisionSession.Render(File.ReadAllText(operational_context.md))`. **Do not stream yet.**
12. **[D] (same process):** write `GetNextDecisions.Render(handoff)`. Keep open. **Stream** → capture into `decisions`.
13. On turn complete: **swap** the read-only stream into an **editable textarea** (seeded with `decisions`) + **Submit**.

**D. Submit (decision turn → operational turn → route)**
14. Persist edited `decisions` → `decisions/decisions.0001.md` (then `0002…`).
15. **[O] Med 1-shot:** `ContinueExecution.Render(MemoryCache.Get("{id}:Plan"), handoff, decisions)`. **Stream**, close on finish.
16. **Verify `handoff.md`** → update `handoff` → move to `handoffs/handoff.0002.md` (then `0003…`).
17. **Router:** `EvaluateAsync(repositoryId)` with the active decision session's token count:
    - **`Continue` (reuse):** **[D]** write `GetNextDecisions.Render(handoff)` to the open decision process → go to step 12's stream/edit/Submit sub-flow.
    - **`Transfer` (new session):**
      a. **[D]** write `ProduceOperationalDelta.Text` to the open decision process → **capture stdout** → write `.agents/operational_delta.md`.
      b. **Close** the active decision session process.
      c. **[O] XH 1-shot:** `UpdateOperationalContext.Text` (reads delta + old context, rewrites `operational_context.md`). Close on finish.
      d. **[D] XH held-open**, zero-perm: `StartDecisionSessionFromTransfer.Render(File.ReadAllText(operational_context.md))`. **Do not stream yet.**
      e. **[D]** write `GetNextDecisions.Render(handoff)`. Keep open. **Stream** → `decisions` → editable textarea + **Submit** → loop to step 14.

---

## 13. Gaps, risks, prerequisites

1. **Persistent interactive codex (highest risk).** Steps 2/11/12/17 require a held-open process with multi-turn stdin. `codex exec -` cannot do this. Validate codex's interactive/app-server/MCP mode (build-order step 2; a codex MCP/server session owned by `CommandCenter.Agents` is the preferred mechanism). Fallback: planning-revise = re-run one-shot with plan+feedback; decisions = per-turn one-shots forcing router **transfer-only**.
2. **"No tools / zero permissions" decision sandbox.** codex has sandbox levels (`read-only` … `danger-full-access`) and approval modes, but no literal "disable all tools" switch. Grounded approximation = `read-only` + approvals `never` + no MCP servers; a stricter bound needs config validation. ProduceOperationalDelta running in the Decision sandbox (stdout-only) is consistent with this.
3. **Effort flag spelling.** Confirm `xhigh`/`medium` tokens against the installed codex (`-c model_reasoning_effort=…`). Surface effort as an enum on `AgentSessionSpec`.
4. **Real token accounting.** Router currently sees `(len+3)/4` estimates. Reuse-vs-transfer fidelity needs live token counts (build-order step 4). Deterministic stays as fallback.
5. **HandoffService divergence (§7).** This flow removes the `AwaitingAcceptance` human gate and uses run-scoped rotation. Decide: dedicated orchestrator rotation (recommended) vs. reusing `HandoffService` (which would mis-transition state).
6. **Repo state addition (§5).** `PlanAuthoring`/`ExecutingPlan` are new; touching `RepositoryExecutionState` interacts with M0.4 governance and contract/oracle fixtures (`repository-dashboard.generated.ts` has `executionState`) — budget governance + contract regen.
7. **Process lifecycle / leaks.** Orchestrator must dispose both long-lived processes on cancel/shutdown/error (extends existing process orphan recovery).
8. **Commit/push authorization.** Step 8 auto-commits and pushes; confirm this is desired without a confirmation gate (it is outward-facing and hard to reverse).
9. **Decision output contract.** `GetNextDecisions` returns free text streamed into the editable textarea. For deterministic, testable persistence, define a structured (JSON) decision schema codex must emit, parse it into the existing `DecisionProposal` model, and render *that* as the editable surface — so the user edits a structured object and `decisions/decisions.000N.md` is not opaque prose.
10. **Test non-determinism.** codex-authored plans/decisions are not byte-stable and cannot be asserted by golden-file/snapshot tests. Keep the deterministic `CommandCenter.Decisions` services (option templates + weighted-sum scoring) as the fixture/oracle path; exercise codex paths through integration tests only.
11. **Feature-flag the codex paths.** Ship the flow behind a provider/feature flag with the deterministic/offline path as the default until the codex paths certify. Each build-order step is independently revertible — step 1 is a no-op refactor, safe to ship alone.

---

## 14. Definition of done

From a repository with no `.agents/plan.md`: the user authors a roadmap + specs, generates and iteratively revises a plan via a **held-open codex process**, and on Execute the system extracts milestones, commits/pushes, runs execution, and sustains a **decision loop** where a **zero-permission decision codex process** (distinct from the operational turns) proposes user-editable decisions, and the **router measurably reuses the warm decision process or transfers operational context** based on its token count — with prompts sourced from `CommandCenter.Core.Prompts.*` generated by `Lib.Prompts`, and `DecisionSessions`/`Execution` reaching codex only through `CommandCenter.Agents`.
