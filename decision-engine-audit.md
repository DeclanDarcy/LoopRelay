# Decision Engine Architecture Audit — Findings

**Scope:** Architectural evolution from the proposed *Decision Engine* roadmap to the post-Epic-3 (and post-Epic-4-frontend) implementation.
**Mandate:** Findings only. No solutions, no implementation plans, no milestone proposals. Every claim is cited to `file:line` where verifiable.
**Companion document:** `audit-findings.md` (prior audit, continuity-preservation framing). This document supersedes it for Decision-Engine-evolution questions and reuses its evidence base.

---

## 0. Headline Findings (read first)

1. **The Decision Engine was never built.** It exists only as `\.agents/roadmap.md` (titled "Epic 4 — Decision Engine"), an unbuilt roadmap. No decision-session, session-router, session-registry, decision-resolution, or interactive-refinement code exists anywhere in `src/`.

2. **The "Epic 4" slot was consumed by a different epic.** The archived, *built* Epic 04 is **Frontend Modernization** (`.agents/archive/epics/04/plan.md:1` — "Command Center Frontend Modernization Plan"; milestones `m0-frontend-foundations` … `m8-capability-gaps-cleanup-and-final-validation`). The Decision Engine roadmap and the shipped Epic 04 share the number "4" but are unrelated. The collision is itself evidence the Decision Engine was authored, shelved, and its slot repurposed.

3. **The charter's premise that the Decision Engine "was created before Epic 2 and Epic 3" is not corroborated by the preserved artifact.** As written, `roadmap.md` *consumes* `operational_context.md` and `handoff.md` (`roadmap.md:63-76`) — outputs of Epics 2 and 3 — and is filed as "Epic 4". The preserved document is therefore a **post-Epic-3 proposal**, not a pre-implementation vision. Whether an earlier pre-Epic-2 version existed is not evidenced in the repository. (See Success Criterion 9.)

4. **The Decision Engine and the implemented architecture are not two designs of the same thing in conflict — they are two layers.** Epics 1–3 deliberately built *execution* + *artifact-mediated continuity* and **explicitly listed the Decision Engine's mechanisms as Non-Goals** (`epics/02/plan.md:844-858`, `epics/03` Single Workflow Authority). The Decision Engine was redirected/deferred, not implemented-then-removed.

5. **The single genuinely homeless concern is *active decision-making as a workflow*** — generating, refining, and resolving decisions through a reasoning session. Everything the roadmap framed as "long-horizon reasoning" that *did* survive collapsed into one mechanism: **human-reviewed promotion of settled understanding into `operational_context.md`**. In-flight reasoning (hypotheses, tradeoffs, rejected alternatives, decision evolution) has no representation and does not cross an execution boundary.

---

## 1. Objective 1 — Original Decision Engine Intent (M0–M10)

Reconstructed from `roadmap.md` verbatim. No evaluation here; intent only.

**Top-level intent** (`roadmap.md:3-23`): a decision-making workflow *distinct from execution*. Execution "performs work"; decision-making "understands project state, identifies ambiguities, evaluates tradeoffs, produces decisions, guides future execution." The Decision Engine "owns decision generation, review, resolution, and continuity across long-running projects" and is explicitly named "the project's long-horizon reasoning layer" (`roadmap.md:814`).

**Cross-cutting authority model** (`roadmap.md:55-97`):
- Execution sessions disposable; **decision sessions may be reused** (`roadmap.md:55-57`) — the key divergence from what was built.
- Consumes `plan.md`, `milestones/*.md`, `handoff.md`, `operational_context.md`; produces `decisions.md` (`roadmap.md:63-76`).
- Engine may Recommend/Explain/Evaluate; may not Approve/Override; human approval authoritative (`roadmap.md:82-97`).

| Milestone | Intended responsibility | Intended capability | Continuity model | Authority boundary |
|---|---|---|---|---|
| **M0 Decision Domain Foundation** (`:101-179`) | Core decision concepts, contracts, persistence | `Decision`, `DecisionOption`, `DecisionRecommendation`, `DecisionResolution`; states Pending/Accepted/Rejected/Superseded/Resolved; decision repository; `decisions.md` + rotation | Persistent decision records w/ history & metadata | Models/persistence only; no resolution authority |
| **M1 Decision Context Resolution** (`:182-247`) | Build decision-making context from artifacts | `DecisionContext` = current state + milestone + recent outcomes + open decisions + operational understanding; validation; pre-launch inspection | Reads continuity inputs incl. `operational_context.md` | Read-only assembly |
| **M2 Decision Session Integration** (`:250-309`) | Launch/run decision sessions | Session launch, monitoring (Running/Completed/Failed/Cancelled), output capture (recommendations, packages, **reasoning output**), failure handling | A *reasoning session* is the runtime locus | Engine runs; does not resolve |
| **M3 Decision Artifact Generation** (`:311-372`) | Generate structured `decisions.md` | Sections Title/Context/Options/Recommendation/Impact/Blocking; multi-decision; validation | Decisions become durable artifacts | Generates proposals, not approvals |
| **M4 Decision Workspace** (`:374-436`) | Review environment for decisions | Full untruncated viewer, navigation, recommendation/impact highlight, artifact history | Decision history browsable | Presentation only |
| **M5 Interactive Decision Refinement** (`:438-486`) | User engages before resolution | Conversation (clarify, change constraints/priorities, explore tradeoffs); session continuation; regenerate options/recommendation | **Reasoning evolves within a live session** | User drives; engine adapts |
| **M6 Decision Resolution** (`:488-547`) | Resolve & persist outcomes | Accept/Reject; persist outcome+timestamp; update `decisions.md`; feed resolved decisions to **Execution Context Resolution** | Resolved decisions flow into execution | Human resolves |
| **M7 Decision Session Registry** (`:550-603`) | Track sessions independent of execution | Session metadata (id/created/last-active/status), token metadata (in/out/total), repo association, restart recovery | Session identity persists across restart | Bookkeeping |
| **M8 Session Router** (`:605-653`) | Decide reuse vs new session | Inputs: session age, token consumption, availability → Reuse / Create New; routing diagnostics ("why a decision was made") | **Continuity-strategy selection** | Mechanism choice, not approval |
| **M9 Operational Context Integration** (`:656-719`) | Continuity transfer on new session | Consume `operational_context.md` during resolution; consolidation **triggered only on Create-New-Session**; review workflow (current/proposed/diff); Accept/Edit/Reject | **Continuity transfer = re-priming a fresh session** | Human accepts transferred context |
| **M10 Certification** (`:722-790`) | Certify end-to-end workflow | Handoff→context→session→generation→review→resolution→execution; restart/session/artifact recovery; multi-repo independent histories & routing | Whole-loop continuity guarantee | Certification |

---

## 2. Objective 2 — Roadmap Responsibility → Implementation Mapping

Classification with evidence. Categories: Implemented / Implemented Elsewhere / Partial / Replaced / Abandoned / Still Missing.

| Roadmap responsibility | Status | Evidence |
|---|---|---|
| Decision domain models (`Decision`/`Option`/`Recommendation`/`Resolution`) | **Still Missing** | None exist. Only `DecisionSignal` (`CommandCenter.Continuity/Models/DecisionSignal.cs:5`), `DecisionAnalysisResult`, `DecisionArtifactInput`. |
| Decision lifecycle states (Pending/Accepted/Rejected/Superseded/Resolved) | **Replaced** (by classification, not lifecycle) | `DecisionTaxonomy` is *classification* — Architectural/Strategic/Tactical/Historical (`CommandCenter.Continuity/Primitives/DecisionTaxonomy.cs:5-8`). A superseded/retired *flag* exists in signal parsing but no state machine. |
| Decision repository + `decisions.md` | **Implemented Elsewhere** | Decisions are repository artifacts under generic artifact services; not a decision-domain repository. |
| Artifact rotation (`decisions.000N.md`) | **Implemented** | `ArtifactRotationService.RotateCurrentDecisionsAsync` (`:14-76`), `ArtifactFamily.Decision` rotation def (`:87-91`). Active archives confirm sequential rotation. |
| M1 Decision Context Resolution / `DecisionContext` | **Still Missing** (decision-specific); execution analogue exists | No `DecisionContext`. Only `ExecutionContext` (`CommandCenter.Execution/Models/ExecutionContext.cs:3-20`) assembled by `ExecutionContextService` in the Plan→Milestone→OpContext→Handoff→Decisions→Git order. |
| M2 Decision Session (reasoning session) | **Abandoned (Non-Goal)** | Explicitly listed Non-Goal (`epics/02/plan.md:846-849`); Single Workflow Authority forbids it (Epic 3). No code. |
| M3 Decision **Generation** into `decisions.md` | **Still Missing** | No code generates decision artifacts. Save/rotate only (`ArtifactsEndpoints` PUT save; `ArtifactRotationService`). `decisions.md` is **human-authored**. |
| M3 Decision **Analysis** (not in roadmap as such) | **Implemented (adjacent capability)** | `DecisionAnalysisService` parses existing decisions → `DecisionSignal`s, classifies, extracts rationale/constraints/consequences/open-questions, flags superseded, detects contradictions (`DecisionAnalysisService.cs:18-205`). It *reads*, never *produces*. |
| M4 Decision Workspace (review UI) | **Partial / Implemented Elsewhere** | Epic 04 frontend ships an Operational Context tab with "decision continuity" and a Continuity tab with "decision retention" (`epics/04/plan.md:15-16`). Decisions are viewable as artifacts; no dedicated decision-review-and-resolve workspace. |
| M5 Interactive Decision Refinement | **Abandoned** | No conversational refinement anywhere. Review is over *operational-context proposals*, not interactive decision dialogue. |
| M6 Decision Resolution (Accept/Reject decisions → execution) | **Still Missing** (namesake exists for executions) | `ExecutionSessionService.AcceptAsync/RejectAsync` (`:234-299`, `WithDecision`:569-614) resolve **executions/handoffs**, not decisions. No resolved-decision feedback into `ExecutionContext` (no decision field on `ExecutionContext`). |
| M7 Decision Session Registry | **Abandoned (Non-Goal)** | No registry. Token metadata tracked only per execution session, not as a reusable decision registry. |
| M8 Session Router | **Abandoned (Non-Goal)** | No router. See Session Router Investigation (§9). |
| M9 Continuity Transfer / Operational Context Integration | **Replaced** | Replaced by always-on artifact-mediated continuity: every execution reconstructs from `operational_context.md` (`docs/architecture.md:88-99`). Roadmap's *conditional* transfer (only on new session) is moot because there is no session to reuse. Review workflow (current/proposed/diff/Accept-Edit-Reject) **is** implemented for op-context proposals (`OperationalContextReviewService.cs:49-109`, `UnderstandingDiffService.cs:9-46`). |
| M10 End-to-end certification | **Replaced** | Epic 3 M8 "Long-Horizon Certification" certifies the *artifact* continuity loop, not a decision-session loop (`epics/03/milestones/m8-long-horizon-certification.md`). |

---

## 3. Objective 3 — Concerns That Survived Roadmap Removal

Where each lives now, and whether ownership is explicit or implicit.

| Concern | Survives? | Current home | Ownership |
|---|---|---|---|
| **Decision continuity** | Yes, narrowed | `DecisionAnalysisService` → assimilation of *durable* (Architectural/Strategic, non-superseded) decisions into `operational_context.md` (`OperationalContextGenerationService.cs:146-207`) | **Explicit** but reduced to "distill settled decisions into understanding" |
| **Long-horizon reasoning** | Partially | Reframed as durable *understanding* in `operational_context.md`; reasoning *process* not retained | **Implicit / diffuse** — no component owns "reasoning"; it survives only as promoted conclusions |
| **Continuity transfer** | Yes, generalized | Every execution reconstructs context from artifacts (`docs/architecture.md:88-99`); op-context review/promote (`OperationalContextReviewService`, `OperationalContextLifecycleService`) | **Explicit** (artifact-mediated continuity principle) |
| **Trajectory preservation** | **No** | Nowhere (see §7) | **Unowned** |
| **Continuity optimization** (fidelity/cost/strategy) | **No** | Compression exists but is bounded to size/preservation-safety only (`UnderstandingCompressionService.cs:55-59,103-142`); M9 instrumentation forbidden from governing | **Unowned** (see §9) |
| **Decision refinement** | **No** | Nowhere | **Unowned / abandoned** |
| **Decision context management** | Partially | Folded into `ExecutionContextService` ordering; decisions are one input among six | **Implicit** (no decision-context owner) |

---

## 4. Objective 4 — Concerns That Lost Ownership

The roadmap bundled *concerns* inside *mechanisms*. Removing the mechanism orphaned the concern.

- **Session Router → Continuity-strategy selection: UNOWNED.** The router was the only proposed home for "decide *how* to continue" (reuse vs rebuild, by age/token/availability, with diagnostics, `roadmap.md:605-653`). With reconstruction-only continuity, there is no strategy decision and no component that reasons about continuity fidelity vs cost. See §9.
- **Decision Session → Active reasoning workflow: ABANDONED.** No runtime locus exists where the system reasons about a decision (generate→refine→resolve). Non-Goal in `epics/02/plan.md:846`.
- **Decision Registry → Cross-session/long-horizon state + token economics: UNOWNED as a first-class concern.** Token metadata exists per execution session only; there is no persistent reasoning-state ledger and no economic view of continuity cost.
- **Continuity Transfer → Conditional re-priming: REPLACED, but the *fidelity question* lost ownership.** Transfer became unconditional reconstruction; nobody owns "is the reconstructed context faithful / sufficient / degraded?"
- **Long-Horizon Reasoning → REDISTRIBUTED to artifacts, owned by humans.** It is no longer a system capability; it is a human review responsibility mediated by `operational_context.md`. The system owns *storage and projection* of conclusions, not *reasoning*.

---

## 5. Objective 5 — Continuity Evolution: Decision Continuity → Artifact Continuity

**Transition.** The roadmap located continuity inside *reusable decision sessions* (live reasoning state carried forward, transferred only when a session was recycled). The implementation relocated continuity entirely into *repository artifacts* reconstructed on every disposable execution (`docs/architecture.md:41-43,88-99`; "Continuity Is Artifact-Mediated", `epics/02-03` principles).

**Assumptions adopted:**
- Continuity = *current settled understanding*, not *accumulated reasoning* (`docs/operational-context-schema.md:5-19`; "must not contain raw history, streams, transcripts, session lifecycle" `:21-30`).
- A human gate is acceptable and desirable on the continuity path (Review Before Mutation, Epic 3).
- Deterministic reconstruction from disk is sufficient; no warm state required.

**Tradeoffs:**
- *Gained:* auditability, crash-safety, provider-independence, no hidden state, simple recovery (reattach is crash-recovery only — `IExecutionProvider.SupportsReattach`/`TryReattachAsync`; `CodexExecutionProvider.cs:13` returns `false`).
- *Lost:* zero in-session memory carry-over; no warm-start/acceleration; no fidelity/cost optimization; **no preservation of in-flight reasoning**.

**Benefits realized:** continuity is fully inspectable as files; every session starts from a known, human-reviewed baseline; understanding compaction is explicit and bounded.

**Unresolved continuity responsibilities:** continuity *fidelity* (is the rebuilt context complete?), continuity *strategy* (when full rebuild is wasteful), and continuity of *reasoning* (not just conclusions) are unowned.

---

## 6. Objective 6 — Operational Context as Replacement Architecture

Did `operational_context.md` / `OperationalContextDocument` absorb the roadmap's responsibilities?

| Roadmap responsibility | Replacement verdict | Basis |
|---|---|---|
| **Decision Sessions** | **Not Replaced** | OpContext is a *document*, not a reasoning runtime. It stores outputs; it does not generate/refine/resolve. |
| **Decision Context Resolution** | **Partially Replaced** | OpContext is consumed as *one* execution-context input in a fixed order (`docs/architecture.md:88-97`); there is no decision-specific context assembly. |
| **Continuity Transfer** | **Fully Replaced (and generalized)** | The roadmap's conditional transfer-on-new-session is replaced by unconditional reconstruction + human-reviewed promotion (`OperationalContextReviewService`, `OperationalContextLifecycleService`, `UnderstandingDiffService`). |
| **Long-Horizon Reasoning** | **Partially Replaced** | Replaced for *settled* understanding (durable mental model, stable decisions, rationale-that-still-matters). **Not** replaced for *active* reasoning — the document explicitly excludes in-flight/process content (`operational-context-schema.md:21-30`). |

**Structural observation — overloading.** `OperationalContextDocument` is declared "the canonical internal representation for generation, review, semantic diff, compression, decision assimilation, projection, diagnostics, and reporting" (`operational-context-schema.md:62`). One model simultaneously carries: (a) project understanding, (b) the continuity substrate, (c) distilled decision history, (d) risk/question tracking. This is the same overloading flagged in the prior audit; it is the practical reason no *separate* concern (trajectory, strategy, decision-workflow) has a home — the one document is asked to be everything continuity-related.

---

## 7. Objective 7 — Reasoning Trajectory Support

Per the roadmap's M2/M5 vision of captured "reasoning output" and evolving recommendations.

| Trajectory element | Status | Evidence |
|---|---|---|
| Active hypotheses | **Absent** | No field/artifact. |
| Emerging contradictions | **Transient only** | `DecisionAnalysisService.cs:176-200` detects contradictions → **warning**, surfaced during proposal review then discarded; not persisted into active context (`OperationalContextGenerationService.cs:42,297-336`). |
| Unresolved tradeoffs | **Absent** | No tradeoff capture in schema or signals. |
| Rejected alternatives | **Absent** | Only "superseded/retired" *flag* on a decision; the rejected reasoning is not retained. |
| Strategic direction | **Partial** | `StableDecisions` + `DecisionRationale` hold *accepted* direction; `OpenQuestions` hold unresolved items (`OperationalContextDocument` sections). |
| Decision evolution over time | **Absent** | No revision history of *how/why* a decision changed; only current state persists. |

**`UnderstandingEvolutionLedger` / `UnderstandingRevisionSnapshot`** record *snapshots and section item-counts* per revision (what changed), **not** a reasoning trajectory (why it changed). `ActiveRisks` are copied through unchanged by generation (`OperationalContextGenerationService.cs:197`). **Resulting limitation:** a new execution session inherits settled conclusions and the last 12 recent-change notes, but **no trace of the reasoning that produced them**. The roadmap's explicit "reasoning output" capture (`roadmap.md:281-287`) has no counterpart in the implementation.

---

## 8. Objective 8 — Decision Lifecycle Support

Roadmap pipeline: Generation → Refinement → Resolution → Execution Consumption.

| Stage | Implemented | Missing | Manual | Artifact-only |
|---|---|---|---|---|
| **Generation** | — | No automated decision generation | Authoring `decisions.md` is human | `decisions.md` is the only product |
| **Refinement** | — | No interactive refinement | Edit the markdown by hand | — |
| **Resolution** | Partial (executions, not decisions) | No decision Accept/Reject state transition; no outcome+timestamp on a decision | Resolution is implicit in human edits/supersession | Supersession expressed by rotation/flags |
| **Execution Consumption** | Yes | Resolved-decision feedback loop absent | — | `decisions.md` consumed as one execution-context input (`ExecutionContextService`); only *durable* signals assimilated into op-context |

**Net:** the lifecycle is **artifact-and-human**, not engine-driven. The system *consumes* and *analyzes* decisions; it does not *generate*, *refine*, or *formally resolve* them. The only automated decision behavior is read-side: signal extraction, classification, assimilation, contradiction warning.

---

## 9. Session Router Investigation — Continuity Strategy Ownership

Per the charter: evaluate Session Router as a *responsibility* (continuity-strategy selection), not as session reuse.

- **As a mechanism:** absent and explicitly rejected (Non-Goals `epics/02/plan.md:846-849`; Epic 3 Single Workflow Authority forbids session routers/reuse). Rejection grounds are **workflow simplicity / single-authority + auditability**, *not* reliability or cache concerns — the only "guarantee-or-fail" reasoning in the records concerns provider *reattach* (crash recovery), not continuity.
- **As a responsibility (continuity strategy / fidelity / degradation / optimization / transfer):** **no component owns it.** Continuity is one fixed strategy: full deterministic reconstruction every time. There is no input (age, cost, token budget, staleness) that selects *how* to continue, and no diagnostic explaining a continuity choice (the roadmap's "why a decision was made", `roadmap.md:638-644`, has no analogue).
- **Closest adjacent code:** `UnderstandingCompressionService` makes *retention* decisions (Preserve/Summarize/Retire), but it is bounded to document size and preservation-safety (`:55-59,103-142`) and explicitly not a strategy/cost optimizer. **Conclusion:** removing the Session Router removed the *only* proposed owner of continuity strategy, and that concern was not relocated.

---

## 10. Decision Engine Gap Analysis — Mechanism vs Responsibility

Separating disposable *mechanisms* from durable *responsibilities*.

| Roadmap mechanism | Underlying responsibility | Responsibility still valuable? | Currently owned? |
|---|---|---|---|
| Decision Session | A locus for active reasoning (generate/refine/resolve) | Yes | **No** |
| Session Router | Continuity-strategy selection + diagnostics | Yes | **No** |
| Decision Registry | Persistent reasoning/session state + token economics | Partially (economics yes; reusable-session no) | **No** (per-execution token only) |
| Continuity Transfer | Continuity *fidelity* assurance | Yes | **No** (reconstruction assumed sufficient) |
| Decision Resolution | Formal decision state + execution feedback | Yes | **No** (execution acceptance ≠ decision resolution) |
| Interactive Refinement | Human-in-the-loop reasoning before commit | Open question | **No** |
| Long-Horizon Reasoning | Continuity of *reasoning*, not just conclusions | Yes | **No** |

**Mechanisms safely shed:** reusable sessions, session router, registry — all conflict with disposable-session + single-authority and are not needed for their *stated* mechanical purpose. **Responsibilities orphaned by shedding them:** continuity-strategy selection, continuity-fidelity assurance, reasoning-trajectory preservation, and formal decision resolution.

---

## 11. Architectural Evolution — Per-Concept Ledger

| Concept | Original purpose | Final outcome | Current owner | Unresolved concern |
|---|---|---|---|---|
| **Decision Session** | Reusable reasoning runtime (`roadmap.md:55-57,250-309`) | Rejected as Non-Goal | None | Where does active reasoning happen? |
| **Session Router** | Reuse-vs-new strategy + diagnostics (`:605-653`) | Rejected | None | Continuity strategy is unowned |
| **Decision Context** | Decision-specific context assembly (`:182-247`) | Folded into ExecutionContext as one input | `ExecutionContextService` (implicit) | No decision-scoped context |
| **Decision Registry** | Session + token tracking, recovery (`:550-603`) | Rejected | Per-execution metadata only | Reasoning-state ledger / economics |
| **Operational Context** | Continuity-transfer input (`:656-719`) | Became the *primary* continuity substrate (Epic 3) | `OperationalContext*` services | Overloaded across 4 concerns (§6) |
| **Continuity Transfer** | Conditional re-priming on new session | Replaced by unconditional reconstruction | Execution/continuity services | Fidelity unowned |
| **Decision Resolution** | Accept/Reject → execution (`:488-547`) | Not built; execution acceptance is a namesake | `ExecutionSessionService` (executions only) | No decision-level resolution |
| **Decision Engine (whole)** | Long-horizon reasoning layer (`:814`) | Unbuilt "Epic 4"; slot taken by Frontend Modernization | None | Reasoning layer absent |

---

## 12. Roadmap Inputs (findings for a future roadmap — not a plan)

**Architectural gaps**
- No locus for *active* decision-making (generate/refine/resolve) — only read-side analysis of human-authored decisions.
- No representation of *in-flight reasoning* crossing execution boundaries.
- No decision-scoped context; decisions are one undifferentiated execution input.

**Missing ownership**
- Continuity-strategy selection (post-Session-Router) — unowned.
- Continuity-fidelity assurance (is reconstruction sufficient?) — unowned.
- Reasoning-trajectory preservation — unowned.
- Formal decision lifecycle/resolution — unowned.

**Unresolved responsibilities**
- Token/cost economics of continuity (registry's economic concern) — only per-execution today.
- Contradiction *persistence* (currently transient warnings only).

**Continuity limitations**
- One fixed strategy (full reconstruction); no degradation/acceleration path.
- `OperationalContextDocument` overloaded across understanding / continuity substrate / decision distillation / risk-question tracking (`operational-context-schema.md:62`).

**Reasoning limitations**
- Only *settled* conclusions persist; hypotheses, tradeoffs, rejected alternatives, and decision evolution are lost at session end.

**Evolution opportunities (observations only)**
- The artifact-mediated substrate (op-context + review/diff/compression + decision analysis + evolution ledger) already provides most read-side and storage primitives a reasoning layer would need; what is absent is a *write-side reasoning workflow* and an *owner for continuity strategy/fidelity*.
- A future "decision/reasoning" capability would not need the rejected mechanisms (sessions/router/registry) to satisfy its *responsibilities*; mechanism and responsibility are separable here (§10).

---

## 13. Success Criteria — Direct Answers

1. **What did the original Decision Engine attempt to solve?** A dedicated long-horizon reasoning layer distinct from execution — generating, refining, resolving decisions and carrying reasoning continuity across a long project, via reusable decision sessions routed by need (`roadmap.md:3-23,792-814`).

2. **Which of those problems were solved elsewhere?** *Continuity across sessions* (artifact-mediated reconstruction, Epic 3). *Decision distillation into durable understanding* (`DecisionAnalysisService` → op-context assimilation). *Decision/continuity review UI* (Epic 04 frontend). *Decision artifact rotation/history* (Epic 1).

3. **Which were intentionally rejected?** Decision sessions, session routing, session reuse, decision registry — explicit Non-Goals (`epics/02/plan.md:844-858`) and forbidden by Single Workflow Authority (Epic 3). Rejection was on simplicity/single-authority/auditability grounds.

4. **Which remain unsolved today?** Automated decision *generation*, interactive *refinement*, formal *resolution* with execution feedback, and *reasoning-trajectory* preservation.

5. **Which concerns lost ownership during simplification?** Continuity-strategy selection (Session Router), continuity-fidelity assurance (Continuity Transfer), reasoning continuity (Decision Session/Long-Horizon Reasoning), and decision-cost economics (Registry).

6. **Which continuity responsibilities lack a first-class home?** Continuity *strategy*, continuity *fidelity/degradation*, and continuity of *reasoning* (vs conclusions). Settled-understanding continuity *is* first-class (op-context).

7. **Which responsibilities should inform a new roadmap?** Active decision-making workflow; reasoning-trajectory preservation; an explicit owner for continuity strategy/fidelity; formal decision lifecycle. (Responsibilities, not the rejected mechanisms.)

8. **What responsibilities exist today that weren't visible when the roadmap was written?** Deterministic context reconstruction + size policy (`ExecutionContextService`, `ExecutionContextSizePolicy`); canonical `OperationalContextDocument` with semantic diff/compression/assimilation; understanding-evolution ledger; crash-recovery reattach distinct from reuse; the entire frontend operational-console surface (Epic 04). None were named in `roadmap.md`.

9. **What pre-implementation assumptions are now invalidated?** That continuity *requires* reusable sessions (artifact reconstruction suffices); that a Session Router is needed (one fixed strategy works for current scope); that continuity must be transferred *conditionally* (it is rebuilt unconditionally). **Also:** the charter's own assumption that the Decision Engine *predates* Epic 2/3 — the preserved `roadmap.md` consumes Epic 2/3 outputs and is filed as the unbuilt "Epic 4", so as preserved it post-dates them (no earlier version is evidenced).

10. **What new opportunities emerged from implementation experience?** A mature artifact substrate (op-context generation/review/diff/compression + decision analysis + evolution ledger) now exists that a reasoning layer could build on without reintroducing rejected mechanisms; mechanism/responsibility separation (§10) lets future work adopt the *responsibilities* (reasoning, strategy, fidelity) within the existing single-authority, artifact-mediated model.

---

*Evidence base: `roadmap.md`; `docs/architecture.md`; `docs/operational-context-schema.md`; `.agents/archive/epics/01–04/{plan,milestones,decisions,handoffs}`; backend `src/CommandCenter.{Execution,Continuity,Middle}`. Findings only — no solutions, plans, or milestone proposals, per charter.*
