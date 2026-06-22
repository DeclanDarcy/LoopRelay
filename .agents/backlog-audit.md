# backlog-audit.md

> Purpose: capture the information required to later regenerate a correct backlog.
> This document describes reality. It does **not** design the replacement backlog, propose
> solutions, or author roadmaps/epics.

Audited artifact: `.agents/backlog.md` (Epics 5–9).
Audit date anchor: 2026-06-22.

---

## 1. Executive Summary

The backlog under audit defines five forward-looking epics:

```text
Epic 5 — Reasoning Trajectory Preservation
Epic 6 — Continuity Fidelity
Epic 7 — Continuity Strategy
Epic 8 — Operational Context Decomposition
Epic 9 — Long-Horizon Research & Brainstorm Evidence
```

**The audit's working premise must be corrected at the outset.** The regeneration prompt assumes
the backlog "was created before substantial implementation occurred." Git contradicts this. The
backlog is the **single most recent commit in the repository** (`11f9fe7 2026-06-22 "backlog"`),
authored *after* all five implementation epics (01–05) were completed and archived on the same day
(`35f4cf0 2026-06-22 "archive"` immediately precedes it). The backlog therefore does **not** suffer
from pre-implementation staleness. It is a *post-Epic-05 "what remains" document.*

Consequently the real audit question is not "what changed after the backlog was written" (nothing
did — it is at HEAD) but **"does the backlog's framing accurately reflect the architecture that
already existed when it was written, or does it carry forward assumptions the implementation had
already resolved, reassigned, or reframed?"**

Headline findings:

1. **Epics 5, 6, 7 are genuine, unbuilt, unowned gaps.** They are a near-verbatim transcription of
   the explicit *Future Work / Non-Goals* deferrals recorded in Epic 05's plan
   (`epics/05/plan.md:33, 784, 788, 830`). The responsibilities do not exist in code today. These
   epics are *aligned with reality*, not stale.

2. **Epic 8 is partially overtaken.** One of the five overloads it names — "Decision Distillation" —
   was already extracted from Operational Context by Epic 05's ratified *assimilation boundary*
   ("Decision Authority is not equivalent to Operational Context Authority",
   `epics/05/decisions.0031.md:12`, `0032.md:18`). Its remaining surface (continuity, diagnostics,
   project memory) is real but smaller than the backlog states.

3. **Epic 9 rests on a partly mythologized premise.** It claims to "recover research-oriented
   responsibilities originally envisioned in Session Continuity," but the word "Brainstorm" appears
   **nowhere** in the entire epic archive, and Epic 03 *deliberately compresses* research/exploration
   into outcomes by design (`epics/03/m5`). The capability was not lost; much of it was never
   designed, and part of it was intentionally compressed away. The backlog itself conceds "Most of
   that capability was never realized" (`backlog.md:209`).

4. **The cited "Session Continuity audit" is not a discoverable artifact.** The phrases
   "Session Continuity audit," "Document Health vs Transfer Success," "Full Reconstruction,"
   "Reasoning Trajectory," "Continuity Fidelity," and "Continuity Strategy" appear **only** in
   `backlog.md`. They are the backlog author's framing layered over real Epic 03/05 design
   boundaries — not quotations from an archived audit. This is a citation/provenance drift the
   regenerated backlog should repair.

5. **A numbering/identity collision exists.** Backlog epics are numbered 5–9 while the implemented
   archive runs 01–05. Worse, Epic 9's own text references "Epic 5" as the *Session Continuity*
   epic — a different conceptual Epic 5 than the backlog's "Epic 5 — Reasoning Trajectory
   Preservation." The numbering does not survive contact with the archive.

6. **The prompt's candidate "new themes" (Decision Lifecycle, Governance, Certification, Execution
   Projection) are already implemented**, not pending. They should not become future backlog epics.
   The genuinely-emergent *unbuilt* theme is **Operational Adoption** (Epic 05 M10, all tasks
   unchecked) plus the **backend-owned frontend gaps** Epic 04 explicitly deferred (abort control,
   global overview, notifications).

---

## 2. Repository Evolution Relative To Backlog Framing

Because the backlog post-dates implementation, "evolution since creation" is, in commit terms, nil.
The material evolution is everything in Epics 01–05 that the backlog's framing must be checked
against. Timeline (first-archived commit per epic):

| Date (2026) | Epic | Theme | What it established that bears on the backlog |
|---|---|---|---|
| 06-19 | 01 | Repository & Artifact Management | Filesystem-authoritative artifacts; archive-only rotation; deferred *all* continuity, op-context, decisions to later epics. |
| 06-20 | 02 | Autonomous Execution | Disposable sessions ("not reused as continuity or project memory"); "Observation Is Not Interpretation"; handoff-is-completion. Created the *conditions* for "Document Health without Transfer Success." |
| 06-20 | 03 | Decision/Understanding **Continuity** | Created **Operational Context** + **Continuity** subsystems: proposal→review→promote→compress→assimilate→project→certify→instrument. Long-horizon certification (M8) mandates *archive-independent full reconstruction*. Read-only continuity instrumentation (M9) measures **document health only**. |
| 06-21 | 04 | Frontend Modernization | Four-workspace console; read-only projections; deliberately added **no backend authority**; deferred abort/global-overview/notifications as backend-owned gaps. |
| 06-22 | 05 | **Decision Lifecycle** | Created **Decision Lifecycle / Governance / Certification** subsystems; extended **Execution Projection**. Ratified the decision↔operational-context **assimilation boundary**. **Explicitly disclaimed** reasoning trajectory preservation, continuity fidelity, continuity strategy. M10 (Operational Adoption) left unstarted. |
| 06-22 | — | **backlog.md committed** | Transcribes Epic 05's deferrals (5/6/7) + Epic 03 continuity-substrate gaps (6/7/9) + an Operational Context overloading concern (8). |

Net: the backlog is a *faithful echo of Epic 05's deferral list and Epic 03's continuity gaps*, with
two framing problems — (a) it attributes the gaps to a non-existent "Session Continuity audit," and
(b) it does not fully internalize the boundaries Epic 05 had *just* ratified (decision distillation
already removed from Operational Context; decision-internal reasoning already captured in proposal
lineage).

Current code reality (subsystem → project):

- **Operational Context** — `CommandCenter.Continuity` (+ `CommandCenter.Middle` generation). Owns
  "current understanding only"; explicitly does *not* own raw history, sessions, or session memory
  (`docs/architecture.md:76, 78, 99`).
- **Session Continuity** — `CommandCenter.Continuity`. Artifact-mediated understanding transfer;
  diagnostics/reports measure revision-over-revision **document health** (`ContinuityDiagnostics`).
- **Decision Lifecycle** — `CommandCenter.Decisions`. `Decision`/`Candidate`/`Proposal` state
  machines; proposal **tradeoffs, options, assumptions, lineage, revisions**.
- **Governance** — `DecisionGovernanceService`; findings carry `BlocksExecutionProjection`.
- **Certification** — `DecisionCertificationService`; pass/fail over the decision lifecycle only
  (no system-wide certification).
- **Execution / Execution Projection** — `CommandCenter.Execution` (disposable sessions, context
  assembly, handoff, git) + `IDecisionProjectionService.BuildExecutionProjectionAsync`
  (`ExecutionDecisionProjection`: constraints/directives/conflicts).

Concept-existence checks (grep-confirmed absent unless noted): no `ReasoningTrajectory`,
`RejectedAlternative`, `TransferOutcome`/`TransferSuccess`/`Fidelity*`, `ContinuityStrategy`/
`ContinuityDegradation`/`ContinuityOptimization`, `ResearchEvidence`/`BrainstormEvidence`/
`DriftAnalytic` types exist. "Brainstorm" appears nowhere in the archive. "drift" survives only as a
discovery keyword (`MilestoneContextDrift` candidate signal). "long-horizon" survives only as
certification fixture thresholds (50/100/200-decision).

---

## 3. Epic-by-Epic Analysis

### Epic 5 — Reasoning Trajectory Preservation

**Original Intent.**
- *Purpose:* preserve the reasoning *behind* settled conclusions — "Settled conclusions survive.
  Reasoning does not." (`backlog.md:7-9`).
- *Assumptions:* that no subsystem persists hypotheses, tradeoffs, rejected alternatives,
  architectural exploration, decision evolution, or contradictions.
- *Ownership model:* a dedicated owner of "Reasoning Trajectory / Decision Evolution / Alternative
  History / Tradeoff Preservation / Architectural Exploration."
- *Responsibilities:* a trajectory model, trajectory artifacts, trajectory review workflows,
  decision-evolution visibility, contradiction persistence.

**Original Gap.** No cross-session persistence of *why* decisions were reached, only *what* was
decided.

**Current State.**
- *What exists:* Decision Lifecycle captures a meaningful slice — `DecisionTradeoff`/
  `DecisionTradeoffRevision` (with previous/revised pairs), `DecisionOption`/`...Revision`/
  `...Comparison`, `DecisionAssumption`/`...Revision`, and `DecisionProposalLineage` +
  lineage-event + revision snapshots. Rejected options and retired assumptions are preserved
  **inside proposal revision history** (`epics/05/m5`). "contradict" exists only as text-matching in
  discovery and an assimilation warning rule (`schema:200`).
- *What moved elsewhere:* the *proposal-scoped* portion of this responsibility is now firmly owned
  by Decision Lifecycle.
- *What remains unowned:* hypotheses; tradeoffs/alternatives *outside* a proposal; free-standing
  architectural exploration; cross-decision reasoning *trajectory*; durable contradiction
  persistence. Epic 05 **explicitly disclaimed** all of this
  (`epics/05/plan.md:33, 94-96, 782-784, 828-830`).

**Architectural Changes Since Creation.** None after the backlog (it is at HEAD). The relevant
*pre-existing* change is Decision Lifecycle's proposal-lineage machinery, which the backlog does not
acknowledge as already owning part of this surface.

**Remaining Problem Surface.**
- *Fully Solved:* proposal-internal tradeoff/option/assumption capture and revision lineage.
- *Partially Solved:* "decision evolution visibility" — exists *within* a proposal's lineage, absent
  *across* decisions.
- *Unsolved:* hypotheses, non-proposal alternatives, architectural exploration, contradiction
  persistence, a cross-session trajectory model.

**Ownership Analysis.** The unsolved core is a candidate for a **new subsystem** (Reasoning/Trajectory)
sitting *above* Decision Lifecycle. It must **not** reclaim Decision Lifecycle's proposal lineage —
that boundary is already owned and ratified. Some "decision evolution visibility" deliverables
duplicate `DecisionProposalLineage` and belong to Decision Lifecycle, not here.

**Regeneration Recommendation:** **Realign** (core preserved). The gap is real and unowned, but the
epic's scope must be narrowed to exclude what Decision Lifecycle already preserves; restate it as the
*non-proposal, cross-decision* reasoning surface.

---

### Epic 6 — Continuity Fidelity

**Original Intent.**
- *Purpose:* measure *transfer success*, not just document health (`backlog.md:52-57`).
- *Assumptions:* the system measures "Document Health" but not "Transfer Success"; missing
  context, incorrect assumptions, transfer failure, and consumer understanding are invisible.
- *Ownership model:* an owner of continuity assessment/validation/transfer-outcome analysis.
- *Responsibilities:* continuity outcome model, transfer assessments, consumer-side feedback,
  fidelity diagnostics/reporting.

**Original Gap.** Continuity instrumentation observes the *producer/document* side only and never
asks whether a consuming session actually understood the transferred context.

**Current State.**
- *What exists:* `ContinuityDiagnostics` (revision counts, byte growth, six evolution trends,
  evolution ledger, compression trend, repeated-investigation/question/rework indicators,
  warnings); `ContinuityReport`. Strictly observational — "diagnostics remain observational only…
  must not introduce continuity scores, automatic promotion/rejection/correction, or workflow
  gates" (archived M9).
- *What moved elsewhere:* nothing — no subsystem owns transfer outcome.
- *What remains unowned:* transfer success/failure, missing-context detection, incorrect-assumption
  detection, consumer understanding, any consumer-side feedback channel.

**Architectural Changes Since Creation.** None post-backlog. Epic 04 M6 added a **Continuity
Workspace** UI that *surfaces the same document-health diagnostics* and explicitly avoids continuity
governance — it did not add transfer-fidelity measurement.

**Remaining Problem Surface.**
- *Fully Solved:* document-health observation.
- *Partially Solved:* none — the "fidelity" axis is structurally absent, not partial.
- *Unsolved:* the entire transfer-outcome / consumer-feedback surface.

**Ownership Analysis.** Belongs to **Session Continuity** (extending the existing `Continuity`
subsystem) since it is the natural home for transfer measurement. A consumer-side feedback channel
would require Execution cooperation (sessions are the consumers) but ownership of the *assessment*
stays in Continuity.

**Regeneration Recommendation:** **Preserve.** Clean, real, unowned gap that matches reality exactly.
(See cross-epic note on pairing with Epic 7 under a single Continuity theme.)

---

### Epic 7 — Continuity Strategy

**Original Intent.**
- *Purpose:* introduce ownership of continuity *strategy* where today there is exactly one —
  "Full Reconstruction" (`backlog.md:107-130`).
- *Assumptions:* no component owns strategy, optimization, degradation, or cost/fidelity tradeoffs.
- *Ownership model:* an owner of continuity strategy/degradation/optimization/diagnostics; explicitly
  **not** session routing (`backlog.md:148`). Notably the epic itself states "The responsibility
  survives. The mechanism does not need to." (`backlog.md:151-153`).
- *Responsibilities:* strategy model, continuity policies, diagnostic explanations, optimization and
  degradation frameworks.

**Original Gap.** Continuity is assembled by a single hard-coded path (deterministic full
reconstruction from artifacts each execution), with no policy layer choosing fidelity vs cost.

**Current State.**
- *What exists:* the fixed reconstruction path. Execution assembles context in a fixed order (Plan →
  Selected Milestone → Operational Context → Current Handoff → Current Decisions → Git Snapshot,
  `docs/architecture.md:90-97`) under a size policy (`ExecutionContextSizePolicy`). Continuity owns
  compression *tiers* (Preserve/Summarize/Retire) but as fixed rules, not a chooseable strategy.
  Epic 03 M8 ratified *archive-independent full reconstruction* as "the core continuity hypothesis."
- *What moved elsewhere:* context-assembly authority lives in **Execution**; compression-tier
  authority lives in **Continuity**.
- *What remains unowned:* strategy selection, degradation, optimization, cost/fidelity tradeoffs.

**Architectural Changes Since Creation.** None post-backlog.

**Remaining Problem Surface.**
- *Fully Solved:* a single working strategy (full reconstruction) plus fixed compression tiers.
- *Partially Solved:* none.
- *Unsolved:* the strategy/policy/optimization/degradation surface.

**Ownership Analysis.** Belongs to **Session Continuity** as policy owner, but with a hard
constraint: it must **not** seize Execution's context-assembly authority or re-implement Continuity's
compression mechanism. This is an authority-boundary hazard (see §4). The backlog's own caveat
("mechanism does not need to survive, responsibility survives") is the correct framing.

**Regeneration Recommendation:** **Preserve** — with an explicit boundary guard against Execution and
the existing compression mechanism. Strong merge candidate with Epic 6.

---

### Epic 8 — Operational Context Decomposition

**Original Intent.**
- *Purpose:* resolve Operational Context overloading — it "simultaneously acts as Understanding /
  Continuity / Decision Distillation / Diagnostics / Project Memory" (`backlog.md:165-179`).
- *Assumptions:* a single artifact/service carries five distinct responsibilities.
- *Ownership model:* determine what belongs in Operational Context and what does not.
- *Responsibilities:* responsibility decomposition, authority boundaries, artifact-boundary
  refinement, ownership clarification.

**Original Gap.** Operational Context is the canonical model "for generation, review, semantic diff,
compression, decision assimilation, projection, diagnostics, and reporting"
(`docs/operational-context-schema.md:7, 62`) — i.e. genuinely overloaded.

**Current State.**
- *What exists / moved elsewhere:* Epic 05 **already extracted the "Decision Distillation" axis.** It
  ratified that "Decision Authority is not equivalent to Operational Context Authority"
  (`epics/05/decisions.0031.md:12`, `0032.md:18`), that decision resolution "must not mutate
  `.agents/operational_context.md`" (`0031.md:10`), and that decisions hand changes to operational
  context only as *recommendation packages* for the op-context workflow to review (M6). Operational
  Context already declares it does **not** own raw history or session memory
  (`docs/architecture.md:78`); "operational context is not session memory" (`:76`).
- *What remains unowned/conflated:* the **Continuity / Diagnostics / Project Memory** axes still live
  inside the single `OperationalContextDocument` + `Continuity` subsystem with single-service
  ownership. Op-context is internally *sectioned* (item kinds, sections) but not *decomposed by
  authority*.

**Architectural Changes Since Creation.** The decisive change (Epic 05's assimilation boundary)
landed *the same day as, and conceptually before,* the backlog — yet the backlog still lists
"Decision Distillation" as an active overload. This is the clearest case of the backlog under-
counting already-ratified boundaries.

**Remaining Problem Surface.**
- *Fully Solved:* the decision-distillation overload (carved out by Epic 05); the "raw history /
  session memory" exclusions (carved out by Epic 03's authority statement).
- *Partially Solved:* understanding vs continuity vs diagnostics separation — sectioned but not
  authority-decomposed.
- *Unsolved:* explicit authority decomposition of Continuity / Diagnostics / Project Memory away from
  the single Operational Context owner.

**Ownership Analysis.** This is an **Operational Context** + **Session Continuity** boundary question.
It must respect — not relitigate — the decision/op-context boundary Decision Lifecycle already
ratified. Its surviving surface heavily overlaps Epics 6 and 7 (all three concern what Continuity vs
Operational Context owns).

**Regeneration Recommendation:** **Realign** (scope materially reduced). The decision axis is done;
restate the epic against the still-conflated continuity/diagnostics/memory axes and fold it into the
Continuity cluster rather than treating it as an independent decomposition mandate.

---

### Epic 9 — Long-Horizon Research & Brainstorm Evidence

**Original Intent.**
- *Purpose:* "recover the research-oriented responsibilities originally envisioned in Session
  Continuity" and turn continuity observations into "evidence for future Brainstorm work"
  (`backlog.md:201-209`).
- *Assumptions:* continuity observations were *intended* to feed a Brainstorm capability and that
  intent was lost.
- *Ownership model:* an owner of Research Evidence / Continuity Observations / Long-Horizon,
  Drift, and Reasoning Analytics.
- *Responsibilities:* research evidence model, long-horizon reporting, drift analysis, continuity
  analysis, brainstorm evidence exports.

**Original Gap.** Continuity observations are produced as on-demand diagnostic reports but are never
exported as durable research/brainstorm evidence; no long-horizon analytics exist.

**Current State.**
- *What exists:* on-demand `ContinuityReport`/diagnostics; `DecisionEvidence` (decision-scoped,
  summary + sources); drift only as a discovery candidate signal; "long-horizon" only as
  certification scale fixtures.
- *What moved elsewhere:* nothing relevant — Epic 03 *actively compresses* resolved investigations
  into outcomes (`epics/03/m5`), the opposite of preserving them as research evidence.
- *What remains unowned:* a research/brainstorm evidence model, long-horizon reporting, drift/
  reasoning analytics, evidence exports. The **Brainstorm consumer these would feed does not exist**
  anywhere in the codebase or archive.

**Architectural Changes Since Creation.** None post-backlog. The relevant pre-existing reality is
that Epic 03 made a *deliberate design choice* to compress exploration away — so the premise that the
capability was "originally envisioned and lost" is only partly true; it was largely never designed,
and partly designed *out*.

**Remaining Problem Surface.**
- *Fully Solved:* none.
- *Partially Solved:* continuity/decision observations *are* generated (raw material exists) but not
  exported or analyzed long-horizon.
- *Unsolved:* the research-evidence model, analytics, and exports — and the *consumer* (Brainstorm)
  itself.

**Ownership Analysis.** Split ownership: the *export of existing observations* belongs to
**Session Continuity** / **Decision Lifecycle** (they own the source data); a "Brainstorm Research"
home is **a new subsystem that does not yet exist**, so the export half cannot be owned until a
consumer exists. The "drift analytics / reasoning analytics" deliverables overlap Epics 5, 6, 7.

**Regeneration Recommendation:** **Realign** (premise repair + de-duplication), with the
Brainstorm-export portion a candidate for **Retire** until a Brainstorm consumer is designed. Strip
the mythologized "originally envisioned" framing and de-conflict the analytics deliverables against
Epics 5/6/7.

---

## 4. Cross-Epic Findings

### Duplicate Responsibilities

- **"Decision evolution / alternative history" (Epic 5)** duplicates Decision Lifecycle's already-
  built `DecisionProposalLineage`, option/tradeoff/assumption revisions.
- **"Drift analytics" and "reasoning analytics" (Epic 9)** overlap Epic 5 (reasoning) and Epics 6/7
  (continuity drift/diagnostics).
- **"Continuity observations as evidence" (Epic 9)** overlaps Epic 6 (continuity assessment) and
  Epic 7 (continuity diagnostics).
- **The Continuity cluster (Epics 6, 7, 8, 9) collectively re-asks "what does Continuity vs
  Operational Context own and measure"** from four angles (measurement, strategy, decomposition,
  analytics). These are facets of one boundary question, not four independent epics.

### Missing Responsibilities (real gaps absent from the backlog)

- **Operational Adoption / rollout** — Epic 05 M10 ("operational adoption") is entirely unstarted
  (all tasks unchecked). The most concrete remaining work in the repo is **not** in the backlog.
- **Backend-owned frontend gaps** Epic 04 explicitly deferred: **abort control, global overview,
  notifications** (`epics/04/m8-final-validation.md:43-54`). Unowned, unrepresented.
- **System-wide certification.** Certification is decision-scoped only; no actor certifies the
  system as a whole. (May be intentional — flagged for evaluation, not asserted as a gap.)

### Authority Boundary Violations (backlog ownership conflicting with current architecture)

- **Epic 7** claims to own "Continuity Optimization / Degradation," but **context assembly is owned
  by Execution** (`ExecutionContextService`, fixed preview ordering) and **compression is owned by
  Continuity** (`UnderstandingCompressionService`). An epic that "owns continuity strategy" risks
  seizing two established authorities.
- **Epic 8** proposes to "determine what belongs in Operational Context," but Decision Lifecycle
  **already ratified** the decision↔operational-context boundary (`decisions.0031/0032`). Re-deciding
  decomposition could conflict with a settled boundary.
- **Epic 5** claims "Decision Evolution / Alternative History," which Decision Lifecycle already owns
  at proposal scope. The claim must be narrowed to non-proposal/cross-decision reasoning.

### Architectural Drift (backlog assumptions no longer matching reality)

- **Non-existent source artifact.** The "Session Continuity audit" the backlog repeatedly cites does
  not exist. The real provenance is Epic 05's plan (Future Work / Non-Goals / Rule 7) and Epic 03's
  M8/M9 milestones.
- **Numbering/identity collision.** Backlog Epics 5–9 collide with archive Epics 01–05; Epic 9's
  internal reference to "Epic 5" denotes the *Session Continuity* concept, not backlog Epic 5.
- **Epic 8 over-counts overloads** — "Decision Distillation" was already removed from Operational
  Context by Epic 05.
- **Epic 9 over-claims lost intent** — "Brainstorm" was never designed; Epic 03 compresses research
  *by design*, so the responsibilities were partly engineered away rather than dropped.
- **"Session Continuity" is treated as one owner**, but reality splits Operational Context / Continuity
  (`CommandCenter.Continuity`) from disposable Execution sessions (`CommandCenter.Execution`).

### New Architectural Themes (evaluated, not assumed)

The prompt offered candidate themes; assessed against reality:

- **Decision Lifecycle, Governance, Certification, Execution Projection** — **already implemented**
  in Epic 05. These are *not* future backlog material; they are the new authority context the backlog
  must respect.
- **Operational Context Adoption** — **genuinely emergent and unbuilt** (Epic 05 M10). A legitimate
  future theme.
- **Continuity Fidelity / Continuity Strategy / Reasoning Preservation** — emergent and unbuilt;
  these *are* the backlog's Epics 5/6/7 and are correctly identified, modulo framing.
- **Research Evidence** — emergent but blocked on a non-existent Brainstorm consumer.

---

## 5. Regeneration Guidance

The regenerated backlog should be built from these factual constraints (this section states
*constraints*, not a design):

1. **Correct the premise.** Treat the backlog as a *post-Epic-05 remaining-work* document, not a
   pre-implementation relic. Most of it is aligned; the problem is framing and overlap, not staleness.
2. **Repair provenance.** Re-anchor each epic to real artifacts (Epic 05 plan Future Work / Non-Goals;
   Epic 03 M8/M9). Remove the "Session Continuity audit" citation or replace it with the actual
   sources.
3. **Resolve the numbering collision** between conceptual epics and the 01–05 archive, and disambiguate
   the two distinct "Epic 5" referents.
4. **Collapse the Continuity cluster.** Epics 6, 7, 8(remainder), and 9(analytics) are facets of one
   boundary/measurement question; regeneration should de-duplicate them before re-scoping.
5. **Realign against ratified boundaries.** Narrow Epic 5 to exclude proposal-scoped reasoning already
   owned by Decision Lifecycle; narrow Epic 8 to exclude the decision-distillation overload already
   removed; ensure Epic 7 does not claim Execution's assembly authority or Continuity's compression
   mechanism.
6. **Decide Epic 9's fate honestly.** Its export half cannot be owned until a Brainstorm consumer
   exists; its analytics half overlaps Epics 5/6/7. Determine whether it survives as anything beyond a
   thin "export existing observations" responsibility.
7. **Add the genuinely-missing items** for consideration: Operational Adoption (Epic 05 M10) and the
   Epic 04 backend-owned deferrals (abort, global overview, notifications).

---

## 6. Recommended Epic Disposition Matrix

| Epic | Theme | Built today? | Primary overlap / boundary conflict | Disposition | Justification |
|---|---|---|---|---|---|
| 5 | Reasoning Trajectory Preservation | No (explicitly disclaimed by Epic 05) | Decision Lifecycle proposal lineage owns proposal-scoped reasoning | **Realign** | Core gap real & unowned; narrow scope to non-proposal / cross-decision reasoning so it does not duplicate Decision Lifecycle. |
| 6 | Continuity Fidelity | No (only Document Health exists) | None — clean gap | **Preserve** | Matches reality exactly; transfer-outcome surface is structurally absent. Merge candidate with 7. |
| 7 | Continuity Strategy | No (single hard-coded Full Reconstruction) | Execution owns context assembly; Continuity owns compression | **Preserve** (boundary-guarded) | Responsibility survives, mechanism need not; must not seize Execution/Continuity authority. Merge candidate with 6. |
| 8 | Operational Context Decomposition | Partial — decision-distillation axis already extracted (Epic 05) | Decision↔OpContext boundary already ratified | **Realign** | Scope materially reduced; restate against still-conflated continuity/diagnostics/memory axes; fold into Continuity cluster. |
| 9 | Long-Horizon Research & Brainstorm Evidence | No (never realized; "Brainstorm" absent) | Analytics overlap 5/6/7; export needs a non-existent Brainstorm consumer | **Realign** (Brainstorm-export half → **Retire** pending a consumer) | Premise partly mythologized; de-duplicate analytics; export cannot be owned without a consumer. |

**Disposition legend used:** Retire / Merge / Split / Realign / Preserve.

> This audit describes reality. It deliberately does not propose milestone plans, replacement epics,
> or implementation. Regeneration of the backlog is a separate exercise.
