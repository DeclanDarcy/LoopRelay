# audit-03.md — Roadmap Minimization, Prioritization & Sequencing

> The pre-regeneration audit. Not archaeology, not capability discovery, not compliance — those are done
> (audit-01, audit-02). This audit decides **what is the highest-leverage, lowest-regret, architecturally-correct
> thing to build next**, and how small the next roadmap can be.
> Reality wins. Repository is authoritative; `roadmap.md` is intent; `audit-01.md`/`audit-02.md` are evidence.
> Generated 2026-06-24 against branch `dev` @ `050f76d`. Current-reality claims re-verified at this HEAD.

---

# Executive Summary

The repository contains **two finished-or-nearly-finished engines that no human can see**, and **one
speculative frontier that should not be built yet**. The next roadmap's job is almost entirely *surfacing*,
not *building*.

1. **The Workflow engine is ~95% complete and certified, and 100% dark.** 15 services, 26/27 HTTP endpoints,
   recovery, idempotency, certification — and **zero live consumers**. Re-verified: `0` workflow references in
   `main.rs` (0/27 bridge commands), no `features/workflow/` UI folder, no `workflow` tab, no
   `RepositoryWorkflowSummary`. The single highest-*leverage* act in the system is a **transport milestone**
   that converts a 0%-reachable certified engine into a usable product.

2. **Session economics is the cheapest real win available, and cheaper than audit-02 thought.** Re-verified:
   `ExecutionSession` *and* `ExecutionSessionSummary` already carry `LastActivityAt`. So "activity" has a backing
   field, "age" is derivable from existing timestamps, and the lifecycle→Middle→execution-UI pipe already reaches
   the screen. Only **token-usage capture and a cost figure** are genuinely net-new. This is the one frontier that
   reaches a human **today with no new transport** and **zero architectural risk** — and it is the stated
   objective of the current roadmap.

3. **Routing / transfer / admission authority must not be built next.** It is ~90% net-new, premised on a
   *mythical* recovered baseline, historically a *declared non-goal* (epics 03/06), and it **collides with
   Execution's one-active-session guard**. It is not a projection; it is a cross-domain authority change. It
   belongs behind an explicit Execution decision gate, not in the next roadmap.

**What should happen next:** ship a **short Hybrid Surfacing roadmap** in leverage order —
(M0) Session Economics observability *(free-ish, zero-risk, reaches UI today)* →
(M1) Workflow Transport *(the critical-path unlock for the dark engine)* →
(M2) Workflow UI Workspace →
(M3) one shared `RepositoryProjectionService` summary carrying **both** workflow + economics aggregates.
Everything routing/transfer/governance is **deferred behind a single Execution session-admission decision gate**.
Four milestones. No re-planning of M0–M9 of either prior roadmap. No new diagnostics, report, certification, or
recovery vocabulary — all already exist and are reused.

---

# Frontier Analysis

## Frontier 1 — Session Economics Observability

### Current State
Lifecycle observability is shipped end-to-end (`IExecutionSessionService` queries →
`RepositoryProjectionService` → `ExecutionSessionPanel`/`ExecutionHistoryPanel`). `LastActivityAt` exists on the
model *and* summary. **Token usage, cost, and surfaced age/activity** are absent.

### Remaining Work
Capture token usage at session boundaries (Execution owns the provider seam); compute a cost figure; surface
age/activity/usage/cost through the **already-wired** read-model pipe.

### User Value
**High** — converts sessions from lifecycle objects into *economic* objects; directly satisfies the current
roadmap's true objective ("throughput economics for workflow replacement").

### Architectural Risk
**Very Low** — read-model only; Execution stays the session authority; no new transport (this pipe already
reaches the UI, unlike the workflow API).

### Dependencies
None new. `LastActivityAt` and timestamps already exist; provider-invocation seam already owned by Execution.

### Urgency
**High** (cheapest path to real, visible value; nothing blocks it).

---

## Frontier 2 — Workflow Transport

### Current State
**0/27 bridge commands.** `api/tauri.ts` is `invoke`-only with no HTTP-direct path, so the workflow API is
**unreachable from a production build** regardless of UI work.

### Remaining Work
Either implement the Tauri bridge workflow commands in `main.rs`, or add an HTTP-direct path in `api/`. This is
the one genuinely net-new infrastructure piece in the surfacing program.

### User Value
**High (indirect, enormous leverage)** — by itself shows the user nothing, but it unlocks 26 already-built
endpoints and ~95% of a certified engine. Highest leverage-per-unit-work in the system.

### Architectural Risk
**Low-Medium** — mechanical transport plumbing, but it is real net-new code and a sequencing prerequisite for
all workflow UI.

### Dependencies
None upstream. **Blocks** Frontier 3 entirely.

### Urgency
**High** — it is the critical path to releasing the largest stranded asset in the codebase.

---

## Frontier 3 — Workflow UI Workspace

### Current State
~10%: workflow appears only as an execution-shaped `WorkflowRail`/`GitWorkflowPanel` derived from *execution*
data, not the workflow projection API. No `features/workflow/`, no `workflow` tab, no types/api/hooks.

### Remaining Work
A dedicated workspace: tab + `types/workflow.ts` + `api/workflow.ts` + `useWorkflow*` hooks + panels for the
*actual* backend surface (8-stage timeline, split Review/Promotion gates, continuation/preparation history with
Allowed/Refused/Skipped/Duplicate, health dimensions, influence trace, certification findings), wired into
`shellState`/`WorkspaceTabs`/`navigation`/`CommandPalette`.

### User Value
**High** — this is the human-facing payoff that makes the workflow engine a product.

### Architectural Risk
**Low** — read-only consumption of a stable API; the risk is *design* (don't restore the obsolete 10-panel plan;
design around what the backend actually exposes).

### Dependencies
**Hard-blocked by Frontier 2 (transport).**

### Urgency
**Medium-High** — high value, but strictly downstream of transport.

---

## Frontier 4 — Middle Summary Aggregation

### Current State
`RepositoryWorkflowSummary` absent from `src` entirely. `RepositoryProjectionService` already hosts sibling
summaries (`RepositoryContinuitySummary`, `RepositoryReasoningSummary`, `ActiveExecutionSession`).

### Remaining Work
Add `RepositoryWorkflowSummary` **and** a session-economics summary alongside existing siblings — one Middle
change serving both surfaces.

### User Value
**Medium** — dashboard-level aggregation for both frontiers.

### Architectural Risk
**Very Low** — additive through a proven seam.

### Dependencies
Shares the projection seam with Frontiers 1 and 3; best done **after** both produce data, in **one** pass.

### Urgency
**Medium**.

---

## Frontier 5 — Session Routing & Admission Authority

### Current State
**Absent and actively contradicted.** `ExecutionSessionService.StartAsync` *refuses* a second active session per
repository. Routing was a *declared non-goal* in epics 03/06. No router, no reuse/replace policy, no transfer.

### Remaining Work
An Execution-domain **decision** (may a repo hold >1 session? who owns reuse-vs-replace?), then — only if
approved — a relaxed admission policy, a router, transfer/bootstrap, routing diagnostics.

### User Value
**Unproven** — speculative; no evidence it serves the mission until economics can even *measure* the problem.

### Architectural Risk
**Highest in the system** — cross-domain authority change, collides with the one-active-session guard,
historically rejected. Cannot be an additive projection.

### Dependencies
Requires Frontier 1 (to measure) **and** an explicit admission-authority decision (to authorize).

### Urgency
**Low** — gate it; do not plan code.

---

## Frontier 6 — Continuity / Context Transfer

### Current State
Absent. Presupposes a multi-session world that does not exist (one-active-session guard).

### Remaining Work / User Value / Risk
Strictly downstream of Frontier 5; meaningless without routing. Value unprovable today; risk inherits Frontier 5.

### Dependencies
Frontier 5.

### Urgency
**Low** (defer with routing).

---

## Frontier 7 — Economics Governance & Certification

### Current State
The *mechanisms* are fully built and reusable: `WorkflowReportService` (4 reports), `WorkflowCertificationService`
+ e2e fixture, fingerprint idempotency, per-repo recovery isolation, `WorkflowInfluenceTrace`/`WorkflowGateEvidence`
explainability. The *subject matter* (economics data to govern) does not exist yet.

### Remaining Work
Once Frontier 1 produces economics data: **extend** the existing report family with an economics report and the
existing certification fixture with an economics scenario. No new stack.

### User Value
**Medium** — governance/long-horizon visibility, but only meaningful after economics data exists.

### Architectural Risk
**Low** — pure reuse/composition.

### Dependencies
Frontier 1.

### Urgency
**Low-Medium** — fast-follow to economics, not a near-term frontier.

---

## Frontier 8 — Operationalization (Engine Deferrals)

### Current State
Two known engine-side deferrals from audit-01: **hosted continuation is shipped but disabled by default**
(`CommandCenter:Workflow:ContinuationEnabled=false`); **legitimate push-skip completion** is modeled
(`WorkflowGitStatus.PushSkipped`) but intentionally never inferred, pending Execution/Git push-skip evidence.
Also a missing certification *list* endpoint.

### Remaining Work
Config rollout + guardrails for hosted continuation; a cross-domain push-skip evidence change (Execution/Git
emit, Workflow consumes); the `GET .../workflow/certification/reports` endpoint.

### User Value
**Low-Medium** — operational polish, not new capability.

### Architectural Risk
**Low** for continuation/endpoint; **Medium** for push-skip (cross-domain).

### Dependencies
Independent; best slotted after the UI exists to observe/operate them.

### Urgency
**Low**.

---

# Frontier Prioritization

## Session Economics Observability
- **Why It Matters:** the roadmap's true objective, and the only thing that makes sessions economic.
- **Why It Should Be Next:** cheapest possible increment, zero architectural risk, **reaches the UI today with no
  transport**, `LastActivityAt` already exists. Lowest-regret first move; banks guaranteed value.
- **Why It Should Not Be Next:** small absolute capability; doesn't release the big stranded asset.
- **Final Priority:** **P0**.

## Workflow Transport
- **Why It Matters:** the only thing standing between a certified engine and any human using it.
- **Why It Should Be Next:** highest leverage-per-unit-work; unlocks 26 endpoints / ~95% done engine; prerequisite
  for all workflow UI.
- **Why It Should Not Be Next:** invisible by itself; real net-new code; nothing ships to a user until the UI lands.
- **Final Priority:** **P0** (co-lead; sequence after economics only because economics is unblocked and free).

## Workflow UI Workspace
- **Why It Matters:** the human-facing payoff of the entire workflow program.
- **Why It Should Be Next:** turns the unlocked engine into a product.
- **Why It Should Not Be Next:** hard-blocked by transport; cannot start first.
- **Final Priority:** **P1**.

## Middle Summary Aggregation
- **Why It Matters:** dashboard aggregation for both surfaces.
- **Why It Should Be Next:** trivial and shared — but only valuable once its data sources exist.
- **Why It Should Not Be Next:** premature before economics + workflow UI produce data; do it once, last.
- **Final Priority:** **P1** (combined, terminal).

## Economics Governance & Certification
- **Final Priority:** **P2** (fast-follow reuse once economics data exists).

## Operationalization (continuation enablement / push-skip / list endpoint)
- **Final Priority:** **P2**.

## Session Routing & Admission Authority
- **Why It Should Not Be Next:** mythical baseline, declared non-goal, authority collision, unmeasurable today.
- **Final Priority:** **P3** (decision gate only; no code).

## Continuity / Context Transfer
- **Final Priority:** **P3** (downstream of routing).

---

# Roadmap Compression

## Candidate Items — Economics primitives (old M0) + Economics observability (old M2 remainder)
- **Why They Can Be Combined:** the primitives are worthless until surfaced, and the surface is one field-addition
  through an already-wired pipe. Splitting them creates a milestone that produces no observable value.
- **Resulting Capability:** one "Session Economics & Observability" milestone: capture token/cost, surface
  age/usage/activity/cost.
- **Risk:** Low. Keep cost-model sophistication out (see Elimination).

## Candidate Items — `RepositoryWorkflowSummary` (audit-01) + session-economics summary (audit-02)
- **Why They Can Be Combined:** both are additive summaries on the **same** `RepositoryProjectionService`,
  alongside the same siblings. Two roadmaps independently planned a Middle change; it is one change.
- **Resulting Capability:** one Middle-summary milestone serving both frontiers.
- **Risk:** Very Low. Avoids touching Middle twice.

## Candidate Items — Old roadmap M6/M7/M9/M10 mechanisms (efficiency/diagnostics/governance/certification)
- **Why They Can Be Combined:** all four are *already-solved mechanisms* (report family, influence trace,
  certification fixture). They collapse from four build-milestones into a single small "extend existing families"
  fast-follow.
- **Resulting Capability:** one optional economics-governance milestone, pure reuse.
- **Risk:** Low; risk is building it before economics data exists.

---

# Roadmap Elimination

| Item | Classification | Why |
|---|---|---|
| "Recover the Decision Session Registry & Session Router" framing | **Obsolete (mythical baseline)** | Never existed in any commit; explicitly declared non-goals (epics 03/06). Nothing to recover. |
| Workflow plan M0–M9 (engine) | **Already Done** | ~95% built + certified; re-planning is churn. Baseline. |
| Session roadmap M1 registry, M2 lifecycle half, M3 doc-growth half, M8 integration mechanism | **Already Done** | Satisfied by `ExecutionSession` + store + recovery, the observability pipe, Continuity diagnostics, and the Workflow↔session injection seam. |
| 15 separate backend workflow test suites | **Already Done (differently)** | Behavior certified by the consolidated class + e2e fixture. Re-mandating 15 = churn. |
| Original 10-panel workflow UI decomposition | **Obsolete** | Predates the richer backend; redesign around actual surface, don't restore. |
| "Decision sessions" registry category | **Premature / Obsolete** | No such type; decisions reference *execution* sessions by FK. Don't invent speculatively. |
| Continuity "transfer/efficiency" as Continuity-domain work | **Wrong Layer** | Continuity = operational-context *documents*, not session transfer. |
| Routing/transfer (M4/M5/M6/M7-routing) as derived infrastructure | **Wrong Owner / Premature** | Requires an Execution authority change, not a Workflow-style projection. Gate, don't plan. |
| Net-new diagnostics/report/certification vocabularies | **Already Done (mechanism)** | Reuse Workflow/Reasoning/Decisions infra; don't rebuild. |
| Defensive "don't become Brainstorm" guardrail | **Obsolete (misread)** | Aimed at Reasoning (disciplined provenance), which doesn't embody the risk. |
| Committed certification artifacts at rest | **Obsolete** | Evidence is runtime-only by design; don't plan browseable artifacts. |

---

# Dependency Graph Recovery

Genuine dependencies only; artificial roadmap sequencing removed.

```text
Session Economics capture + observability        Workflow Transport (bridge / HTTP-direct)
        (rides existing UI pipe; no blocker)                    ↓
                    │                              Workflow UI Workspace
                    │                                           │
                    └───────────────┬───────────────────────────┘
                                    ↓
                 Shared Middle Summary (RepositoryWorkflowSummary + economics summary)
                                    ↓ (data now exists to govern)
                 Economics Governance & Certification (reuse report/cert families)


GATED, SEPARATE TRACK (not in next roadmap):

Session Economics (measurement)
        +
Execution Session-Admission DECISION  ── if approved ──▶  Routing ──▶ Transfer ──▶ Routing diagnostics
   (authority change, owned by Execution)                                              (reuse influence-trace)

Operationalization (continuation enablement · push-skip evidence · cert list endpoint)
   — independent; slot after UI exists to observe it.
```

Key recovered facts: economics has **no upstream blocker** (it does not wait on transport). Workflow UI has
**exactly one** hard blocker (transport). The Middle summary should be **last and shared**. Routing depends on a
**decision**, not on code.

---

# Recommended Roadmap Themes

## Theme A — Session Economics & Observability
- **Objective:** make token usage, cost, age, and activity first-class, visible properties of `ExecutionSession`.
- **Existing Assets:** `ExecutionSession`/`ExecutionSessionSummary` (incl. `LastActivityAt`), the
  query→`RepositoryProjectionService`→execution-UI pipe, the provider-invocation seam.
- **Missing Assets:** token-usage capture at session boundaries; a cost figure; UI rendering of age/usage/cost.
- **Recommended Scope:** one milestone — capture + surface through the existing pipe. Reuse `*ReportService` for an
  optional economics report later.
- **Explicit Non-Goals:** no cost *model* sophistication; no monetary modeling; no routing; economics never mutates
  lifecycle (read-only, derived).

## Theme B — Workflow Surfacing
- **Objective:** make the certified Workflow engine reachable and visible to humans.
- **Existing Assets:** ~95% engine, 26/27 endpoints, certification, recovery, idempotency.
- **Missing Assets:** transport (0/27 bridge commands or HTTP-direct), dedicated UI workspace, wiring.
- **Recommended Scope:** transport milestone → UI workspace milestone, designed around the *actual* 8-stage
  surface.
- **Explicit Non-Goals:** don't restore the 10-panel plan; don't re-plan the engine; don't commit evidence at rest.

## Theme C — Shared Middle Aggregation
- **Objective:** one dashboard summary pass carrying both workflow and economics aggregates.
- **Existing Assets:** `RepositoryProjectionService` + sibling summaries.
- **Missing Assets:** `RepositoryWorkflowSummary`; session-economics summary.
- **Recommended Scope:** one additive milestone after Themes A/B produce data.
- **Explicit Non-Goals:** don't touch Middle twice; don't pre-build before data exists.

## Theme D — Admission Authority (GATE, not build)
- **Objective:** answer "may a repository hold >1 session, and who owns reuse-vs-replace?" before any routing code.
- **Existing Assets:** `ExecutionSessionService` state machine + `ReplaceSessionAsync`; Decisions/Reasoning
  provenance to capture the decision.
- **Missing Assets:** the decision itself (authority), then a relaxed admission policy.
- **Recommended Scope:** a decision/spike milestone only. **No router code.**
- **Explicit Non-Goals:** routing, transfer, bootstrap, efficiency — all forbidden until this gate passes.

---

# Alternative Futures

## Future A — Minimum Viable Path
**Scope:** Theme A only (Session Economics & Observability).
- **Pros:** smallest possible; zero risk; reaches UI today; satisfies the current roadmap's stated objective; days
  not weeks.
- **Cons:** leaves the ~95% workflow engine completely dark — the largest stranded asset untouched.
- **Risks:** strategic regret: you ship a small win and ignore the big one.
- **Estimated Effort:** **S** (1 small milestone).

## Future B — Balanced Path *(recommended)*
**Scope:** Theme A → Theme B (transport → UI) → Theme C (shared summary). Theme D as an explicit gate. Governance
+ operationalization as P2 fast-follows.
- **Pros:** banks the free economics win first, then releases the stranded engine; both surfaces share the Middle
  pass; routing safely quarantined; nearly all mechanism reused.
- **Cons:** more than a quarter's work; requires real transport plumbing.
- **Risks:** transport is the only genuine net-new infra — scope it tightly (mirror existing bridge patterns).
- **Estimated Effort:** **M** (4 milestones).

## Future C — Maximum Capability Path
**Scope:** Future B + Theme D executed (admission relaxed) + routing/transfer + efficiency/governance +
operationalization (hosted continuation enablement, push-skip, list endpoint).
- **Pros:** the "complete" system on paper.
- **Cons:** builds the historically-rejected, highest-risk, authority-colliding frontier on top of speculative
  value; large surface; high churn risk.
- **Risks:** authority regression; building routing before economics proves it's needed; cross-domain push-skip
  change.
- **Estimated Effort:** **L** (8+ milestones, multi-quarter).

---

# Strategic Recommendation

**Choose Future B — the Balanced Surfacing Path.**

**Why this path.** The defining reality across audit-01 and audit-02 is that Command Center has *built more than it
has surfaced*. Two engines (workflow coordination; session lifecycle) are done or nearly done and invisible. The
highest-leverage, lowest-regret move is therefore to **surface, in leverage order**: bank the free, zero-risk
economics win that reaches the UI today (Theme A), then spend the one real infrastructure investment — transport —
to release the certified workflow engine (Theme B), then consolidate both into one Middle pass (Theme C). This
recovers the *current* roadmap's true objective (economics) and the *prior* roadmap's frontier (workflow surface)
in one minimal, non-overlapping program, reusing every report/diagnostic/certification/recovery mechanism that
already exists.

**Why not Future A.** It is correct but timid. Leaving a 95%-complete certified engine 100% dark is the single
largest unrealized-value regret in the codebase; transport is too high-leverage to defer indefinitely.

**Why not Future C.** It builds the one frontier that should not be built: routing/transfer is a mythical
"recovery," a declared non-goal, an Execution authority collision, and unmeasurable until economics ships. Putting
it in the next roadmap reintroduces exactly the false premise audit-02 dismantled. It belongs behind a decision
gate (Theme D), never in the build queue by default.

---

# Roadmap Generation Inputs

## Build First (Milestone 0 candidates)
- **Session Economics & Observability** — token/cost capture at session boundaries; surface age/usage/cost/activity
  through the existing `RepositoryProjectionService` → execution-UI pipe. (`LastActivityAt` already exists; age
  derivable.) **No new transport. Zero authority change.**

## Build Next (M1–M3)
- **Workflow Transport** — Tauri bridge workflow commands (or HTTP-direct in `api/`). The critical-path unlock.
- **Workflow UI Workspace** — tab/types/api/hooks/panels around the actual 8-stage surface; wire into shell/nav/palette.
- **Shared Middle Summary** — `RepositoryWorkflowSummary` + session-economics summary in one `RepositoryProjectionService` pass.

## Build Later (deferred, P2 — reuse-only fast-follows)
- Economics report (extend `WorkflowReportService` family) + economics certification scenario (extend e2e fixture).
- Operationalization: hosted continuation enablement (config + guardrails), `GET .../workflow/certification/reports`.
- Push-skip completion — **cross-domain** (Execution/Git emit evidence, Workflow consumes).

## Do Not Build
- A session router, reuse/replace policy, transfer, or bootstrap **before** the Execution admission-authority
  decision (Theme D) is made and certified.
- "Decision sessions" as a registry category. A new session registry from scratch. New diagnostics/report/
  certification/recovery vocabularies. Committed certification artifacts at rest. The 15-suite test layout. The
  original 10-panel UI plan.

## Preserve (architectural invariants)
- The authority model + six architecture rules: domains own truth; derived layers observe/project/recover/explain/
  certify. Economics and workflow are **derived, never authoritative**.
- Execution owns session lifecycle **and** session admission (the one-active-session guard) — do not silently override it.
- Per-repository error isolation in recovery; runtime-derived, disposable evidence.
- Continuation vs preparation separation; four-outcome preparation vocabulary; gate→existing-command mapping;
  fingerprint idempotency.
- Disambiguate "continuity": document-continuity (Continuity domain) ≠ session-continuity (economics).

## Reuse (existing infrastructure)
- `ExecutionSession` + store + recovery hosted service (the real registry baseline).
- The projection→Middle→execution-UI pipe (already reaches the UI).
- The `evaluate→persist→recover→certify→act` scaffold; fingerprint idempotency; recovery-first startup.
- `*ReportService` family; `WorkflowCertificationService` + e2e fixture; `WorkflowInfluenceTrace`/
  `WorkflowGateEvidence`/`DecisionInfluenceTrace` for explainability.

---

# Final Verdict

## Recommended Roadmap Type
**Hybrid Roadmap (Surfacing-dominant).** It is mostly a Surfacing roadmap — both economics and workflow are
surfacing efforts over finished or nearly-finished engines — with one small Economics-foundation capture piece and
one explicitly-quarantined authority gate. It is *not* an Economics roadmap (the economics engine is trivial), not
an Integration roadmap (the integration seams already exist), and not an Operationalization roadmap (that is a P2
fast-follow).

## Recommended Roadmap Length
**Short** — four milestones (M0 economics observability, M1 transport, M2 workflow UI, M3 shared Middle summary),
plus a decision gate and two reuse-only P2 fast-follows. The prior roadmaps' M0–M9 collapse into baseline; routing/
transfer leave the roadmap entirely. This is deliberately the smallest roadmap that releases the most built value.

## Confidence
**High.**
- The two heavy analyses (archaeology, reconciliation) are done and mutually consistent; this audit re-verified
  their load-bearing facts at HEAD (`050f76d`): 0/27 transport, no workflow UI/tab/summary, economics fields
  absent except `LastActivityAt`.
- The recommendation rests on *verified asymmetries*, not preference: economics is unblocked and reaches the UI
  today; workflow UI has exactly one hard blocker (transport); routing requires an authority change, not code.
- The only residual uncertainty is **transport mechanism** (27 bridge commands vs HTTP-direct) — an
  implementation choice, not a strategic one — and the **eventual** outcome of the admission-authority decision,
  which is correctly deferred behind a gate rather than guessed.
