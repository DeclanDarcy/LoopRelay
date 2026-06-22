# Reasoning Trajectory Preservation — Architecture Audit

> Purpose: capture the information required to create a **Reasoning Trajectory Preservation
> Roadmap** aligned with the post-Epic-05 architecture. This document describes reality. It does
> **not** design solutions, propose milestones, or author the roadmap.
>
> Audit date anchor: 2026-06-22. Architecture state: post-Epic-05 (Decision Lifecycle complete).
> Companion document: `.agents/backlog-audit.md` (audits the Epic 5–9 backlog framing). This audit
> goes one level deeper — into the *code* — to ground the single claim both share:
>
> > Settled conclusions survive. Reasoning does not.

---

## 0. Method & Evidence Base

Findings are grounded in three code inventories with `file:line` citations plus the two
architecture contracts (`docs/architecture.md`, `docs/operational-context-schema.md`) and the
Epic 05 plan (`.agents/archive/epics/05/plan.md`). Subsystem → project map:

| Capability (audit term) | Project | Authority |
|---|---|---|
| Repository Authority | `CommandCenter.Core/Repositories`, `Artifacts` | Filesystem-authoritative artifacts under `.agents` |
| Execution Lifecycle | `CommandCenter.Execution` | Disposable sessions; observation-not-interpretation |
| Operational Context | `CommandCenter.Continuity` (+ `CommandCenter.Middle` generation) | Settled understanding only |
| Decision Lifecycle | `CommandCenter.Decisions` | Decision/candidate/proposal state, options, tradeoffs, lineage |
| Governance | `DecisionGovernanceService` | Advisory; `BlocksExecutionProjection` |
| Certification | `DecisionCertificationService` | Decision-scoped pass/fail |
| Execution Projection | `IDecisionProjectionService.BuildExecutionProjectionAsync` | Governed decisions → constraints/directives |
| Continuity (transfer) | `ContinuityDiagnosticsService` | Document-health observation only |

**Concept-existence checks (grep-confirmed absent under `src/`):** `ReasoningTrajectory`,
`RejectedAlternative`, `Hypothesis` (type), `ArchitecturalExploration`, `TransferOutcome`,
`TransferSuccess`, `Fidelity*`, `ContinuityStrategy`, `ContinuityDegradation`, `ResearchEvidence`.
None exist. The reasoning-trajectory domain is *empty of code*, exactly as Epic 05 declared it would
be (`plan.md:33, 94-96, 782-787, 830`).

---

## Objective 1 — What Is Reasoning Trajectory?

**Definition.** Reasoning Trajectory is the time-ordered, cross-artifact record of *how the project
arrived at its current understanding and decisions* — the path, not the destination. It is distinct
from the two things the architecture already preserves:

- **Operational Context** preserves the *destination of understanding* (settled mental model).
- **Decision Lifecycle** preserves the *destination of decisions* (authoritative outcomes) plus the
  reasoning **internal to a single proposal** as a byproduct of resolving it.

Reasoning Trajectory is the **horizontal** dimension neither owns: reasoning *across* decisions,
*across* time, *across* artifacts, and — critically — reasoning that **never became a decision at
all** (hypotheses entertained, directions explored and abandoned, contradictions encountered and
worked through).

**Concept classification — what belongs in the reasoning domain vs. elsewhere:**

| Concept | Reasoning domain? | Where it lives today |
|---|---|---|
| Hypotheses (proposed, unconfirmed beliefs) | **Yes — unowned** | Nowhere (no `Hypothesis` type) |
| Architectural exploration / dead ends | **Yes — unowned** | Nowhere (compressed away by design, `epics/03/m5`) |
| Rejected alternatives | **Yes — partially owned** | Owned *inside one proposal's revisions*; unowned across decisions / outside proposals |
| Tradeoff evolution | **Yes — partially owned** | `DecisionTradeoffRevision` (proposal-scoped); unowned as cross-decision trajectory |
| Decision evolution | **Split** | Proposal-internal: Decision Lifecycle. Cross-decision "why superseded": **unowned** |
| Constraint evolution | **Yes — partially owned** | Op-context coarse diff (`ConstraintAdded/Removed`); not a trajectory |
| Emerging contradictions | **Yes — partially owned** | Transient warnings + recomputed governance findings; no durable history |
| Strategic direction (long-horizon rationale) | **Yes — unowned** | Survives as *facts* in op-context, not as *trajectory* |
| Settled understanding | **No** | Operational Context (correct owner) |
| Authoritative resolved outcome | **No** | Decision Lifecycle (correct owner) |
| Raw execution streams / transcripts | **No — discard by design** | Execution (capped, ephemeral) |

**The defining test.** Decision Lifecycle answers *"what did we decide, and why this option over the
others on the table at resolution time?"* Reasoning Trajectory answers *"how did our thinking move
over the whole project — what did we believe and abandon, what did we explore and reject, what
contradicted what, and how did the strategy shift?"* The second question has **no owner**.

---

## Objective 2 — Inventory of Existing Reasoning Representations

| Representation | Purpose | Owner | Persistence | Authority | Lifecycle / fidelity |
|---|---|---|---|---|---|
| `DecisionOption` / `…Revision` / `…Comparison` | Capture choices considered | Decisions | `proposal.json` + `revisions/REV-*.json` | Reasoning artifact (not authority) | `PreviousOption` + `RetiredOptions` retained per revision (`DecisionProposalRevision.cs:16-17`) — **proposal-scoped** |
| `DecisionTradeoff` / `…Revision` | Weigh benefit vs cost | Decisions | proposal + revisions | Reasoning artifact | `PreviousTradeoff`/`RevisedTradeoff` pair retained (`DecisionTradeoffRevision.cs:3-8`) — **proposal-scoped** |
| `DecisionAssumption` / `…Revision` | Record beliefs underpinning a proposal | Decisions | proposal + revisions | Reasoning artifact | `PreviousStatement` + `RetiredAssumptions` retained — **proposal-scoped** |
| `DecisionProposalLineage` (+events, revision snapshots) | Evolution of one proposal | Decisions | `proposals/PROP-*/history.json`, `revisions/` | Reasoning artifact | Durable but **single-proposal scoped** (`DecisionProposalLineage.cs:5-15`) |
| `DecisionResolution.SourceProposalSnapshot` | Freeze full proposal at resolution | Decisions | `decision.json` | Authoritative | All options/tradeoffs/assumptions/evidence/revisions embedded (`DecisionResolutionService.cs:106-112`) |
| `DecisionResolutionRationale` (incl. `RecommendationDiverged`) | Why the human chose as they did | Decisions | `decision.json` | Authoritative | Retained per decision (`DecisionResolutionRationale.cs:3-6`) |
| `DecisionRelationship` (`Supersedes`,`ConflictsWith`,`DerivedFrom`,…) | Link decisions | Decisions | `decision.json` | Authoritative | Type + thin `Rationale` string only — **no reasoning carried** (`DecisionRelationship.cs:5-9`) |
| `DecisionGovernanceReport` / `…Finding` | Detect contradictions, gaps | Governance | `governance/governance.<ts>.json` | Advisory | **Durable but recomputed**; contradictions appear as findings, not history (`DecisionGovernanceService.cs:36`) |
| `DecisionCandidate` / `DecisionSignal` | Surface latent decisions | Decisions | `candidates/CAND-*` | Reasoning artifact | Dismiss/expire recorded, but **no dismissal rationale** on the signal (`DecisionSignal.cs:5-10`) |
| `OperationalContextDocument` (StableDecisions, DecisionRationale, OpenQuestions, ActiveRisks, RecentUnderstandingChanges) | Settled understanding | Continuity | `.agents/operational_context.md` | Settled understanding | Current snapshot only; `RecentUnderstandingChanges` is a **12-item sliding window** (`UnderstandingCompressionService.cs:9`) |
| `UnderstandingEvolutionLedger` / `UnderstandingRevisionSnapshot` | Trend understanding over revisions | Continuity | Recomputed from archived artifacts | Observational | Metrics only (bytes, item counts) — **no "why" / `CurrentRevision` is the only durable handle** |
| `ContinuityDiagnostics` / `ContinuityReport` | Measure document health | Continuity | **Recomputed on-demand** | Observational | Revision counts, byte growth, six trends, rework indicators — **document health, not transfer success** |
| `DecisionAssimilationRecommendation` | Bridge resolved decision → op-context | Decisions→Middle | `assimilation/DEC-*` | Recommendation only | Carries outcome + rationale; **rejected alternatives dropped** (`DecisionOperationalContextAssimilationService.cs:101-104`) |
| `ExecutionConstraint` / `ExecutionDirective` / `ExecutionDecisionConflict` | Project governed decisions into execution | Execution Projection | derived | Authoritative-for-execution | Carry only `Statement`+`Classification`+`Sources` — **rationale/options/evidence stripped** |
| Handoff (`handoff.md` + `handoff.NNNN.md`) | Compact result of last slice | Execution | numbered archive | Result | Result text only; **no reasoning metadata** (`HandoffService.cs:95-116`) |
| `ExecutionEvent` stream | Observe provider activity | Execution | session store, capped | Observational | 200 events / 64 KiB cap; provider reasoning **ephemeral** (`ExecutionMonitoringService.cs:279-291`) |

---

## Objective 3 — What Reasoning Information Is Currently Preserved

Classification across execution boundaries, repository restart, artifact rotation, op-context
promotion, and decision-lifecycle transitions:

**Fully Preserved**
- **Proposal-internal alternative history.** Rejected options, retired assumptions, and prior
  tradeoffs survive as durable revision fields and comparisons (`DecisionProposalRevision.cs:16-31`;
  `DecisionProposalRevisionComparison.cs:16-26`). Reconstructable from `.agents/decisions` after
  restart.
- **Resolution-time reasoning freeze.** The complete proposal (all options/tradeoffs/assumptions/
  evidence/revisions) is embedded in the resolved decision, plus `RecommendationDiverged`
  (`DecisionResolutionService.cs:106-112`).
- **Decision rationale as settled understanding.** Architectural/strategic decision rationale is
  carried verbatim into `DecisionRationale` and is *not* dropped during compression
  (`UnderstandingCompressionService.cs:33`).

**Partially Preserved**
- **Decision evolution.** Visible *within* a proposal's lineage; **absent across decisions**. A
  supersede creates a `Supersedes` relationship but does not migrate or link the old reasoning
  (`DecisionResolutionService.cs:203-207`).
- **Contradictions.** Surface as governance findings (durable per report) and as op-context
  assimilation warnings (transient), but neither is a durable contradiction *history* — see Obj 8.
- **Understanding evolution.** Numbered op-context archives survive rotation infinitely, and a ledger
  can trend them — but only as **counts/bytes**, never the reason a belief changed.
- **Constraint/question/risk evolution.** Op-context coarse semantic diff emits `ConstraintAdded/
  Removed`, `QuestionAdded/Removed`, `RiskAdded/Removed` between two revisions; this is a pairwise
  delta, not a trajectory, and is recomputed rather than stored.

**Not Preserved**
- Hypotheses entertained and abandoned (no type, no artifact).
- Architectural exploration and dead ends (compressed to outcomes by design, `epics/03/m5`).
- Rejected alternatives **outside** a single proposal (cross-decision or non-proposal).
- The *reason* one decision superseded another.
- Provider reasoning streams (capped, ephemeral).
- Any cross-decision / cross-project reasoning trajectory.

---

## Objective 4 — What Reasoning Information Is Currently Lost

Precise disappearance points:

- **When a decision resolves →** the *losing* options survive only inside the embedded snapshot; once
  the decision is projected to execution they are stripped to a single `Statement`
  (`DecisionProjectionService.cs:177-192`; `ExecutionConstraint.cs:5-12`). The *why-not* of rejected
  options never reaches the consumer.
- **When a decision is superseded/archived →** only the state flips and a relationship is added
  (`DecisionResolutionService.cs:219-294`). The superseded decision's reasoning is **not linked to
  its replacement**, so "we changed our mind because…" is unrecoverable across the pair.
- **When operational context is promoted →** the prior document is rotated to a numbered archive, but
  the **reason it changed** is reduced to ≤12 `RecentUnderstandingChanges` bullets, then aged out
  (`UnderstandingCompressionService.cs:103-142`). The archive bytes persist; **no service reads them
  back as reasoning** (Epic 03 M8 certifies *current understanding* reconstruction *archive-
  independently* — the archive has no consumer).
- **When artifacts rotate →** `decisions.NNNN.md`, `handoff.NNNN.md`, `operational_context.NNNN.md`
  accumulate with no pruning (`ArtifactRotationService.cs:22-85`) but are **write-only history**:
  preserved as text, consumed by nothing.
- **When an execution session ends →** provider output beyond 200 events / 64 KiB is dropped
  (`ExecutionMonitoringService.cs:279-291`); the handoff records the *result*, not the reasoning that
  produced it (`HandoffService.cs`).
- **When a candidate is dismissed/expired →** the transition is recorded but **the justification for
  dismissal is not** (`DecisionSignal.cs:5-10`) — an abandoned line of inquiry leaves no rationale.

**Net:** every lifecycle transition is a *funnel that keeps the conclusion and discards the path*.
The architecture is, by construction, conclusion-preserving and trajectory-shedding.

---

## Objective 5 — Decision Lifecycle Boundaries

**Reasoning responsibilities Decision Lifecycle already owns (must not be re-claimed):**
- Options, tradeoffs, assumptions, and their **revision history within a proposal**.
- Proposal lineage and revision snapshots (`DecisionProposalLineage`).
- Resolution rationale, selected option, recommendation divergence.
- The authoritative resolved outcome and its frozen proposal snapshot.
- Decision-to-decision *relationships* (the typed edges `Supersedes`/`ConflictsWith`/`DerivedFrom`).

**Reasoning responsibilities that must remain outside Decision Lifecycle** (Rule 7,
`plan.md:94-96`; Non-Goals `plan.md:782-787`):
- Hypotheses, exploration paths, rejected arguments not attached to a proposal.
- Cross-decision reasoning trajectory (how thinking moved across many decisions over time).
- Durable contradiction history independent of a governance run.
- Strategic-direction evolution spanning decisions, plans, and understanding.

**Duplication risks (a future reasoning owner must avoid):**
1. **Re-implementing proposal lineage.** Any "decision evolution visibility" feature must *read*
   `DecisionProposalLineage`, not recreate it. (`backlog-audit.md §3 Epic 5`, §4.)
2. **Re-deciding the assimilation boundary.** Epic 05 ratified "Decision Authority is not equivalent
   to Operational Context Authority" (`decisions.0031.md:12`, `0032.md:18`); resolution must not
   mutate op-context. A reasoning owner must respect this, not relitigate it.
3. **Owning the resolved outcome.** Authority over *what was decided* stays in Decision Lifecycle; a
   reasoning owner may reference decisions but must never become a second decision database
   (`plan.md:80-86`, Rule 4).

---

## Objective 6 — Operational Context Boundaries

**What it should own (and does):** settled understanding only — mental model, architecture, authority
boundaries, constraints, stable decisions, decision rationale, open questions, active risks, recent
understanding changes (`docs/operational-context-schema.md:9-19`).

**Reasoning content currently stored:** `DecisionRationale` (rationale that still affects future
work) and a bounded `RecentUnderstandingChanges` window. This is **settled** reasoning — the residue
that remains *true*, not the path.

**Reasoning content intentionally excluded (correctly):** raw history, execution streams,
conversation logs, complete handoff archives, git history, milestone status, provider transcripts,
session metadata (`schema:21-30`; `architecture.md:78`).

**Reasoning content that should never be stored there:** trajectory itself — abandoned hypotheses,
dead ends, rejected alternatives, contradiction history. Storing these would violate "operational
context is not session memory" (`architecture.md:76`) and re-create the overloading Epic 8 names.
**Finding:** Operational Context is *correctly scoped* for reasoning purposes. It is the home of
*settled* reasoning and must **not** be stretched to hold *trajectory*. The compression "Retire" tier
actively enforces this by shedding superseded tactical detail and raw history
(`schema:181-188`) — i.e., the subsystem is *designed* to destroy trajectory, which is the right
behavior for an understanding store and precisely why trajectory needs a *different* home.

---

## Objective 7 — Reasoning Continuity Across Horizons

| Horizon | What reasoning survives | Fidelity | Structural cause of degradation |
|---|---|---|---|
| **Single execution** | Handoff result; capped event stream; current decisions/op-context | Medium | Provider reasoning ephemeral; handoff is result-only |
| **Milestone** | Resolved decisions + frozen proposal snapshots; op-context current revision; numbered archives on disk | Medium-high *for conclusions*, low *for path* | Funnel transitions keep outcomes, shed alternatives |
| **Project** | Stable decisions + rationale in op-context; decision relationship graph; governance findings (recomputed) | Low for trajectory | No cross-decision evolution; archives unread; contradictions transient |
| **Multi-year** | Only the *current* settled understanding + the authoritative decision set | Trajectory ≈ zero | No trajectory owner; understanding is a rolling snapshot; M8 certifies *current* reconstruction, **not** historical reasoning |

**Where fidelity degrades:** sharply at the milestone→project boundary. Within a milestone, proposal
lineage holds the path. Across milestones, the only inter-decision link is the typed relationship
edge with a thin `Rationale` string — the *narrative* of how the project's thinking evolved is gone.
**Structural cause:** preservation is **vertical** (within a proposal) but the project's reasoning is
**horizontal** (across many decisions and much non-decision exploration), and nothing spans the
horizontal axis.

---

## Objective 8 — Contradiction Handling

| Category | How handled | Persisted / Transient / Discarded |
|---|---|---|
| Contradictory **decisions** (resolved decisions that conflict) | `ConflictsWith` relationship + governance `AnalyzeRelationships` finding (`DecisionGovernanceService.cs:193-209`); projected as `ExecutionDecisionConflict` | **Persisted** as relationship; finding is **durable-but-recomputed** (rebuilt each governance run, not an append-only history) |
| Competing **assumptions** within a proposal | Captured as assumption revisions | **Persisted** (proposal-scoped) |
| Contradictory **decision signals** (discovery) | `DecisionAnalysisService.FindContradictions` text/negation match → warning (`:176-200`) | **Transient** — warning in the current proposal's compression summary; no durable record |
| Conflicting **understanding** (op-context) | Assimilation contradiction → warning, never auto-replacement (`schema:200`) | **Transient** advisory |
| Failed **hypotheses** / invalidated beliefs | — | **Discarded** (no representation) |

**Finding:** the system *detects* contradictions well at a point in time but has **no durable
contradiction ledger**. A contradiction that is surfaced, discussed, and resolved leaves no trace
once the governance report is regenerated or the proposal is resolved — so the same contradiction can
recur with no memory that it was previously worked through. "Emerging contradictions" as a
*preserved trajectory* is unowned.

---

## Objective 9 — Alternative-History Preservation

| Scope | Preserved? | Evidence |
|---|---|---|
| **Proposal-scoped** (alternatives within one proposal) | **Yes — fully** | `RetiredOptions`/`RetiredAssumptions`/`PreviousTradeoffs` + revision comparisons (`DecisionProposalRevision.cs:16-31`) |
| **Cross-decision** (alternatives weighed across related decisions) | **No** | Only typed relationship edges; no shared alternative space; supersede carries no reasoning (`DecisionResolutionService.cs:203-258`) |
| **Cross-project / non-proposal** (alternatives explored without ever forming a proposal) | **No** | No `RejectedAlternative`/`ArchitecturalExploration` type; exploration compressed to outcomes by design (`epics/03/m5`) |
| **Alternatives revisited** (re-opening a previously rejected path) | **No** | Candidate rediscovery creates a *fresh* candidate with no linkage to the prior rejection (`plan.md:352`) |

**Finding:** alternative history exists in exactly one place — inside a single proposal's revision
chain. The moment an alternative lives *between* proposals/decisions, or never reached proposal
status, it is unpreserved. "Alternatives revisited" is structurally impossible to surface because
rediscovery is memoryless.

---

## Objective 10 — Strategic-Direction Preservation

Direction survives as **facts and settled understanding, never as reasoning trajectory:**

- **Project direction** → distilled into op-context *Architecture* / *Stable Decisions* as current
  statements (facts). The *evolution* of direction is not recorded.
- **Architectural direction** → architectural decisions + their rationale in `DecisionRationale`
  (understanding). How the architecture's direction *shifted* over the project is absent.
- **Decision direction** → the resolved decision set (facts) + proposal-internal lineage (reasoning,
  but bounded to each proposal).
- **Constraint direction** → constraints as current facts; coarse add/remove diffs between two
  revisions; no longitudinal constraint trajectory.

**Mapping to Facts / Understanding / Reasoning:**
- **Facts:** strongly preserved (resolved decisions, current constraints, stable decisions).
- **Understanding:** preserved as a *rolling current snapshot* (op-context).
- **Reasoning (trajectory):** **not preserved** at strategic scope. Strategic *direction* is legible;
  strategic *reasoning over time* — why the direction is what it is and how it moved — is not.

---

## Objective 11 — Long-Horizon Reasoning Requirements

The original Decision Engine roadmap tried to serve long-horizon reasoning via **decision sessions /
session reuse / session routing**. The architecture **rejected the mechanism** (`architecture.md:41`;
`plan.md` Non-Goals `:782-789`; backlog Epic 7 concedes *"the responsibility survives; the mechanism
does not need to"*, `backlog.md:151-153`).

**The responsibility remains.** Rejecting sessions removed an *implementation*, not the need.

- **Long-horizon reasoning requirements (unmet):** ability to answer, months later, *why* a direction
  was taken, what alternatives were rejected and on what grounds, which hypotheses failed, and how a
  contradiction was previously resolved — without re-deriving it from scratch.
- **Long-horizon reasoning gaps:** no trajectory artifact; no cross-decision evolution; no durable
  contradiction/hypothesis history; archives are write-only; understanding is a rolling snapshot that
  *certifies current-state reconstruction, not historical reasoning* (Epic 03 M8).
- **Long-horizon reasoning risks:** recurrence of settled debates (no contradiction memory);
  silent reversal of rejected alternatives (no revisit linkage); strategy drift invisible because
  only the current direction is legible; "long-horizon" today exists **only** as certification scale
  fixtures (50/100/200-decision), i.e., as a *scalability* property, not a *reasoning* property.

---

## Objective 12 — Ownership Gaps

| Reasoning responsibility | Owned / Partially / Unowned | Owner / note |
|---|---|---|
| Hypothesis tracking | **Unowned** | No type, no artifact |
| Alternative preservation — *proposal-scoped* | **Owned** | Decision Lifecycle |
| Alternative preservation — *cross-decision / non-proposal* | **Unowned** | — |
| Architectural exploration / dead ends | **Unowned** | Compressed to outcomes by design |
| Decision evolution — *within a proposal* | **Owned** | Decision Lifecycle (lineage) |
| Decision evolution — *across decisions ("why superseded")* | **Unowned** | Only a typed edge exists |
| Tradeoff/constraint evolution as trajectory | **Partially** | Proposal-scoped + coarse op-context diff; no longitudinal owner |
| Contradiction preservation (durable history) | **Partially → effectively Unowned** | Detection exists; durable ledger does not |
| Strategic-direction trajectory | **Unowned** | Direction-as-fact only |
| Cross-decision rationale | **Unowned** | Thin relationship `Rationale` string only |
| Reasoning evolution (project-wide) | **Unowned** | The core gap |

**Unowned responsibilities (consolidated):** hypothesis tracking; cross-decision & non-proposal
alternative preservation; architectural-exploration preservation; cross-decision decision evolution;
durable contradiction history; strategic-direction trajectory; cross-decision rationale; project-wide
reasoning evolution.

---

## Cross-Epic Analysis — Where Reasoning Responsibilities Belong

Relationships among the existing owners and where the unowned surface naturally sits:

- **Operational Context ↔ Reasoning.** Op-context owns *settled* reasoning; it must **not** absorb
  trajectory (would re-create overloading, Epic 8). Boundary is already ratified and healthy — a
  reasoning owner sits *beside* it, feeding *from* trajectory *into* candidate understanding, never
  the reverse.
- **Decision Lifecycle ↔ Reasoning.** Decision Lifecycle owns proposal-internal reasoning and the
  authoritative outcome. A reasoning owner sits **above** it: it must *reference* decisions/lineage,
  consume the `Supersedes`/`ConflictsWith` edges, and **never** duplicate lineage or re-own outcomes.
- **Governance ↔ Reasoning.** Governance *detects* contradictions transiently per run. A durable
  contradiction history is the reasoning owner's; governance would *emit into* it rather than be it.
- **Certification ↔ Reasoning.** Certification is decision-scoped (recovery integrity). It does not
  certify reasoning survival; "long-horizon" there is a scale property only.
- **Execution Projection ↔ Reasoning.** Projection deliberately strips reasoning to directives —
  correct for execution. The stripped reasoning is exactly what a reasoning owner should retain
  upstream so it is not lost system-wide.
- **Continuity ↔ Reasoning.** Continuity measures *document health* (that understanding changed), not
  *why*. The "why understanding changed" signal is a reasoning-evolution input that currently has no
  durable home.

**Natural placement (descriptive, not prescriptive):** the unowned surface clusters into a single
*horizontal, cross-artifact, append-mostly* concern that sits **above Decision Lifecycle and beside
Operational Context**, reading from both plus Governance/Execution-projection, owning none of their
authorities. Boundaries that **must be preserved**: repository-as-authority; human-authoritative
resolution; disposable execution; op-context = settled understanding only; decision authority ≠
op-context authority; proposal lineage stays in Decision Lifecycle; Execution owns context assembly;
Continuity owns compression.

---

## Roadmap Inputs (findings only — no solutions, plans, or milestones)

**Reasoning capability gaps**
- No hypothesis representation or lifecycle.
- No cross-decision / non-proposal alternative preservation.
- No architectural-exploration / dead-end preservation.
- No cross-decision decision-evolution ("why superseded") record.
- No durable contradiction history.
- No strategic-direction trajectory.
- No project-wide reasoning-evolution artifact; archives are write-only and unread.

**Ownership gaps** — see Objective 12. The unowned cluster is coherent enough to constitute one
domain, distinct from all five existing capabilities.

**Preservation gaps** — every lifecycle transition (resolve, supersede, archive, promote, rotate,
session-end, candidate-dismiss) is conclusion-preserving and trajectory-shedding (Obj 4).

**Continuity gaps** — fidelity collapses at the milestone→project boundary; multi-year trajectory
fidelity ≈ zero (Obj 7).

**Boundary constraints (must be respected by any roadmap)**
- Do not duplicate `DecisionProposalLineage` or proposal-scoped option/tradeoff/assumption revisions.
- Do not re-own resolved-decision authority or become a second decision database.
- Do not stretch Operational Context to hold trajectory (Epic 8 overloading hazard).
- Do not seize Execution's context-assembly authority or Continuity's compression mechanism.
- Do not relitigate the ratified decision↔op-context assimilation boundary.
- Preserve filesystem-as-authority and human-authoritative resolution.

**Architectural risks**
- A reasoning owner that reads everything risks *becoming* session memory — the rejected mechanism in
  new clothing. The responsibility must survive without resurrecting session reuse/routing.
- Trajectory data is inherently unbounded; without explicit retention/compaction policy it conflicts
  with the no-unbounded-growth posture — but compaction must not become the same trajectory-shedding
  funnel it exists to fix.
- Citation/provenance discipline: the prior backlog leaned on a non-existent "Session Continuity
  audit" (`backlog-audit.md §4`). Roadmap provenance should cite real artifacts.

**High-leverage opportunities (lowest duplication risk)**
- The reasoning *raw material already exists* but is unread: numbered archives, proposal lineage,
  governance findings, op-context recent-changes, decision relationships. The gap is a *durable,
  cross-artifact, queryable trajectory layer over existing data*, not new capture at every site.
- Two transitions leak the most recoverable reasoning cheaply: **supersede/archive** (carries no
  "why") and **candidate dismissal** (carries no rationale). These are concentrated, high-signal
  loss points.

---

## Success Criteria — Direct Answers

1. **What is reasoning trajectory?** The cross-artifact, time-ordered *path* to current understanding
   and decisions — including reasoning that never became a decision — distinct from settled
   understanding (Op-Context) and authoritative outcomes (Decision Lifecycle). (Obj 1)
2. **What survives today?** Proposal-internal alternative history; resolution-time proposal
   snapshots; settled decision rationale; the resolved decision set; numbered archives on disk
   (unread); recomputed document-health diagnostics. (Obj 3)
3. **What is lost?** Hypotheses, exploration/dead ends, cross-decision & non-proposal alternatives,
   "why superseded," durable contradiction history, strategic-direction trajectory, provider
   reasoning. (Obj 4)
4. **Which reasoning responsibilities are owned?** Proposal-scoped options/tradeoffs/assumptions +
   revisions + lineage; resolution rationale; decision relationships — all in Decision Lifecycle.
   Settled reasoning in Operational Context. (Obj 5, 12)
5. **Which are unowned?** Hypothesis tracking; cross-decision/non-proposal alternatives; architectural
   exploration; cross-decision evolution; durable contradiction history; strategic-direction
   trajectory; cross-decision rationale; project-wide reasoning evolution. (Obj 12)
6. **What boundaries must be preserved?** Repository authority; human resolution; disposable
   execution; Op-Context = settled understanding only; decision authority ≠ op-context authority;
   proposal lineage stays in Decisions; Execution owns assembly; Continuity owns compression.
   (Cross-Epic, Roadmap Inputs)
7. **What belongs to Decision Lifecycle?** Proposal-internal reasoning, lineage, resolution rationale,
   authoritative outcomes, typed decision relationships. (Obj 5)
8. **What belongs outside Decision Lifecycle?** Hypotheses, non-proposal exploration, cross-decision
   trajectory, durable contradiction history, strategic-direction evolution. (Obj 5)
9. **What long-horizon capabilities are still missing?** Recall of *why* directions/decisions/
   rejections happened months/years later without re-derivation; contradiction memory; revisit
   linkage; visible strategy drift. The responsibility survives the rejected session mechanism.
   (Obj 11)
10. **What should inform the roadmap?** The unowned cluster forms one coherent horizontal domain above
    Decision Lifecycle and beside Operational Context; its raw material already exists but is unread;
    the highest-leverage, lowest-duplication targets are a queryable trajectory layer over existing
    artifacts plus the supersede/archive and candidate-dismissal leak points — all within the
    boundary constraints above. (Cross-Epic, Roadmap Inputs)

> This audit describes reality. It deliberately does not propose a trajectory model, artifacts,
> workflows, milestones, or implementation. Roadmap generation is a separate exercise.
