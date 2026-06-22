# Audit Findings — Post-Epic 3 Continuity Architecture

Status: findings only. No solutions, implementation plans, or technology proposals.
Scope evidence: `docs/architecture.md`, `docs/operational-context-schema.md`, `.agents/archive/epics/01–03/` (plans, milestones, decisions, handoffs), and backend code under `src/CommandCenter.Backend/` plus `tests/`.

---

## Executive Summary

The post-Epic 3 architecture is internally consistent and was built deliberately. Its continuity model rests on two explicit principles: **Disposable Execution Sessions** (Epic 2) and **Continuity Is Artifact-Mediated** (Epic 3). Truth lives in repository files; understanding lives in `.agents/operational_context.md`; every execution session is a fresh worker that reconstructs its context from disk and emits a single handoff snapshot before terminating.

The audit's central hypothesis is **confirmed**: the architecture has collapsed several distinct continuity concerns into one mechanism. Concretely:

1. **All continuity paths reduce to *reconstruction*.** Preservation exists only for human-promoted settled understanding; acceleration (warm-start / cache / affinity / reattach) does not exist at all.
2. **Reasoning trajectory has no representation anywhere** — not in artifacts, not in session state, not in the document model. Only *settled* understanding and *final* snapshots survive an execution boundary.
3. **`OperationalContextDocument` is an overloaded abstraction** carrying four orthogonal concerns through one canonical model.
4. **Continuity optimization (cost/fidelity) has no first-class owner.** It is structurally homeless.
5. The removed session concepts (Decision Session, Session Router, Session Reuse, Continuity Session, Long-Horizon Session State) were **rejected-never-built**, not deleted. The genuine loss is not those mechanisms but the *concern* they implicitly carried — continuity *strategy* — which left with them and was never re-homed.

A key correction to the charter's premises: the **OpenAI / provider-cache findings are external premises with no counterpart in the project's records.** The architecture never relied on provider cache reuse, so those findings do not invalidate anything currently built; they would only become load-bearing if a continuity-acceleration capability were ever introduced.

---

## Objective 1 — Are facts, understanding, and reasoning trajectory preserved as separate categories?

The three categories exist conceptually but are treated very differently, and one is entirely absent.

| Category | Where it lives | Survival mode |
|---|---|---|
| **Facts** (git state, artifact contents, lifecycle metadata) | Git snapshot rebuilt per launch (`ExecutionContextService.cs:61`); artifacts on disk; session lifecycle facts (SHA, push, timestamps, `DecisionNote`) in `execution-sessions.json` (`ExecutionSession.cs:47-71`) | Mostly **reconstructed**; lifecycle facts **persisted** |
| **Understanding** (mental model, architecture, stable decisions) | `.agents/operational_context.md` → `OperationalContextDocument` (`OperationalContextDocument.cs:7-25`) | **Preserved**, but written *only* via human promotion (`OperationalContextLifecycleService.PromoteAsync`, reachable only through the POST `/promote` endpoint) |
| **Reasoning trajectory** (active hypotheses, in-flight tradeoffs, leanings) | — | **Absent entirely** |

- Facts are largely **re-derived**, not carried: the git snapshot is taken fresh at context-build time and never reused. The only inter-session linkage is `PreviousHandoffContent`, copied solely to support archival diffing (`ExecutionSessionService.cs:176-181`, `HandoffService.cs:118-122`).
- Understanding **survives only if a human promotes it.** Generation synthesizes from already-persisted artifacts; nothing settles automatically.
- Reasoning trajectory is **never represented.** A grep for `hypothes|tradeoff|leaning|rejected alternative|in-flight` across `src` returns nothing.

**Finding:** The architecture preserves *facts* and *settled understanding* as separable categories but provides **no category for reasoning trajectory**. It cannot distinguish "what we have decided" from "what we are mid-way through deciding."

---

## Objective 2 — Is OperationalContext overloaded?

Yes. The schema/docs assign **all four** of the following to the single operational-context artifact:

- **(a) Project understanding** — `operational-context-schema.md:7` "represents current project understanding"; fields `CurrentMentalModel`, `Architecture`.
- **(b) Continuity substrate** — `architecture.md:76` "Operational context is not session memory… the repository filesystem remains authoritative"; it is the thing carrying knowledge across disposable sessions.
- **(c) Decision history** — fields `StableDecisions` / `DecisionRationale`; a whole "Decision Assimilation" pathway (`operational-context-schema.md:190-202`).
- **(d) Trajectory carrier** — fields `OpenQuestions`, `ActiveRisks`, `RecentUnderstandingChanges`.

One `OperationalContextDocument` class with nine flat section fields is declared canonical for **~8 operations at once**: "generation, review, semantic diff, compression, decision assimilation, projection, diagnostics, and reporting" (`operational-context-schema.md:62`).

**The conflation is never acknowledged.** The project argues for distinctness, but only between *sibling* artifacts (Plan / Milestone / Handoff / Decisions vs. Operational Context). The multiplicity of concerns *inside* operational context is unexamined. The terms "conflate" / "overload" appear zero times in the corpus.

**Finding:** Responsibilities (a)–(d) are genuinely combined and should be treated as a candidate seam. The tension is sharpest in `RecentUnderstandingChanges`, which injects inherently *transient* content into an artifact defined as "compact, durable" (`operational-context-schema.md:7`), and is itself the prime "Summarize" target of compression (`:185`).

---

## Objective 3 — Did removing SessionRouter eliminate more than session reuse?

Session Router was **rejected-never-built** — it appears in the records only as a prohibition under "Single Workflow Authority" (`epics/03/plan.md`). So no *implemented* routing logic was deleted.

However, the audit's deeper question stands: removing the *abstraction* removed the *only place a continuity-strategy concern could have lived.* With one active session per repository and a flat non-goal on routing, the following have **no owner anywhere in the architecture**:

- continuity strategy *selection* (choosing how to carry understanding for a given situation),
- session-affinity opportunities,
- graceful-degradation *paths* (the system has exactly one path),
- cost optimization.

**Finding:** SessionRouter's removal did not remove a built feature; it removed the *seam* where continuity strategy would naturally be selected. The concern was not relocated — it was dissolved.

---

## Objective 4 — Preservation vs. acceleration vs. reconstruction

| Mode | Status in code |
|---|---|
| **Preservation** | Partial — only human-promoted settled understanding; active sessions hold no reasoning to preserve |
| **Acceleration** (warm-start / cache / affinity / reattach) | **Does not exist.** `CodexExecutionProvider.SupportsReattach => false`; `TryReattachAsync` always returns false (`CodexExecutionProvider.cs:13,59-64`) |
| **Reconstruction** | The only real mode — every launch rebuilds context from disk (`ExecutionContextService.cs:23-81`) |

**Finding:** **All continuity paths collapse into reconstruction.** This is confirmed, not merely suspected. The three modes are not separate concepts in the architecture; two of the three are effectively null.

---

## Objective 5 — Does active reasoning survive execution boundaries?

No. For every item the charter enumerates, the survival representation is **absent**:

- active hypotheses — none
- unresolved tradeoffs — none
- rejected alternatives — none
- active risks **as evolving state** — `ActiveRisks` exists as a flat list but the generator **copies it through unchanged** (`OperationalContextGenerationService.cs:197`); execution never adds risks
- evolving rationale — `DecisionRationale` records rationale only for *settled* `StableDecisions`
- strategic direction — none
- emerging contradictions — only a **transient warning string** from a static negation-matching heuristic (`DecisionAnalysisService.cs:176-197`), never persisted as state

`OpenQuestions` / `ActiveRisks` are **not updated by execution at all**; they change only via the human generate→edit→accept→promote loop. An execution session emits **only a final handoff snapshot**; it has no durable channel for in-flight reasoning.

**Resulting failure modes (observed, not hypothesized):**
- Each new session re-derives reasoning that a prior session had already worked through but never wrote down → repeated rediscovery.
- Rejected alternatives are silently re-litigated, because nothing records that they were considered and dismissed.
- A contradiction noticed mid-execution evaporates at termination unless it happens to land in the final handoff prose.

---

## Objective 6 — State authority vs. continuity management vs. continuity optimization

- **State authority (who owns truth):** clearly owned by the **repository filesystem** (`architecture.md:5,43`; `epics/03/plan.md` "Repository remains authoritative"). The phrase isn't used, but the concept is unambiguous.
- **Continuity management (how understanding survives):** clearly owned by **operational context + the generate/review/promote loop** ("Review Before Mutation").
- **Continuity optimization (how cost/fidelity improve):** **no first-class owner.** "fidelity," "degradation," "optimization," "continuity strategy" return zero matches corpus-wide. Compression (M5) is the nearest candidate but is explicitly framed as **size-bounding / preservation-safety hygiene**, not cost/fidelity optimization ("Compression must preserve the operational model rather than create a smaller narrative"; "compression must never reduce authority"). M9 instrumentation is explicitly **forbidden from governing** ("Do not introduce a continuity score… or instrumentation-driven governance workflow").

**Finding:** Two of the three concerns are cleanly owned; the third (**optimization**) is structurally homeless and was deliberately fenced off from the two components that could have grown into it.

---

## Session Reuse Assessment

Evaluated on the charter's axes, against the records:

- **Value when available:** not documented. Session reuse is named only as continuity/memory and uniformly excluded; no record attributes positive value (token, cost, latency, or cache savings) to it.
- **Cost when unavailable:** not documented as a cost. The design asserts disposability has no downside it cares about ("every execution is new").
- **Fallback / graceful degradation:** designed only for **process reattach** (a crash-recovery capability contract, `SupportsReattach` / `TryReattachAsync`), **not** for session/continuity reuse.
- **Did rejection require perfect reliability before admitting the capability?** **No — and this corrects a charter assumption.** The session-reuse rejection is **categorical/architectural** (to preserve "Single Workflow Authority"), not reliability-graded. The *only* documented "guarantee-it-or-fail-explicitly" reasoning in the entire corpus concerns **live-process reattach** ("Do not implement real Codex reattach unless it can be guaranteed"), which is a different concept from provider cache or continuity reuse.
- **Would graceful-fallback have sufficed?** Unanswerable from records — the project never evaluated session/continuity reuse on a value/cost/reliability axis at all, so there was no reliability bar for a fallback to clear.

**Finding:** The charter's framing ("prior discussions incorrectly required perfect reliability before admitting a capability") does **not** match the record for session reuse. Reuse was rejected for *simplicity and authority*, never for *unreliability*. The perfect-reliability-or-reject pattern is real, but it lives only in the reattach decision.

---

## OpenAI Routing and Cache Findings

All seven charter findings were checked against the records. Result: **zero** matches across epics 01–03 and `docs/` for `prompt_cache_key`, `affinity`, `cache reuse`, `cache locality`, `routing locality`, `probabilistic`, `cross-session`, `provider cache`, or `OpenAI`. The only "cache" concept in the architecture is an **in-memory performance cache, rebuildable from filesystem** (`architecture.md:11`). All "routing" references mean *session routing* (a workflow non-goal), never network/cache routing.

| Charter finding | Status vs. records |
|---|---|
| No reliable cross-session cache-affinity guarantees | External premise, not in records |
| Prompt equivalence necessary but not sufficient | External premise, not in records |
| Routing locality ≠ cache locality | External premise, not in records |
| `prompt_cache_key` improves probability, not guarantee | External premise, not in records |
| Conversations give context continuity, not cache affinity | External premise, not in records |
| Cross-session cache reuse is probabilistic | External premise, not in records |
| Cache-reliant architectures need fallback paths | External premise (analogous reasoning exists only for *process reattach*) |

**Assessment:** These findings **do not invalidate any continuity-optimization concept in the current architecture, because the architecture contains none.** It relies wholly on deterministic reconstruction, which is *immune* to provider-cache unreliability. The findings become relevant **only** as constraints on a *future* acceleration capability — i.e., they argue that any such capability must be probabilistic-by-design with a reconstruction fallback. They are forward-looking constraints, not retroactive criticisms.

---

## Lost Capability Analysis

| Concept | Verdict | Original intent | Why excluded | Replacement | Equivalent? |
|---|---|---|---|---|---|
| Decision Session | Rejected-never-built | Deferred in Epic 1 as future decision generation/management | Decisions are artifacts, not session state | `decisions/*.md` + M6 decision-continuity | Yes — nothing was lost |
| Session Router | Rejected-never-built | (only ever a prohibition) route work across sessions | One-active-session-per-repo makes routing moot | single-session model + projections | Yes mechanically; **but the strategy concern was not re-homed** |
| Session Reuse as continuity | Rejected-never-built | in-session memory carry-over | "must not be reused, resumed as continuity carriers, or treated as project memory" | reconstruction from artifacts | **Only if artifacts capture everything** — the one genuine tradeoff |
| Continuity Session | Rejected-never-built | long-lived session holding understanding | "Continuity Is Artifact-Mediated" | operational-context artifact + review loop | Yes — replacement is the whole of Epic 3 |
| Long-Horizon Session State | Rejected-never-built (concern survives, mechanism doesn't) | persistent state across many cycles | conflicts with disposable-session + artifact authority | understanding compression (M5) + long-horizon certification (M8) | Outcome plausibly met via artifacts; mechanism genuinely gone |

**The single genuine capability tradeoff:** loss of **in-session / provider memory carry-over** — consciously exchanged for auditable, human-reviewable, repository-owned artifacts. Everything else was a deferral that was canceled, not a feature deleted.

---

## Continuity Strategy Gap Analysis

A first-class concept for **continuity strategy / fidelity / degradation / optimization** is **confirmed absent** — these terms appear nowhere in the corpus.

Consequences:
- The system has exactly one continuity behavior (reconstruction) and therefore no way to *choose*, *degrade gracefully*, or *trade fidelity for cost*.
- Compression is the only force acting on continuity cost, and it is deliberately scoped away from optimization toward preservation-safety.
- Instrumentation (M9) can *observe* continuity but is explicitly forbidden from *acting* on it.

Architectural seams where the gap surfaces (characterized, not prescribed):
- **The execution boundary** — today the handoff snapshot is the *only* natural capture point for anything a session learned; it is a lossy, settled-only channel.
- **The operational-context document** — currently the only continuity carrier, but it conflates authoring, carrying, and (would-be) optimizing roles in one model.
- **The recovery path** — restores session lifecycle metadata only, never reasoning or understanding.

Candidate abstractions implied by the evidence (named as seams, not designs): a *trajectory-bearing representation* distinct from settled understanding; a *continuity-strategy owner* distinct from state authority and from understanding-authoring; an explicit *degradation contract* analogous to the existing reattach capability contract.

---

## Roadmap Inputs

**Verified observations**
- All continuity collapses into reconstruction; acceleration and trajectory-preservation do not exist (Obj. 4, 5).
- `OperationalContextDocument` is canonical for ~8 operations and carries 4 orthogonal concerns (Obj. 2).
- Continuity optimization has no owner; compression and instrumentation are fenced away from it (Obj. 6).
- The removed session concepts were never built; the genuine loss is the *strategy concern*, not the mechanisms (Lost Capability Analysis).
- The charter's cache/routing findings are external to the project's records and are orthogonal to the current (reconstruction-only) design.

**Architectural tensions**
- Transient trajectory content (`RecentUnderstandingChanges`) inside a "compact, durable" artifact.
- One model coupling authoring, carrying, and optimization roles.
- Decision-history overlap policed only at the artifact boundary, never recognized as a second concern *inside* operational context.
- "Settled-only" survival vs. the reality that valuable continuity is often in-flight.

**Unresolved questions**
1. Should reasoning trajectory be a first-class state category, or is its loss an acceptable, permanent tradeoff?
2. Is reconstruction-equivalence true for *understanding*, given that understanding is only ever human-promoted (i.e., is anything lost when a human declines to promote)?
3. Should continuity optimization exist at all, or is determinism-over-cost a deliberate permanent stance?
4. If acceleration is ever introduced, where does the reconstruction fallback live, and what is its degradation contract?

**Capability gaps**
- No trajectory representation surviving execution boundaries.
- No continuity-strategy / degradation / optimization owner.
- No acceleration path; recovery restores lifecycle metadata only.

**High-leverage opportunities (concerns to assign, not solutions to build)**
- Separate "settled understanding" from "in-flight reasoning" as distinct categories.
- Give continuity *strategy* a home now that its former host (SessionRouter) is gone.
- Decouple the operational-context model's authoring / carrying / optimizing roles.

---

## Success Criteria — Direct Answers

1. **Intentionally removed continuity capabilities:** None were built-then-deleted. Intentionally *excluded*: session reuse / in-session memory carry-over, decision sessions, continuity sessions, session routing — all categorically, to preserve Single Workflow Authority and artifact authority.
2. **Unintentionally removed:** Strictly none (nothing was built to remove). But *collaterally dissolved*: the **continuity-strategy concern** (strategy selection, graceful degradation, optimization) that had no home once the session abstractions were rejected; and any path toward **reasoning-trajectory preservation**.
3. **Never replaced:** Reasoning-trajectory preservation; continuity acceleration; ownership of continuity optimization; continuity strategy/degradation as first-class concepts.
4. **Assumptions that drove the decisions:** (a) artifacts can capture everything continuity needs; (b) settled-snapshot understanding is sufficient and trajectory is disposable; (c) reconstruction is equivalent to preservation; (d) live/session state is volatile and non-auditable, so it must not be canonical; (e) workflow simplicity outweighs continuity optimization; (f) human-mediated promotion is the correct and sole gate.
5. **Assumptions still valid:** (d) is sound — sessions are a poor home for canonical truth; artifact authority gives real auditability and deterministic recovery. Reconstruction genuinely works for *facts* and *settled understanding*.
6. **Assumptions falsified or unsupported:** (c) is false for reasoning trajectory — trajectory that lived only in session memory is unrecoverable by reconstruction because it was never written; (a) is falsified for in-flight reasoning, which has no artifact slot; (e) is an unexamined bet, not a validated finding. The charter's provider-cache worries are *moot* for the current design (it depends on reconstruction, not cache).
7. **Missing continuity-preservation invariant:** *No invariant guarantees that reasoning trajectory survives an execution boundary, nor distinguishes settled understanding from in-flight reasoning.* Everything that survives is settled-by-human-promotion; everything in-flight dies at session termination.
8. **Who should own that invariant in the future (responsibility/seam, not implementation):** A **continuity-strategy responsibility distinct from both state authority and understanding-authoring** — one that owns (i) capture of in-flight reasoning at the execution boundary, (ii) selection among preserve / accelerate / reconstruct modes, and (iii) an explicit degradation contract. The natural seams are the **execution boundary** (only current capture point) and a **trajectory-bearing artifact separate from the settled operational-context document**.
