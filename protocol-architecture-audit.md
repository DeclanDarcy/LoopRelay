# Command Center — Protocol Architecture Audit

Status: architectural audit (behavioral architecture reconstruction)
Date: 2026-06-26
Question answered: **What are the irreducible protocols that govern the behavior of a running Command Center — the stable interaction contracts the roadmap should be driven by, rather than individual endpoints, services, or transports?**

Inputs treated as authoritative: the current implementation (`src/`, verified type-by-type and re-verified for this audit), the Runtime Object Model Audit (`runtime-object-model-audit.md`, the nouns), the Vision Realization Audit (`vision-realization-audit.md`, the evolution), the proposed design (`design.md`, the concrete flow), and the certified governance/contract/regression net.

The Runtime Object Model identified the **nouns** — the long-lived runtime objects. This audit identifies the **verbs** — the conversations those objects have. Where the object model asked *what ultimately exists*, this audit asks *how those objects collaborate*: the canonical protocols that, once named, let services, endpoints, transports, streams, hosted services, and persistence emerge as their implementations rather than as the architecture itself.

Method, as before: every claim about *today* is grounded in a verified type (`Type` / `file:line`); every claim about *the target* is derived from the design and the two prior audits; the two are kept explicitly distinct so the protocol architecture is never confused with current code.

---

## Executive Behavioral Assessment

**Command Center is a distributed state machine whose product is a small set of conversations between long-lived runtime objects — and today those conversations exist only as fragments, scattered across stateless request handlers and a periodic background poll, never spoken end-to-end over a held-open process.** The Runtime Object Model found that the per-repository runtime exists *implicitly*, smeared across `repositoryId`-keyed singletons. The behavioral counterpart is sharper still: the system's defining conversation — **the Decision Loop, in which an Operational turn's handoff feeds a Decision turn whose verdict the human edits and submits, after which a router decides whether the next decision reuses a warm process or transfers into a fresh one** — is today approximated by `WorkflowContinuationHostedService`'s 60-second `PeriodicTimer` (`WorkflowContinuationHostedService.cs:55-80,102`) calling stateless continuation/preparation services. The loop *polls*; it does not *converse*. There is no held-open process for two parties to speak over, no human gate inside the turn, no live token count feeding the router (`DeterministicTokenEstimator` is `(len+3)/4`, `DeterministicTokenEstimator.cs:9`).

The decisive behavioral finding is that the system already implements its **substrate protocols** to a certified standard while barely implementing its **drive protocols**:

- **Substrate (built, reusable):** *Turn Streaming* is a proven `Channel`-based multi-subscriber broadcast with retained-then-live replay (`ExecutionMonitoringService.cs:154-222,359-370`) projected as `text/event-stream` (`ExecutionSessionsEndpoints.cs:79-112`). *Journal Rotation* is a working 4-digit monotonic write (`HandoffService.cs:134-155`). *Runtime Reconciliation* runs at every startup across three hosted services. *Routing* is a complete, deterministic, advisory authority (`DecisionSessionLifecyclePolicy.cs:66-150`). These are conversations the system already holds correctly.
- **Drive (fragmentary, the real work):** *Plan Authoring* and *Decision Turn* — the two **warm, multi-turn, human-gated dialogues** — cannot be held at all, because `ProcessRunner` closes stdin after the first write (`ProcessRunner.cs:101`) and `SupportsReattach => false` (`CodexExecutionProvider.cs:13`). *Run Activation* (the Execute hinge) and the *Decision Loop* composition exist only as the design's §12 prose.

The behavioral architecture therefore reduces to **eleven canonical protocols** in three tiers — three *lifecycle* protocols that govern object existence, six *drive* protocols that govern forward progress, and two *substrate* protocols that carry signal and truth while owning nothing. Beneath them sit just **two turn shapes**: a *one-shot turn* (today's only shape) and a *warm multi-turn dialogue* (the one genuinely new behavior). Every drive protocol is one of these two shapes bound to a sandbox role.

The single most important behavioral fact: **the same authority discipline the object model demanded spatially (composition never becomes domain) is enforced *temporally* by the protocols** — at no point in any conversation does the conductor (Repository Runtime) author a decision, decide a route, or rotate a journal; it only *sequences* parties who own those acts. And the one protocol where semantic authority genuinely changes ownership — *Session Transfer*, where a Decision Session's identity is retired and a new one minted — is governed end-to-end by a pure, non-mutating router. Authority moves exactly once, exactly where the design intends, and never drifts.

**Verdict.** Command Center's behavior is **a per-repository runtime holding at most one journaled Run that alternates two conversations — an Operational turn and a Decision turn — under a routing authority that governs decision-session identity, with every turn streamed live and every completed turn rotated to an append-only journal so the conversation can be abandoned and resumed at any boundary.** The substrate conversations are built and certified; the drive conversations are the same two turn shapes the design names, blocked on the single new primitive (a held-open process) the object model already isolated. The roadmap should be organized around these eleven protocols, because they are the enduring behaviors the system has been converging on — and most of them are already being spoken, just not yet to each other.

---

## Canonical Protocol Catalog

Eleven protocols, each reconstructed against the full template (Purpose, Participants, Initiator, Authority Flow, Message Flow, State Transitions, Persistence, Recovery, Concurrency, Observability, Completion, Failure). For each, **Today** grounds it in verified code; **Target** states what it becomes. Protocols are numbered by tier: **L** = lifecycle, **D** = drive, **S** = substrate.

The catalog deliberately *excludes* protocols that the Minimalism Review (below) proves derived: the "Decision Loop" (a composition, not a protocol), "Session Reuse" (the degenerate branch of Routing), "Commit/Push" (a bounded act inside two drive protocols), "Human Intervention" and "Pause/Resume" (the completion condition of the Decision Turn), "Repository Switching" (Disposal-to-idle then Activation), and "Process Supervision / Cancellation / Shutdown" (triggers of Disposal).

---

### L1 · Repository Activation

> *Today:* no single act. A repository becomes "active" only in the sense that singletons re-derive everything from `repositoryId` per request; the closest thing is the recovery hosted services materializing state at startup. *Target:* the Repository Runtime Registry materializes (fresh or reconstructed) exactly one Repository Runtime for a `repositoryId`.

- **Purpose.** Bring the per-repository coordinator into existence so processes and run state can outlive a request. Preserves the invariant **one Repository Runtime per `repositoryId`** (no split-brain over one `.agents/` tree).
- **Participants.** Registry/Supervisor (initiator), the new Repository Runtime (subject), the durable session stores and `.agents/` journal (read), the Human (selects the repository). **Never participate:** any Agent Process (none spawned yet), the Router (no run to govern), the domain authorities.
- **Initiator.** The Registry, triggered by repository selection or by L2 reconstruction at startup. Why: only the Registry may enforce singleness by keyed insert.
- **Authority flow.** Before: Registry holds lifecycle authority, no live coordinator exists. During: Registry. After: a Repository Runtime holds **composition authority** for that repository. No domain authority is created or moved.
- **Message flow.** *select(repositoryId)* → Registry checks for an existing Runtime → if absent, *materialize* → Runtime reads durable records to determine lifecycle state (`PlanAuthoring` if `!File.Exists(.agents/plan.md)`, else `ExecutingPlan`/idle) → *ready(state)*.
- **State transitions.** Before: no Runtime. After: Runtime present; repository lifecycle state projected (`PlanAuthoring` | `ExecutingPlan`). No durable session record changes.
- **Persistence.** Nothing new becomes durable. The Runtime is reconstructed, never serialized; the durable records it reads are unchanged.
- **Recovery.** Idempotent — re-selecting a loaded repository returns the existing Runtime. This protocol *is* the front half of L2.
- **Concurrency.** Exactly one Runtime per `repositoryId`, Registry-enforced; many across repositories.
- **Observability.** The repository lifecycle state (which surface is foregrounded). No stream.
- **Completion.** The Runtime exists and has projected its lifecycle state. Decided by the Registry.
- **Failure.** Durable store unreadable → Activation fails closed; the repository stays unselectable rather than starting a Runtime over corrupt truth. A concurrent second Activation is a no-op (keyed insert wins).

---

### L2 · Runtime Reconciliation *(recovery)*

> *Today:* three independent hosted services run at startup — `ExecutionSessionRecoveryHostedService` (→ `ExecutionSessionService.RecoverAsync`, `ExecutionSessionService.cs:27-87`), `DecisionSessionRecoveryHostedService` (→ `DecisionSessionRecoveryService.RecoverAsync`), `WorkflowRecoveryHostedService` (→ `RecoverCurrentWorkflowAsync`). Each reconciles its own context. *Target:* one Supervisor reconciling **durable records vs absent live processes** uniformly, reconstructing Repository Runtimes and resuming Runs from the journal.

- **Purpose.** Restore consistency after the live tier is lost (always, on restart). Preserves the master invariant: **a session survives restart; its process never does** — truth is reloaded, liveness rebuilt.
- **Participants.** Supervisor (initiator), every durable Session record, the `.agents/` journal, the Agent Runtime (to dispose leaked OS processes), reconstructed Repository Runtimes and Runs. The Router participates only as a *default*-provider for an interrupted route. **Never participate:** the Human (recovery is automatic), the Event Stream.
- **Initiator.** The Supervisor at application start, on demand, and on crash detection. Why: it alone owns reconciliation authority.
- **Authority flow.** Before: durable records are the sole truth (all live authority gone). During: Supervisor. After: authority returns to *each context's own recovery service* — the Supervisor never invents a domain outcome, it dispatches to the owner.
- **Message flow.** *reconcile(repositoryId)* → load records + journal → for each: classify (orphaned-operational | orphaned-decision | unfinished-run | leaked-process) → dispatch to the owning recovery authority → collect findings/diagnostics.
- **State transitions.** The asymmetry that must unify: **Operational Session** mid-`Executing` → **`Failed`** ("Active provider process could not be reattached", `ExecutionSessionService.cs:60-65`) — conservative, re-driven by the human. **Decision Session** `Active` with no process → today **diagnostic only** (rebuild derived snapshots, validate registry; `DecisionSessionRecoveryService.cs`), *target* → cold-start a fresh process from `operational_context.md` if a Run is active, else **`Retired`** as orphan. **Repository Run** → resume from the highest *complete* `handoff.{NNNN}`/`decisions.{NNNN}`; discard any half-written turn.
- **Persistence.** Writes recovery events/diagnostics (`DecisionSessionRecoveryDiagnostics`, workflow timeline). Records may transition (`Executing→Failed`, `Active→Retired`). The journal is the resume point, never rewritten.
- **Recovery.** It *is* recovery; idempotent and re-runnable.
- **Concurrency.** Global, iterating repositories; per-repository reconciliation serialized through that Runtime's gate.
- **Observability.** Recovery findings surface to inspection tabs; no live stream.
- **Completion.** Every repository with durable work has a reconstructed Runtime and a reconciled live tier. Decided by the Supervisor.
- **Failure.** A record that cannot be classified is quarantined, not guessed. Corrupt `operational_context.md` → fall back to `plan.md` as the seed and flag for review. The Supervisor never fabricates a missing turn.

---

### L3 · Runtime Disposal

> *Today:* implicit — no process is held across requests, so nothing needs disposing mid-life; hosted services stop on shutdown. *Target:* a single protocol releasing live processes/streams on **unload, shutdown, cancel, or error** — these are *triggers*, not separate protocols.

- **Purpose.** Release OS resources without losing truth. Preserves: **truth is always externalized**, so disposal is always safe.
- **Participants.** Repository Runtime or Supervisor (initiator, by trigger), Agent Runtime (dispose mechanics), every held Agent Process (destroyed), Event Streams (closed), the active Run (marked cancelled when the trigger is cancel). The Human participates only for the *cancel* trigger.
- **Initiator.** Repository Runtime (unload/cancel/error) or Supervisor (shutdown). Why: only the holder of a process handle may dispose it.
- **Authority flow.** Before: Runtime/Run hold composition/progress authority. During: Runtime/Supervisor. After: durable records are again the sole authority; no live authority remains.
- **Message flow.** *dispose(reason)* → Runtime asks Agent Runtime to kill each held process → close streams → (if cancel) mark Run `Cancelled`, evict the plan cache → leave the journal intact as audit trail.
- **State transitions.** Live tier → gone. On *cancel*: Run → `Cancelled`; Operational Session may go `Cancelled` (`RecordCancellationAsync`, `ExecutionSessionService.cs:128-140`). On *unload/shutdown*: no durable transition — the records are simply left for L2 to reload.
- **Persistence.** Nothing new except the cancellation transition; the journal and records remain. Disposal **never** writes a partial turn.
- **Recovery.** Disposal is the *clean* path that L2 handles on the *unclean* path; a crash mid-disposal degrades to L2.
- **Concurrency.** Disposal cascades downward and is idempotent: disposing a Runtime disposes its processes and streams; disposing the app disposes all Runtimes.
- **Observability.** Cancellation surfaces as a terminal stream event and a Run state; shutdown is silent.
- **Completion.** No live process or stream remains for the disposed scope. Decided by the disposer.
- **Failure.** A process that ignores kill is force-terminated; a leaked handle the Agent Runtime cannot account for is swept by L2 on next start. No failure can strand truth (it is on disk).

---

### D1 · Plan Authoring

> *Today:* impossible as a held conversation — `ProcessRunner.StartAsync` is one-shot (`ProcessRunner.cs:97-101`). `IPlanningService` exists but cannot hold a process. *Target:* a **warm multi-turn dialogue** — `WritePlan` then `Revise*` over one held-open Operational-sandbox process, producing `plan.md`.

- **Purpose.** Let the human and codex co-author a plan over a warm context until it is good. Preserves: planning is *iterative over one context*, not a sequence of cold one-shots.
- **Participants.** Repository Runtime (holds the planning handle, composes), Planning Session (the liveness record), one Agent Process (Operational sandbox, ExtraHigh), Event Stream (planning kind), Artifact Store (`specs/`, `plan.md`), the Human (drives Write/Revise/Execute). **Never participate:** the Decision Session, the Router, git.
- **Initiator.** The Human, via *Write Plan* (enabled only when the Roadmap textarea is non-empty, `design.md §11`).
- **Authority flow.** Before: Human holds intent. During: **codex-in-the-Planning-Session is the content authority** (it authors `plan.md`); the Repository Runtime holds only composition. After: authority returns to the Human (revise again, or execute). Composition authority never leaves the Runtime; content authority is never held by the Runtime.
- **Message flow.** *write(roadmap, specs[], newCodebase)* → persist `specs/roadmap.md`, `specs/s{n}.md` → spawn held-open process → send `WritePlanForNewCodebase.Text` | `WritePlanAgainstCodebase.Text` → **stream** turn → on turn-complete, *verify `plan.md` exists* → render + Copy. Then *revise(feedback)* → send `RevisePlan.Render(feedback)` to the **same** process → stream. Repeatable.
- **State transitions.** Repository lifecycle: `PlanAuthoring` throughout. No durable session state machine runs (the Planning Session is largely ephemeral). The Agent Process accumulates a token count across turns.
- **Persistence.** Durable residue: `specs/roadmap.md`, `specs/s{n}.md`, `plan.md` (written *by codex*, existence-verified by the Runtime). The Planning Session itself is **not** persisted; the process is ephemeral.
- **Recovery.** Not resumable as a warm conversation — on restart the process is gone. Degrade: re-seed a one-shot with `plan.md` + feedback (`design.md §4` fallback). The durable `specs/` and `plan.md` are the resume seed.
- **Concurrency.** One planning process per repository, gated by `PlanAuthoring` state. No Run exists yet.
- **Observability.** The planning SSE stream (`GET /plan/stream`); the rendered plan + Copy icon.
- **Completion.** The human clicks **Execute Plan** (hands off to D2). *Not* a codex signal — the human decides the plan is done. The protocol can also end by abandonment (the repository is left in `PlanAuthoring`).
- **Failure.** Turn ends without `plan.md` → surface the error, keep the process open for a corrective revise. Process dies mid-turn → discard the partial stream, fall back to one-shot re-seed.

---

### D2 · Run Activation *(the Execute hinge)*

> *Today:* the nearest precursor is `WorkflowInstance` transitioning through stages and the operational `StartAsync` path (`ExecutionSessionService.cs:137`). *Target:* the composite transition `design.md §12.C` — close planning, extract milestones, **commit/push**, start execution, set `ExecutingPlan`, produce the first handoff, open the Decision Session.

- **Purpose.** Convert an authored plan into a running, journaled Run. Preserves the lifecycle split: `PlanAuthoring` (coordinator, no Run) → `ExecutingPlan` (active Run). This is the single protocol that crosses the **outward-facing, irreversible git boundary**.
- **Participants.** Repository Runtime (composes the sequence), the Repository Run (*born here*), Planning Session (closed), Operational Session (ExtractMilestones, StartExecution one-shots), **GitService** (commit + push, `GitService.cs:85-149`), Decision Session (*opened*), Artifact Store (`operational_context.md`, `handoff.0001`), Event Stream (execution), Human (initiates Execute). **Never participate:** the Router (no decision turn has run yet).
- **Initiator.** The Human, via *Execute Plan*. Why: this fires the irreversible push; it must be a deliberate human act.
- **Authority flow.** Before: Human (decides to execute). During: Operational Session holds **operational/git authority**; Repository Runtime holds sequencing. After: the **Repository Run holds progress authority** and the repository is `ExecutingPlan`. The decision *content* authority (codex-in-Decision-Session) is *bootstrapped* but not yet exercised.
- **Message flow.** *execute()* → close planning process → copy `plan.md`→`operational_context.md`, cache `{id}:Plan` → `[O] ExtractMilestones.Text` (one-shot) → **`git commit -A && push`**, set `ExecutingPlan` → `[O] StartExecution.Render(plan)` (one-shot) → verify `handoff.md`, read, **rotate to `handoffs/handoff.0001.md`** → `[D] StartDecisionSession.Render(operational_context)` (held-open, zero-perm) → `[D] GetNextDecisions.Render(handoff)` → stream → editable decisions (hands to D4).
- **State transitions.** Repository lifecycle `PlanAuthoring→ExecutingPlan`. Repository Run: *created* → `Executing`. Operational Session: `Ready→Executing→…` per one-shot. Decision Session: *created* → `Active`.
- **Persistence.** Durable: `operational_context.md`, `handoff.0001.md`, the commit SHA + pushed SHA (`WithCommitResult`/`WithPushResult`, `ExecutionSessionService.cs:811,857`), the Run journal's opening entries, the Decision Session record (`Active`). The plan cache `{id}:Plan` is the ephemeral mirror.
- **Recovery.** The **only protocol with a non-recoverable outward effect**: once pushed, the push is done. If interrupted *after* push but *before* the Decision Session opens, L2 resumes the Run from `handoff.0001` and cold-starts the decision process — the push is *not* repeated (idempotency keyed on the rotated handoff). If interrupted *before* push, the Run is discarded and the human re-executes.
- **Concurrency.** Strictly serial; one Run Activation per repository, under the gate. No second Run may begin while one is active.
- **Observability.** Execution SSE stream; the lifecycle flips to `ExecutingPlan` (UI foregrounds the loop surface).
- **Completion.** The first decisions stream into an editable surface (entry to D4). Decided by turn-complete on `GetNextDecisions`.
- **Failure.** Commit/push failure → halt before `ExecutingPlan`; the Run is not born; surface the git error (reuse `GitWorkflowPanel` acceptance pattern per `design.md §13.8`). Milestone/StartExecution failure → fail the operational one-shot, no Decision Session opens.

---

### D3 · Operational Turn

> *Today:* `ExecutionSessionService.StartAsync` + provider exit → `HandoffService.ProcessProviderCompletionAsync` rotates the handoff and transitions to **`AwaitingAcceptance`** (`HandoffService.cs:92-97`) — a human gate. *Target:* the same turn shape (a **one-shot**) but the `AwaitingAcceptance` gate is **bypassed**; the handoff flows automatically into a Decision Turn, and the human gate relocates to D4's Submit.

- **Purpose.** Do one unit of coding work and emit a handoff. Preserves operational authority (git/commit/push, the `RepositoryExecutionState` machine) inside the Operational Session and nowhere else.
- **Participants.** Repository Run (owns progress), Repository Runtime (composes), one Agent Process (Operational sandbox, one-shot), Operational Session (state machine + git), GitService, Artifact Store (handoff rotation), Event Stream (execution). **Never participate:** the Decision Session, the Router.
- **Initiator.** The Repository Run — either as the StartExecution step of D2 (first turn) or as the ContinueExecution step after a D4 Submit (subsequent turns).
- **Authority flow.** Before: Repository Run (progress). During: **codex-in-the-Operational-Session does the work**; the Operational Session owns git semantics. After: the handoff is durable and progress authority returns to the Run, which proceeds to D4. The Run never authors the work; the Operational Session never decides what comes next.
- **Message flow.** *continue(plan, handoff, decisions)* → `[O] ContinueExecution.Render(cachePlan, handoff, decisions)` (one-shot, Medium) → **stream** → on exit, *verify `handoff.md`* → read into `handoff` → **rotate to `handoffs/handoff.{NNNN}.md`** (S2).
- **State transitions.** Operational Session: `Ready→Executing→`(provider exit)`→` *target* directly available for the next turn (**not** `AwaitingAcceptance`). Repository Run: `Executing`. The iteration counter advances.
- **Persistence.** Durable: the rotated `handoff.{NNNN}.md`, the operational session record, any commit/push SHAs if the turn committed. The handoff content is the turn's truth.
- **Recovery.** Operational work is **conservatively failed**, never silently resumed (`SupportsReattach => false`). A turn with no completed rotation is re-run from the last journal entry; a half-written handoff is discarded.
- **Concurrency.** One active operational turn per repository, serialized under the gate (`SemaphoreSlim(1,1)`, `ExecutionSessionService.cs:25`).
- **Observability.** Execution SSE stream; the rotated handoff is inspectable.
- **Completion.** Provider exit + verified, rotated handoff. Decided by the Run on turn-complete.
- **Failure.** Non-zero exit → Operational Session `Failed` (`ExecutionSessionService.cs:244-247`); the Run halts the loop and surfaces the failure. Missing `handoff.md` → the turn is incomplete, re-run.

---

### D4 · Decision Turn

> *Today:* does not exist as a codex conversation — `DecisionSessions` cannot run codex (no `Execution` reference; verified `CommandCenter.DecisionSessions.csproj:29-33`). Decisions are computed deterministically by `CommandCenter.Decisions` (`DecisionProposal`, weighted-sum scoring). *Target:* a **warm dialogue** where codex authors decisions over a handoff under the zero-permission Decision sandbox, the human edits, and **Submit** is the gate.

- **Purpose.** Reason over the Operational turn's handoff and propose decisions the human ratifies. Preserves: **decision content is authored by codex, validated by `Decisions`** — and the human is the completion authority.
- **Participants.** Repository Run (owns progress), Repository Runtime (holds the decision handle), one Agent Process (Decision sandbox: `read-only` + approvals `never` + no MCP/tools), Decision Session (record), `Decisions` oracle (validate/fallback — observes), Artifact Store (`decisions/decisions.{NNNN}.md` on submit), Event Stream (decision), the **Human** (edits + Submits). **Never participate:** the Operational Session, GitService.
- **Initiator.** The Repository Run, immediately after a D3 handoff (first turn from D2, subsequent turns from D5/Routing).
- **Authority flow.** Before: Repository Run (progress). During: **codex-in-the-Decision-Session is the live content authority**; `Decisions` is the schema/oracle/fallback. After: **the Human is the completion authority** — Submit ratifies the (possibly edited) decisions. Authority momentarily inverts to codex, then resolves to the human; the Run never authors a decision.
- **Message flow.** *getDecisions(handoff)* → `[D] GetNextDecisions.Render(handoff)` to the warm process → **stream** → on turn-complete, **swap the read-only stream into an editable textarea** seeded with `decisions` → human edits → *submit(decisions)* → persist `decisions/decisions.{NNNN}.md` (S2) → hand to D3 (ContinueExecution).
- **State transitions.** Decision Session: stays `Active` across reuse turns. Repository Run: `Executing→DecisionPending`(awaiting submit)`→`(on submit)`→Executing`. The Agent Process accumulates token count (the Router's real input, target).
- **Persistence.** Durable on **Submit**: `decisions/decisions.{NNNN}.md`. The streamed-but-unsubmitted decisions are ephemeral. Target: codex emits *structured* JSON parsed into `DecisionProposal` so the journal is not opaque prose (`design.md §13.9`).
- **Recovery.** The warm process is not resumable; on restart, cold-start a fresh Decision Session from `operational_context.md` and re-issue `GetNextDecisions(handoff)` for the last un-submitted handoff. An *un-submitted* decision turn is simply re-run — no human input is lost because none was durable yet.
- **Concurrency.** Exactly one active Decision Session per repository (`GetActiveSessionAsync`, single-active invariant enforced, `FileSystemDecisionSessionRepository.cs:47-58`); the held-open decision process count mirrors it.
- **Observability.** The decision SSE stream; the editable decision surface; the human's edits.
- **Completion.** **The human clicks Submit.** This is the system's central human gate — the relocation of today's `AwaitingAcceptance` (`design.md §7`). No codex signal completes a decision turn.
- **Failure.** Codex output unparseable → `Decisions` oracle/fallback supplies a deterministic proposal (`design.md §13.10`); the human still edits/submits. Process dies mid-turn → cold-start and re-issue.

---

### D5 · Decision Routing

> *Today:* fully built and deterministic — `IDecisionSessionLifecyclePolicy.EvaluateAsync(repositoryId)` → `Continue`|`Transfer` from `ReuseScore`/`TransferScore` (weighted sums over metrics/economics/coherence, `DecisionSessionLifecyclePolicy.cs:66-150`); **advisory, never mutating** (`:98-99`). *Target:* unchanged interface, fed the **live** process token count instead of `(len+3)/4`.

- **Purpose.** Decide whether the next Decision Turn **reuses** the warm process or **transfers** into a fresh one. Preserves: **the router governs Decision Session identity** — `Continue` keeps it, `Transfer` mints a new one.
- **Participants.** Repository Run (consults), the Router (authority), Transfer-Eligibility service, the token/metrics/economics/coherence services (inputs), the active Decision Session (subject). **Never participate:** any Agent Process (routing reads state, runs no codex), the Human, git.
- **Initiator.** The Repository Run, after each D3 ContinueExecution turn + handoff rotation (`design.md §12.D.17`).
- **Authority flow.** Before: Repository Run. During: **the Router holds identity-governance authority** — and holds it *purely*, mutating nothing. After: the Run acts on the verdict (re-enter D4 on reuse, or run D6 then D4 on transfer). The Router is a pure function; it never owns the process or the session.
- **Message flow.** *evaluate(repositoryId)* → Router reads metrics (token count), economics, coherence → computes `ReuseScore`, `TransferScore` → returns `Continue` (`TransferScore ≤ ReuseScore`, ties → Continue to avoid churn) or `Transfer`. On `Transfer`, *checkEligibility(repositoryId)* → `Eligible`|`Blocked`|`Deferred`|`NotApplicable` gates whether D6 may proceed.
- **State transitions.** None directly — the Router is non-mutating. Its verdict *causes* the next protocol's transitions.
- **Persistence.** Nothing of its own (the continuity artifact is written by D6). The verdict is captured into the continuity artifact's `PolicyEvaluation` snapshot if a transfer follows.
- **Recovery.** Stateless and idempotent — re-evaluable at any time. On router unavailability, default to the **continuity-preserving** choice (reuse the warm process if present, else transfer); never block the loop.
- **Concurrency.** One evaluation per loop iteration per repository; trivially safe (pure read).
- **Observability.** The verdict + `Reason` + `ContributingFactors` are inspectable diagnostics; no stream.
- **Completion.** A verdict is returned. Decided by the Router.
- **Failure.** Eligibility `Blocked`/`Deferred` → the transfer is refused; the loop **reuses** instead (degraded continuity, never a stall). Metrics unavailable → fall back to the deterministic estimate.

---

### D6 · Session Transfer

> *Today:* the machinery exists — `MarkTransferPendingAsync`/`MarkTransferredAsync`/`RetireSessionAsync` (`DecisionSessionRegistry.cs:46-114`), `DecisionSessionContinuityArtifact` with SHA-256 `ContinuityFingerprint`, `CreateAsync`/`AttachTargetSessionAsync` — but governs an abstraction, not a real process. *Target:* the `design.md §12.D.17.Transfer` sequence — produce delta, close old, rewrite context, seed new.

- **Purpose.** Retire a token-saturated Decision Session and mint a fresh one seeded by the continuity payload. **This is the one protocol where semantic authority changes ownership** — a new session *identity* takes over decision authoring. Preserves: the takeover is *governed* (router-decided, eligibility-gated, continuity-seeded), never silent.
- **Participants.** Repository Run (sequences), the old Decision Session (`Active→TransferPending→Transferred→Retired`), the new Decision Session (`Created→Active`), the **Operational Session** (UpdateOperationalContext — the one decision-adjacent step run in the Operational sandbox), the Continuity Artifact (the payload), Transfer-Eligibility (gate), Agent Runtime (close old / spawn new), Artifact Store (`operational_delta.md`, rewritten `operational_context.md`). **Never participate:** the Human (transfer is automatic), git.
- **Initiator.** The Repository Run, when D5 returns `Transfer` and eligibility is `Eligible`.
- **Authority flow.** Before: old Decision Session holds (saturated) content authority. During: a *handover* — the Continuity Artifact carries authority across the seam (delta + rewritten context). After: **the new Decision Session holds content authority**; the old is `Retired`. This transition is exactly where identity ownership moves — and it is fully governed by D5 + eligibility, so it never drifts.
- **Message flow.** *transfer()* → `MarkTransferPending` (old) → `[D] ProduceOperationalDelta.Text` on old process → capture stdout → write `operational_delta.md` → **close old process** → `[O] UpdateOperationalContext.Text` (one-shot, reads delta + old context) → rewrite `operational_context.md` → create `DecisionSessionContinuityArtifact` → `[D] StartDecisionSessionFromTransfer.Render(operational_context)` (held-open, seeds new process) → `MarkTransferred`(old→new) + `AttachTargetSession` → `RetireSessionAsync`(old) → `[D] GetNextDecisions.Render(handoff)` (re-enter D4).
- **State transitions.** Old Decision Session: `Active→TransferPending→Transferred→Retired` (`RetiredAt` set). New Decision Session: `Created→Active`. Metadata records `TransferReason`, `TransferredToSessionId`.
- **Persistence.** Durable: `operational_delta.md`, the rewritten `operational_context.md` (the literal transfer payload, not audit-only), the continuity artifact (`.agents/decision-sessions/continuity-artifacts/{artifactId}`) with its SHA-256 fingerprint, both session records.
- **Recovery.** If interrupted mid-transfer: the durable continuity artifact + `operational_context.md` are the resume seed — L2 cold-starts the *new* session from them and retires the old as orphan. A half-rewritten context falls back to the prior `operational_context.md`.
- **Concurrency.** Strictly serial within the loop; at most one transfer in flight per repository. The single-active invariant means old and new never both `Active`.
- **Observability.** The decision stream switches to the new process; the continuity artifact and transfer reason are inspectable.
- **Completion.** The new session is `Active` and streaming its first `GetNextDecisions` (re-enters D4). Decided by the Run.
- **Failure.** Delta capture fails → abort transfer, **reuse** the old process (eligibility would not have passed if context were unavailable). New-process spawn fails → the loop halts with the old session `Transferred` but no replacement — L2 cold-starts on next opportunity.

---

### S1 · Turn Streaming *(stream publication)*

> *Today:* fully built — `ExecutionMonitoringService` `Channel<ExecutionEvent>` per subscriber (`SingleReader=true, SingleWriter=false`, `:170-174`), `Dictionary<Guid,List<Channel>>` broadcast (`:359-370`), retained-then-live replay (`:190-194`), projected as `text/event-stream` with `id:`/`event:`/`data:{json}` framing (`ExecutionSessionsEndpoints.cs:101-103`). *Target:* the same substrate generalized to **three stream kinds** (planning, execution, decision) behind one client live-view.

- **Purpose.** Carry an Agent Process's turn output to many observers without coupling producer to consumer. Preserves: **the stream is bytes, the record is truth** — it never persists or decides.
- **Participants.** Event Stream (owns the broadcast), the producing Agent Process, the Repository Runtime (owns the per-repository streams), UI/recorder subscribers (observe). **Never participate:** domain authorities, the journal.
- **Initiator.** Any drive protocol that runs a turn (D1–D4, D6) — it is *invoked by*, never standalone.
- **Authority flow.** None semantic, before/during/after. It moves bytes.
- **Message flow.** Agent Process *emits(chunk)* → broadcast to all subscriber channels → SSE frames to clients. A new subscriber *replays* retained events, then resumes live.
- **State transitions.** None durable. Subscriber set grows/shrinks; retained buffer trims by count/bytes (`ApplyRetention`, `:334-346`).
- **Persistence.** None of the broadcaster. The *events* are persisted into the session record / journal by the consuming protocol, not by the stream.
- **Recovery.** None needed — on reconnect, re-subscribe and replay retained events. Durable history lives in the record/journal.
- **Concurrency.** Multi-subscriber, single-writer per stream. Independent across the three kinds and across repositories.
- **Observability.** It *is* the observability layer's live half.
- **Completion.** The turn's terminal event (`ProviderExited`/`TurnCompleted`) ends a stream cycle; subscribers may persist or detach.
- **Failure.** A slow/dead subscriber's channel is dropped without affecting others (`TryWrite`). A lost producer ends the stream with a terminal event; the consuming protocol handles the turn-level failure.

---

### S2 · Journal Rotation *(artifact production)*

> *Today:* `HandoffService` scans `.agents/handoffs/` for `handoff.{NNNN}.md`, takes `Max()+1` (`:134-155`), writes via `IArtifactStore` (`Path.Combine(repoPath, rel)`, `FileSystemArtifactStore.cs:22-28`) — but couples rotation to the `AwaitingAcceptance` side-effect (`:92-97`). *Target:* a **single rotation owner** using a run-scoped monotonic counter, *without* the `AwaitingAcceptance` transition (`design.md §7`).

- **Purpose.** Make each completed turn's output durable, monotonic, and replayable. Preserves: **one rotation owner per artifact** — exactly one writer of the `{NNNN}` sequence.
- **Participants.** The rotation owner / Artifact Store (owns the write), the Repository Run (initiates), the Repository Runtime (composes). **Never participate:** `HandoffService`'s `AwaitingAcceptance` side-effect on the loop path.
- **Initiator.** The Repository Run, at the end of every D3 (handoff) and on every D4 Submit (decisions), and within D6 (delta, context).
- **Authority flow.** None semantic — it is a durability act. The Run decides *when*; the rotation owner decides *the next number*.
- **Message flow.** *rotate(content)* → determine next `{NNNN}` (run-scoped monotonic counter, target; `Max()+1` scan, today) → `IArtifactStore.WriteAsync(handoffs/handoff.{NNNN}.md | decisions/decisions.{NNNN}.md)`.
- **State transitions.** None on a session record (target — the divergence from today's `AwaitingAcceptance`). The journal's high-water mark advances.
- **Persistence.** This protocol *is* the persistence boundary for forward progress: `handoffs/handoff.{NNNN}.md`, `decisions/decisions.{NNNN}.md`, `operational_delta.md`, `operational_context.md`. Append-only, monotonic.
- **Recovery.** The highest *complete* `{NNNN}` is every Run's resume point. A half-written entry is discarded and re-produced.
- **Concurrency.** Serialized through the rotation owner under the per-repository gate; never two writers of one sequence.
- **Observability.** Rotated artifacts are inspectable through the existing tabs.
- **Completion.** The artifact is on disk at the next sequence number. Decided by the rotation owner.
- **Failure.** Write failure → the turn is *not* considered complete; the Run re-runs it (the journal's last complete entry is unchanged). Today's `archiveFailure` path transitions to `Failed` (`HandoffService.cs:92`); the target rotation must surface the IO failure without a spurious state change.

---

## Protocol Dependency Graph

Prerequisite/triggering relationships. **This is a behavioral dependency structure, not a roadmap.**

```
                       L1 Repository Activation
                                │ (materializes the coordinator)
                                ▼
        ┌──────────────── Repository Runtime present ───────────────┐
        │                                                           │
        ▼ (PlanAuthoring)                                           ▼ (ExecutingPlan, via L2 on restart)
   D1 Plan Authoring                                          L2 Runtime Reconciliation
        │ (human: Execute)                                          │ (resumes Run from journal)
        ▼                                                           │
   D2 Run Activation ◀───────────────────────────────────────────┘
        │ (Run born; first handoff; Decision Session opened)
        ▼
   ┌──────────────────────  THE LOOP (composition, not a protocol)  ──────────────────────┐
   │   D3 Operational Turn ──▶ S2 Journal(handoff)                                          │
   │        │                                                                               │
   │        ▼                                                                               │
   │   D4 Decision Turn ──(human Submit)──▶ S2 Journal(decisions)                           │
   │        │                                                                               │
   │        ▼                                                                               │
   │   D5 Decision Routing                                                                  │
   │     ├─ Continue ──────────────────────────────────▶ re-enter D4 (warm process)        │
   │     └─ Transfer ─▶ D6 Session Transfer ─▶ S2 Journal(delta, context) ─▶ re-enter D4    │
   └───────────────────────────────────────────────────────────────────────────────────────┘
        │ (cancel / complete / shutdown)
        ▼
   L3 Runtime Disposal ──▶ (records + journal remain) ──▶ L1/L2 can re-activate

   S1 Turn Streaming  ── invoked by every D-protocol that runs a turn (D1, D3, D4, D6); owns nothing
   S2 Journal Rotation ── invoked by every D-protocol that completes a turn (D2, D3, D4, D6)
```

True dependency structure: **L1 gates everything** (no coordinator, no conversation). **D2 is the hinge** that turns authoring into a Run and is the sole prerequisite of the loop. **The loop is a composition** of D3→D4→D5→(D4 | D6→D4), not a protocol. **S1 and S2 are leaf substrate** — depended upon by every turn, depending on nothing. **L2 is the universal recovery path** that can re-enter the graph at D2 (resume an unfinished Run) from durable truth alone.

---

## Runtime Participation Matrix

Rows: runtime objects + consulted authorities + the Human. Columns: the eleven protocols. **O** = owns/initiates, **P** = participates (exchanges messages / changes state), **B** = observes, **·** = unaffected.

| Object \ Protocol | L1 | L2 | L3 | D1 | D2 | D3 | D4 | D5 | D6 | S1 | S2 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **Supervisor / Registry** | O | O | P | · | · | · | · | · | · | · | · |
| **Repository Runtime** | P | P | O | O | O | P | P | P | P | P | P |
| **Repository Run** | · | P | P | · | O | O | O | O | O | · | O |
| **Agent Runtime** | · | P | P | P | P | P | P | · | P | P | · |
| **Agent Process** | · | P | P | P | P | P | P | · | P | O¹ | · |
| **Planning Session** | · | · | P | P | P | · | · | · | · | · | · |
| **Operational Session** | P² | P | P | · | P | P | · | · | P | · | B |
| **Decision Session** | P² | P | P | · | P | · | P | B | P | · | B |
| **Event Stream** | · | · | P | P | P | P | P | · | P | O | · |
| **Router** (authority) | · | B | · | · | · | · | · | O | B | · | · |
| **Transfer-Eligibility** | · | · | · | · | · | · | · | P | P | · | · |
| **Continuity Artifact** | · | P | · | · | · | · | · | · | P | · | P |
| **Rotation owner / Artifact Store** | B³ | P | B | P | P | P | P | · | P | · | O |
| **GitService** (authority) | · | · | · | · | P | P | · | · | · | · | · |
| **Decisions oracle** (authority) | · | · | · | · | · | · | B | · | · | · | · |
| **Human** | P | · | P⁴ | O | O | · | O | · | · | B | · |

¹ The Agent Process is the *producer* of S1; the Event Stream owns the broadcast. ² As durable records read/loaded. ³ Read-only (load durable state). ⁴ Only for the *cancel* trigger.

**What the matrix reveals.** (1) The **Repository Run owns every loop protocol** (D2–D6 progress) while the **Repository Runtime only ever participates** in them (composition) — the spatial "composition never becomes domain" rule made temporal. (2) The **Supervisor owns only lifecycle** (L1–L2) and never touches a drive protocol — recovery is not a domain authority. (3) **No object owns more than one tier** except the Repository Runtime, whose ownership is always *composition* (L3, and the conductor role in drive protocols). (4) The **Router's single `O` is D5** and it is otherwise inert — the purest authority in the system. (5) **GitService participates in exactly two protocols** (D2, D3) — the outward-facing boundary is narrow and locatable.

---

## Authority Transition Matrix

For each protocol: who holds the load-bearing authority **before → during → after**. The column that matters is whether *semantic* (domain) authority moves.

| Protocol | Before | During | After | Semantic authority moves? |
|---|---|---|---|---|
| **L1 Activation** | Registry (lifecycle) | Registry | Repository Runtime (composition) | No — only composition materializes |
| **L2 Reconciliation** | Durable records (sole truth) | Supervisor (dispatch) | Each context's recovery authority | No — Supervisor never invents outcomes |
| **L3 Disposal** | Runtime/Run | Runtime/Supervisor | Durable records (sole truth) | No — live authority is released, not moved |
| **D1 Plan Authoring** | Human (intent) | **codex-in-Planning (content)** | Human (revise/execute) | Temporarily to codex; returns to human |
| **D2 Run Activation** | Human (Execute) | Operational Session (git) + Runtime (sequence) | **Repository Run (progress)** | Progress authority is *created*; git fired once |
| **D3 Operational Turn** | Repository Run | **codex-in-Operational (work)** | Repository Run | Temporarily to codex; returns to Run |
| **D4 Decision Turn** | Repository Run | **codex-in-Decision (content)** | **Human (Submit ratifies)** | To codex, then resolves to the human gate |
| **D5 Decision Routing** | Repository Run | **Router (identity governance, pure)** | Repository Run (acts on verdict) | No — Router mutates nothing |
| **D6 Session Transfer** | old Decision Session (saturated) | Continuity Artifact (carries authority) | **new Decision Session (content)** | **YES — identity ownership moves here, governed** |
| **S1 Streaming** | none (transport) | none | none | No — never holds domain authority |
| **S2 Journaling** | none (durability) | rotation owner (sequence) | none | No — durability act, not domain |

**The one drift-risk, neutralized.** Semantic authority changes ownership in exactly **one** protocol — **D6 Session Transfer**, where a Decision Session's content authority passes to a freshly-minted identity. That move is *always* preceded by D5 (router verdict) and a Transfer-Eligibility gate, and *always* carried by a fingerprinted Continuity Artifact. Everywhere else, authority either stays fixed (composition with the Runtime; progress with the Run; identity-governance with the Router) or oscillates temporarily to codex and back to the Run or the Human. **The conductor never authors; the author never conducts.** This is the behavioral statement of the object model's "composition never becomes domain."

---

## State Transition Model

Four durable state machines, driven by the protocols. (Today's machines verified; target additions marked.)

**Repository lifecycle** *(new — `design.md §5`)*: `(none) ──L1──▶ PlanAuthoring ──D2(after push)──▶ ExecutingPlan ──L3(complete/cancel)──▶ idle/PlanAuthoring`. Projects which surface is foregrounded.

**Repository Run** *(new — externalized to the journal)*: `(none) ──D2──▶ Executing ──D3 done──▶ DecisionPending ──D4 Submit──▶ Executing ──D5──▶ {Continue→DecisionPending | Transfer→(D6)→DecisionPending} ⟲ ──L3──▶ Cancelled / Completed`.

**Operational Session** `RepositoryExecutionState` *(exists, `RepositoryExecutionState.cs:3-13`)*: `Ready ──D3 start──▶ Executing ──provider exit──▶ AwaitingAcceptance ──Accept──▶ {Accepted/Ready | AwaitingCommit ──WithCommitResult──▶ AwaitingPush ──WithPushResult──▶ Ready}`; `Executing ──recover/exit≠0──▶ Failed`; `* ──cancel──▶ Cancelled`. **Target divergence:** the loop path **bypasses `AwaitingAcceptance`** (`design.md §7`) — the handoff flows straight to D4; commit/push happen inside D2 and any committing D3, not via the human Accept gate.

**Decision Session** `DecisionSessionState` *(exists, `DecisionSessionModels.cs:5-12`)*: `Created ──Activate──▶ Active ──D5 Transfer──▶ TransferPending ──D6──▶ Transferred ──▶ Retired`; `Active/TransferPending ──Retire──▶ Retired`. Metadata carries `TransferReason`, `TransferredToSessionId`.

**Protocol → transition map.** D2 drives `Run:(none)→Executing`, `Repo:PlanAuthoring→ExecutingPlan`, `Decision:Created→Active`. D3 drives `Operational:Ready→Executing→(target:available)`. D4 drives `Run:Executing→DecisionPending→Executing`. D5 drives no transition (pure). D6 drives `Decision:Active→TransferPending→Transferred→Retired` (old) and `Created→Active` (new). L2 drives `Operational:Executing→Failed`, `Decision:Active→Retired` (orphan). L3 drives `Run:→Cancelled`.

---

## Persistence Boundary Model

For each protocol, exactly where information crosses **ephemeral runtime → durable record → projection**.

| Protocol | Ephemeral (lost on restart) | Durable crossing (the write that survives) | Projection (recomputed) |
|---|---|---|---|
| L1 Activation | the Runtime object, lifecycle gate | *(none — reads only)* | repository lifecycle state |
| L2 Reconciliation | reconstructed Runtimes/Runs | recovery events; `Executing→Failed`, `Active→Retired` | recovery diagnostics |
| L3 Disposal | processes, streams, Run object | `Run→Cancelled` (cancel only) | terminal stream event |
| D1 Plan Authoring | planning process, stream | **`specs/*`, `plan.md`** (codex-written, verified) | rendered plan |
| D2 Run Activation | one-shot processes, plan cache | **`operational_context.md`, `handoff.0001`, commit/pushed SHA, Run journal open, Decision record `Active`** | `ExecutingPlan` |
| D3 Operational Turn | the one-shot process, stream | **`handoffs/handoff.{NNNN}.md`** (+ any commit SHA) | execution feed |
| D4 Decision Turn | warm process, streamed-unsubmitted decisions | **`decisions/decisions.{NNNN}.md`** (on Submit only) | editable decision surface |
| D5 Decision Routing | the verdict | *(none of its own)* | router diagnostics (`Reason`, factors) |
| D6 Session Transfer | old/new processes | **`operational_delta.md`, rewritten `operational_context.md`, continuity artifact (+SHA-256), both session records** | continuity view |
| S1 Streaming | subscriber channels, retained buffer | *(none — events persisted by the consuming protocol)* | the live view |
| S2 Journaling | — | **the `{NNNN}` artifact itself** (this protocol *is* the boundary) | inspection tabs |

**The boundary principle.** Every drive protocol has **exactly one durable crossing per turn**, and it is always a *journal rotation (S2)* or a *session-record write*. The streamed output (S1) is *never* the crossing — it is ephemeral until the consuming protocol rotates it. This is why **D4's durable crossing is Submit, not turn-complete**: the human's ratification is the persistence event. The plan cache `{id}:Plan` is a performance mirror of `operational_context.md`/`plan.md`, never a crossing.

---

## Recovery Boundary Model

For each protocol: safe restart points, safe replay points, safe retry points, non-recoverable boundaries, human-intervention boundaries.

| Protocol | Safe restart (resume from durable) | Safe retry (re-run idempotently) | Non-recoverable / human boundary |
|---|---|---|---|
| L1 Activation | always (reconstruct from records) | always (idempotent insert) | — |
| L2 Reconciliation | it *is* the restart | always (idempotent) | unclassifiable record → human review |
| L3 Disposal | n/a (clean teardown) | always | — |
| D1 Plan Authoring | from `specs/*`+`plan.md` (one-shot re-seed) | the turn (warm state is lost) | **human** decides plan is done (Execute) |
| D2 Run Activation | from `handoff.0001` if past push | milestones/StartExecution (pre-push) | **push is irreversible** — not retried once done |
| D3 Operational Turn | from last complete `handoff.{NNNN}` | the turn (discard half-written handoff) | committed work is durable; not rolled back |
| D4 Decision Turn | cold-start + re-issue `GetNextDecisions` | the un-submitted turn (no human input lost) | **Submit** is the human boundary |
| D5 Decision Routing | stateless — re-evaluate anytime | always (pure) | router down → default continuity choice |
| D6 Session Transfer | from continuity artifact + `operational_context.md` | re-seed new session; retire old orphan | half-rewritten context → fall back to prior |
| S1 Streaming | re-subscribe + replay retained | always | — |
| S2 Journaling | from highest complete `{NNNN}` | re-produce discarded partial | write failure → turn not "complete" |

**The two hard boundaries.** Only two boundaries are genuinely **non-recoverable**: (1) **D2's git push** — outward-facing and irreversible, which is why D2 must be a deliberate human act and the push must be idempotency-keyed on the rotated `handoff.0001`; (2) the **human Submit in D4** — not "non-recoverable" but *non-automatable*: the loop *must* pause for it, and no recovery may fabricate a decision. Every other boundary degrades to "reload the journal/record and rebuild the live tier." The asymmetry today (operational *fails*, decision is *diagnostic*) must unify under L2 into: operational → fail (conservative), decision → cold-start (if Run active) else retire, Run → resume from journal.

---

## Failure Model

| Failure | Detected by | Contained to | Recovery | User visibility |
|---|---|---|---|---|
| Application crash | L2 at startup | whole live tier | reconstruct Runtimes; reconcile sessions; resume Runs from journal | recovery diagnostics on tabs |
| Held process dies mid-turn (D1/D4) | turn-complete absent | one process + its stream | cold-start; re-issue last prompt | stream ends; turn re-runs |
| One-shot exit ≠ 0 (D3) | provider exit | one Operational Session | `Failed`; loop halts | failure surfaced on execution feed |
| Push fails (D2) | GitService result | pre-`ExecutingPlan` | Run not born; human re-executes | git error (reuse `GitWorkflowPanel`) |
| Router unavailable (D5) | call failure | the verdict | default continuity choice (reuse else transfer) | none — loop continues |
| Transfer blocked/deferred (D5→D6) | eligibility outcome | the transfer | **reuse** instead (degraded continuity) | inspectable eligibility reason |
| Corrupt `operational_context.md` | parse failure | one transfer/cold-start | fall back to `plan.md`/prior context; flag | review flag |
| Unparseable decision JSON (D4) | parse failure | one decision turn | `Decisions` oracle/fallback proposal | human still edits/submits |
| Journal write fails (S2) | IO error | one turn | turn not "complete"; re-run | turn appears to retry |
| Orphaned Decision Session | L2 classification | one record | retire (no Run) / cold-start (Run active) | diagnostic |
| **Two Runtimes, one repo** | — | — | **prevented by construction** (Registry keyed insert) | n/a |

**Unifying principle (behavioral form).** *No conversation can lose truth, because every completed turn is rotated to the journal before the next turn begins, and no turn is "complete" until its rotation succeeds.* Every failure degrades to re-speaking the last turn from the last durable artifact. The highest-severity *structural* failure — duplicate orchestrators conversing over one journal — is designed out at L1, not recovered.

---

## Observability Model

Three live streams, one driving surface, durable diagnostics — all already shaped today.

- **Streams (S1, the live half).** Three kinds — *planning* (`GET /plan/stream`), *execution* (`GET /execution/stream`, the existing `ExecutionSessionsEndpoints` shape), *decision* (`GET /decision/stream`) — all `text/event-stream` + `data:{json}` behind one client live-view generalized from `useExecutionEvents`. The user observes: codex's per-turn output as it streams, then (for D4) an editable decision surface on turn-complete.
- **The driving surface (the projection of run state).** The repository lifecycle state foregrounds *which* conversation is active: `PlanAuthoring` → the authoring surface (D1); `ExecutingPlan` → the loop surface (D3/D4). The seven inspection tabs are the always-available depth.
- **Durable diagnostics (internal, inspectable).** Router `Reason`/`ContributingFactors` (D5), `DecisionSessionRecoveryDiagnostics` and registry validation (L2), `WorkflowProjectionDiagnostics`/timeline, continuity artifact + transfer reason (D6), recovery findings. These are *not* streamed — they are recomputed projections over durable records.
- **What stays internal.** Process PIDs (diagnostic only), the plan cache, subscriber channel bookkeeping, the rotation counter. The user never observes a live process handle — only its stream.

The observability rule: **the user observes the conversation (stream) and the run's state (lifecycle projection); the user inspects the record (diagnostics, journal). Liveness is shown, never owned by the view.**

---

## Concurrency Model

- **Global, single-instance:** Supervisor/Registry (one), Agent Runtime (one), the Process Registry (bounds total processes across all repositories).
- **Per `repositoryId`, exactly one:** Repository Runtime (Registry-enforced — the no-duplicate-orchestrators guarantee), active Repository Run (≤1), active Decision Session (`GetActiveSessionAsync`, single-active invariant), active operational turn.
- **Per turn, strictly serial:** within a Run the conversation is totally ordered — D3 → D4 → D5 → (D4 | D6→D4) — and every mutation runs under the per-repository gate (`SemaphoreSlim(1,1)`, generalized from `ExecutionSessionService.cs:25` to the Runtime).
- **Multi-subscriber, single-writer:** every Event Stream (S1).
- **Independent across repositories:** all per-repository conversations run concurrently; the only shared bound is the global Process Registry capacity.

The model's rule, behaviorally: **all cross-request mutation for a repository funnels through one Runtime, one gate, one serial conversation; all fan-out is read-only streaming.** Concurrent across repositories, strictly serial within one — the only correctness-safe shape for a journal-backed Run. The loop's serialism is *not* a limitation: a turn cannot begin until the prior turn's rotation (S2) succeeds, which is precisely what makes any boundary a safe resume point.

---

## Protocol Composition Model

The architecture is **composed, not monolithic** — and naming the compositions (rather than building one "execution protocol") is the central behavioral decision.

- **The Decision Loop is a composition, not a protocol:** `D3 Operational Turn → D4 Decision Turn → D5 Routing → (Continue: re-enter D4 | Transfer: D6 → re-enter D4)`. Building this as one protocol would re-merge progress, content, routing, and identity authority into a god-conversation — exactly the object model's forbidden god-orchestrator, in time. Keeping it composed means each sub-protocol owns one authority and the loop is just their ordering.
- **Run Activation (D2) is a composition with a single irreversible step:** `close-planning (L3-fragment) → ExtractMilestones (D3-shape) → commit/push (git) → StartExecution (D3-shape) → first journal (S2) → open Decision Session`. It is cataloged separately because it crosses the outward-facing boundary and flips the lifecycle state — the one place several sub-acts must be a single atomic-feeling transition.
- **Session Transfer (D6) is a composition that crosses the Decision/Operational seam exactly once:** it runs one *Operational-sandbox* step (`UpdateOperationalContext`) amid otherwise-Decision steps. That single crossing is allowed because it is composed by the Repository Runtime (the only object permitted to see both roles), never by either session referencing the other.
- **Substrate is composed *into* every drive protocol:** S1 (streaming) and S2 (journaling) are not called standalone — they are the inhale/exhale of every turn. Every D-protocol *is* "spawn → S1 → turn-complete → S2."

The composition principle: **prefer many small protocols ordered by the Run over one large protocol owning everything.** The roadmap should deliver the *sub-protocols* (D3, D4, D5, D6) and let the *loop* emerge as their composition — never the reverse.

---

## Runtime Timeline

A complete conversation, from repository selected to idle, naming every protocol.

```
Repository selected
  │  L1 Repository Activation ── Registry materializes the Runtime; reads lifecycle state
  ▼
PlanAuthoring (no plan.md)
  │  D1 Plan Authoring ── Write → (Revise)* over a warm Operational process   [S1 streams each turn]
  ▼  (human: Execute)
  │  D2 Run Activation ── close planning · ExtractMilestones · COMMIT+PUSH · StartExecution
  │                       · first handoff [S2] · open Decision Session · → ExecutingPlan
  ▼
ExecutingPlan ── THE LOOP begins:
  │  D3 Operational Turn ── codex does work → handoff   [S1 streams; S2 rotates handoff.{NNNN}]
  │  D4 Decision Turn   ── codex reasons over handoff → decisions   [S1 streams]
  │                        → human EDITS → SUBMIT   [S2 rotates decisions.{NNNN}]   ◀ the human gate
  │  D5 Decision Routing ── Router: Continue or Transfer? (pure)
  │     ├─ Continue ──▶ re-enter D4 on the warm decision process
  │     └─ Transfer ──▶ D6 Session Transfer ── delta · close old · rewrite context · seed new
  │                       [S2 rotates delta + context; retire old, activate new] ──▶ re-enter D4
  │  ⟲ (loop until plan complete or cancelled)
  ▼
  │  L3 Runtime Disposal ── dispose processes/streams; journal + records remain
  ▼
Repository idle  ──(re-select / restart)──▶  L1 / L2 reconstruct from durable truth
                                              (L2 resumes an unfinished Run at D2/loop)
```

Every protocol appears exactly where the design's §12 places it; the timeline is the design's flow re-expressed as *conversations* rather than *steps*.

---

## Protocol Minimalism Review

Each candidate challenged: *is it irreducible, or derived?*

- **Decision Loop — derived (a composition).** It is D3→D4→D5→(D4|D6→D4). **Not a protocol.** Promoting it would create a god-conversation.
- **Session Reuse — derived (the degenerate branch of D5).** "Reuse" is just *re-enter D4 on the warm process* when the Router says `Continue`. It exchanges no new messages of its own. **Fold into D5/D4.**
- **Commit / Push — derived (a bounded act inside D2 and committing D3s).** It is the Operational Session's git authority exercised within a turn, not a conversation between objects. **Keep as a step; flag its irreversibility.** (Cataloged only as the timeline's one outward boundary.)
- **Human Intervention / Pause / Resume — derived (D4's completion condition).** The run *pauses* because D4 completes only on Submit; the human is a *participant* in D4, not a separate protocol. **Fold into D4.**
- **Operational Execution vs Operational Turn — the same protocol.** StartExecution (D2) and ContinueExecution (loop) are one turn shape with different prompts. **One protocol (D3).**
- **Repository Shutdown / Process Supervision / Cancellation — derived (triggers of L3).** Unload, shutdown, cancel, error are four *reasons* to dispose, one *protocol*. **Merge into L3.**
- **Repository Switching — derived.** It is L3-to-idle (or just de-foreground) on one repository plus L1 on another. **Compose, don't catalog.**
- **Runtime Synchronization — derived (it is S1 + the lifecycle projection).** No separate sync conversation exists. **Fold into S1/observability.**
- **Repository Recovery vs Runtime Recovery — one protocol (L2).** The three hosted services are one reconciliation concern. **Merge into L2.**

**The deepest reduction.** Beneath the six drive protocols sit just **two turn shapes**: a **one-shot turn** (D3, and D2's/D6's one-shot steps — today's *only* shape) and a **warm multi-turn dialogue** (D1 and D4 — the one genuinely new behavior). D1 and D4 are *the same shape* (held-open process, human-driven turns, human completion gate) distinguished only by **sandbox profile, content authority, and completion semantics** (D1 completes at Execute; D4 at Submit and feeds D5). Recognizing this collapses the new runtime work to **a single primitive — the held-open process — proven once and instantiated under two sandbox profiles.** The eleven protocols reduce, behaviorally, to **two turn shapes orchestrated by one Run, governed by one pure Router, recovered by one Supervisor, carried by two substrate channels.**

---

## Behavioral Readiness Assessment

- **Conversations already spoken correctly (substrate + governance):** S1 Streaming (certified `Channel`/SSE), S2 Journaling (4-digit rotation), D5 Routing (complete, deterministic, advisory), L2 Reconciliation (three hosted services, to be unified), and the entire D6 *machinery* (registry transitions, continuity artifact + fingerprint). These are conversations the system holds today, against a real (if abstract) subject.
- **Conversations that exist only as fragments (the drive tier):** D1 and D4 — the two warm dialogues — cannot be held at all (stdin closes, no reattach). D2 (Run Activation) and the loop composition exist only as `design.md §12` prose. The decision loop is *approximated* by a 60-second poll (`WorkflowContinuationHostedService`), not spoken interactively.
- **The single behavioral gating fact:** every drive protocol's *one-shot* steps already work; every drive protocol's *warm* steps wait on the one new primitive (held-open process). The Router waits on a *live* token count but degrades gracefully to its estimate. The human gate (D4 Submit) requires only the UI relocation of an existing acceptance pattern.
- **Behavioral leverage:** highest at proving the held-open process (unblocks both D1 and D4 simultaneously — one primitive, two protocols) and at *naming the Run* (turns the periodic poll into an interactive, journaled conversation). The risk lives almost entirely in one turn shape; the leverage lives almost entirely in conversations already half-spoken.

**Readiness verdict.** The behavioral architecture is reachable by *naming the conversations the system already half-holds and giving two of them a warm process to be spoken over.* The substrate and governance conversations are built and certified; the drive conversations are the same two turn shapes under different sandboxes; and because every turn is journaled before the next begins, the new live conversations are *safe to be interrupted at any boundary.*

---

## Final Conclusion — Command Center as a Behavioral System

Stripped of its services, endpoints, and transports, Command Center is **a small set of conversations between long-lived objects, ordered by one per-repository Run and made durable at every turn.** It is not a collection of request handlers; it is a state machine that *talks*.

**What conversations occur.** A coordinator is brought into being (L1) and reconciled against truth after any loss (L2). A human and codex co-author a plan over a warm context (D1). That plan is committed, pushed, and turned into a running Run (D2). Then the system's defining conversation repeats: codex does work and emits a handoff (D3); codex reasons over that handoff and proposes decisions the human edits and submits (D4); a pure router decides whether the next decision reuses the warm process or transfers into a fresh, continuity-seeded identity (D5/D6). Every turn is streamed live (S1) and rotated to an append-only journal (S2) before the next begins, and the live processes are released without loss whenever the conversation ends (L3).

**Which conversations define the product.** Exactly two: **D4 (the Decision Turn) and D5 (Decision Routing)**, composed into the loop with D3. D4 is where the product's value lives — codex proposes, the human ratifies, governed. D5 is where the product's *governance* lives — the routing of decision-session identity is the behavioral expression of the entire role-separation invariant. These two conversations *are* Command Center; D1 and D2 set them up, and L1/L2/L3/S1/S2 keep them safe and observable.

**Which conversations are implementation details.** S1 (streaming) and S2 (journaling) are substrate — load-bearing but owning nothing; they could be any transport and any durable store without changing the product. L1/L3 are bookkeeping. D6's *machinery* is detail; D6's *governed authority handover* is product.

**Which protocols the roadmap should be organized around.** The foundational protocols are **D3, D4, D5, and D6** — the loop's four sub-conversations — sitting on the **one new primitive** (the held-open process) that the warm dialogues (D1, D4) require, recovered by the **unified Supervisor (L2)** and carried by the **two substrate channels (S1, S2)**. The roadmap should deliver these sub-protocols and let the *loop* and the *run* emerge as their composition — never build a monolithic "execution protocol" that re-merges the authorities the protocols exist to keep apart.

This protocol architecture is the behavioral counterpart to the Runtime Object Model. Where the object model said *what exists* — five irreducible runtime objects, mostly already present and unnamed — this audit says *how they collaborate*: **eleven protocols reducible to two turn shapes, one governed loop, and one new primitive.** Together they give the roadmap its final foundation: a system organized not around the services it happens to have, but around the enduring conversations it was always trying to hold.
