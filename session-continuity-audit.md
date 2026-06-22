# Decision Engine ↔ Session Continuity — Architectural Evolution Audit

**Scope:** The architectural relationship between two recovered roadmaps — **Epic 4 — Decision Engine** (`.agents/roadmap.md`) and **Epic 5 — Session Continuity** (`.agents/roadmap-2.md`) — and the architecture that was ultimately implemented (built Epics 01–03 + the Frontend-Modernization Epic 04).
**Mandate:** Findings only. No solutions, no implementation plans, no milestones. Every claim is cited to `file:line` where verifiable.
**Companion document:** `decision-engine-audit.md` (prior audit; Decision-Engine-only framing). This document adds the **Session Continuity (Epic 5)** dimension and the **two-epic relationship/compression** analysis, and reuses the prior evidence base. Where the two overlap, this document is authoritative for the continuity-assessment and roadmap-compression questions because it incorporates evidence not available to the prior pass (`roadmap-2.md`; `ContinuityDiagnostics`; `epics/03/milestones/m9-continuity-instrumentation.md`).

---

## 0. Headline Findings (read first)

1. **Epic 5 is downstream of Epic 4 by construction.** Session Continuity's entire trigger model depends on the Decision Engine's session ecosystem: M2 consumes the **Session Registry** (`roadmap-2.md:200-218`), M3 fires continuity **only on the Session Router's "Create New Session"** (`roadmap-2.md:247-296`), and M7 bootstraps into **decision** context resolution (`roadmap-2.md:472-490`). Epic 5 as written **cannot run without Epic 4**.

2. **The implementation kept Epic 5's artifact spine and discarded its session spine.** Operational-context domain, infrastructure, consolidation, review, acceptance, revision history, and instrumentation were all built (as the shipped **Epic 03**). The session-dependent halves — Session Registry integration (M2), session-replacement detection (M3), and decision-session bootstrap (M7) — were **not** built, and were **explicitly rejected** as non-metrics (`epics/03/milestones/m9-continuity-instrumentation.md:36-47`).

3. **The continuity *consumer* was reassigned from decision sessions to execution sessions.** Epic 5 defines operational context as "the minimum information required for a newly-created **decision session** to regain full project understanding" (`roadmap-2.md:81`). The implementation re-aimed it at "a future **execution slice**" (`docs/operational-context-schema.md:7`). Same artifact, different audience.

4. **Continuity *assessment* was built — but it measures the document, not the transfer.** `ContinuityDiagnosticsService` answers "Is continuity succeeding?" (`epics/03/milestones/m9-continuity-instrumentation.md:85`) by comparing successive revisions of `operational_context.md` to itself. Epic 5 M8 wanted that answer from the **next session's experience** ("repeated questions, lost decisions, missing context, incorrect assumptions", `roadmap-2.md:538-546`). Document self-analysis can surface drift; it **cannot** detect "incorrect assumptions" or "missing context" — those require a consumer report that the rejected session model never produced.

5. **Roadmap compression was real, deliberate, and mostly sound — with two evidenced blind spots.** Collapsing two epics into Execution (Epic 2) + Operational Context (Epic 3) was documented, not accidental. The blind spots it introduced: **continuity fidelity/outcome** (no consumer-side success signal) and **continuity strategy** (the only proposed owner, the Session Router, spanned both epics and was dropped). Both are now **unowned**.

---

## 1. Objective 1 — Intended Responsibility Boundaries (Decision Engine vs Session Continuity)

Reconstructed from the two roadmaps verbatim. Intent only; no evaluation.

The two epics were authored as a **producer/consumer pair around a shared session model**:
- **Decision Engine** *owns* the decision-session lifecycle and produces `decisions.md` (`roadmap.md:63-76`).
- **Session Continuity** *consumes* that lifecycle's replacement events and produces/maintains `operational_context.md` (`roadmap-2.md:71-81`).
- The **Session Router** (Decision Engine M8) is the hinge: it emits Reuse/Create-New, which is the **sole trigger** for Session Continuity's work (`roadmap.md:605-653` → `roadmap-2.md:247-296`).

### 1a. Decision Engine responsibilities (Epic 4)

| Responsibility | Intended owner | Inputs | Outputs | Authority boundary |
|---|---|---|---|---|
| Decision generation (M3) | Decision session | `DecisionContext` | `decisions.md` (Title/Context/Options/Recommendation/Impact/Blocking, `roadmap.md:333-342`) | Proposes, never approves |
| Decision refinement (M5) | Live decision session | User dialogue (clarify, constraints, priorities, tradeoffs, `:448-457`) | Updated options/recommendation | User drives; engine adapts |
| Decision resolution (M6) | Human, via engine | Reviewed decisions | Resolved `decisions.md` + outcome/timestamp; fed to Execution Context Resolution (`:532-538`) | Human resolves |
| Reasoning continuity / long-horizon reasoning | **Reusable** decision session (`:55-57`) + Registry (M7) + Router (M8) | Decision history, session state | Carried-forward reasoning across the project (`:814`) | Engine reasons; human approves |
| Decision context resolution (M1) | Decision Engine | `plan.md`, milestone, `handoff.md`, `operational_context.md` (`:196-201`) | `DecisionContext` | Read-only assembly |

### 1b. Session Continuity responsibilities (Epic 5)

| Responsibility | Intended owner | Inputs | Outputs | Authority boundary |
|---|---|---|---|---|
| Operational context (M0/M1) | Continuity domain | `.agents/operational_context.md` + revision history (`roadmap-2.md:107-136,148-197`) | `OperationalContext`, `ContinuitySnapshot`, `ContextRevision`, `ContextTransferResult` | Storage/contracts only |
| Session-metadata for continuity (M2) | Continuity, **via Session Registry** | Decision-session metadata: age, token usage, last activity (`:200-244`) | Continuity metadata | Reads registry |
| Session replacement detection (M3) | Continuity, **via Session Router** | Router outcome Reuse/Create-New (`:257-266`) | Continuity trigger + "why transfer occurred" diagnostics | Fires **only** on Create-New (`:270-278`) |
| Continuity transfer / consolidation (M4) | Context Consolidation Engine | current op-context + `handoff.md` + `decisions.md` + decision discussion + artifacts (`:309-323`) | **Proposed** `operational_context.md` (`:327-333`) | Generates proposal, not authority |
| Review + acceptance (M5/M6) | Human | current/proposed/diff (`:368-398`) | Accepted op-context (authoritative, `:459-461`) | Human accepts/edits/rejects |
| New-session bootstrap (M7) | Continuity | Accepted op-context | Injected into **decision** context resolution (`:482-490`) | Inject + validate |
| Continuity assessment (M8) | Continuity | **Session-startup review**: repeated questions, lost decisions, missing context, incorrect assumptions (`:538-546`) | Outcome ∈ Successful/Partial/Failed + persisted observations (`:550-566`) | Observes; non-authoritative |
| Continuity analytics (M9) | Continuity | Transfers, revisions, acceptance rate, size growth, outcomes (`:591-601`) | Trends + reports as **evidence for Brainstorm research** (`:581,619-621`) | Reporting only |

**Boundary in one line:** the Decision Engine owns *making decisions inside reusable reasoning sessions*; Session Continuity owns *carrying project understanding across the boundary when such a session is recycled*. Neither owns execution, which both declare disposable (`roadmap.md:55`, `roadmap-2.md:7-9,25-45`).

---

## 2. Objective 2 — Decision Engine → Implementation Mapping

Focus responsibilities, classified. (Full M0–M10 detail in `decision-engine-audit.md` §2; summarized here.)

| Decision Engine responsibility | Status | Evidence |
|---|---|---|
| **Decision generation** | **Still Missing** | No code generates `decisions.md`; save/rotate only (`ArtifactsEndpoints` PUT; `ArtifactRotationService.cs:14-76`). `decisions.md` is human-authored. |
| **Decision refinement** | **Abandoned** | No conversational/interactive refinement anywhere. Review is over *operational-context proposals*, not decisions. |
| **Decision resolution** | **Still Missing** (namesake exists) | `ExecutionSessionService.AcceptAsync/RejectAsync` resolve **executions/handoffs**, not decisions (`:234-299`). No decision state machine; no resolved-decision feedback field on `ExecutionContext`. |
| **Reasoning continuity** | **Replaced (narrowed)** | Survives only as *settled understanding* assimilated into op-context (`DecisionAnalysisService` → `OperationalContextGenerationService.cs:146-207`). In-flight reasoning not retained. |
| **Long-horizon reasoning** | **Partially Replaced** | Durable conclusions persist in op-context; the *reasoning process* does not (see §7). |
| Decision **analysis** (adjacent, not in roadmap) | **Implemented** | `DecisionAnalysisService` parses existing decisions → signals, classifies (Architectural/Strategic/Tactical/Historical), extracts rationale/constraints/consequences/open-questions, flags superseded, warns contradictions (`epics/03/milestones/m6-decision-continuity.md:11-46`). Read-side only. |

**Net:** every *active* Decision-Engine responsibility (generate/refine/resolve/reason) is missing or abandoned; only the *read-side* distillation of human-authored decisions into understanding survives.

---

## 3. Objective 3 — Session Continuity → Implementation Mapping

This is the new core. Each Epic 5 milestone classified against built code.

| Session Continuity responsibility | Status | Evidence |
|---|---|---|
| **Operational context** (M0 domain, M1 infra) | **Implemented (renamed)** | Artifact, load, save, revision history all built. Target model names (`OperationalContext`, `ContinuitySnapshot`, `ContextRevision`, `ContextTransferResult`, `roadmap-2.md:107-112`) were **not adopted**; built as `OperationalContextDocument`, `UnderstandingEvolutionLedger`, `UnderstandingRevisionSnapshot`. |
| **Continuity transfer / consolidation** (M4) | **Implemented, decoupled from trigger** | `OperationalContextGenerationService` produces a proposed op-context from current context + handoff + decisions + artifacts. But invocation is **human-initiated** (`generate_operational_context_proposal`), not fired by a session-replacement event. |
| **Session replacement detection** (M3) | **Abandoned** | No Session Router → no Create-New event → no replacement trigger. Explicitly rejected: "session routing", "session reuse" as non-metrics (`epics/03/milestones/m9-continuity-instrumentation.md:39-41`). |
| **Bootstrap** (M7) | **Replaced + re-aimed** | Op-context is injected into **execution** context reconstruction, every run (`docs/architecture.md:88-99`; `ExecutionContextService`), not into decision-session bootstrap. Audience changed from decision sessions to execution slices. |
| **Continuity assessment** (M8) | **Partial / Substituted** | Built as **document-evolution** diagnostics (`ContinuityDiagnostics.cs:3-44`): `RepeatedQuestionIndicators`, `RepeatedInvestigationIndicators`, `DecisionReworkIndicators`, `ContinuityWarnings`, and `ContinuityTrend.LostCount` (`ContinuityTrend.cs:3-12`). **Absent:** session-startup review, "missing context", "incorrect assumptions", and the Successful/Partial/Failed **outcome classification** (`roadmap-2.md:550-560`). |
| **Continuity analytics** (M9) | **Partial / Reoriented** | `ContinuityDiagnosticsService` + `ContinuityReportService` compute revision count, frequency, byte growth, per-section trends, compression trends; reports persisted to `.agents/operational_context/reports/continuity.{timestamp}.json` (`epics/03/milestones/m9-continuity-instrumentation.md:9-33`). **Absent:** session transfers, acceptance *rate*, transfer frequency, continuity-outcome metrics, and the **"Brainstorm research"** evidence export (`roadmap-2.md:581,692-700` — named in roadmap only). |

**Mapping summary:** Epic 5's milestones split cleanly into a **built artifact spine** (M0, M1, M4, M5, M6) and a **dropped session spine** (M2, M3, M7-as-designed), with the **instrumentation layer** (M8, M9) built but **re-targeted from transfer-outcomes to document-evolution**.

---

## 4. Objective 4 — Operational Context Evolution

**Original responsibility (Epic 5):** narrow and single-purpose — "Preserve the minimum information required for a newly-created **decision session** to regain full project understanding" (`roadmap-2.md:81`); explicitly **not** execution state, execution history, or project archive (`:83-89`).

**Did the responsibility change? Yes — expanded and re-aimed.**

| Dimension | Epic 5 intent | Implemented reality |
|---|---|---|
| Consumer | A new **decision session** (`roadmap-2.md:81`) | A future **execution slice** (`docs/operational-context-schema.md:7`) |
| Trigger | On **session replacement** (`roadmap-2.md:270-278`) | On **every execution** (unconditional reconstruction, `docs/architecture.md:88-99`) |
| Declared role | Continuity-transfer payload | "Canonical internal representation for generation, review, semantic diff, compression, decision assimilation, projection, diagnostics, and reporting" (`docs/operational-context-schema.md:62`) |

**Which roles does it now perform?** All of the following simultaneously:
- **Continuity artifact** — yes (as intended).
- **Understanding artifact** — yes (`CurrentMentalModel`, `Architecture`).
- **Decision artifact (distilled)** — yes (`StableDecisions`, `DecisionRationale`, decision assimilation, `epics/03/milestones/m6-decision-continuity.md:25-29`).
- **Reasoning artifact** — partial (`DecisionRationale` only; in-flight reasoning excluded, `operational-context-schema.md:21-30`).
- **Project memory** — yes (`RecentUnderstandingChanges` + `UnderstandingEvolutionLedger`).

**Role expansion beyond original intent: confirmed and material.** A document scoped to "minimum info for one decision session" became the single canonical substrate for understanding, continuity, decision-distillation, risk/question tracking, diagnostics, and reporting. This **overloading** (flagged in `decision-engine-audit.md` §6) is the practical reason no *separate* continuity concern — strategy, fidelity, reasoning trajectory — has its own home: one document is asked to be everything continuity-related.

---

## 5. Objective 5 — Collapse of the Decision Session Ecosystem

Separating **mechanism** from **responsibility** throughout.

| Ecosystem element | Mechanism status | Responsibility | Responsibility fate |
|---|---|---|---|
| **Decision Session** | Abandoned (Non-Goal, `epics/02/plan.md:846-849`) | A locus for *active reasoning* (generate/refine/resolve) | **Unowned** — no runtime where the system reasons about a decision |
| **Session Router** | Abandoned; explicitly rejected (`m9-continuity-instrumentation.md:39-41`) | *Continuity-strategy selection* + "why" diagnostics (`roadmap.md:638-644`) | **Unowned** (see §9) |
| **Session Registry** | Abandoned | Persistent reasoning-state + **token economics** of continuity | **Unowned as first-class**; token data per-execution only |
| **Session Reuse** | Abandoned; rejected as continuity signal (`m9...:39-40`) | Warm-state acceleration / cost amortization | **Disappeared** (every session cold-starts from artifacts; reattach is crash-recovery only, `CodexExecutionProvider.cs:13` returns false) |

**What disappeared:** session reuse and any warm-state/acceleration concept.
**What survived (relocated):** continuity *transfer of understanding* — relocated from "on replacement, into a decision session" to "always, into execution, via artifact reconstruction."
**What became unowned:** continuity-strategy selection, continuity-fidelity assurance, and the economic view of continuity cost.

**Crucial distinction:** the mechanisms were rejected on **workflow-simplicity / single-authority / auditability** grounds (`epics/02/plan.md:844-858`; `m9-continuity-instrumentation.md:36-47`), **not** because their underlying responsibilities were judged worthless. Rejecting the mechanism silently orphaned the responsibility.

---

## 6. Objective 6 — Continuity Architecture Evolution (5-stage → 3-stage)

**Original (Epic 5, `roadmap-2.md:717-737`):**
`Decision Session → Session Router → Create New Session → Context Consolidation → Proposed op-context → User Review → Accepted op-context → Decision Context Resolution → New Decision Session`

**Implemented (`docs/architecture.md:88-99`):**
`Operational Context → Execution Context Resolution → Fresh Execution Session`
(with a **parallel, human-invoked** maintenance loop: `Generate Proposal → Review/Diff → Accept/Edit/Reject → Promote`, `OperationalContextReviewService.cs:49-109`, `UnderstandingDiffService.cs:9-46`, `OperationalContextLifecycleService`.)

| | Gained | Lost |
|---|---|---|
| Trigger | Simplicity: one unconditional path; no router to reason about | The *conditional* "only transfer when needed" optimization; no notion of *when* transfer is warranted |
| Consumer | Execution gets full continuity every run | Decision-session priming (the original purpose) does not exist |
| Coupling | Continuity no longer depends on a session lifecycle | Continuity work is decoupled from any *event*; it is a standing human chore, not an automatic response |
| Authority | Human-reviewed promotion preserved (Review-Before-Mutation) | — |

**Assumptions changed:**
- Continuity *requires reusable sessions* → **false in practice**; deterministic reconstruction suffices for current scope.
- Transfer must be *conditional/triggered* → replaced by *unconditional/standing*.
- The continuity beneficiary is a *reasoning* session → reassigned to an *execution* session.

**Unresolved continuity concerns:** with no trigger and no router, there is no component that decides *how* or *whether* to continue (strategy), and no measurement of *whether the rebuilt context actually primed the next worker* (fidelity). See §8–§9.

---

## 7. Objective 7 — Reasoning Continuity

Compared against **both** epics' promises (Decision Engine M2/M5 "reasoning output" + refinement; Session Continuity's "useful for future reasoning", `roadmap-2.md:741`).

| Reasoning element | Status | Evidence |
|---|---|---|
| Reasoning trajectory | **Absent** | No field/artifact captures in-flight reasoning. |
| Decision evolution over time | **Absent** | Only current state persists; no "how/why a decision changed" history. |
| Rejected alternatives | **Absent** | Only a "superseded/retired" *flag*; the rejected reasoning is discarded. |
| Tradeoff history | **Absent** | No tradeoff capture in schema or signals. |
| Architectural exploration | **Absent** | `Architecture` holds *settled* structure, not explored options. |
| Hypothesis evolution | **Absent** | No hypothesis representation. |
| Emerging contradictions | **Transient only** | Detected → warning during review, then discarded; not persisted into active context (`DecisionAnalysisService` contradiction warnings; `epics/03/milestones/m6-decision-continuity.md:46`). |

**`UnderstandingEvolutionLedger` / `UnderstandingRevisionSnapshot` record section item-*counts* per revision (what changed), not *why* it changed.** The result is identical against both epics' aims: a new session inherits **settled conclusions** and the last recent-change notes, but **no trace of the reasoning that produced them**. Epic 4's explicit "reasoning output" capture (`roadmap.md:281-287`) and Epic 5's "useful for future reasoning" goal (`roadmap-2.md:741`) both reduce, in implementation, to *durable conclusions only*.

---

## 8. Objective 8 — Continuity Assessment & Research Capabilities

This is where Epic 5's most distinctive ambition lives, and where the implementation diverges most subtly.

**What Epic 5 envisioned (M8/M9):**
- **Session-startup review** capturing four continuity-failure signals: *repeated questions, lost decisions, missing context, incorrect assumptions* (`roadmap-2.md:538-546`).
- **Outcome classification**: Successful / Partially Successful / Failed (`:550-560`).
- **Persisted observations** (`:564-566`) and **analytics** (transfers, revisions, acceptance rate, size growth, outcomes; `:591-601`) producing **research evidence for "Brainstorm"** (`:581,692-700`).

**What was built (Epic 03 M9 "Continuity Instrumentation"):**
- `IContinuityDiagnosticsService`, `IContinuityReportService`, `UnderstandingEvolutionLedger` (`m9-continuity-instrumentation.md:9-11`).
- Read-only diagnostics: revision count/frequency, context bytes/growth, per-section retention trends (architecture/constraint/decision/rationale/open-question/risk), compression trends, and indicator sets: **repeated investigation, repeated question, decision rework** (`:12-26`; fields in `ContinuityDiagnostics.cs:37-43`).
- On-demand reports persisted under `.agents/operational_context/reports/` as **diagnostic artifacts, not workflow gates** (`:27-33`).

**The divergence, precisely:** the built instrumentation measures **the operational-context document's evolution against itself** (does a question reappear across revisions? did a decision's count drop?), whereas Epic 5 M8 measures **the next session's lived experience** (did *this new session* re-ask, lose, miss, or misassume?).

| Epic 5 M8 signal | Built equivalent | Verdict |
|---|---|---|
| Repeated questions | `RepeatedQuestionIndicators` (document self-comparison) | **Proxy only** — detects doc churn, not session re-asking |
| Lost decisions | `DecisionReworkIndicators`, `ContinuityTrend.LostCount` | **Proxy only** — inferred from revision deltas |
| Missing context | — | **Absent** — undetectable without a consumer report |
| Incorrect assumptions | — | **Absent** — structurally impossible from self-comparison |
| Outcome ∈ Successful/Partial/Failed | — | **Absent** (`ContinuityOutcome`/`ContextTransferResult` do not exist) |
| Research evidence ("Brainstorm") | — | **Absent** — named in roadmap only |

**Observability that *does* exist** answers the certification questions "Is understanding improving / degrading? Are decisions surviving? Are questions resolved? Is compression working? Is continuity succeeding?" (`m9-continuity-instrumentation.md:80-86`) — but every answer is derived from **artifact telemetry**, never from a **transfer outcome**. The explicit non-metrics list (`:36-47`) confirms this was a *deliberate* choice to avoid session/productivity signals, not an omission.

**Bottom line for Objective 8:** continuity *assessment as artifact health* is **implemented and persisted**; continuity *assessment as transfer success* is **abandoned**. The two failure signals that genuinely require a consumer feedback loop — *missing context* and *incorrect assumptions* — have **no representation anywhere**.

---

## 9. Objective 9 — Ownership of Continuity Strategy

| Continuity concern | Owner? | Evidence |
|---|---|---|
| **Continuity strategy** (how/whether to continue) | **Unowned** | The Session Router was the sole proposed owner (`roadmap.md:605-653`); abandoned. Continuity is one fixed strategy: full unconditional reconstruction. No input (age/cost/staleness) selects an approach. |
| **Continuity fidelity** (is the rebuilt context sufficient?) | **Unowned** | No outcome measurement exists (§8). Reconstruction is *assumed* sufficient. |
| **Continuity degradation** | **Partial owner** | `ContinuityDiagnosticsService` detects **document** degradation (repeated questions, decision rework, `LostCount`, warnings) — but not **transfer** degradation. Degradation of the artifact is observed; degradation of the *handoff to the next worker* is not. |
| **Continuity optimization** (fidelity vs cost) | **Unowned** | `UnderstandingCompressionService` optimizes for *size/preservation-safety* only (Preserve/Summarize/Retire); it is explicitly not a strategy/cost optimizer. |
| **Continuity assessment** | **Partial owner** | `IContinuityDiagnosticsService` / `IContinuityReportService` own *artifact-evolution* assessment; no owner for *transfer-outcome* assessment (M8 outcome classification absent). |

**Where ownership was lost:** continuity strategy and continuity fidelity were **bundled inside the rejected mechanisms** (Router, Registry, decision-session reuse) that spanned *both* epics. Epic 4 owned the *routing decision* (which session, reuse vs new); Epic 5 owned the *transfer-quality decision* (did it work?). Collapsing both epics removed both owners, and neither responsibility was relocated. What *was* relocated — assessment — was narrowed to artifact telemetry.

---

## 10. Objective 10 — Did Roadmap Compression Create Blind Spots?

The two epics were authored as **independent concerns** but were **mechanically interdependent** (Epic 5 needs Epic 4's session model; §1). The implementation compressed them into **Execution (Epic 2)** + **Operational Context (Epic 3)**, dropping Epic 4 entirely and adopting Epic 5's artifact half. Verifying consequences against evidence (not assuming harm):

**Compression that was sound (no blind spot):**
- *Execution continuity stayed out of scope* in both roadmaps and the implementation (`roadmap-2.md:7-9,25-45`; `docs/architecture.md:41-43`). Consistent and intentional.
- *The artifact machinery* (domain, infra, consolidation, review, acceptance, diff, compression, instrumentation) shipped substantially complete and tested (`m6-decision-continuity.md`, `m9-continuity-instrumentation.md` checklists).
- *Rejection of session/productivity metrics* was an explicit, reasoned decision (`m9-continuity-instrumentation.md:36-47`), not an oversight.

**Blind spots that compression *did* introduce (evidenced):**
1. **Continuity fidelity / transfer outcome.** Epic 5 M8's success signal lived in a *session-startup feedback loop*. That loop required decision sessions (Epic 4). Dropping Epic 4 silently removed the *only* place transfer success would have been observed; the implementation substituted document self-analysis, which **cannot** see "missing context" or "incorrect assumptions" (§8). A faithful-*looking* document that fails to prime the next worker is invisible to the system.
2. **Continuity strategy ownership.** The Session Router spanned both epics (routing in Epic 4, transfer-trigger in Epic 5). Collapsing both left the *strategy* responsibility with no home (§9).
3. **Responsibility collapse via overloading.** Two epics' worth of distinct concerns (decision distillation + continuity substrate + understanding + risk/question tracking + diagnostics) collapsed into one document model (`operational-context-schema.md:62`), which is *why* no separate concern can be independently owned or evolved (§4).

**Hidden assumption surfaced by compression:** that *measuring how the continuity artifact changes* is equivalent to *measuring whether continuity worked*. The built certification treats them as the same question ("Is continuity succeeding?", `m9-continuity-instrumentation.md:85`); Epic 5 treated them as different (M8 vs M9). This conflation is the single clearest blind spot.

---

## 11. Roadmap Inputs (findings for a replacement roadmap — not a plan)

**Unresolved responsibilities**
- Active decision-making workflow (generate/refine/resolve) — only read-side analysis of human-authored decisions exists.
- Transfer-outcome assessment (did the next worker actually regain understanding?) — no consumer feedback loop.
- Reasoning-trajectory preservation across boundaries.

**Missing ownership**
- Continuity *strategy* selection (post-Session-Router).
- Continuity *fidelity* assurance (is reconstruction sufficient for the consumer?).
- Continuity *cost economics* (only per-execution token data today).
- The two consumer-side continuity signals — *missing context* and *incorrect assumptions* — have no owner and no representation.

**Continuity limitations**
- One fixed strategy (unconditional full reconstruction); no conditional/degraded/accelerated path.
- Assessment measures artifact evolution, not transfer success; "Is continuity succeeding?" is answered by proxy.
- `OperationalContextDocument` overloaded across understanding / continuity substrate / decision distillation / risk-question tracking / diagnostics (`operational-context-schema.md:62`).

**Reasoning limitations**
- Only *settled conclusions* persist; hypotheses, tradeoffs, rejected alternatives, decision evolution lost at session end.
- Contradictions are transient warnings, never persisted.

**Architectural tensions**
- *Audience mismatch:* op-context was designed for decision sessions, serves execution sessions; M8's feedback loop assumed a decision-session consumer that does not exist.
- *Trigger absence:* continuity is a standing human chore, decoupled from any event — no signal says "continuity work is now due."
- *Proxy-vs-outcome:* artifact telemetry is treated as continuity quality.

**Evolution opportunities (observations only)**
- The shipped artifact substrate (op-context generate/review/diff/compression + decision analysis + evolution ledger + diagnostics/reports) already supplies most read-side and storage primitives a transfer-outcome layer or reasoning layer would need.
- Mechanism and responsibility are separable here: the orphaned responsibilities (strategy, fidelity, transfer-outcome, active reasoning) do **not** require reintroducing the rejected mechanisms (sessions/router/registry) to be satisfied.
- A consumer-side feedback signal could attach to the *execution* lifecycle that already exists, rather than the *decision-session* lifecycle that was rejected.

---

## 12. Success Criteria — Direct Answers

1. **What responsibilities originally belonged to Decision Engine?** Decision generation, refinement, and resolution inside *reusable decision sessions*; decision context resolution; and long-horizon **reasoning** continuity — routed by need via the Session Router/Registry (`roadmap.md:3-23,55-97,792-814`).

2. **What responsibilities originally belonged to Session Continuity?** Operational-context as the authoritative continuity artifact; continuity transfer on session replacement; session-replacement detection; new-session bootstrap; and continuity **assessment + analytics** producing research evidence (`roadmap-2.md:3-19,71-81,524-628`).

3. **Which responsibilities were successfully implemented?** Operational-context domain/infrastructure/revision-history; consolidation (proposal generation); review/diff; acceptance/promotion; and artifact-evolution diagnostics/reports — i.e. Epic 5's **artifact spine** (built as Epic 03; `m6-decision-continuity.md`, `m9-continuity-instrumentation.md`). Decision **analysis** (read-side) was also implemented.

4. **Which responsibilities were absorbed by Operational Context?** Continuity transfer (generalized to unconditional reconstruction), decision distillation (assimilation of durable decisions/rationale), understanding storage, risk/question tracking, and project memory (evolution ledger). It absorbed **more** than Epic 5 scoped it for (§4).

5. **Which responsibilities disappeared entirely?** Reusable decision sessions, the Session Router, the Session Registry, session reuse/warm-state, decision **generation/refinement/formal resolution**, transfer-outcome classification (Successful/Partial/Failed), and the "Brainstorm" research export.

6. **Which responsibilities remain unresolved?** Active decision-making workflow; transfer-outcome (fidelity) assessment; continuity-strategy selection; reasoning-trajectory preservation; continuity cost economics.

7. **Which continuity concerns currently lack ownership?** Continuity *strategy*, continuity *fidelity*, continuity *optimization* (fidelity-vs-cost), and *transfer-outcome* assessment. (Artifact-health assessment and settled-understanding continuity *are* owned.)

8. **Which reasoning concerns currently lack ownership?** Hypotheses, tradeoff history, rejected alternatives, architectural exploration, decision evolution, and persisted contradictions — none have an owner; only settled conclusions survive (§7).

9. **Did roadmap compression introduce architectural blind spots?** Yes — two, evidenced: (a) **continuity fidelity/outcome** (the consumer-side success signal required decision sessions that were dropped; document self-analysis cannot replace it — "missing context" and "incorrect assumptions" are undetectable); (b) **continuity-strategy ownership** (the Session Router spanned both epics and was dropped). A third structural effect is **overloading** of one document with both epics' concerns. Compression was otherwise sound (execution-continuity exclusion and metric-rejection were deliberate and consistent).

10. **What responsibilities should inform a replacement roadmap?** (Responsibilities, not mechanisms.) An owner for **continuity strategy and fidelity**; a **transfer-outcome feedback** signal attached to the *execution* lifecycle that exists; **active decision-making** (generate/refine/resolve) distinct from read-side analysis; **reasoning-trajectory** preservation; and disentangling the **overloaded operational-context document** so distinct concerns can be owned independently.

---

*Evidence base: `.agents/roadmap.md` (Epic 4); `.agents/roadmap-2.md` (Epic 5); `.agents/archive/epics/01–04/{plan,milestones}` (esp. `epics/03/milestones/m6-decision-continuity.md`, `m9-continuity-instrumentation.md`; `epics/02/plan.md:844-858`; `epics/04/plan.md`); `docs/architecture.md`; `docs/operational-context-schema.md`; backend `src/CommandCenter.{Continuity,Execution,Backend}` (esp. `ContinuityDiagnostics.cs`, `ContinuityTrend.cs`, `ContinuityDiagnosticsService.cs`, `ContinuityReportService.cs`, `UnderstandingEvolutionLedger.cs`, `UnderstandingRevisionSnapshot.cs`, `OperationalContext*Service.cs`, `DecisionAnalysisService.cs`). Companion: `decision-engine-audit.md`. Findings only — no solutions, plans, or milestone proposals, per charter.*
