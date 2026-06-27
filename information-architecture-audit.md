# Command Center — Information Architecture Audit

Status: architectural audit (information architecture reconstruction)
Date: 2026-06-26
Question answered: **What information exists inside Command Center, and how does it evolve over time — the stable information concepts the roadmap should ultimately be driven by, rather than runtime objects, protocols, services, or storage?**

Inputs treated as authoritative: the current implementation (`src/`, verified type-by-type across every bounded context for this audit), the certified governance/contract/regression net (`.agents/`, `tests/`, `docs/`, M0.x–M1.2), and the three prior audits — Vision Realization (`vision-realization-audit.md`, the destination), Runtime Object Model (`runtime-object-model-audit.md`, the nouns), Protocol Architecture (`protocol-architecture-audit.md`, the verbs) — plus the proposed design (`design.md`, the concrete flow).

The three prior audits reconstructed **destination**, **runtime**, and **behavior**. This audit reconstructs **information**. Where the Runtime Object Model asked *what exists* and the Protocol Architecture asked *how those things collaborate*, this audit asks the prior question both depend on: *what is the substance being held, moved, and preserved* — the information that exists before any object owns it, any protocol moves it, or any store persists it. Runtime objects exist to own information; protocols exist to move it; persistence exists to preserve it; UI exists to project it. The information itself is the architectural constant, and it is what the roadmap should be organized around.

Method, as before: every claim about *today* is grounded in a verified type or artifact (`Type` / `file:line`, or a concrete `.agents/` path); every claim about *the target* is derived from the design and the three prior audits; the two are kept explicitly distinct so the information architecture is never confused with current code.

---

## Executive Information Assessment

**Command Center is not a decision tool, an execution runner, or an inspection dashboard. It is a system for transforming human *Intent* into an ever-evolving *Operational Context* — the living, compressible model of "what is true about this repository's work, and why" — advanced one Handoff→Decision increment at a time, with every increment journaled append-only and bound to its evidence.** Every runtime object, every protocol, and every store exists to seed that context, increment it, project it, govern its shapes, or give it momentary liveness. The Operational Context is the one piece of information in the system that must never lose identity, and it is the information from which every live process, every projection, and every future decision can be rebuilt.

The decisive information finding mirrors — at the data layer — what the runtime audit found at the object layer and the protocol audit found at the behavioral layer: **the system already holds nearly all of its irreducible information in durable, certified form, and the genuinely new work is narrow.** The implementation has been accreting an extraordinarily rich information model — structured decisions with evidence/history/relationships (`CommandCenter.Decisions`, ~115 types), a fingerprinted continuity-and-economics corpus (`CommandCenter.DecisionSessions`), a compression-aware operational-context pipeline (`CommandCenter.Continuity`), a queryable reasoning graph (`CommandCenter.Reasoning`), and a generated-contract/governance net — all serialized to `.agents/` and `%APPDATA%`. What it lacks is not information *richness* but information *naming*: four of its load-bearing concepts (Intent, Plan, the Run Journal, and the loop's structured Decision) exist today only as **prose markdown or transient strings**, with no identity, no version, and no schema, even though information-rich structured counterparts already exist one context away.

Three structural information facts dominate everything below:

1. **The three-tier liveness split has an information twin: a three-tier *authority* split.** Information is either **authoritative** (born from a human or codex authoring act, irreplaceable), **derived** (a projection recomputable from authoritative information at will), or **transient** (liveness that is designed to be lost). The system's integrity rests on never confusing the three — a derived projection (a dashboard, the reasoning graph, a router score) must never become the source of truth, and a transient signal (a stream chunk, a token count, a memory cache) must never be persisted as if authoritative.

2. **There is exactly one append-only spine, and it is the product's memory.** The `.agents/` journal — rotated handoffs, rotated decisions, continuity artifacts, the reasoning graph, the operational-context history — is the only information that must survive forever, because it is the only information that *cannot be regenerated*: it records observations of work and acts of human ratification that have no other source.

3. **Compression is a first-class information act, not an afterthought.** The Operational Context is, definitionally, a *lossy compression* of the full handoff/decision history (`OperationalContextCompressionResult`/`Summary`, the assimilation limit of 8). The system already has an explicit, evidence-backed forgetting mechanism — which is exactly what a long-lived agent runtime needs, and exactly the kind of thing most systems lack.

**Verdict.** Command Center's information architecture is **a small set of authoritative information objects — Intent, Plan, Handoff, Decision, Operational Context — bound by pervasive Evidence/Provenance, journaled append-only, projected into derived knowledge (Reasoning, Workflow, dashboards), and governed by a meta-information net (Prompts, Contracts, Governance records) that owns the *shapes* information flows through without ever owning the information itself.** The irreducible information already exists and is mostly certified; the new work is to give four implicit concepts first-class identity and to converge the loop's prose Decision onto the structured model that already exists. The roadmap should be organized around these enduring information objects, because they are the substance every runtime object and protocol exists to serve.

---

## Information Object Catalog

Each information object is profiled against the full template (Meaning, Identity, Authority, Origin, Evolution, Relationships, Persistence, Historical Value, Compression, Evidence, Trust, Projection). Objects are numbered **I-n** and ordered along the information flow: authored seed → forward-progress increments → accreting understanding → session governance → derived knowledge → meta-information → substrate. For each, **Today** grounds it in verified types/artifacts; **Target** states what it becomes.

The catalog deliberately groups the ~150 concrete types discovered across the contexts into **sixteen first-class information objects**; the Minimalism Review reduces these to the irreducible core.

---

### I-1 · Intent *(Roadmap + Specs)*

> *Today:* exists only as free text in two textareas persisted to `.agents/specs/roadmap.md` and `.agents/specs/s{n}.md` (`design.md §7,§11`). No `Intent` type, no identity, no versioning. *Target:* the authoritative human-authored seed of a run — still markdown, but recognized as a first-class information object.

- **Meaning.** What the human wants done: a roadmap plus any number of specifications. The originating desire from which everything downstream derives.
- **Identity.** `(repositoryId, run)` — there is one Intent per run-to-be. Today identity is implicit in the file paths.
- **Authority.** **The Human owns it absolutely.** Nothing else may author or modify it. Codex *reads* it; never writes it.
- **Origin.** Born in the Plan Authoring surface, written to `specs/` at Write Plan.
- **Evolution.** Largely write-once per run; the human may rewrite specs before executing. After Execute it is frozen as the run's premise.
- **Relationships.** The sole input to **Plan** (I-2). Referenced (as `specs/`) by nothing downstream — its influence flows entirely through the Plan it seeds.
- **Persistence.** `.agents/specs/roadmap.md`, `.agents/specs/s{n}.md`. Survives the run as the durable record of *why*.
- **Historical value.** High — it is the only record of original intent. Should be preserved per run (archived, not overwritten across runs).
- **Compression.** Must remain lossless; it is small and irreplaceable.
- **Evidence.** It *is* primary evidence — the root provenance for every decision that cites "the roadmap said…".
- **Trust.** Maximal (human-authored, observed, never inferred).
- **Projection.** The Roadmap/Specs textareas; read-only thereafter.

### I-2 · Plan *(+ Plan Revisions)*

> *Today:* `.agents/plan.md`, written **by codex**, existence-verified by the orchestrator (`design.md §12.A.3`); mirrored as a transient `{repositoryId}:Plan` memory string (`design.md §8`). Revisions are ephemeral conversation turns (`RevisePlan.Render(feedback)`) leaving no versioned trace. No `Plan` type. *Target:* a first-class plan with identity and (optionally) captured revisions; the cache demoted to a pure read-through mirror.

- **Meaning.** The codex-authored strategy that realizes the Intent — the milestone-bearing plan the run executes against.
- **Identity.** `(repositoryId, run)`; one active plan per run. A Plan Revision is a successor version of the same identity.
- **Authority.** **Codex authors the content; the Human ratifies by executing.** The orchestrator owns only *existence verification*, never content. The Plan is authoritative once Execute fires (it becomes the run's premise and the seed of Operational Context).
- **Origin.** Born from `WritePlanForNewCodebase`/`WritePlanAgainstCodebase` over the warm planning process; revised via `RevisePlan.Render(feedback)`.
- **Evolution.** Iteratively revised over one warm context until the human executes; then copied to Operational Context (I-5) and frozen.
- **Relationships.** Derived from **Intent** (I-1). Seeds **Operational Context** (I-5, via copy at D2). Re-read every loop turn as the `plan` argument to `ContinueExecution`. Milestones are extracted from it (`ExtractMilestones`).
- **Persistence.** `.agents/plan.md` (durable). `{repositoryId}:Plan` is a *performance mirror*, not a persistence tier (run-scoped, evicted on completion).
- **Historical value.** Medium-high. The executed plan is a historical fact; pre-execution revisions are lower-value (the design treats them as ephemeral). A genuine evolution opportunity: capture Plan Revisions as versioned information rather than losing them.
- **Compression.** The plan itself stays lossless; its *understanding* is what gets compressed into Operational Context downstream.
- **Evidence.** Cites the Intent; is itself evidence for milestone extraction and for every operational turn that executes against it.
- **Trust.** High but *inferred by codex* — unlike Intent (observed), the Plan is a generated artifact and is trusted only after human ratification (Execute).
- **Projection.** Rendered in the authoring surface with a Copy control; thereafter the silent premise of the loop.

### I-3 · Handoff *(the operational increment)*

> *Today:* `.agents/handoff.md` written by each operational turn, then rotated to `.agents/handoffs/handoff.{NNNN}.md` (4-digit, `HandoffService.cs:134-155`); referenced by `ExecutionSession.HandoffPath`. Rotation today couples to an `AwaitingAcceptance` state side-effect (`HandoffService.cs:92-97`). *Target:* the same artifact, rotated by a single run-scoped owner *without* the `AwaitingAcceptance` side-effect; the handoff flows automatically into a Decision Turn.

- **Meaning.** What one operational (coding) turn did, and what questions/issues it raises for adjudication. The atomic record of work performed.
- **Identity.** `(repositoryId, run, NNNN)` — a monotonic sequence within a run. Each handoff is a distinct, immutable increment.
- **Authority.** **Codex-in-the-Operational-Session authors it.** No one edits a handoff; the Run rotates it; downstream only reads it.
- **Origin.** Born at the end of every Operational Turn (D3, and the first one inside D2's `StartExecution`).
- **Evolution.** None after rotation — it is **append-only and immutable** once written to `handoffs/handoff.{NNNN}.md`.
- **Relationships.** Produced by an Operational Turn; consumed as the input to the next **Decision** (I-4, via `GetNextDecisions.Render(handoff)`). Re-extracted by Continuity into operational-context inputs. The single most-consumed forward-progress artifact.
- **Persistence.** `.agents/handoffs/handoff.{NNNN}.md`. Append-only; never rewritten. The current `handoff.md` is a transient staging slot moved into the sequence.
- **Historical value.** Maximal — a handoff is an *observation of work* with no other source; it can never be regenerated. The journal of handoffs is the literal history of execution.
- **Compression.** Individually lossless; collectively summarized into Operational Context. The full handoff history is preserved even after compression (the context is a view, not a replacement).
- **Evidence.** Primary evidence for the decisions it triggers; cited by reasoning events and operational-context items.
- **Trust.** High (codex-authored observation), but *unratified* — it states what happened and what is open; the human's judgment enters at the Decision, not the Handoff.
- **Projection.** The execution stream (live) and the inspection tabs (durable); never edited in the UI.

### I-4 · Decision *(the ratified increment — the product's core)*

> *Today:* **two disconnected representations.** (a) The information-rich structured model in `CommandCenter.Decisions`: `Decision` (`Models/Decision.cs:5`), `DecisionProposal` (`:5-28`), `DecisionCandidate`, `DecisionPackage`, with `DecisionOption`/`DecisionTradeoff`/`AnalyzedDecisionOption`/`DecisionRecommendation`/`DecisionResolution`/`DecisionEvidence`/`DecisionHistoryEntry`/`DecisionRelationship`/`DecisionQualityAssessment` — all immutable records serialized via `DecisionArtifactDocument<T>`. (b) The loop's `.agents/decisions/decisions.{NNNN}.md` — **opaque prose** streamed from codex and human-edited (`design.md §12.D.14`, gap §13.9). *Target:* converge (b) onto (a) — codex emits structured JSON parsed into `DecisionProposal`, so the loop decision *is* the rich model, not prose.

- **Meaning.** The adjudicated verdict over a handoff: options, tradeoffs, a recommendation, and a human-ratified resolution. **This is where the product's value lives** — codex proposes, the human ratifies, governed.
- **Identity.** Structured: `DecisionId` (+ `RepositoryId`). Loop artifact: `(repositoryId, run, NNNN)`. Identity is stable across the decision's lifecycle; its *state* changes (`Open → UnderReview → Resolved → Superseded → Archived`, `Primitives/DecisionState.cs`).
- **Authority.** **Codex proposes the content; the Human is the completion authority (Submit ratifies); `CommandCenter.Decisions` owns the *shape* (validator/oracle/fallback).** This tri-party authority — propose / ratify / validate — is the single most important authority arrangement in the system, and the one place human judgment is load-bearing and non-automatable.
- **Origin.** Today: computed deterministically by `DecisionGenerationService` (templates + weighted-sum scoring). Target: authored by codex over a handoff (`GetNextDecisions`), edited by the human, validated against `DecisionProposal`.
- **Evolution.** State machine with an **append-only `History[]`** (`DecisionHistoryEntry`: timestamp, event, from/to state, reason, sources). Resolution is written once at closure (`DecisionResolution`, immutable). Relationships to other decisions (`DependsOn`/`Supersedes`/`ConflictsWith`/…) accrete.
- **Relationships.** Derived from a **Handoff** (I-4 ← I-3); feeds the next Operational Turn (as the `decisions` argument to `ContinueExecution`); re-extracted by Continuity into `DecisionSignal`/`DecisionAssimilationRecord` for context assimilation; materialized by Reasoning into the graph. The most relationship-dense object in the system.
- **Persistence.** Structured: `CommandCenter.Decisions` store (JSON, `DecisionArtifactDocument<T>`). Loop: `.agents/decisions/decisions.{NNNN}.md`, append-only on Submit.
- **Historical value.** Maximal — a ratified decision is a human judgment with no other source; decisions must be preserved forever, append-only, with full history.
- **Compression.** Individual decisions are lossless; their *operational consequences* are assimilated (with an explicit limit) into Operational Context. Superseded decisions are retained, not deleted (`DecisionState.Superseded`).
- **Evidence.** The richest evidence model in the system: `DecisionEvidence` + `DecisionSourceReference` (source kind, path, section, excerpt) on every option, tradeoff, recommendation, and resolution. Every decision is traceable to the handoff, plan, and prior decisions that justify it.
- **Trust.** Codex-proposed (inferred) → human-ratified (observed) → schema-validated (checked). Quality is explicitly assessed (`DecisionQualityAssessment`/`Signal`, with `HumanAuthoringBurden` signals).
- **Projection.** The decision stream (live), then an **editable** surface seeded with the proposal (the human gate), then the inspection decisions tab. The only forward-progress information the human *edits*.

### I-5 · Operational Context *(+ Operational Delta — the accreting truth)*

> *Today:* `.agents/operational_context.md` (markdown, parsed into `OperationalContextDocument`, `Continuity/Models/OperationalContextDocument.cs:3`), generated through a rich pipeline producing `OperationalContextProposal` (`:5`) under `.agents/operational_context/proposals/{proposalId}/`, with `DecisionAssimilationProjection`, `OperationalContextCompressionResult`/`Summary`, `OperationalContextSemanticChange`, and `OperationalContextInputFingerprint`. The transfer delta is `.agents/operational_delta.md`. *Target:* the literal **transfer payload** seeding fresh decision sessions, rewritten on transfer via `UpdateOperationalContext` — its role sharpens from audit record to active state hand-off.

- **Meaning.** The living model of *what is true about this repository's work and why* — current mental model, architecture, constraints, stable decisions and their rationale, open questions, active risks, recent understanding changes. **The one piece of enduring understanding the whole system accretes.**
- **Identity.** `repositoryId` (one current context per repository), with a rotated version history. The Delta is the diff between two versions.
- **Authority.** **Generated/owned by `Middle`/`Continuity` (`IOperationalContextGenerationService`); rewritten by codex on transfer (`UpdateOperationalContext`).** It is authoritative *understanding*, distinct from the authoritative *increments* (Handoff/Decision) it summarizes.
- **Origin.** Born at Run Activation as a **copy of the Plan** (`plan.md → operational_context.md`, `design.md §12.C.6`); thereafter regenerated/rewritten from accumulated handoffs + decisions.
- **Evolution.** Compressed and rewritten over time: `OperationalContextProposal` captures generated vs edited content, semantic changes, decision assimilation (with a limit of 8 to stay reviewable), and a compression summary classifying every item (preserved/added/modified/removed/compressed). On transfer: `ProduceOperationalDelta` → `operational_delta.md` → `UpdateOperationalContext` rewrites the context.
- **Relationships.** Seeded by **Plan** (I-2); summarizes **Handoffs** (I-3) and **Decisions** (I-4); carried by the **Continuity Artifact** (I-7) across a transfer; the seed for every fresh Decision Session (`StartDecisionSession.Render(operationalContext)`).
- **Persistence.** `.agents/operational_context.md` (current), rotated history, `.agents/operational_delta.md`, and structured proposals under `.agents/operational_context/proposals/{proposalId}/`.
- **Historical value.** The *current* context is load-bearing; prior versions are valuable history but reconstructable from the journal. The Delta is a precious audit of *how understanding changed*.
- **Compression.** **This object IS the system's compression mechanism** — a deliberately lossy, evidence-backed summary of the full history, with explicit limits, noise-removal indicators, and stable-understanding retention warnings. The most architecturally significant compression in the system.
- **Evidence.** Every context item carries `SourceRelativePath` and rationale; input fingerprints record exactly which artifacts (and their hashes) produced this version; semantic changes carry supporting evidence and an identity basis.
- **Trust.** Generated (inferred) but evidence-backed and fingerprinted; the most-validated derived-yet-authoritative object — it is *derived* from increments but *authoritative* as the transfer payload.
- **Projection.** The operational-context tab; the basis of the continuity view; never directly edited in the loop (edited only as a reviewed proposal).

### I-6 · Session Record *(Operational + Decision identity)*

> *Today:* `ExecutionSession` (`Execution/Models/ExecutionSession.cs:5`; Guid; `RepositoryExecutionState`; `CommitSha`/`PushedCommitSha`/`HandoffPath`/`Events`; JSON at `execution-sessions.json`) for the operational role; `DecisionSession` (`DecisionSessionModels.cs:24`; `DecisionSessionId`; `Created→Active→TransferPending→Transferred→Retired`; `Ownership`; `Metadata`; JSON at `.agents/decision-sessions/registry.json`) for the decision role. *Target:* both bind to a live agent process; the Decision role gains codex without referencing Execution.

- **Meaning.** The durable identity and state-machine of one unit of agent work. The *record* whose *liveness* is a detachable process.
- **Identity.** `ExecutionSession.Id` (Guid) / `DecisionSessionId`, each subordinate to `repositoryId`. **Decision Session identity is governed by the router** — preserved on `Continue`, newly minted on `Transfer`.
- **Authority.** Each context owns its own record and state machine; the operational record owns git facts (commit/push SHAs); neither references the other's orchestration.
- **Origin.** Operational: created at execution start. Decision: created at Run Activation / transfer.
- **Evolution.** State-machine transitions; the operational record carries an **append-only `Events[]`** (`ExecutionEvent`, sequenced) and accreting git SHAs; the decision record carries mutable transfer metadata (`TransferReason`, `TransferredToSessionId`).
- **Relationships.** Owns/points to its Handoff (operational) and continuity artifacts (decision); the subject of routing/transfer/recovery.
- **Persistence.** Operational: `%APPDATA%\CommandCenter\execution-sessions.json`. Decision: `.agents/decision-sessions/registry.json` + analysis snapshots.
- **Historical value.** The identity and its terminal state are durable history; the live process state is disposable.
- **Compression.** Records persist in full; their content (handoffs/decisions) is compressed elsewhere.
- **Evidence.** The operational `Events[]` are an evidence trail of provider/git activity; the decision record links to its continuity artifacts.
- **Trust.** Observed (state transitions are facts), not inferred.
- **Projection.** Execution/decision inspection tabs; recovery diagnostics.

### I-7 · Continuity Artifact *(the transfer certificate)*

> *Today:* `DecisionSessionContinuityArtifact` (`DecisionSessionAnalysisModels.cs:354`): `ArtifactId`, source/target session, `PolicyEvaluation` snapshot, metrics/economics/coherence snapshots, decision/reasoning/operational-context references, and a **SHA-256 `ContinuityFingerprint`**; immutable; at `.agents/decision-sessions/continuity-artifacts/`. Paired with `DecisionSessionTransfer` (append-only events). *Target:* the concrete continuity payload is the pair `operational_delta.md` + rewritten `operational_context.md` (`design.md §9`).

- **Meaning.** The immutable, integrity-checked certificate that authority over decision-authoring passed from one session identity to a fresh one, seeded by a captured understanding.
- **Identity.** `ArtifactId` (`continuity.{sourceSessionId}.{timestamp}`), immutable.
- **Authority.** Created once by the continuity-capture service; **never modified.** It is the only object whose entire purpose is to *certify* an authority handover.
- **Origin.** Born at transfer, capturing the router verdict, the analysis snapshots, and references to the source session's corpus.
- **Evolution.** None — write-once, fingerprinted.
- **Relationships.** References the source/target **Session Records** (I-6); carries the **Operational Context** (I-5) as the seed; snapshots the **Routing Assessment** (I-8).
- **Persistence.** `.agents/decision-sessions/continuity-artifacts/continuity.{id}.json`.
- **Historical value.** High — it is the auditable proof of every transfer; append-only across a run's transfers.
- **Compression.** Lossless; it is itself a compressed snapshot but must not be further reduced.
- **Evidence.** The fingerprint *is* integrity evidence; references link to the corpus it summarizes.
- **Trust.** Maximal integrity (SHA-256), capturing inferred analysis at a point in time.
- **Projection.** The continuity view; the transfer reason is inspectable.

### I-8 · Economic / Routing Assessment *(advisory governance)*

> *Today:* fully built and deterministic. `DecisionSessionMetricsSnapshot`/`EconomicsSnapshot`/`CoherenceSnapshot` (`DecisionSessionAnalysisModels.cs:80,160,229`) feed `DecisionSessionLifecycleEvaluation` (`:284`: `Decision`=Continue/Transfer, `ReuseScore`, `TransferScore`, `Reason`, `ContributingFactors`), gated by `DecisionSessionTransferEligibility` (`:320`: NotApplicable/Eligible/Blocked/Deferred). Token input via `DeterministicTokenEstimator` `(len+3)/4` (`DeterministicTokenEstimator.cs:9`). *Target:* unchanged shapes, fed the **live** process token count; estimate retained as fallback.

- **Meaning.** The computed, advisory information that governs decision-session identity: should the next decision reuse the warm process or transfer to a fresh one?
- **Identity.** `(repositoryId, generatedAt)` snapshots; the evaluation is keyed by its scores and reason.
- **Authority.** Owned by the router/economics services as **pure, non-mutating** authorities. **Advisory only** — it changes no durable domain information; it *informs* a transition the Run executes.
- **Origin.** Computed on demand from session metrics/economics/coherence after each loop turn.
- **Evolution.** Recomputed each iteration; snapshots are immutable point-in-time captures.
- **Relationships.** Consumes **Session Record** + **Operational Context** metrics; informs **Continuity Artifact** (I-7, captured into `PolicyEvaluation`); its verdict drives whether D6 Transfer runs.
- **Persistence.** Snapshots under `.agents/decision-sessions/{analysis,lifecycle}/...`. The verdict itself owns no durable state beyond the continuity capture.
- **Historical value.** Low-medium — useful diagnostics; fully regenerable from the inputs.
- **Compression.** Freely discardable and recomputable.
- **Evidence.** `ContributingFactors`/diagnostics explain every score; assessments cite their component contributions (`ReuseScoreAssessment`/`TransferScoreAssessment`).
- **Trust.** **Inferred and explicitly degradable** — token count is an estimate today; the router defaults to the continuity-preserving choice when inputs are unavailable. The clearest example of *derived, non-authoritative* information in the system.
- **Projection.** Router reason/factors and eligibility findings as inspectable diagnostics; never streamed.

### I-9 · Reasoning Graph *(durable derived knowledge)*

> *Today:* authoritative graph records — `ReasoningEvent`/`ReasoningThread`/`ReasoningRelationship` (`Reasoning/Models/ReasoningRecords.cs:47,61,72`; immutable; JSON+MD under `.agents/reasoning/{events,threads,relationships}/{id}/`) — plus derived projections `ReasoningGraph`/`ReasoningTrace`/`ReasoningReconstruction` (transient, recomputed) and `ReasoningCertificationReport`. Rich provenance (`ReasoningProvenance`/`ReasoningCaptureProvenance`). *Target:* largely stable; its input becomes codex-authored decisions.

- **Meaning.** The queryable memory of *how the work reasoned* — events, threads, and typed relationships materialized over decisions and sessions; the organizational knowledge layer.
- **Identity.** `ReasoningEventId`/`ThreadId`/`RelationshipId` (e.g. `EVT-NNNN`), subordinate to `repositoryId`.
- **Authority.** Owned by `CommandCenter.Reasoning`; **read-only over decisions/sessions** — it materializes, it does not author domain decisions (boundary-enforced: `ReasoningBoundaryViolation`).
- **Origin.** Captured (manually or automatically) from decision/session activity, with explicit capture provenance.
- **Evolution.** Graph records are **immutable and append-only**; threads accrete event ids; graph/trace/reconstruction are recomputed on demand.
- **Relationships.** Derived from **Decisions** (I-4) and **Session Records** (I-6); references them via `ReasoningReference` (kind/id/path/section/excerpt/fingerprint).
- **Persistence.** `.agents/reasoning/...` (JSON+MD dual representation — machine + human). Reports under `.agents/reasoning/reports/`.
- **Historical value.** High — the durable derived memory; but fully *reconstructable* from the decisions/sessions it materializes over.
- **Compression.** The graph is the compression (a navigable summary of reasoning); individual events are lossless.
- **Evidence.** Provenance and references on every node/edge; certification reports prove materialization quality.
- **Trust.** Derived but evidence-linked; confidence is explicit in reconstructions (`Confidence`/`ConfidenceRationale`).
- **Projection.** The reasoning tab; graph/trace/reconstruction views; historical queries (`HistoricalAt`).

### I-10 · Run / Workflow Projection *(derived progress)*

> *Today:* `WorkflowInstance` (`Workflow/Models/WorkflowInstance.cs:5`; keyed by `repositoryId`(+stage); ~46 fields; **derived/computed on demand**) with `WorkflowProjectionDiagnostics`, timeline, gates; plus `ExecutionContext` and the `Repository{Dashboard,Workspace}Projection` (`Middle/Projections/`). *Target:* the runtime audit **demotes `WorkflowInstance` to a projection** the Run produces; the Run's own progress state is externalized to the journal.

- **Meaning.** The recomputable view of *where the run is* — current stage/progress/blocking-gate, timeline, and the composed repository dashboard/workspace.
- **Identity.** `repositoryId` (one current projection per repository).
- **Authority.** **No authority of its own** — it is a pure projection over execution/decision/continuity/reasoning records. Must never become a source of truth (the runtime audit's explicit demotion).
- **Origin.** Computed per request by projection services from durable records.
- **Evolution.** Recomputed continuously; holds no durable state.
- **Relationships.** Derives from **Session Records**, **Handoff**, **Decision**, **Operational Context**, **Reasoning** — the most downstream consumer.
- **Persistence.** None (transient) — except diagnostics that may be logged.
- **Historical value.** None intrinsically; its inputs hold the history.
- **Compression.** Entirely derivable; freely discarded.
- **Evidence.** `WorkflowProjectionDiagnostics` explains chosen stage/gate and transition reasoning (a projection that explains itself).
- **Trust.** Derived; trustworthy only insofar as its inputs are.
- **Projection.** **It is itself the projection layer** — the dashboard and the seven inspection tabs.

### I-11 · Prompt *(authored instruction + execution manifest)*

> *Today:* the `Lib.Prompts` mechanism (Roslyn generator) exists; **no prompts in `CommandCenter.Core`** yet. The 11 PoC `.prompt` files generate `CommandCenter.Core.Prompts.*` classes with `const Template`, `const SourceHash`, and `Text`/`Render(...)` members (`design.md §3`). Separately, `ExecutionPromptManifest` (`Execution/Models/ExecutionPromptManifest.cs`) is the rich provider-interaction record (requested vs delivered artifacts, divergence reason, context bytes). *Target:* add the 11 `.prompt` files; every codex call uses generated `Render`/`Text`.

- **Meaning.** Two related things: the **authored instruction** that drives every codex turn (the prompt template), and the **manifest** certifying what was actually sent to and delivered by a provider.
- **Identity.** Prompt: class name + `SourceHash` (content fingerprint). Manifest: `(sessionId, generatedAt)`.
- **Authority.** Prompts are **hand-authored source of truth** (the `.prompt` files); the generated classes are derived-and-drift-protected. The manifest is a generated governance record.
- **Origin.** Prompts authored under `Core/Prompts/`, compiled by the generator. Manifests produced at each execution.
- **Evolution.** Prompt templates evolve with code (the `SourceHash` detects drift); manifests are immutable execution records.
- **Relationships.** Prompts are the input *form* of every transformation (Intent→Plan, Handoff→Decision, etc.); the manifest links a turn to the exact artifacts and context that fed it.
- **Persistence.** Prompts: source tree (generated at build). Manifest: execution session store.
- **Historical value.** Prompt templates: versioned with code. Manifests: high — they are the provenance of *what the agent was actually told*.
- **Compression.** Templates lossless; manifests are themselves compact records.
- **Evidence.** `SourceHash` is integrity evidence for prompts; the manifest *is* evidence (requested vs delivered, divergence reason — the system already records when reality diverged from intent).
- **Trust.** Prompts: authored (maximal). Manifest: observed record of a provider interaction.
- **Projection.** Prompts are invisible (the silent form of every turn); manifests are inspectable governance artifacts.

### I-12 · Contract *(the canonical shape of projected information)*

> *Today:* the M0.2–M1.2 net. Golden fixtures (`tests/.../ContractFixtures/*.golden.json`, **authoritative** baselines), the canonical contract model / IR (`ContractGenerationIr` + `ContractGenerationField`/`Shape`/`FieldMetadata`, `ContractGenerationSupport.cs:7`), generated TypeScript (`src/CommandCenter.UI/src/contracts/generated/repository-dashboard.generated.ts`, **derived**), and freshness/consumer-verification. *Target:* the generation pipeline becomes the default birthplace of every new contract (repo-state additions, three SSE shapes, the structured decision schema).

- **Meaning.** The certified, drift-protected definition of the *shape* of information crossing a boundary (backend → UI). Meta-information: it owns shapes, never domain content.
- **Identity.** `contractId`/`contractName` (e.g. `repository-dashboard`).
- **Authority.** **The contract oracle owns shape.** Golden fixtures are hand-authored/governance-approved truth; the IR and generated TS are derived from them; changes require governance.
- **Origin.** Golden fixture authored → IR derived → TypeScript generated → freshness/consumer verification.
- **Evolution.** Baselines change only through governance (M0.4); generated artifacts regenerate from baselines and are drift-checked.
- **Relationships.** Projects the shapes of **Run/Workflow Projection** (I-10) and (target) the decision schema (I-4) and SSE streams. Governed by **Governance Records** (I-13).
- **Persistence.** Fixtures + IR snapshots in `tests/`; generated TS in the UI source tree (committed, marked generated).
- **Historical value.** The baseline is the durable contract; generated artifacts are regenerable.
- **Compression.** N/A (definitions, not data).
- **Evidence.** The golden fixture *is* the evidence the contract holds; field metadata records semantic domain, identity role, array ordering, nullability — rich shape provenance.
- **Trust.** Authored baseline (maximal) + generated derivation (drift-protected).
- **Projection.** The contract *is* the projection's specification; consumed by UI types.

### I-13 · Governance Record *(architectural-decision certification)*

> *Today:* the M0.4 net — 11 `RequiredDecisionClasses` (new authority, new projection, contract change, regression weakening, state ownership change, reference-architecture change, …), `docs/architecture-decision-governance.md` (authoritative policy), and `ArchitecturalDecisionGovernanceTests`/`ArchitecturalRegressionFrameworkTests` (executable enforcement). *Target:* ratify the session-role invariant and every new contract/state through this net.

- **Meaning.** The certified record that an *architectural* decision (about authority, contracts, regressions, state ownership) was made with the required evidence and ratification. Distinct from a *domain* Decision (I-4): this governs the system's own evolution.
- **Identity.** The decision class + the governance record/test that enforces it.
- **Authority.** **M0.4 governance owns architectural decisions.** Hand-authored policy + executable guards; nothing may weaken a regression or change a contract/authority without a governance record.
- **Origin.** Authored when an architectural change is proposed; ratified through the governance process; encoded as a regression test.
- **Evolution.** Append-only history of architectural decisions; policy evolves deliberately.
- **Relationships.** Governs **Contracts** (I-12), authority boundaries, and the regression framework; it is the meta-information that protects every other information object's invariants.
- **Persistence.** `docs/`, `tests/`, `.agents/` governance artifacts.
- **Historical value.** Maximal — the architectural memory; append-only.
- **Compression.** Lossless.
- **Evidence.** Each class mandates an evidence package; certification reports prove exit criteria.
- **Trust.** Authored + executably enforced (the strongest trust model in the system — claims are tests).
- **Projection.** Governance docs and CI test results; not a runtime surface.

### I-14 · Repository Identity & Lifecycle

> *Today:* `Repository` (`Core/Repositories/Repository.cs:10`; `Id`/`Name`/`Path`; `RepositoryAvailability`), `ApplicationConfiguration` (the repository list, JSON), and execution/session lifecycle via `RepositoryExecutionState` (`Primitives/RepositoryExecutionState.cs`). *Target:* add repository-level lifecycle states `PlanAuthoring`/`ExecutingPlan` (`design.md §5`), persisted, surfaced via `GET .../plan/status`.

- **Meaning.** The stable identity of a repository and its current lifecycle phase — the spine every other information object hangs from.
- **Identity.** `repositoryId` (Guid). **The information spine** (as `repositoryId` is the runtime spine).
- **Authority.** `Core`/configuration owns repository identity; the orchestrator owns the new lifecycle state.
- **Origin.** Registered in `ApplicationConfiguration`; lifecycle derived from `.agents/` presence (`!File.Exists(plan.md)` ⇒ `PlanAuthoring`).
- **Evolution.** Identity is stable for the repository's registered life; lifecycle transitions `PlanAuthoring → ExecutingPlan → idle`.
- **Relationships.** The parent identity of *every* other information object; the lifecycle state gates which projection is foregrounded.
- **Persistence.** Configuration JSON; lifecycle persisted by the orchestrator store.
- **Historical value.** Identity durable; lifecycle is current-state (reconstructable).
- **Compression.** N/A.
- **Evidence.** `RepositoryAvailability` (Available/Missing/AccessDenied) records the truth of the repository's existence.
- **Trust.** Observed.
- **Projection.** The repository selector; the lifecycle state foregrounds the driving vs inspection surface.

### I-15 · Event *(observation — durable vs transient)*

> *Today:* **two kinds.** Durable: `ExecutionEvent` (`Execution/Models/ExecutionEvent.cs:5`; `Sequence`/`Timestamp`/`Type`/`Category`/`Consequence`/`Message`; append-only in `ExecutionSession.Events`). Transient: the stream chunks broadcast by `ExecutionMonitoringService` (`Channel`-based, retained-then-live, projected as SSE). *Target:* three stream kinds (planning/execution/decision); the durable/transient split preserved.

- **Meaning.** A single observation of activity — either a durable, sequenced session event or an ephemeral stream chunk of live output.
- **Identity.** Durable: `(sessionId, Sequence)`. Transient: none (replayable from a retained buffer, not addressable).
- **Authority.** Produced by the agent process / execution machinery; **the stream owns nothing** — it carries bytes.
- **Origin.** Emitted as work happens.
- **Evolution.** Durable events are **append-only and immutable**; stream chunks are produced and discarded.
- **Relationships.** Stream chunks are the *liveness* of a turn whose durable residue is the Handoff/Decision; durable events form the operational session's audit trail.
- **Persistence.** Durable events in the session record; **stream chunks never persist** (the consuming protocol persists the turn's outcome, not the stream).
- **Historical value.** Durable events: medium (audit). Stream chunks: none (transient by design).
- **Compression.** Stream retained-buffer trims by count/bytes; durable events kept in full.
- **Evidence.** Durable events evidence provider/git activity within a session.
- **Trust.** Observed.
- **Projection.** **The stream IS the live-observation projection** (three SSE kinds); durable events surface in the execution feed.

### I-16 · Evidence / Provenance *(the connective tissue)*

> *Today:* pervasive and uniform across contexts: `DecisionEvidence`/`DecisionSourceReference` (Decisions), `ReasoningProvenance`/`ReasoningReference`/`ReasoningCaptureProvenance` (Reasoning), `OperationalContextInputFingerprint`/per-item `SourceRelativePath` (Continuity), `ContinuityFingerprint` SHA-256 (DecisionSessions), `SourceHash` (Prompts), contract field metadata + IR hashes (Contracts), and the `ExecutionPromptManifest` divergence record (Execution). *Target:* unchanged in kind; extended to the new structured loop-decision.

- **Meaning.** Not a standalone object but the **cross-cutting binding** that links every piece of information to its sources and proves its integrity. The property that makes the whole information system *trustworthy and explainable*.
- **Identity.** Borne by the objects it annotates (source kind, path, section, excerpt, fingerprint).
- **Authority.** Owned by whatever object it annotates; never an authority of its own.
- **Origin.** Recorded at the moment each object is authored or derived.
- **Evolution.** Immutable once recorded; accretes as evidence lists grow.
- **Relationships.** Touches *every* information object — it is the edge set of the entire information graph.
- **Persistence.** Embedded in its host object.
- **Historical value.** Maximal — without provenance the journal is opaque; with it, the system is auditable end to end.
- **Compression.** Must remain lossless; it is what makes compression elsewhere *safe* (you can always trace back).
- **Evidence.** It is evidence, by definition.
- **Trust.** Fingerprints (SHA-256, `SourceHash`, content hashes) make integrity *checkable*, not merely asserted.
- **Projection.** Surfaced as sources/citations/excerpts throughout the inspection surfaces.

---

## Information Ontology

The sixteen objects resolve into **six ontological categories**, defined by *how information comes to exist and how much it can be trusted to be recreated*:

```
AUTHORED SEED        Intent (I-1) ──▶ Plan (I-2)
                       human desire → codex strategy, human-ratified

FORWARD INCREMENTS   Handoff (I-3) ⇄ Decision (I-4)
                       what was done ↔ what was decided (the loop)

ACCRETING TRUTH      Operational Context (I-5) (+ Delta)
                       the compressed living model of understanding

SESSION GOVERNANCE   Session Record (I-6) · Continuity Artifact (I-7) · Routing Assessment (I-8)
                       identity, transfer certificates, advisory economics

DERIVED KNOWLEDGE    Reasoning Graph (I-9) · Run/Workflow Projection (I-10)
                       queryable memory · recomputable progress views

META-INFORMATION     Prompt (I-11) · Contract (I-12) · Governance Record (I-13)
                       the SHAPES information flows through — never the content

SUBSTRATE / IDENTITY Repository Identity & Lifecycle (I-14) · Event (I-15) · Evidence/Provenance (I-16)
                       the spine, the observations, the connective tissue
```

The ontology's governing rule: **information's category determines its rights.** Authored information may never be silently regenerated (it has no other source). Derived information may never become a source of truth (it must always be rebuildable). Meta-information may govern shapes and invariants but must never own domain content. Substrate binds and observes but never decides. Every architectural error in an information system is a category violation — a stream treated as durable, a projection treated as authoritative, a governance record absorbing domain authority, a derived score driving a decision it was only meant to advise.

---

## Information Dependency Graph

Which information depends on (is seeded/computed from) which. **A dependency structure, not a flow** — arrows mean *needs as input*, read bottom-up for "what must exist first."

```
Repository Identity & Lifecycle (I-14)  ◀── the spine; everything is subordinate
        │
        ▼
Intent (I-1)
        │  (human authors)
        ▼
Plan (I-2) ──────────────────────────────┐  (codex authors from Intent)
        │  (copied at Run Activation)      │  (re-read every loop turn)
        ▼                                   │
Operational Context (I-5) ◀────────────────┼──── seeds every Decision Session
        ▲                                   │
        │  (summarizes/compresses)          │
        │                                   ▼
Handoff (I-3) ──────────▶ Decision (I-4) ───┘  (the loop: handoff → decision → next handoff)
        │                      │
        │                      ├──▶ Reasoning Graph (I-9)        (materializes over decisions/sessions)
        │                      └──▶ Routing Assessment (I-8)     (token/economics over the session)
        ▼                              │
Session Record (I-6) ◀─────────────────┘     │ (on Transfer)
        │                                     ▼
        └──────────────────────────▶ Continuity Artifact (I-7)  (certifies transfer, carries I-5)

Run/Workflow Projection (I-10) ◀── derives from I-3,I-4,I-5,I-6,I-9   (pure projection, no dependents)

Evidence/Provenance (I-16) ── binds ALL of the above (edges, not a node)

META (govern the shapes, depend on nothing domain):
  Prompt (I-11) ── the input form of every transformation
  Contract (I-12) ── the output shape of every projection      ◀── governed by ──┐
  Governance Record (I-13) ──────────────────────────────────────────────────────┘
```

Critical observations: **I-14 (repository identity) gates everything** — no information exists outside a repository. **I-5 (Operational Context) is the convergence point** — seeded by Plan, summarizing Handoff+Decision, carried by Continuity, seeding every Decision Session; it has the most edges of any node. **I-10 (Workflow Projection) is a pure sink** — everything depends *into* it; nothing depends *on* it (which is exactly why it can be demoted to a projection). **The meta layer (I-11/12/13) is dependency-isolated from domain content** — it shapes flows without consuming domain information, which is what keeps governance from becoming a domain owner.

---

## Information Transformation Graph

Every transformation that converts one information object into another, with the protocol that performs it and the authority that owns the act.

```
Intent ──────────────[D1 Plan Authoring]──────────────▶ Plan
   (human desire)        codex authors, human revises      (strategy)

Plan ────────────────[D2 Run Activation: copy]─────────▶ Operational Context (v0)
   (frozen at Execute)   orchestrator copies plan.md         (initial understanding)

Plan ────────────────[D2: ExtractMilestones]───────────▶ Milestones (in plan)
                          codex (operational, 1-shot)

(work) ──────────────[D3 Operational Turn]─────────────▶ Handoff
   plan+handoff+decisions  codex (operational) does work      (what was done)

Handoff ─────────────[D4 Decision Turn]────────────────▶ Decision
   (the question)        codex proposes → HUMAN ratifies        (the verdict)
                         → Decisions validates shape

Decision ────────────[D3 next turn: ContinueExecution]─▶ Handoff (next)
   (feeds back)          codex (operational)                    (the loop closes)

Handoff+Decisions ───[Continuity generation]───────────▶ Operational Context (v_n)
   (accumulated)         Middle/Continuity, codex on transfer    (compressed understanding)

Operational Context ─[D6: ProduceOperationalDelta]─────▶ Operational Delta
   (saturated session)   codex (decision sandbox)                (the change)

Delta + old Context ─[D6: UpdateOperationalContext]────▶ Operational Context (rewritten)
                         codex (operational, 1-shot)             ◀── the ONE rewrite

Decisions + Sessions ─[Reasoning capture]──────────────▶ Reasoning Graph
   (the corpus)          Reasoning (read-only materialization)    (queryable memory)

Session metrics ─────[D5 Decision Routing]─────────────▶ Routing Assessment (verdict)
   (token/economics)     Router (pure, non-mutating)             (advisory)

Routing + snapshots ─[D6 Session Transfer]─────────────▶ Continuity Artifact (+ fingerprint)
                         continuity-capture service              (transfer certificate)

codex free text ─────[parse, target]───────────────────▶ DecisionProposal (structured)
   (loop output)         Decisions oracle validates              ◀── the convergence target

All records ─────────[projection]──────────────────────▶ Workflow Projection / Dashboard
   (durable truth)       projection services (pure)              (recomputable view)

Process output ──────[S1 Streaming]────────────────────▶ Event (transient chunk)
   (live turn)           Event Stream (carries bytes)            (discarded after journaling)

Completed turn ──────[S2 Journaling]───────────────────▶ {NNNN} artifact (durable)
   (turn outcome)        rotation owner                          ◀── the durability boundary
```

The transformations that **define the product** are exactly two: **Handoff → Decision** (codex proposes, human ratifies — where value is created) and the **Operational Context rewrite on transfer** (where understanding is compressed and carried forward, enabling unbounded runs). Everything else either sets these up (Intent→Plan→Context-v0), feeds them (the loop), records them (journaling, reasoning), or views them (projection). The transformation that must **never lose fidelity** is the Operational Context rewrite — it is the only point where the system deliberately *forgets*, and a bad rewrite silently corrupts every downstream decision.

---

## Information Authority Model

Who may author, modify, and observe each information object. Authority must never drift; the model's job is to make ownership structurally fixed.

| Information | Author (creates) | May modify | May observe | Must NEVER modify |
|---|---|---|---|---|
| **Intent** (I-1) | Human | Human (pre-execute) | all | codex, any service |
| **Plan** (I-2) | codex | codex (revise) | all | the orchestrator (verifies existence only) |
| **Handoff** (I-3) | codex (operational) | **no one** (immutable) | all | every consumer — read-only |
| **Decision** (I-4) | codex (proposes) | **Human (ratifies)** | all | the Run/orchestrator (sequences only) |
| **Operational Context** (I-5) | Middle/Continuity; codex (rewrite) | the generator/rewriter | all | the loop directly (only via reviewed proposal) |
| **Session Record** (I-6) | owning context | owning context's state machine | all | the *other* role's context (⟂ invariant) |
| **Continuity Artifact** (I-7) | continuity-capture | **no one** (immutable+fingerprinted) | all | everyone after creation |
| **Routing Assessment** (I-8) | router/economics (pure) | recomputed only | all | anyone (it mutates nothing) |
| **Reasoning Graph** (I-9) | Reasoning | append-only | all | domain contexts (read-only materialization) |
| **Workflow Projection** (I-10) | projection services | recomputed only | all | anyone (no durable state) |
| **Prompt** (I-11) | Human (`.prompt` files) | Human + regeneration | all | hand-edit of generated output |
| **Contract** (I-12) | Human (golden) + generation | governance only | all | anyone outside governance |
| **Governance Record** (I-13) | governance process | append-only | all | anyone outside governance |
| **Repository Identity** (I-14) | configuration | configuration; orchestrator (lifecycle) | all | domain contexts |
| **Event** (I-15) | process/execution | append-only (durable); discarded (stream) | all | consumers (read-only) |
| **Evidence/Provenance** (I-16) | the host object | append-only | all | anyone after recording |

The authority bright lines, stated as information rules: **(1) Only the Human authors Intent and only the Human ratifies Decisions** — these are the two non-automatable authoring acts, and the product's governance rests on them. **(2) Handoffs and Continuity Artifacts are immutable** — observations and certificates are never edited, only superseded. **(3) Derived information (Routing Assessment, Workflow Projection) has no modification authority at all** — it is recomputed, never written. **(4) Meta-information authority (Prompts, Contracts, Governance) is held outside the domain** — it shapes flows but owns no domain content, mirroring the runtime audit's "composition never becomes domain" as "governance never becomes domain." **(5) The Decision ⟂ Operational role separation is an authority fact about Session Records** — neither role may modify the other's record.

---

## Information Lifecycle Model

Every information object passes through Birth → Evolution → Transformation → Consumption → Persistence → Archival → Deletion. The objects sort into **five lifecycle classes**, by how they end:

1. **Write-once, immortal** (born, never changed, never deleted): Handoff, Continuity Artifact, durable Event, Governance Record, Reasoning graph records, Decision `History` entries. *Archival:* moved to history dirs / epic archives; never deleted.
2. **Stateful, append-history** (identity stable, state evolves, history append-only, terminal state durable): Decision, Session Record. *Deletion:* never — terminal states (`Resolved`/`Superseded`/`Retired`/`Failed`) are kept.
3. **Versioned-and-rewritten** (current version load-bearing, prior versions rotated to history): Operational Context, Plan. *Archival:* rotation to `{NNNN}` / archive; *Deletion:* prior versions reconstructable from journal.
4. **Recomputed-on-demand** (born per request, never persisted, dies at end of request): Workflow Projection, Routing Assessment, reasoning graph/trace/reconstruction, dashboards. *Deletion:* every request (regenerated next time).
5. **Transient liveness** (born per turn, designed to be lost): stream-chunk Events, the `{id}:Plan` cache, in-flight unsubmitted decisions, live token counts. *Deletion:* on turn end / restart — never persisted.

The lifecycle's defining rule: **information's persistence class is the inverse of its derivability.** The less an object can be regenerated, the longer it must live — write-once authored increments live forever; fully-derived projections die every request. The system is correct precisely when this inverse holds, and the four "implicit" objects (Intent, Plan, Run Journal, structured loop-Decision) are risks specifically because today they sit in the *wrong* class — irreplaceable information held as transient prose or strings.

---

## Information Persistence Model

What must survive forever, one run, one session, or never. Four persistence tiers, matching the lifecycle classes (and the runtime audit's three liveness tiers, refined by category):

| Tier | What | Where | Lifetime |
|---|---|---|---|
| **Forever (append-only spine)** | Handoffs, Decisions, Continuity Artifacts, Reasoning graph, Operational Context history, Governance records, Intent | `.agents/` journal (per-repo, archived per epic) | permanent; the product's memory |
| **Durable records (JSON stores)** | Session Records (operational + decision), application config | `%APPDATA%\CommandCenter\execution-sessions.json`, `.agents/decision-sessions/registry.json`, config JSON | per repository; reconstructable around the journal |
| **Run-scoped** | current Operational Context, current Plan, run lifecycle state, iteration counter | `.agents/` current files + orchestrator store | one run; rotated to history at boundaries |
| **Never (regenerated)** | Workflow Projection, Routing Assessment, dashboards, reasoning projections, generated contracts, live token count, stream chunks, `{id}:Plan` cache | nowhere (recomputed) | one request / one turn |

The persistence invariant (the information form of the runtime audit's "truth is always externalized"): **no information object holds, as its only copy, anything that cannot be regenerated from the append-only spine.** Authoritative information lives on the spine; derived information lives nowhere; records and run-state are conveniences reconstructable around the spine. This is what makes the live tier safe to lose — the spine is the truth, and the spine is append-only.

Note the **one outward-facing persistence escape**: the git **commit/push SHA** (`ExecutionSession.CommitSha`/`PushedCommitSha`). It is the only information that leaves Command Center's own stores and becomes durable *in the repository's history*, irreversibly. It is therefore the one persistence event that is genuinely non-recoverable and must be gated by a deliberate human act (Execute).

---

## Information Versioning Model

| Versioning kind | Information | Mechanism |
|---|---|---|
| **Immutable** | Handoff, Continuity Artifact, durable Event, Reasoning records, snapshots, certification reports, golden fixtures | write-once; fingerprinted where integrity matters |
| **Append-only** | Decision `History[]`, Session `Events[]`, transfer events, the `{NNNN}` rotations, reasoning events | monotonic sequence; never rewritten |
| **Stateful (versioned identity)** | Decision (state machine + history), Session Record | identity stable, state transitions logged |
| **Rewritten (current + rotated history)** | Operational Context, Plan, handoff/decisions current slots | current overwritten; prior rotated to `{NNNN}` / archive |
| **Schema-versioned** | every persisted document | `SchemaVersion` on `DecisionArtifactDocument`/`DecisionSessionArtifactDocument`/etc. |
| **Derived snapshots** | metrics/economics/coherence/lifecycle, dashboards, workflow projection | point-in-time, regenerable |
| **Compatibility projections** | generated TypeScript contracts | regenerated from IR/golden; drift-checked |
| **Historical projections** | `ArtifactVersionKind.Historical`, reasoning `HistoricalAt` queries, epic archives | views over rotated/archived versions |

The versioning model is already mature and uniform: **every persisted document carries a schema version, every sequence is monotonic, every integrity-critical object carries a fingerprint, and every rewrite preserves the prior version in history.** The single versioning gap is the implicit objects — Plan Revisions are *not* versioned (lost as ephemeral turns), and the loop's prose Decision has no schema version because it has no schema. Converging it onto `DecisionProposal` (which *is* schema-versioned) closes the gap.

---

## Information Integrity Model

How the system represents the hard cases — identity, contradiction, modification, uncertainty, absence, and provenance:

- **Identity preserved by:** strongly-typed ids (`DecisionId`, `DecisionSessionId`, `ReasoningEventId`, `ArtifactId`), monotonic sequences (`{NNNN}`, `ExecutionEvent.Sequence`), and content fingerprints (SHA-256 `ContinuityFingerprint`, prompt `SourceHash`, `OperationalContextInputFingerprint`, contract IR hashes). The router governs Decision-Session identity explicitly (preserve on reuse, mint on transfer).
- **Contradiction represented explicitly:** `ContinuityDecisionContradiction` (conflict type, severity, evidence, resolution guidance) and `DecisionRelationship.ConflictsWith` — the system has a *first-class representation of two decisions that disagree*, rather than silently overwriting. This is rare and valuable.
- **Modification represented as history, not mutation:** `DecisionHistoryEntry` (from/to state, reason, sources), append-only `Events[]`, rotation rather than overwrite. Change is recorded, never erased.
- **Uncertainty represented:** `ReasoningReconstruction.Confidence` + `ConfidenceRationale`, `DecisionTaxonomyBasis.IsHeuristicFallback` + `FallbackReason`, quality signals with direction/severity, router `ContributingFactors`. The system distinguishes "known" from "inferred with this confidence."
- **Absence represented:** `RepositoryAvailability` (Missing/AccessDenied), `OperationalContextInputFingerprint.Present`, `planExists` in plan-status, `Resolution?` nullability. Missing information is a recorded state, not a null guess.
- **Provenance represented everywhere:** source references with path/section/excerpt, capture provenance with mode/reason, the `ExecutionPromptManifest`'s requested-vs-delivered + `DivergenceReason` (the system records *when reality diverged from what was asked*). Provenance is the integrity backbone (I-16).

The integrity model's strength is that **trust is checkable, not asserted** — fingerprints make tampering detectable, history makes change auditable, and contradiction/uncertainty/absence have explicit representations rather than being collapsed to silence or null. This is exactly the integrity posture a long-lived, codex-authored, lossily-compressed information system requires.

---

## Information Compression Model

| Information | May be summarized? | May be forgotten? | Must remain lossless? |
|---|---|---|---|
| Intent | no | no | **yes** (irreplaceable seed) |
| Plan | understanding yes, text no | prior revisions: yes | the executed plan: yes |
| Handoff | collectively (into context) | **no** (individually) | individually: yes |
| Decision | consequences assimilated | **no** (superseded, not deleted) | individually: yes |
| **Operational Context** | **it IS the summary** | older versions: yes (rotated) | current version: yes |
| Routing Assessment | — | **yes** (recomputable) | no |
| Reasoning Graph | the graph is the summary | regenerable | events: yes |
| Workflow Projection | — | **yes** (recomputed) | no |
| Event (stream) | retained-buffer trims | **yes** (transient) | no |
| Continuity / Governance / Evidence | no | no | **yes** |

The compression model has one centerpiece: **the Operational Context is the system's deliberate, evidence-backed forgetting.** It is the only place where authoritative information (the full handoff/decision history) is intentionally reduced to a smaller, lossy view — with an explicit item limit (8), classified outcomes (preserved/added/modified/removed/compressed), noise-removal indicators, and *stable-understanding retention warnings* that flag when compression risks dropping something load-bearing. Crucially, **compression is non-destructive of the source** — the full handoffs and decisions remain on the spine; the context is a *view*, so a bad compression is always recoverable by re-deriving from the journal. The compression's effect on future reasoning is direct and high-stakes: every fresh Decision Session is seeded from this compressed context, so what the context forgets, the next session cannot see (until it re-reads the journal). This makes the rewrite the single most consequential compression act in the product — and the reason the system already invests so heavily in making it evidence-backed and reversible.

---

## Information Recovery Model

For each object, if lost: can it be reconstructed, regenerated, or must it be recovered — or may it disappear?

| Information | If lost | Source of recovery |
|---|---|---|
| Intent | **must be recovered** (no other source) | the `specs/` files; otherwise gone |
| Plan | reconstructable (pre-execute: re-author; post-execute: on spine) | `plan.md` / `operational_context.md` v0 |
| Handoff | **must be recovered** (observation, irreplaceable) | the `handoffs/{NNNN}` journal |
| Decision | **must be recovered** (human ratification, irreplaceable) | the `decisions/{NNNN}` journal + Decisions store |
| Operational Context | reconstructable (fall back to plan.md; re-derive from journal) | journal + Plan |
| Session Record | reconstructable around the journal | JSON store; rebuilt by Supervisor |
| Continuity Artifact | **must be recovered** (transfer certificate) | continuity-artifacts dir |
| Routing Assessment | **regenerable** | recompute from session metrics |
| Reasoning Graph | **regenerable** | re-materialize from decisions/sessions |
| Workflow Projection | **regenerable** | recompute from records |
| Prompt | regenerable (build) | `.prompt` source files |
| Contract (generated) | **regenerable** | regenerate from golden/IR |
| Governance Record | **must be recovered** | docs/tests (in version control) |
| Repository Identity | reconstructable | configuration |
| Event (durable) | recover from session | `Events[]` in record |
| Event (stream) | **may disappear** (by design) | re-subscribe + replay retained |

The recovery model's law: **the must-recover set is exactly the authored-and-observed set** — Intent, Handoff, Decision, Continuity Artifact, Governance Record, and the commit SHA. Everything else is either reconstructable around the spine or freely regenerable. This is why the append-only spine is the entire backup strategy: protect the spine, and every recoverable thing recovers; lose the spine, and only the spine's contents are truly gone. The runtime/protocol audits' "resume from the highest complete `{NNNN}`" is the operational expression of this — the journal *is* the recovery substrate.

---

## Information Projection Model

How information should and should not be observed:

- **Live observation (streams):** three SSE kinds (planning/execution/decision) carry transient Event chunks — the user watches codex think in real time. The stream is *shown, never owned*; on turn-complete it is replaced by the durable artifact's projection.
- **The driving surface (foregrounded by lifecycle):** `PlanAuthoring` foregrounds Intent/Plan authoring; `ExecutingPlan` foregrounds the Decision loop (the editable Decision is the one place forward-progress information is *projected for editing*). The repository lifecycle state (I-14) decides which projection is primary.
- **Inspection depth (the seven tabs):** durable projections over the spine — execution feed, operational-context, decisions, reasoning, continuity, governance, workspace. These recompute Workflow Projection (I-10), Reasoning views (I-9), and continuity/decision views on demand.
- **Governance/diagnostics (internal, inspectable):** router reason/factors, recovery diagnostics, continuity transfer reasons, contract freshness, prompt manifests — explanations recomputed over durable records.
- **Never projected:** live process handles, raw token counts (only their *effect* via the router), the `{id}:Plan` cache, subscriber bookkeeping, rotation counters, schema-version envelopes. The user observes information and its provenance, never the machinery.

The projection rule (the information form of the protocol audit's observability rule): **the user observes authoritative increments as they stream and as durable artifacts, edits exactly one (the Decision), inspects derived knowledge that explains itself, and never sees transient substrate.** Projection is one-directional except at the single human-authority gates (Intent authoring, Decision Submit) — everywhere else the surface reflects information it must not mutate.

---

## Runtime Information Matrix

For each runtime object (from the Runtime Object Model audit): what information it **owns** (authoritative), **transforms** (changes form), merely **transports** (carries unchanged), or **projects** (derives a view).

| Runtime object | Owns | Transforms | Transports | Projects |
|---|---|---|---|---|
| **Agent Runtime** (RO-1) | — | — | Prompt in → output bytes out | — |
| **Agent Process** (RO-2) | live token count *(transient)* | **Prompt → Handoff/Decision/Plan/Delta** (the authoring act) | — | — |
| **Repository Runtime** (RO-3) | live run handles *(transient)* | — | commands → run advances | repository lifecycle state |
| **Repository Run** (RO-4) | **the Run Journal identity + iteration counter** | sequences Handoff⇄Decision | plan/handoff/decisions between turns | — |
| **Operational Session** (RO-5b) | Session Record + **commit/push SHA** | work → Handoff (via process) | — | — |
| **Decision Session** (RO-5c) | Session Record | Handoff → Decision (via process) | operational_context as seed | — |
| **Event Stream** (RO-6) | — | — | **Event chunks** (bytes) | the live view |
| **Runtime Supervisor** (RO-7) | — | durable records → reconciled state | — | recovery diagnostics |
| *Router / Economics* (authority) | — | metrics → **Routing Assessment** | — | router diagnostics |
| *Continuity / Middle* (authority) | Operational Context | Handoff+Decisions → **Operational Context** | — | continuity view |
| *Reasoning* (authority) | Reasoning graph records | Decisions/Sessions → **Reasoning Graph** | — | graph/trace/reconstruction |
| *Decisions oracle* (authority) | the Decision *shape* | codex text → **DecisionProposal** | — | — |

What the matrix reveals: **(1) The Agent Process is the only runtime object that transforms transient input into authoritative information** — it is where authoring happens, which is why it concentrates all the risk (runtime audit) *and* produces all the irreplaceable increments (this audit). **(2) The Repository Run owns the one new piece of durable identity — the Run Journal** — binding the per-run artifacts that today float unbound. **(3) The Operational Session is the only runtime object that owns outward-facing information (the commit SHA).** **(4) Composition objects (Agent/Repository Runtime, Event Stream, Supervisor) own no authoritative domain information** — exactly the "composition never becomes domain" invariant, restated as "the conductor owns no irreplaceable information."

---

## Protocol Information Matrix

For each protocol (from the Protocol Architecture audit): what information **enters**, what **leaves** (is newly durable), what **changes**, and what stays **invariant**.

| Protocol | Information in | Information out (durable) | Changes | Invariant |
|---|---|---|---|---|
| **L1 Activation** | durable records | — | lifecycle state | all domain information |
| **L2 Reconciliation** | records + journal | recovery diagnostics; state transitions | session states (orphans) | **the journal (never rewritten)** |
| **L3 Disposal** | run state | cancellation (only) | run terminal state | journal + records |
| **D1 Plan Authoring** | **Intent** | **Plan** (`plan.md`, specs) | — | Intent (frozen at execute) |
| **D2 Run Activation** | Plan | **Operational Context v0, handoff.0001, commit SHA, Run journal opened, Decision record** | lifecycle → ExecutingPlan | Plan (now premise) · *the irreversible git fact is born here* |
| **D3 Operational Turn** | Plan + Handoff + Decisions | **Handoff (`{NNNN}`)** + commit SHAs | iteration counter | Plan, prior Handoffs |
| **D4 Decision Turn** | Handoff | **Decision (`{NNNN}`)** | — | **the Handoff (read-only); human ratification is the crossing** |
| **D5 Decision Routing** | token + economics | — *(advisory only)* | nothing durable | **all domain information** (pure) |
| **D6 Session Transfer** | old Context | **Operational Delta + rewritten Context + Continuity Artifact** | decision-session identity | the journal; the source increments |
| **S1 Streaming** | process output | — *(events transient)* | nothing | **all durable information** |
| **S2 Journaling** | completed-turn content | **the `{NNNN}` artifact** | journal high-water mark | prior artifacts (append-only) |

What the matrix reveals: **(1) Exactly two protocols introduce irreplaceable information — D4 (Decision, via human ratification) and D2/D3 (Handoff, plus the outward commit SHA).** **(2) D6 is the only protocol that rewrites the Operational Context** — the single compression/forgetting event, and the only place decision-authoring identity changes. **(3) D5 and S1 change *no* durable information** — routing is advisory, streaming is transient; they are pure with respect to the information model. **(4) S2 is the universal durability boundary** — every authoritative increment becomes permanent here and nowhere else. **(5) The journal is invariant under every protocol except its own append** — no protocol may rewrite history; this is the information backbone of recovery.

---

## Information Boundaries

Clean category assignment, so no surface mixes kinds (the most common information-architecture failure):

- **Authoritative:** Intent, Plan (post-execute), Handoff, Decision, Operational Context, Session Record, Continuity Artifact, Governance Record, the golden Contract baseline, the `.prompt` source. *Rule:* never silently regenerate.
- **Derived:** Routing Assessment, Reasoning Graph projections, Workflow Projection, dashboards, generated Contracts, live token count, decision economics. *Rule:* never persist as truth; always rebuildable.
- **Transient:** stream-chunk Events, `{id}:Plan` cache, unsubmitted decisions, live process state. *Rule:* never persist at all.
- **Historical:** rotated `{NNNN}` artifacts, prior Operational Context versions, epic archives, `ArtifactVersionKind.Historical`. *Rule:* immutable; read-only.
- **Projected:** the streams, the seven tabs, the dashboard. *Rule:* reflect, never own.
- **Ephemeral:** the live view, retained stream buffers. *Rule:* reconstruct on reconnect.
- **Generated:** Core.Prompts.* classes, `.generated.ts`, IR snapshots. *Rule:* never hand-edit; regenerate.
- **Observed:** durable Events, commit SHAs, repository availability, state transitions. *Rule:* record facts, not inferences.

The boundary discipline is the whole game: **the four implicit objects (Intent, Plan, Run Journal, structured loop-Decision) are exactly the places where the current code blurs a boundary** — holding authoritative information (irreplaceable Intent, ratified loop-Decisions) as if it were transient (prose, strings) or unbound (a run journal with no run identity). Naming them as authoritative is the central information-architecture move the roadmap should make.

---

## Information Evolution Analysis

Which information objects already exist, are implicit, fragmented, duplicated, or genuinely new:

- **Already first-class (rich, certified):** Decision (and its ~115-type structured model), DecisionSession, ExecutionSession, OperationalContextProposal + compression pipeline, Reasoning graph, Continuity Artifact (fingerprinted), Handoff artifact + rotation, Governance records, Contracts, ExecutionPromptManifest, Evidence/Provenance everywhere. **The information richness is already extraordinary.**
- **Implicit (load-bearing but unnamed):**
  - **Intent** — only `specs/` text; no identity/version.
  - **Plan** (+ **Plan Revision**) — only `plan.md` + a cache string; revisions lost.
  - **Run Journal / run identity** — the per-run artifacts (`handoffs/`, `decisions/`, delta, context) are not bound under one `executionRunId`; the "run" exists only as a scatter of files.
  - **Repository lifecycle state** — `PlanAuthoring`/`ExecutingPlan` are new persisted information.
  - **Live token count** — real per-process tokens (today only the `(len+3)/4` estimate).
- **Fragmented (one concept, several representations):**
  - **Operational Context** in three forms — markdown (`operational_context.md`), structured (`OperationalContextProposal`), and cache mirror (`{id}:Plan`).
  - **Decision** across three contexts — structured (`Decisions`), loop prose (`decisions/{NNNN}.md`), and re-extracted (`DecisionSignal`/`DecisionAssimilationRecord` in Continuity).
  - **Handoff** as current file, rotated history, and `ExecutionSession.HandoffPath`.
- **Duplicated (legitimately or not):** Plan text (`plan.md` ↔ `operational_context.md` v0 ↔ cache) — a *legitimate* seed copy, but should be a clearly-named derivation, not three loose copies. Reasoning's re-materialization of decisions is a *legitimate* projection, not harmful duplication.
- **Should become first-class:** Intent, Plan (+ versioned Revisions), the Run Journal (with `executionRunId`), the **structured loop-Decision** (converge `decisions/{NNNN}.md` onto `DecisionProposal`), and the **single-sourced token count** (live, estimate as fallback).
- **Genuinely new:** the live token count, the structured loop-decision JSON schema, the persisted repository lifecycle state, and the run identity binding the journal. **All four are small, additive, and born-protectable through the existing contract/governance net** — none requires new information *machinery*, only new information *names*.

---

## Information Topology

The seven graphs the prompt requests, collapsed (several appear above) and summarized here as one statement each:

- **Dependency graph** (above): I-14 gates all; I-5 (Operational Context) is the convergence hub; I-10 is the pure sink; meta is dependency-isolated from domain.
- **Transformation graph** (above): two product-defining transforms (Handoff→Decision; Context rewrite); journaling is the universal durability boundary.
- **Authority graph** (above): two human authoring acts (Intent, Decision ratification); immutables (Handoff, Continuity Artifact); derived has no write authority; meta-authority sits outside domain.
- **Lifecycle graph** (above): five classes; persistence inversely proportional to derivability.
- **Persistence graph** (above): four tiers; the append-only spine is the entire backup strategy; one outward escape (commit SHA).
- **Projection graph** (above): live streams + driving surface + inspection depth + self-explaining diagnostics; one-directional except two gates.
- **History graph:** the append-only spine (`.agents/handoffs`, `decisions`, `continuity-artifacts`, `reasoning`, operational-context history, governance) plus the schema-versioned record stores; the only information that becomes enduring organizational knowledge.

---

## Information Minimalism Review

Each information object challenged: *could it disappear, be derived, or must it be explicit?*

- **Intent — irreducible.** The human origin; no other source. Must become *more* explicit (named), not less. **Keep, promote.**
- **Plan — semi-reducible, must be explicit.** Pre-execution it is regenerable from Intent; post-execution it is an authoritative historical fact (the run's premise). Cannot be merely a cache. **Keep, promote; demote the cache to a mirror.**
- **Handoff — irreducible.** An observation of work with no other source. **Keep.**
- **Decision — irreducible, and the core.** Human ratification is irreplaceable. The *prose* representation should disappear, converged onto the structured `DecisionProposal`. **Keep the structured form; retire the prose.**
- **Operational Context — irreducible, the singular constant.** It is the one piece of information that must never lose identity; everything live rebuilds from it. **Keep; it is the spine of understanding.**
- **Session Record — fundamental but reducible to one shape.** Operational and Decision records are two roles of one durable-record shape (the runtime audit's same conclusion). **Keep; consider unifying the shape, preserving role separation.**
- **Continuity Artifact — fundamental.** The immutable transfer certificate; its fingerprint is irreplaceable integrity. **Keep.**
- **Routing Assessment — derived; could be transient.** It owns no truth; it is recomputable advisory information. **Keep as derived; never persist as authority.**
- **Reasoning Graph — derived but valuable.** Regenerable from decisions/sessions; the queryable-memory value justifies materializing it. **Keep as durable projection.**
- **Workflow Projection — derived; demote.** A pure projection with no dependents; should not be a stored object (the runtime audit's demotion). **Demote to projection.**
- **Prompt / Contract / Governance — meta; keep separate from domain.** They govern shapes and invariants; the minimalism win is *refusing to let them own domain content*. **Keep, isolate.**
- **Repository Identity & Lifecycle — irreducible spine.** **Keep.**
- **Event — split: durable irreducible, stream transient.** Durable events are a thin audit trail; stream chunks must stay transient. **Keep both, never persist the stream.**
- **Evidence/Provenance — irreducible connective tissue.** Not a node but the edge set; removing it makes the journal opaque. **Keep, pervasive.**
- **`{id}:Plan` cache, live token count, unsubmitted decisions — transient; dissolve into their owners.** The cache mirrors the Plan; the token count belongs to the process; unsubmitted decisions belong to the turn. **Not first-class information.**

**Reduced ontology.** The sixteen catalog objects collapse to **five irreducible authoritative information objects** — **Intent, Plan, Handoff, Decision, Operational Context** — bound by one pervasive cross-cut (**Evidence/Provenance**), recorded on one **append-only journal**, governed by one **meta-information net** (Prompt/Contract/Governance), and viewed through **derived projections** (Reasoning, Workflow) over **substrate identity** (Repository, Session, Event). Of the five, **Operational Context is the one that must never lose identity** — it is the singular information constant the entire system orbits.

---

## Information Readiness Assessment

- **What already exists (the information richness is mostly built and certified):** the structured Decision model, the fingerprinted continuity/economics corpus, the compression-aware operational-context pipeline, the queryable reasoning graph, the schema-versioned record stores, the append-only `{NNNN}` journal, pervasive evidence/provenance with checkable fingerprints, explicit contradiction/uncertainty/absence representations, and the contract/governance meta-net. **The hard parts of an information system — provenance, integrity, compression, history, recovery — are done.**
- **What must evolve (naming, not building):** give **Intent**, **Plan** (+ Revisions), the **Run Journal** (run identity), and the **repository lifecycle state** first-class identity; **converge the loop's prose Decision** onto the structured `DecisionProposal`; **single-source the token count** (live, estimate as fallback); and **clarify the three forms of Operational Context** as one authoritative object with one mirror.
- **What is genuinely new (small, additive, born-protectable):** the live token count, the structured loop-decision JSON schema, the persisted lifecycle state, and the `executionRunId` binding the journal — every one of which can be born through the existing contract pipeline and ratified through M0.4 governance.
- **Information leverage:** highest at **converging the loop-Decision onto the structured model** (it turns the product's core information from opaque prose into a rich, queryable, testable object that the existing reasoning/continuity/governance net immediately consumes) and at **naming the Run Journal** (it turns a scatter of files into one recoverable, identity-bearing record). The information risk lives almost entirely in *category drift* (treating authoritative information as transient); the leverage lives almost entirely in *naming* what already exists.

**Readiness verdict.** The information architecture is reachable by *naming the five authoritative objects the system already produces and giving its core object — the Decision — the structured form its richer sibling already has.* The provenance, integrity, compression, and recovery machinery is built and certified; the new work is to stop holding irreplaceable information as prose and strings, and to bind a run's artifacts under one identity — both small, additive, and protectable by the net already in place.

---

## Final Conclusion — Command Center as an Information System

Strip away the runtime objects, the protocols, and the stores, and Command Center resolves to a single sentence:

> **A system that transforms human Intent into an ever-evolving Operational Context — the living, compressible, evidence-bound model of "what is true about this repository's work and why" — advanced one immutable Handoff and one human-ratified Decision at a time, journaled append-only so that the Operational Context is the one piece of information that must never lose identity and the source from which every live process, projection, and future decision can be rebuilt.**

**What information defines the product.** Five authoritative objects: **Intent** (the human seed), **Plan** (the codex strategy), **Handoff** (the operational increment), **Decision** (the ratified increment — where value is created), and **Operational Context** (the accreting truth — the singular constant). Of these, the Decision is where the product's *value* lives (codex proposes, the human ratifies, governed) and the Operational Context is where the product's *continuity* lives (compressed understanding carried forward, enabling unbounded runs).

**What information merely supports implementation.** Routing assessments, economics snapshots, workflow projections, dashboards, generated contracts, token counts, caches, and stream chunks — all derived or transient, all regenerable, none irreplaceable. They make the system fast, observable, and governed, but the product would still be itself without any particular one of them.

**Which information objects are foundational.** The five authoritative objects, bound by pervasive Evidence/Provenance and recorded on the append-only journal. These are foundational precisely because they cannot be regenerated — they record human intent, observed work, human judgment, and compressed understanding, none of which has any source but the act that created it.

**Which transformations define the product.** Two: **Handoff → Decision** (the value-creating adjudication, where authority momentarily inverts to codex and resolves to the human) and the **Operational Context rewrite on transfer** (the deliberate, evidence-backed compression that lets a run continue indefinitely without losing the thread). The loop is these two transformations, ordered.

**Which information must never lose identity.** The Operational Context, above all — it is the one object everything live rebuilds from, the one place understanding is compressed, the one seed of every fresh decision session. After it: the immutable increments (Handoff, Decision) and certificates (Continuity Artifact, Governance Record) whose identity *is* their integrity.

**Which information becomes enduring organizational knowledge.** The append-only spine — handoffs, ratified decisions, continuity artifacts, the reasoning graph, the operational-context history, and the governance record — bound by provenance into a fully auditable trace. This is the product's memory; it is what a repository *knows about its own work*, and it is the information whose preservation is the entire backup strategy.

This information architecture is the fourth and final layer beneath the Vision, Runtime, and Protocol audits. Where the Vision audit named the **destination**, the Runtime audit named **what exists** (five irreducible objects), and the Protocol audit named **how they collaborate** (eleven protocols, two turn shapes, one loop), this audit names **what flows** — **five irreducible authoritative information objects, bound by provenance, journaled append-only, compressed into one living context.** Together the four models give the roadmap its complete conceptual foundation: a system organized not around its services, its classes, its endpoints, or its files, but around the **enduring information it transforms** — so that every implementation decision can be traced back to stable information, owned by a stable runtime object, collaborating through a stable protocol, in service of the stable destination. The product is not the code that holds the information; the product *is* the information, and the code is how it lives, moves, and is remembered.
