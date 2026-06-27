# Command Center — Runtime Object Model Audit

Status: architectural audit (runtime ontology reconstruction)
Date: 2026-06-26
Question answered: **What are the first-class runtime objects that together constitute a running Command Center — the stable runtime concepts the roadmap should ultimately be driven by, rather than the current project boundaries?**

Inputs treated as authoritative: the current implementation (`src/`, verified type-by-type), the completed MVP architecture and certified governance/contract/regression net (`.agents/`, `tests/`, M0.x–M1.2), the previous Vision Realization Audit (`vision-realization-audit.md`), the Refactor Readiness Analysis (`refactor-readiness.md`), and the proposed design (`design.md`).

The previous audit explained *how to evolve*. This audit explains *what ultimately exists*: the irreducible runtime objects the implementation and design are jointly evolving toward, each profiled completely, then assembled into a runtime topology, lifecycle, authority, ownership, persistence, recovery, communication, concurrency, failure, and evolution model — closing with the invariants and a minimalism review that reduces the ontology to only what is fundamental.

A note on method: every claim about *today* is grounded in a verified type (cited as `Type` / `file:line`). Every claim about *the target* is derived from the design and prior audits. The two are kept explicitly distinct so the runtime ontology is not confused with current code.

---

## Executive Runtime Assessment

**Command Center is not a collection of request handlers. It is a long-lived, per-repository runtime that today exists only *implicitly* — smeared across global singletons keyed by `repositoryId` — and the design's real work is to make that runtime *explicit*.** Every backend context is registered `Singleton` (verified: `Execution/Extensions/ServiceCollectionExtensions.cs`, `DecisionSessions`, `Workflow`, `Continuity`, `Reasoning`, `Backend/Program.cs`); there is no `AddMemoryCache`, no per-repository stateful object held across requests, and no process held open past a single turn (`ProcessRunner.cs:101` closes stdin; `CodexExecutionProvider.cs:13` `SupportsReattach => false`). The system already *behaves* as a per-repository runtime — `GetActiveSessionAsync(repositoryId)`, `EvaluateAsync(repositoryId)`, `WorkflowInstance` keyed by `(repositoryId, stage)`, `.agents/` artifacts namespaced per repo — but no object *is* that runtime. The repository runtime is a latent object the whole codebase gestures at and none of it names.

The reconstruction below resolves to **seven irreducible runtime objects** sitting above a settled domain model and a proven infrastructure substrate:

1. **Agent Runtime** — the role-agnostic process/stream/registry substrate (today's `ProcessRunner` + `CodexExecutionProvider` + `ExecutionMonitoringService`, relocated into `CommandCenter.Agents`).
2. **Agent Process** — the one genuinely new primitive: a held-open codex OS process bound to a sandbox profile. The single concentration point of all runtime risk.
3. **Repository Runtime** — the per-repository coordinator (the design's "singleton orchestrator"), the runtime object the system currently lacks a name for.
4. **Repository Run** — the active forward-drive within a Repository Runtime (plan → execute → decision-loop), externalized to `.agents/` as its own durable journal.
5. **Session** (three roles: **Planning**, **Operational**, **Decision**) — durable records whose *liveness* is a detachable Agent Process.
6. **Event Stream** — the live multi-subscriber broadcast (today's `Channel`-based `ExecutionMonitoringService`), generalized to three kinds.
7. **Runtime Supervisor** — the recovery/disposal authority (today's `*RecoveryHostedService` trio), generalized from "fail orphaned batch sessions" to "reconcile durable records against live processes."

The decisive structural finding is a **three-tier liveness model** that the current code already half-implements and the design must make a first-class rule: every concept exists as a **durable record** (reconstructed from JSON/`.agents`), a **live process** (ephemeral, never survives restart), or a **derived projection** (recomputed on demand). A *Session* is a durable record; its *codex process* is a live process; its *stream* is a projection. **A session survives restart; its process never does.** This single distinction answers nearly every recovery, persistence, and concurrency question below, and it is why the architecture is genuinely ready: the durable and projection tiers already exist and are certified; only the *live-process* tier and the object that owns it (Repository Runtime) are new.

**Verdict.** The runtime Command Center becomes is a **registry of per-repository runtimes, each holding at most one active run that drives a real codex agent through planning, execution, and a governed decision loop, with all forward-progress state externalized to an append-only `.agents/` journal so the live processes are always disposable and the durable record is always authoritative.** Six of the seven runtime objects already exist in reusable form; one (Agent Process) is new; one relationship (Repository Runtime → owns processes across requests) is the new composition. The ontology is small, and it is almost entirely already here — unnamed.

---

## Runtime Object Catalog

Each object is profiled against the full template (Identity, Purpose, Lifetime, Ownership, Authority, State, Relationships, Persistence, Recovery, Concurrency, Projection). Objects are ordered from substrate upward. For each, **Today** grounds it in verified code; **Target** states what it becomes.

### RO-1 · Agent Runtime

> *Today:* `ProcessRunner` (Singleton, `Execution/Services/ProcessRunner.cs`), `CodexExecutionProvider` (Singleton, `Modules/CodexExecutionProvider.cs`), `CodexExecutableResolver`, `ExecutionMonitoringService` (Singleton) — all living *inside* `CommandCenter.Execution`.
> *Target:* relocated verbatim (no behavior change) into `CommandCenter.Agents`, plus a persistent-process capability and an `AgentSessionSpec`.

- **Identity.** A single global object; not keyed. Identity never changes; it is process-wide infrastructure. It owns a **Process Registry** (keyed by `sessionId`) that *is* keyed.
- **Purpose.** Spawn, stream, hold, and dispose codex processes; resolve the executable; broadcast their output. It owns the *mechanics* of running an agent and nothing about *why*. This responsibility is not owned elsewhere because both session roles need it and neither may own it (owning it in `Execution` is the accident the relocation corrects — it is what currently forces `DecisionSessions` to be unable to run codex).
- **Lifetime.** Application lifetime. Created at composition root, disposed at shutdown. Survives repository reload and run completion; does **not** survive application restart (it is stateless across restarts — the live processes it held are gone).
- **Ownership.** Created by the composition root (`Backend`). Mutated by no one (it is a factory/supervisor); observed by every consumer that spawns a process. The Process Registry it holds is mutated only through its own spawn/dispose methods.
- **Authority.** Owns "how to run a codex process." Must **never** own: sandbox *policy* (which role gets which profile — that is `AgentSessionSpec` *data* supplied by the caller), git/commit/push (operational semantics, stays in `Execution`), decision semantics, or handoff rules. Its authority is purely mechanical.
- **State.** The Process Registry (`sessionId → IAgentProcess`) — ephemeral, in-memory, never persisted. Plus the streaming substrate's subscriber tables. No durable state.
- **Relationships.** Referenced by: Operational Session, Decision Session, Planning Session, Repository Run (all spawn through it). References: the OS process, the codex binary. **Forbidden:** referencing any domain context; referencing `Execution`'s operational orchestration; branching on role (`if (role == Decision)`).
- **Persistence.** None. Reconstructed empty on every start. Live handles are regenerated by re-spawning, never serialized.
- **Recovery.** On crash, every handle it held is gone. It comes back empty; recovery of *what was running* is the Supervisor's job against durable Session records, not the Agent Runtime's.
- **Concurrency.** Global singleton; the Process Registry bounds concurrent processes across all repositories. Internally thread-safe (today's services already serialize via channels/locks).
- **Projection.** Not directly projected. Its *output* is projected through Event Streams. Process IDs surface in `ExecutionSession.ProviderProcessId` for diagnostics only.

### RO-2 · Agent Process *(the one genuinely new runtime object)*

> *Today:* does not exist. `ProcessRunner.StartAsync` is one-shot — stdin closed at `ProcessRunner.cs:101`, `codex exec --cd <repo> -`, stream-to-exit, `SupportsReattach => false`.
> *Target:* `IAgentProcess { WritePromptAsync(text), per-turn stream, TurnCompleted, Dispose }`, keyed by `sessionId` in the Process Registry; a held-open codex app-server/MCP session owned by the Agent Runtime.

- **Identity.** `sessionId` (the session it is bound to). Identity is the binding: one Agent Process backs exactly one live Session. Identity is fixed at spawn and dies with the process.
- **Purpose.** Be the *liveness* of a Session — the held-open codex that accepts multiple sequential prompts and streams each turn to a turn-complete signal without closing the channel. It exists because the design's Plan-authoring (Write→Revise*) and Decision (Start→GetNextDecisions*) loops are multi-turn over one warm context, which `codex exec -` cannot express.
- **Lifetime.** Created when a Session needs to run codex; destroyed on Session close, transfer, cancel, error, or shutdown. **Cannot survive process restart** (it *is* an OS process), **cannot survive repository unload**, **cannot survive application restart.** It is the most ephemeral object in the system and the only one that holds an OS resource.
- **Ownership.** Created by the Agent Runtime (spawn), on behalf of a Session, at the request of the Repository Run. Disposed by the Repository Runtime (the only object allowed to hold its handle across requests) or the Supervisor (on shutdown/recovery). Mutated only by writing prompts. Observed via its Event Stream.
- **Authority.** Owns one live codex conversation under one immutable sandbox profile. Must **never** own: which prompt to send (the Run decides), what its output means (the consuming domain decides), or persistence (its transcript is captured to `.agents/`, not by the process).
- **State.** The live codex conversation + accumulated token count (the real number the router wants). Bound at spawn: `AgentSessionSpec { Role, Effort, SandboxProfile, WorkingDirectory, SessionId }`. **Sandbox profile is immutable for the process's life** — an Operational process can never become a Decision process. All ephemeral.
- **Relationships.** Owned by Agent Runtime; held by Repository Runtime; bound to one Session; observed through one Event Stream. **Forbidden:** two Sessions sharing one process; a process outliving its Session; a handle escaping to an endpoint.
- **Persistence.** None of the process. Its *output* is persisted as `.agents/` artifacts (handoff, decisions, delta) by the Run. The conversation itself is not serialized; on transfer it is *replaced*, its essence carried forward as the continuity payload (`operational_context.md`), not as a serialized process.
- **Recovery.** **Not recoverable.** On any interruption the process is gone. Recovery means: the durable Session record is reloaded, its live process is known-absent, and the Run either re-seeds a fresh process from the last durable artifact (decision role: cold-start from `operational_context.md`) or fails the session (operational role today: `Executing → Failed`, `ExecutionSessionService.RecoverAsync`). The held-open process is *designed to be lost* — that is what makes the `.agents/` journal load-bearing.
- **Concurrency.** Many may exist globally; per active repository, typically two held-open (one Planning *or* one Decision, plus transient one-shots for ExtractMilestones/StartExecution/ContinueExecution). Bounded by the Process Registry. **At most one held-open Decision process per repository** (mirrors one active Decision Session).
- **Projection.** Never exposed directly. Only its Event Stream is projected (planning/execution/decision SSE). Its PID is diagnostic.

### RO-3 · Repository Runtime *(the object the system currently lacks)*

> *Today:* does not exist as an object. Its responsibilities are scattered across Singleton services that re-derive everything from `repositoryId` per request. The design calls it the "singleton, per-repository orchestrator."
> *Target:* a hosted, per-repository coordinator held in a global registry keyed by `repositoryId`, owning the open processes, the active run, and the streams across HTTP requests.

- **Identity.** `repositoryId`. **This is the stable spine of the entire runtime ontology.** Identity is preserved across every request, every run, every session, every restart (the repo is reloaded under the same id). It changes only when the repository is unregistered. Every other runtime object's identity is *subordinate* to a Repository Runtime's.
- **Purpose.** Be the single coordinator that holds live process handles and run state *between* requests, so the Write→Revise→Execute→Submit→… flow can pause for human input without losing its processes. This cannot be stateless (the design's load-bearing reason for the object) and must be exactly one per repository (to avoid split-brain over the same `.agents/` tree and process handles).
- **Lifetime.** Created on first need (repository selected / run started), held for the repository's loaded life. Survives across requests and across runs. Does **not** survive application restart as a *live* object — but it is fully *reconstructable* from durable state (Session records + `.agents/` journal), which is what makes restart safe. Disposed on repository unload or shutdown (disposing its processes).
- **Ownership.** Created and disposed by a global **Repository Runtime Registry** (a hosted singleton — the generalization of today's `*RecoveryHostedService`). Mutated by the endpoints that drive the flow (write/revise/execute/submit), serialized per-repository (extends `ExecutionSessionService`'s `SemaphoreSlim(1,1)` from session-state to runtime-state). Observed by projection endpoints.
- **Authority.** Owns **composition and sequencing** — hold processes, route prompts, advance the run, fan out streams. **Must never become a domain authority** (the prior audit's highest structural risk). It must *call into* `DecisionSessions` (router/transfer), `HandoffService`/the rotation owner, the contract oracle, and git — never reimplement them. It is the conductor; the domains are the players.
- **State.** Held processes (Planning, Decision handles); the current Repository Run (or none); the per-repository serialization gate; the three Event Streams. The *content* state (plan, handoff, decisions, iteration counter) belongs to the Repository Run, not the Runtime. The Runtime's own state is the *handles and the gate*.
- **Relationships.** Holds: Agent Processes (via Agent Runtime), one Repository Run, three Event Streams. References: router, transfer-eligibility, continuity-artifact, HandoffService/rotation, git, Artifact Store, Session registries. Referenced by: the Registry, the endpoints. **Forbidden:** two Repository Runtimes for one `repositoryId`; a Runtime referencing another repository's runtime; a Runtime owning decision/handoff *semantics*.
- **Persistence.** The Runtime *object* is not serialized; it is reconstructed. What persists is everything it *coordinates*: Session records (JSON), the `.agents/` journal, repo lifecycle state. On restart the Registry rebuilds a Runtime by reading those.
- **Recovery.** On crash: the live Runtime and its processes are gone. The Registry, at startup, reconstructs a Runtime per repository with an active Session/run by reading durable state, then asks the Supervisor to reconcile (dispose-known-absent processes, fail or cold-start sessions, resume or abandon the run from the last `.agents/` artifact). **The Runtime is recoverable precisely because it owns no durable state of its own — only handles, which are meant to be lost.**
- **Concurrency.** **Exactly one per repository**, enforced by the Registry (keyed insert). Many across repositories. Internally single-writer per repository via the gate.
- **Projection.** Projected as repository lifecycle state (`PlanAuthoring` / `ExecutingPlan`, new) gating *which* surface is foregrounded, plus the three streams. The handles themselves are never exposed.

### RO-4 · Repository Run

> *Today:* the closest existing object is `WorkflowInstance` (keyed `(repositoryId, CurrentStage)`, `Workflow/Models/WorkflowInstance.cs`) driven by `WorkflowContinuationHostedService`'s periodic loop. Run *content* (plan, handoff, decisions) is re-read from `.agents/` per request; there is no single Run object.
> *Target:* the design's "run state" — cached plan, current handoff, current decisions, iteration counter, router inputs — made an explicit object owned by the Repository Runtime, journaled to `.agents/`.

- **Identity.** `executionRunId`, subordinate to `repositoryId`. A new Run begins at Execute Plan; a repository has **at most one active Run**. Identity is preserved across the entire decision loop (every Submit→Continue→route iteration is the *same* run); it changes only when a new plan is executed.
- **Purpose.** Be the *active forward drive* — the live state machine that moves a repository through `Planning → Executing → DecisionLoop(iterate)`. It exists to separate the *coordinator that always exists when a repo is loaded* (Repository Runtime) from the *drive that exists only while executing* (Repository Run). This split is what the new repo lifecycle states encode: `PlanAuthoring` = Runtime present, no Run; `ExecutingPlan` = Run active.
- **Lifetime.** Created at Execute Plan (after commit/push); lives through the decision loop; ends on completion/cancel. Survives across requests (it *is* the thing that pauses for human Submit). Survives application restart **only through its `.agents/` journal** — the Run object is reconstructed from the last `handoff.NNNN.md`, `decisions.NNNN.md`, `operational_context.md`, and `plan.md`. This externalization is the single most important persistence decision in the runtime.
- **Ownership.** Created, advanced, and ended by the Repository Runtime. Mutated only through the serialized gate (one turn at a time). Observed via the execution + decision Event Streams.
- **Authority.** Owns **run progress** — the iteration counter, the current phase, "what prompt comes next." Must **never** own: the *decision* (codex authors it, the user edits it), the *route* (the router decides reuse vs transfer), git semantics, or contract shape. It sequences; it does not decide.
- **State.** Cached `plan` (design's `{repositoryId}:Plan`), current `handoff`, current `decisions`, iteration counter, last router inputs, current phase. **Durable** via the `.agents/` journal (handoff/decisions rotation, operational_context). **Ephemeral** mirror in the Runtime for speed. The design's memory cache is the ephemeral mirror; the journal is the truth.
- **Relationships.** Owned by Repository Runtime; drives Operational Session turns and Decision Session turns; consults router + transfer-eligibility; writes through the Artifact Store / rotation owner. **Forbidden:** two active Runs per repository; a Run holding process handles directly (it asks the Runtime); a Run reaching across repositories.
- **Persistence.** Append-only `.agents/` journal: `specs/`, `plan.md`, `operational_context.md`, `handoffs/handoff.{NNNN}.md`, `decisions/decisions.{NNNN}.md`, `operational_delta.md`. The 4-digit rotation (`HandoffService.cs:144`) generalizes from "next-free scan" to a run-scoped monotonic counter. **One rotation owner** (a dedicated orchestrator rotation that does *not* fire `HandoffService`'s `AwaitingAcceptance` side-effect — `HandoffService.cs:92-97`).
- **Recovery.** On crash mid-loop: reconstruct the Run from the highest `handoff.NNNN`/`decisions.NNNN` and `operational_context.md`; the live processes are gone, so resume by cold-starting a fresh Decision Session from `operational_context.md` (transfer-style) and continuing from the last completed turn. A half-written turn (no `TurnCompleted`, no rotated artifact) is *discarded* and re-run — the journal's last *complete* entry is the resume point. This is the runtime expression of "the durable record is authoritative; the process is disposable."
- **Concurrency.** One active per repository. Turns are strictly serial within a run (operational turn → decision turn → route → next). Cross-repository runs are independent.
- **Projection.** The decision loop UI surface (read-only stream → editable decisions → Submit) and the repo state (`ExecutingPlan`). The iteration counter and journal are inspectable through the existing tabs.

### RO-5 · Session *(three roles, one shape)*

A **Session** is a durable record of a unit of agent work, bound (when live) to an Agent Process. The three roles differ only by **sandbox profile and what they may touch** — never by being prevented from running codex. This is the governing invariant, expressed as a runtime object.

> *Today:* `ExecutionSession` (Guid, durable JSON at `%APPDATA%\CommandCenter\execution-sessions.json`, `RepositoryExecutionState` state machine, `ExecutionSessionService` Singleton with `SemaphoreSlim`) for the operational role; `DecisionSession` (`DecisionSessionId`, durable JSON, `Created→Active→TransferPending→Transferred→Retired`, `IDecisionSessionRegistry`/`Repository` Singletons) for the decision role. **No Planning Session exists.** Critically, today neither role's record is bound to a *held-open* process — `ExecutionSession` records a one-shot's PID; `DecisionSession` records *no process at all* (it cannot run codex: `DecisionSessions` does not reference `Execution` — verified in the `.csproj`).
> *Target:* both roles bind to an Agent Process through the Agent Runtime; the Decision role *gains* codex without gaining a path to `Execution`.

**RO-5a · Planning Session** *(new)*
- **Identity.** `planSessionId`, subordinate to `repositoryId`. Exists only during plan authoring; one at a time.
- **Purpose.** Hold the warm Operational-sandbox process for Write→Revise*→Execute. Closed at Execute.
- **Lifetime.** Created at Write Plan, closed at Execute Plan. Does not survive restart (degrade: re-seed a one-shot with plan+feedback). Largely ephemeral — its *output* (`plan.md`, `specs/`) is the durable residue.
- **Authority.** None over the plan's *content* (codex authors it); it owns the *liveness* of authoring.
- **Persistence.** Not itself persisted; its artifacts (`specs/roadmap.md`, `specs/s{n}.md`, `plan.md`) are.
- **Concurrency.** One per repository (gated by `PlanAuthoring` state).

**RO-5b · Operational Session** *(= today's `ExecutionSession`, narrowed)*
- **Identity.** Guid (`ExecutionSession.Id`). Durable; preserved across restart (reloaded from JSON).
- **Purpose.** Do coding work — edit, commit, push — under the Operational (`workspace-write`) sandbox; produce a handoff per turn. After the Agent extraction it becomes the *operational consumer* of the Agent Runtime; its retained semantics are git/commit/push (`GitService`, `ExecutionSessionService.WithCommitResult/WithPushResult`).
- **Lifetime.** Durable record; live process is one-shot today, persistent-capable later. Survives restart as a record; its process never does.
- **Authority.** Owns operational semantics (git/commit/push, the `RepositoryExecutionState` machine). Must never own decision semantics. Its `AwaitingAcceptance` gate (`HandoffService.cs:92-97`) **narrows** — the design's loop bypasses it; the human gate relocates to decision Submit.
- **State.** Durable: `Id`, `RepositoryState`, `CommitSha`, `PushedCommitSha`, `HandoffPath`, `Events`. Serialized to JSON; mutated under `SemaphoreSlim(1,1)`.
- **Recovery.** `ExecutionSessionService.RecoverAsync`: `Executing → Failed` ("provider could not be reattached") because `SupportsReattach => false`. This is the *correct, conservative* recovery and should remain: operational work that was mid-flight is not silently resumed; it is failed and re-driven.
- **Concurrency.** One active operational turn per repository; serialized.

**RO-5c · Decision Session** *(= today's `DecisionSession`, the role that gains the most)*
- **Identity.** `DecisionSessionId`, subordinate to `repositoryId`. **Identity is preserved on router `Continue` (reuse) and changes on `Transfer` (a new session record).** This is the runtime meaning of the router: reuse = same identity + same warm process; transfer = new identity + fresh process seeded by the continuity payload.
- **Purpose.** Reason over the operational handoff under the zero-permission Decision sandbox (`read-only` + approvals `never` + no MCP/tools) and emit decisions. It does no coding, commit, or push. It is a *separate codex process* from the operational turns — separation preserved by the *shared* Agent Runtime, not by isolation.
- **Lifetime.** Durable record (`Created→Active→TransferPending→Transferred→Retired`); held-open live process across the loop's iterations until transfer/retire. Record survives restart; process does not (cold-start from `operational_context.md`).
- **Authority.** After the inversion, codex (running *in* this session) is the **live authority** over decision content; `CommandCenter.Decisions` becomes its **validator/oracle/fallback** (`DecisionProposal` is the parse target). The session owns the *governance* of its own lifecycle (reuse/transfer) via the router; it does not own *what a valid decision is* (that stays in `Decisions`).
- **State.** Durable: `Id`, `State`, `Ownership`, `Metadata` (transfer reason/target). Live: the held-open process + its token count. Continuity captured as `DecisionSessionContinuityArtifact` (`ArtifactId`, source/target, fingerprint).
- **Recovery.** `DecisionSessionRecoveryHostedService` + `IDecisionSessionRecoveryService.RecoverAsync(repositoryId)`. An `Active` record with no live process and an active Run → cold-start a fresh process from `operational_context.md`. An `Active` record with no Run → Supervisor retires it (orphan).
- **Concurrency.** **Exactly one active per repository** (`GetActiveSessionAsync(repositoryId)` returns one). Held-open process count mirrors this.
- **Invariant binding.** *Decision Session ≠ Operational Session* is now an object-level fact: distinct records, distinct process bindings, distinct sandbox profiles, distinct registries — composed only by the Repository Runtime, never referencing each other.

### RO-6 · Event Stream

> *Today:* `ExecutionMonitoringService` (Singleton) — unbounded `Channel<ExecutionEvent>` per subscriber, `Dictionary<Guid, List<Channel>>` multi-subscriber broadcast, retained-then-live replay; consumed by `GET /api/execution-sessions/{id}/events/stream` as `text/event-stream` with `id:`/`event:`/`data: {json}\n\n` (`ExecutionSessionsEndpoints.cs:100-104`).
> *Target:* the same substrate generalized to **three stream kinds** (planning, execution, decision) behind one reusable client live-view.

- **Identity.** `(repositoryId, streamKind)` or `sessionId`. One broadcaster per live stream.
- **Purpose.** Carry an Agent Process's turn output to many observers (UI, recorders) without coupling producer to consumer. It is the *projection liveness* of a session.
- **Lifetime.** Created when a session starts streaming; lives for the turn(s); subscribers come and go. Ephemeral — never persisted (the *events* are persisted into the session record / `.agents/`, the *broadcaster* is not).
- **Authority.** None semantic. It moves bytes. It must never interpret or persist domain meaning — that is the consuming domain's job.
- **State.** Subscriber channels + retained events for replay. Ephemeral.
- **Relationships.** Fed by an Agent Process (via Agent Runtime); read by SSE endpoints and the UI live-view; owned (per repository) by the Repository Runtime. **Forbidden:** a stream that persists, decides, or mutates domain state.
- **Recovery.** None needed; on reconnect the consumer re-subscribes and replays retained events, then resumes live. Durable event history lives in the session record / journal.
- **Concurrency.** Multi-subscriber, single-writer per stream (the channel pattern already guarantees this: `SingleReader` per subscriber, many subscribers).
- **Projection.** *Is* the projection layer's live half. Three SSE endpoints; one client hook (today's execution-events hook, generalized).

### RO-7 · Runtime Supervisor

> *Today:* three hosted services — `ExecutionSessionRecoveryHostedService`, `DecisionSessionRecoveryHostedService`, `WorkflowRecoveryHostedService` (+ `WorkflowContinuationHostedService`'s periodic loop). Each reconciles its context's durable records at startup.
> *Target:* generalized to also own the lifetime of Repository Runtimes and their live Agent Processes — the **Repository Runtime Registry** is the Supervisor.

- **Identity.** Global singleton(s). Process-wide.
- **Purpose.** Reconcile *durable records* against *live processes* at startup, on shutdown, and on demand: fail or cold-start orphaned sessions, dispose leaked processes, reconstruct Repository Runtimes for repositories with active runs, enforce one-Runtime-per-repository. It owns the answer to "the live tier is gone — now what."
- **Lifetime.** Application lifetime; runs at start (recover), continuously (continuation/supervision), and at shutdown (dispose).
- **Authority.** Owns **lifecycle reconciliation and disposal**, not domain semantics. It may fail/retire/cold-start sessions per each context's *own* recovery service; it must not invent decision/handoff/git outcomes.
- **State.** None durable. It reads durable records and live registries and acts.
- **Relationships.** Creates/disposes Repository Runtimes; invokes each context's recovery service; disposes Agent Processes on shutdown. **Forbidden:** holding domain state; bypassing a context's own recovery authority.
- **Recovery.** It *is* recovery. Idempotent and re-runnable.
- **Concurrency.** Global; iterates repositories; per-repository reconciliation is serialized through that Runtime's gate.
- **Projection.** Recovery diagnostics (`DecisionSessionRecoveryDiagnostics`, `WorkflowInstance.Diagnostics`) surface to the inspection tabs.

### Non-runtime objects (catalogued for boundary clarity)

These are **not** runtime objects — they are domain records, stateless authorities, infrastructure, or projections. Naming them prevents the catalog above from absorbing responsibilities that must stay out of the runtime.

- **Domain records (durable, reconstructed, no liveness):** `Decision`, `DecisionProposal`, `DecisionCandidate`, `DecisionSessionContinuityArtifact`, `OperationalContextProposal`/`OperationalContextDocument`, `ReasoningEvent`/`Thread`/`Relationship`, `WorkflowInstance` and its timeline artifacts, the `.agents/` documents themselves.
- **Stateless authorities (Singleton policies/functions — *consulted*, never *live*):** the **Router** (`IDecisionSessionLifecyclePolicy.EvaluateAsync` → `Continue`/`Transfer` from `ReuseScore`/`TransferScore`), **Transfer Eligibility** (`Eligible`/`Blocked`/`Deferred`/`NotApplicable`), **Token Estimator** (`(len+3)/4`), **Continuity Artifact Service**, **HandoffService** (rotation authority), **GitService**, the **Economics/Coherence/Metrics** services. *These have no lifetime and no live state — they are pure transformations the runtime objects call into.* Treating any of them as a runtime object would be the first step toward a god-orchestrator.
- **Infrastructure (durable substrate):** **Artifact Store** (`IArtifactStore`/`FileSystemArtifactStore`, the `.agents/` filesystem), the **session JSON stores** (`FileSystemExecutionSessionStore`, `FileSystemDecisionSessionRepository`, `FileSystemWorkflowRepository`), the **Process Registry** (inside Agent Runtime), the **Prompt Library** (`Core.Prompts.*` via `Lib.Prompts`, new), and the **Memory Cache** (transitional — see Minimalism).
- **Projection objects:** the SSE endpoints, the generated contracts (`repository-dashboard.generated.ts`), `useShellState`/workspace hooks, the 7 `WorkspaceTabs`, `SelectedRepositorySummary`. The **Shell** (Tauri/Rust) is a *passive transport* projection — verified zero-state HTTP relay; it must never gain runtime status.

---

## Runtime Topology

The same seven objects, viewed through six graphs. `repositoryId` is the spine; everything subordinate hangs from a Repository Runtime.

**Ownership graph** (who creates/disposes whom):
```
Composition Root (Backend)
└─ Agent Runtime (global)            └─ Runtime Supervisor / Repository Runtime Registry (global)
   └─ Process Registry                  └─ Repository Runtime  [one per repositoryId]
      └─ Agent Process  ◀──────────────────┤  holds handles
                                            ├─ Repository Run  [≤1 active]
                                            │     └─ drives Sessions' turns
                                            ├─ Session (Planning | Operational | Decision)
                                            │     └─ bound to one Agent Process when live
                                            └─ Event Stream  [planning | execution | decision]
```

**Lifecycle graph** (longest-lived → most ephemeral):
```
Agent Runtime / Supervisor (app life)
  > Repository Runtime (repo-loaded life, reconstructable)
    > Repository Run (one plan-execution, journaled)
      > Session record (durable; survives restart)
        > Agent Process (one process; never survives restart)
          > Event Stream / turn (one turn; replayable, not durable)
```

**Communication graph** (who talks to whom, and how):
```
Endpoint ──(command)──▶ Repository Runtime ──(advance)──▶ Repository Run
Repository Run ──(spawn/prompt)──▶ Agent Runtime ──▶ Agent Process ──(turn output)──▶ Event Stream ──(SSE)──▶ UI
Repository Run ──(consult)──▶ Router / Transfer-Eligibility / Token-Estimator  (pure calls)
Repository Run ──(write/rotate)──▶ Artifact Store (.agents journal)
Repository Run ──(parse/validate)──▶ Decisions (DecisionProposal)   [codex output is the authority; Decisions validates]
```

**Authority graph** (who owns which semantics — and the bright lines):
```
how-to-run-codex ......... Agent Runtime
run-progress ............. Repository Run
compose/sequence ......... Repository Runtime        ◀ MUST NOT cross into domain semantics
operational (git/push) ... Operational Session (Execution)
decision content ......... codex-in-Decision-Session (live)  ⟂ Operational
decision shape/oracle .... Decisions (DecisionProposal)
reuse-vs-transfer ........ Router (DecisionSessions)
handoff rotation ......... single rotation owner
contract shape ........... contract oracle (M0.2/M1.1/M1.2)
architectural decisions .. M0.4 governance
transport ................ Shell (passive)
```

**Persistence graph** (what is written where):
```
%APPDATA%/CommandCenter/execution-sessions.json ........ Operational Session records
FileSystem decision-session store ...................... Decision Session records + metrics
FileSystem workflow store .............................. WorkflowInstance timelines/continuation
.agents/ (IArtifactStore) .............................. the Repository Run journal:
   specs/, plan.md, operational_context.md, operational_delta.md,
   handoffs/handoff.{NNNN}.md, decisions/decisions.{NNNN}.md,
   reasoning/{events,threads,relationships}, workflow/{timelines,…}
(nothing) .............................................. Agent Runtime, Agent Process,
                                                          Repository Runtime, Event Stream — all reconstructed
```

**Dependency graph** (project participation, post-evolution):
```
Core (+Prompts)  ◀── leaf, dependency-free additions
Agents (NEW: Execution's process/stream/registry, relocated)  ◀── Operational & Decision both depend here
Execution (operational semantics: git/commit/push) ──▶ Agents
DecisionSessions (router/transfer/economics) ──▶ Agents     ⟂ Execution (never references it)
Decisions (validator/oracle/fallback)
Continuity / Reasoning (durable projections over decisions/sessions)
Middle (composition of all contexts)
Backend (composition root; hosts Agent Runtime, Repository Runtime Registry, SSE)
Shell (passive transport)  ·  UI (driving + inspection projection)
```

---

## Runtime Lifecycle Model

The runtime has exactly **five nested lifetimes**, and every object belongs to one:

1. **Application** — Agent Runtime, Runtime Supervisor / Registry, the stateless authorities, the infrastructure stores. Born at composition root; die at shutdown.
2. **Repository-loaded** — Repository Runtime. Born when a repository is first driven; reconstructable after restart; dies on unload.
3. **Run** — Repository Run. Born at Execute Plan; journaled to `.agents/`; dies at completion/cancel. At most one active per repository.
4. **Session record** — durable, survives restart, spans many turns; ends at close/transfer/retire/fail.
5. **Turn / live process** — Agent Process + Event Stream. Born per held-open process or one-shot; **never survive restart**.

The lifecycle's defining rule: **liveness decreases monotonically down the nesting, and durability is externalized at every boundary.** A child may always be lost without losing its parent, because the parent's durable record (or the `.agents/` journal) can re-create the child's *starting point*. The repository lifecycle states (`PlanAuthoring`, `ExecutingPlan`) are the *projection* of which nesting level is active: `PlanAuthoring` = a Repository Runtime with a Planning Session but no Run; `ExecutingPlan` = a Repository Runtime with an active Run.

---

## Runtime Authority Model

Authority is owned at exactly the level that has the narrowest legitimate claim, and three classes must never mix:

- **Mechanical authority** (Agent Runtime): how to spawn/stream/dispose. No "why."
- **Progress authority** (Repository Run): what turn comes next, the iteration counter. No "what the answer is."
- **Composition authority** (Repository Runtime): hold handles, fan out streams, sequence calls. **The one bright line of the whole architecture: composition must never become domain.** Encode it as an M0.3 regression test (the Repository Runtime may *depend on* but never *reimplement* router, rotation, contract oracle, git).
- **Domain authority** (the contexts): decision content (codex-live) ⟂ operational semantics (Execution); decision shape (Decisions); routing (DecisionSessions); rotation (single owner); contract shape (oracle); architectural decisions (M0.4); transport (Shell, passive).

The design moves domain authority in exactly three places (decision content inverts to codex; process mechanics extract to Agents; a *new* composition authority appears and is bounded) and preserves it everywhere else. The runtime model's job is to make the bright line between *composition* and *domain* structurally unviolable.

---

## Runtime Ownership Model

A single ownership chain governs disposal and prevents leaks: **Registry → Repository Runtime → (Run, Sessions, Streams, held Processes).** The rules:

- Only the **Repository Runtime** may hold a live Agent Process handle *across requests*. Endpoints never hold process handles (they hold `repositoryId` and ask the Runtime).
- Only the **Registry/Supervisor** may create or dispose a Repository Runtime, and it enforces **one per `repositoryId`**.
- Only the **Repository Run** may advance the journal, through the **single rotation owner**, under the per-repository gate.
- The **Agent Runtime** owns the *creation and disposal mechanics* of processes but never decides *when* a session should run — the Run does.
- Disposal cascades downward and is idempotent: disposing a Runtime disposes its processes and streams; disposing the app disposes all Runtimes.

---

## Runtime Persistence Model

Three persistence tiers, matching the three liveness tiers:

1. **Durable records (JSON stores):** Operational Sessions (`execution-sessions.json`), Decision Sessions + metrics, Workflow timelines. Serialized in full; reconstructed on read. These are the *identity and state-machine* truth.
2. **The `.agents/` journal (Artifact Store):** the Repository Run's externalized progress — specs, plan, operational_context/delta, rotated handoffs and decisions, reasoning graph, workflow artifacts. **Append-only, monotonic, 4-digit rotation, single owner.** This is the *forward-progress* truth and the reason a Run can resume after a crash that destroyed every live object.
3. **Nothing (reconstructed):** Agent Runtime, Agent Process, Repository Runtime, Repository Run object, Event Stream. These are *regenerated* from tiers 1–2, never serialized. The Memory Cache (`{repositoryId}:Plan`) is a *performance mirror* of journal state, not a fourth tier.

The model's invariant: **no runtime object holds durable state that is not also derivable from a record or the journal.** Live handles are always disposable; truth is always on disk.

---

## Runtime Recovery Model

Recovery is uniform across every failure because of the liveness/durability split. The algorithm at startup (the generalized Supervisor):

1. Load all durable Session records and the `.agents/` journal per repository.
2. For each repository with an `Active`/`Executing` session or an unfinished Run, **reconstruct a Repository Runtime** (Registry enforces one).
3. Reconcile the live tier (always empty after restart):
   - **Operational Session** mid-`Executing` → `Failed` ("provider could not be reattached"; `SupportsReattach => false`). Conservative by design; re-driven by the user.
   - **Decision Session** `Active`, Run still unfinished → **cold-start** a fresh process from `operational_context.md` (transfer-style seed), preserving the session record where the router permits, or open a new session.
   - **Decision Session** `Active`, no Run → **retire** (orphan).
   - **Repository Run** → resume from the highest *complete* `handoff.NNNN`/`decisions.NNNN`; discard any half-written (uncommitted) turn and re-run it.
4. Dispose any leaked OS processes the Agent Runtime cannot account for.

Per-event recovery: **app crash** → full reconstruction as above. **process exit mid-turn** (`ProviderExited` with no `TurnCompleted`) → discard the turn, re-spawn, re-run from the last journal entry. **repository unload** → dispose that Runtime and its processes; records remain on disk. **execution cancelled** → dispose processes, mark the Run cancelled, leave the journal as the audit trail. **codex disconnects** → identical to process exit; the held-open process is *expected* to be losable.

---

## Runtime Communication Model

Two transport shapes, strictly separated:

- **Command (request/response, synchronous):** endpoint → Repository Runtime → Run advance. Mutating; serialized per repository; returns an id/ack. The design's `plan/write`, `plan/revise`, `plan/execute`, `decision/submit`.
- **Stream (one-way, asynchronous, multi-subscriber):** Agent Process → Event Stream → SSE → UI live-view. Non-mutating; the existing `Channel`/`text/event-stream` substrate, generalized to three kinds. Replayable from retained events; durable history lives in the record/journal, not the stream.

Internal calls into the stateless authorities (router, rotation, estimator, git) are **synchronous pure calls** — no eventing, no shared mutable state. The runtime never communicates domain meaning through the stream; the stream is bytes, the record is truth.

---

## Runtime Concurrency Model

- **Global, many-instance:** Agent Runtime (one), Supervisor/Registry (one), Process Registry (one, bounds total processes).
- **Per `repositoryId`, exactly one:** Repository Runtime (Registry-enforced — the **no-duplicate-orchestrators** guarantee), active Repository Run (≤1), active Decision Session (`GetActiveSessionAsync` returns one), active operational turn.
- **Per turn, serialized:** within a Run, turns are strictly ordered (operational → decision → route → next); mutation runs under the per-repository gate (`SemaphoreSlim(1,1)`, generalized from `ExecutionSessionService` to the Runtime).
- **Multi-subscriber:** Event Streams (single-writer, many-reader per stream).
- **Independent across repositories:** all per-repository runtimes run concurrently; the only shared bound is the global Process Registry's capacity.

The model's rule: **all cross-request mutation for a repository funnels through one Repository Runtime and one gate; all fan-out is read-only streaming.** This makes the system concurrent across repositories and serial within one — the only correctness-safe shape for a journal-backed run.

---

## Runtime Failure Model

| Failure | Live objects lost | Durable truth | Recovery |
|---|---|---|---|
| Application crash | all live (Runtimes, Runs, Processes, Streams) | session JSON + `.agents/` journal | Supervisor reconstructs Runtimes; reconciles sessions; resumes Runs from journal |
| Lost agent process (mid-turn) | one Agent Process + its Stream | last complete journal entry | discard partial turn; re-spawn; re-run |
| Partial restart | the restarted side's live tier | both stores | reconcile; conservative fail for operational, cold-start for decision |
| Router failure / unavailable | none | n/a | default to the **continuity-preserving** choice (reuse the warm process if present, else transfer); never block the loop |
| Context corruption (`operational_context.md`) | none live | `plan.md` as fallback seed | re-derive context from plan; flag for review |
| Orphaned Planning Session | the warm planning process | `specs/`, `plan.md` if written | dispose; repo returns to `PlanAuthoring` |
| Orphaned Decision Session | the warm decision process | session record | Supervisor retires if no Run; cold-starts if Run active |
| Stale runtime memory (`{id}:Plan`) | n/a (mirror) | journal | evict on run end; re-read journal on miss |
| **Duplicate orchestrators** (two Runtimes, one repo) | — | — | **prevented by construction** (Registry keyed by `repositoryId`); the single highest-severity *structural* failure, designed out rather than recovered |

The unifying principle: **no failure can destroy truth, because truth never lives only in a runtime object.** Every failure degrades to "reload the record/journal and rebuild the live tier."

---

## Runtime Evolution Model

How each project participates in the runtime (not "which services it has"):

- **Core** — supplies the durable vocabulary (`Repository`, `RepositoryExecutionState`, `IArtifactStore`) and gains two additions that are pure runtime-state vocabulary: the lifecycle states (`PlanAuthoring`, `ExecutingPlan`) and the generated **Prompt Library** every agent turn consumes. Leaf; participates as *vocabulary*, not behavior.
- **Agents (new)** — *becomes the runtime substrate.* Hosts the Agent Runtime and Agent Process; the relocation of `Execution`'s `ProcessRunner`/`CodexExecutionProvider`/`ExecutionMonitoringService` with no behavior change. The keystone: both session roles become its consumers.
- **Execution** — narrows to the **Operational Session** runtime object: git/commit/push semantics and the operational state machine, now a consumer of Agents. Donates its process/stream machinery upward.
- **DecisionSessions** — *becomes the Decision Session runtime object plus the routing authority.* Gains the ability to run codex (through Agents) without gaining a path to Execution. Its router/transfer/economics services remain stateless authorities the Repository Run consults.
- **Decisions** — *becomes a stateless authority* (validator/oracle/fallback), not a runtime object. `DecisionProposal` is the parse target for codex output.
- **Continuity** — supplies the durable **transfer payload** (`OperationalContextProposal` / `operational_context.md` / `operational_delta.md`); participates as the Run's continuity record, sharpened from audit to active hand-off.
- **Reasoning** — supplies a durable **projection** (the reasoning graph) over decisions/sessions; participates read-only.
- **Middle** — the **context-evolution composition tier**; owns operational-context lifecycle (generation, delta, rewrite-on-transfer). Composition, not a runtime object.
- **Backend** — hosts the runtime: the **Repository Runtime Registry / Supervisor**, the Agent Runtime, the three SSE streams, the command endpoints. The sole composition root and the only place both session roles meet.
- **Shell** — passive transport; relays the new commands/streams; never gains runtime status (verified zero-state today; must stay so).
- **UI** — projects the runtime: the driving surface (Plan Authoring + decision loop) foregrounded by lifecycle state, the inspection tabs as depth, three stream consumers behind one live-view.

**Trajectory:** today's *implicit per-repository runtime smeared across singletons* → an *explicit registry of Repository Runtimes*, each owning a journaled Run that drives real Agent Processes through three session roles, recovered by a generalized Supervisor, projected through three streams and one driving surface.

---

## Runtime Invariants

The properties that must hold regardless of implementation:

1. **`repositoryId` is the runtime spine.** Every runtime object's identity is `repositoryId` or subordinate to it.
2. **One Repository Runtime per `repositoryId`.** No duplicate orchestrators — enforced by the Registry, not by convention.
3. **A session survives restart; its process never does.** Liveness is always reconstructable; never the source of truth.
4. **Truth is always externalized.** No runtime object holds durable state not derivable from a record or the `.agents/` journal.
5. **Decision Session ⟂ Operational Session.** Distinct records, distinct process bindings, distinct sandbox profiles, distinct registries; composed only by the Repository Runtime; they never reference each other.
6. **Separation is preserved by the shared Agent Runtime, not by isolation.** Both roles run codex through Agents; neither reaches the other's orchestration.
7. **Sandbox profile is immutable for a process's life.** An Operational process never becomes a Decision process.
8. **Composition never becomes domain.** The Repository Runtime and Agent Runtime hold no decision/handoff/contract/git semantics — they call into the owners.
9. **The Agent Runtime is role-blind.** No `if (role == Decision)` branch; role lives in `AgentSessionSpec` data.
10. **One rotation owner per artifact.** The Run's journal has exactly one writer of the `{NNNN}` sequence; the `AwaitingAcceptance` side-effect never fires on the loop path.
11. **At most one active Decision Session and one active Run per repository.**
12. **Codex authors decisions; Decisions validates them.** Liveness of content ≠ authority over shape.
13. **The router governs identity:** `Continue` preserves the Decision Session identity and warm process; `Transfer` mints a new identity seeded by the continuity payload.
14. **The Shell stays passive.** Transport never gains runtime status.

---

## Runtime Minimalism Review

Each runtime object challenged: *could it disappear?*

- **Agent Process — fundamental.** It is the only object that owns an OS resource and the only thing that makes a session *live*. Cannot be merged into anything; everything else exists to create, feed, and dispose it. **Irreducible.**
- **Repository Runtime — fundamental.** Without it, process handles cannot outlive a request and the flow cannot pause for human input. It is the minimal object that makes the system stateful-per-repository. **Irreducible** — though it must be *bounded* (composition only) to stay minimal in *authority*.
- **Repository Run — fundamental, but distinct from the Runtime.** Could it merge into the Repository Runtime? No: the Runtime exists whenever a repo is loaded (including `PlanAuthoring` with no run); the Run exists only while executing. Collapsing them would conflate "coordinator always present" with "drive sometimes present" and break the lifecycle-state projection. **Keep separate.**
- **Session (three roles) — fundamental, but one shape.** The three roles are not three objects; they are one durable-record shape parameterized by sandbox profile. Minimalism *unifies* them: do not build three session subsystems; build one Session abstraction with a role/profile field. (Today they are two separate stores — `ExecutionSession` and `DecisionSession`; a *future* minimalization could unify the record, but the role-separation invariant must survive the unification.)
- **Agent Runtime vs Process Registry — the Registry is a part, not a peer.** The Process Registry is internal state of the Agent Runtime, not a separate runtime object. **Fold in.**
- **Event Stream — fundamental but thin.** It cannot be removed (multi-subscriber fan-out is real), but it must stay a pure transport with no domain claim. **Keep, constrain.**
- **Runtime Supervisor — fundamental, and it *absorbs* the Registry.** The three recovery hosted services and the Repository Runtime Registry are the same concern (reconcile durable vs live) and should be one Supervisor. **Merge the three into one.**
- **Memory Cache — should largely disappear.** `{repositoryId}:Plan` exists only because today's model is stateless; once the Repository Run holds run state in-process and journals it, the plan text lives there. The cache reduces to an optional read-through mirror. **Dissolve into Run state.**
- **The Router, Token Estimator, Transfer-Eligibility, HandoffService, GitService — not runtime objects at all.** They are stateless functions. The minimalism win is *refusing to promote them* to runtime objects — doing so is exactly how the orchestrator becomes a god-object. **Keep as consulted authorities.**
- **WorkflowInstance — subsumed.** Its continuation loop is the Decision Loop's precursor; in the target it is a *projection* the Run produces, not a separate live object. **Demote to projection.**

**Reduced ontology:** the seven catalog objects collapse to **five truly irreducible runtime objects** — Agent Runtime, Agent Process, Repository Runtime, Repository Run, Session — plus **two thin supporting objects** (Event Stream, Supervisor) that exist to feed and reconcile the five. Everything else is a record, a stateless authority, infrastructure, or a projection. That is the final, minimal runtime.

---

## Runtime Readiness Assessment

- **What already exists (six of seven, in reusable form):** the streaming substrate (Event Stream), the durable session records and JSON stores, the `.agents/` journal and rotation, the router/transfer/continuity authorities, the recovery hosted services (Supervisor precursor), and the entire domain/projection stack. The role-separation invariant (RO-5: Decision ⟂ Operational) is *already structurally true* in the dependency graph.
- **What must evolve (relocation + generalization, low risk):** `Execution`'s process/stream machinery → Agent Runtime (no behavior change); three recovery services → one Supervisor; `WorkflowInstance` → run projection; the Memory Cache → dissolved into Run state; `Decisions` → stateless authority.
- **What is genuinely new (concentrated risk):** the **Agent Process** (held-open codex — the single gating spike) and the **Repository Runtime** as an explicit cross-request object (a new composition on patterns already practiced). Plus the Run's `.agents/` journal *as a resumable run record* (the artifacts exist; treating them as the authoritative resume point is the new discipline).
- **Architectural leverage:** highest at the Agent Runtime relocation (unblocks the Decision role's liveness with zero behavior change) and at naming the Repository Runtime (turns implicit per-repo singletons into one owned object). The risk lives almost entirely in one object (Agent Process); the leverage lives almost entirely in objects that already exist unnamed.

**Readiness verdict.** The runtime is reachable by *naming and composing what already runs*, plus proving one new primitive. The durable and projection tiers are built and certified; only the live-process tier and its owner are new; and because truth is externalized at every boundary, the new live tier is *safe to be fragile* — it can be lost and rebuilt at will.

---

## Conclusion — the runtime Command Center becomes

Strip away the projects and the request handlers and Command Center resolves to a single sentence:

> **A global registry of per-repository runtimes — one Repository Runtime per `repositoryId` — each holding at most one active, `.agents/`-journaled Run that drives a real codex Agent Process through three sandbox-separated session roles (Planning, Operational, Decision), streamed live to a driving surface and reconciled by one Supervisor, with every unit of truth externalized to a durable record so the live processes are always disposable and never authoritative.**

The product is not a set of services; it is this runtime. Repositories become living runtimes the moment they are loaded; planning, execution, and decision-making become *phases of one journaled run* rather than endpoints; a session becomes a durable record whose codex process is a detachable, losable liveness; and orchestration becomes a strictly-bounded conductor that holds processes and sequences turns but owns no domain meaning. The governing invariant — Decision ⟂ Operational, preserved by a shared runtime rather than isolation — stops being an aspiration enforced by a missing reference and becomes an *object-level fact*: two session records, two process bindings, two sandbox profiles, composed only at the one place allowed to see both.

What makes this runtime *real and ready* is that it is mostly already here, unnamed. The implementation has been building a per-repository runtime all along — keying every service by `repositoryId`, journaling every step into `.agents/`, recovering orphans at startup, separating the two roles in the dependency graph — without ever instantiating the object that *is* that runtime. This audit's claim is narrow and total: **Command Center's roadmap should be driven by these five irreducible runtime objects, because they are the stable concepts the whole system has been converging on, and the remaining work is simply to name them, prove the one new primitive that gives a session life, and let the projects fall into place beneath them.**
