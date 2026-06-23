# Command Center — Post-Reasoning-Trajectory Strategic Capability Audit

**Date:** 2026-06-23
**Branch:** `dev`
**Charter:** Determine *what meaningful capabilities are still missing* from Command Center as a whole — not which prior epics survive. Reasoning Trajectory Preservation, Decision Lifecycle, Operational Context, Outcome Certification, and Historical Reconstruction are treated as **complete and closed**. The reasoning persistence pipeline `Repository → Graph → Trace → Reconstruction` is not re-litigated.
**Method:** Whole-repository evidence sweep across all seven backend contexts (`Core`, `Decisions`, `Reasoning`, `Continuity`, `Execution`, `Middle`, `Backend`) and the React/Tauri UI. Capabilities were classed *owned* only where a named service holds the responsibility — not where data is merely displayed. Absence was confirmed by empty keyword sweeps, reported as evidence. Every claim is cited `file:line`.

---

## 0. Executive Summary

Command Center has, with unusual discipline, completed **one entire hemisphere of its mission and deliberately declined the other.**

The **completed hemisphere is passive cognition**: *perceive → record → preserve → reconstruct → certify-as-faithful.* The repository can say, with certified fidelity, **what happened, why it happened, and how the thinking evolved** — and prove it survives recovery. This is real, owned, and tested. It is also, per the project's own framing, the articulated north-star — *"Settled conclusions survive. Reasoning does not."* (`.agents/backlog.md:8-9`) — and that north-star is now **met**.

The **unowned hemisphere is active cognition**: *evaluate → judge → steer → enforce → recover → learn → generalize.* The system can reconstruct where a project **has been** with perfect fidelity, but owns almost nothing about where it **should go**, whether it is **on track**, whether its work is **any good**, or how to **get better next time**. Critically, this is not an accident of incomplete delivery — it is the consistent result of a foundational architectural bet. **Every deliberate exclusion in the codebase points the same direction** (§4): the system was built to *record without interpreting* (`docs/architecture.md:49`), to *preserve findings but not enforce them* (`docs/reasoning-ownership-boundaries.md:25`), to *never certify that a decision is correct* (`docs/reasoning-authority-boundary.md:45`), and to stay *repository-scoped* throughout (`docs/reasoning-taxonomy.md:3`).

**This makes the central strategic question not "what bug remains" but "how far should Command Center's authority extend?"** Today it is a flawless **flight recorder and historian**. The category its name claims — *Command* Center — implies a **pilot and an air-traffic controller**. That gap is the subject of this audit.

**Reconciliation with `audit.md`:** The prior audit asked *"is there a demonstrated failure?"* and correctly answered *no*. This audit asks the charter's different question — *"what strategic responsibility is unowned against the broader vision?"* — and answers: **a coherent active-management hemisphere is unowned.** Both are true. There is no defect in what was built; there is a large, deliberate, unbuilt responsibility surface.

**Recommendation (§5): `The following major responsibilities remain.`**

---

## 1. Capability Inventory

### 1.1 Implemented (owned by a named service, tested)

| Capability | Owner | Evidence |
| --- | --- | --- |
| Repository registration / removal / refresh | `RepositoryService` | `Core/Repositories/*` |
| Artifact discovery / load / save / rotation (6 types) | `ArtifactService`, `*ArtifactStore` | `ArtifactService.cs:9-22`; `Artifact.cs:3-21` |
| Decision **lifecycle** (candidate→proposal→resolve→supersede) | `DecisionDiscoveryService`, `DecisionResolutionService` | `DecisionDiscoveryService.cs:196`; `DecisionResolutionService.cs:203-252` |
| Decision governance **detection** (10 analyzers) | `DecisionGovernanceService` | `DecisionGovernanceService.cs:57-68` |
| Reasoning trajectory: events / threads / graph / query / reconstruction / certification | `Reasoning/Services/*` | (closed — not re-audited) |
| Operational Context lifecycle: generate / review / promote / compress / diagnose | `Continuity/Services/*`, `Middle/Continuity/*` | `OperationalContextReviewService.cs:49,88`; `UnderstandingCompressionService.cs:11` |
| Understanding evolution **tracking** | `UnderstandingEvolutionLedger` | `Continuity/Models/UnderstandingEvolutionLedger.cs:3-25` |
| Execution session lifecycle, single-provider launch, handoff validation, accept/commit/push | `ExecutionSessionService`, `CodexExecutionProvider`, `HandoffService` | `ExecutionSessionService.cs`; `HandoffService.cs:22-79` |
| Crash recovery (reattach-or-fail) | `ExecutionSessionRecoveryHostedService` | `ExecutionSessionService.cs:24-62` |
| Dashboard / workspace **read-model** projections | `RepositoryProjectionService` | `Middle/Projections/RepositoryProjectionService.cs:31-148` |

### 1.2 Partially implemented (a thin version exists; the responsibility is mostly unmet)

| Capability | What exists | What is missing |
| --- | --- | --- |
| **Planning** | 3-state readiness (`MissingPlan`/`MissingMilestones`/`Ready`) — a *file-presence check* | Goals, target outcomes, sequencing, prioritization, scheduling. A milestone is a filename; it has no status, order, or completion (`PlanningService.cs:28-38`; `Artifact.cs:29-40`) |
| **Decision quality** | `ProposalQuality` findings check *presence* of options/recommendation/evidence | No score, grade, risk, reversibility, or confidence. Evidence is a string, never weighed (`DecisionGovernanceService.cs:572-586`) |
| **Proactive decision discovery** | Keyword scan surfaces undecided forks/ambiguity | Cannot detect a fork no one phrased with a trigger word; not derived from system state (`DecisionDiscoveryService.cs:279-342`) |
| **Governance enforcement** | Exactly **one** actuator: blocking findings exclude a decision from the execution projection | Does not gate promotion, resolution, or supersession; advisory everywhere else (`DecisionProjectionService.cs:93-96,108-111`) |
| **Execution quality gate** | Handoff-file-present + zero exit code → `AwaitingAcceptance` | No acceptance-criteria evaluation, no test run, no goal-met check (`HandoffService.cs:45-59`) |
| **Understanding-error feedback** | Proxy signals: repeated-question / decision-rework / lost-item counts | No ground-truth "this understanding was wrong, corrected by reality" signal (`ContinuityDiagnosticsService.cs:159-210`) |
| **Human oversight** | In-UI accept/reject/refine buttons; passive risk/question counts on the dashboard | No escalation engine, notification dispatch, review queue, or approval-gate service (§3.7) |

### 1.3 Missing (no owner anywhere in the repository)

1. **Project goals / target outcomes / charter as a first-class artifact** — no artifact type, model, or service. `ArtifactType` has no objective/charter member (`Artifact.cs:3-11`).
2. **Project health / risk / drift computation** — nothing computes "off track," "milestone at risk," "scope drifting." `ActiveRiskCount` is human-authored markdown surfaced as a count (`RepositoryProjectionService.cs:58-59`).
3. **Work prioritization, sequencing, scheduling, allocation** — a human supplies each `MilestonePath`; one session per repo (`ExecutionSessionService.cs:134-145`).
4. **Decision / execution quality evaluation** — merit is never judged, only lifecycle state and faithfulness.
5. **Execution recovery beyond mark-`Failed`** — no retry, replan, or remediation; no stuck/anomaly/timeout detection (sweep for `stuck|anomaly|timeout|heartbeat|watchdog` → none).
6. **Multi-provider orchestration / selection / fallback** — one hardcoded provider; `SupportsReattach => false` (`CodexExecutionProvider.cs:11,13`; `ServiceCollectionExtensions.cs:22`).
7. **Learning loop** — captured reasoning is **write-only**. `IReasoning*` is imported **nowhere** in `CommandCenter.Decisions`; history is never read back to improve future behavior.
8. **Cross-project / cross-repository intelligence** — every service is single-repo; the only multi-repo iteration builds independent cards with zero comparison (`Middle/Projections/RepositoryProjectionService.cs:31-67`).
9. **Evidence generation / research** — "evidence" is citation of existing artifacts, never generated; the posture is passive reconstruction.
10. **Proactive escalation / oversight infrastructure** — oversight is pull-based; risk surfaces only if a human opens the right panel.

---

## 2. Responsibility Map

```
                          PASSIVE COGNITION  (built, owned, certified)
   perceive ─► record ─► preserve ─► reconstruct ─► certify-faithful
   ✔ artifacts  ✔ reasoning   ✔ graph     ✔ trace        ✔ provenance
   ✔ decisions  ✔ handoffs    ✔ ledger    ✔ narrative    ✔ certification

   ─────────────────────────  AUTHORITY BOUNDARY  ─────────────────────────
        (drawn deliberately; every exclusion in §4 sits on this line)

                          ACTIVE COGNITION  (unowned)
   evaluate ─► judge ─► steer ─► enforce ─► recover ─► learn ─► generalize
   ✘ quality   ✘ merit  ✘ goals  ✘ gates   ✘ replan   ✘ loop   ✘ portfolio
```

**Owned responsibilities:** repository/artifact custody; decision lifecycle; reasoning preservation & reconstruction; operational-context lifecycle; understanding-evolution tracking; single-session execution & handoff custody; faithful certification; read-model projection.

**Unowned responsibilities:** project steering (goals/health/risk/drift); work prioritization & sequencing; quality/merit evaluation of decisions and execution; governance enforcement; execution recovery, replan & orchestration; multi-provider management; the reasoning→behavior learning loop; cross-project intelligence; proactive escalation.

**Ambiguous responsibilities (placement understated or split):**
- `DecisionReasoningCaptureService` lives in `Backend` and captures far more than decisions (governance, OC promotion, execution handoff) — its name understates a cross-context choreography role.
- `RepositoryProjectionService` is the *only* component that sees all repositories at once, yet owns no cross-repository responsibility — making it the natural, currently-vacant seat for portfolio intelligence (`Middle/Projections/RepositoryProjectionService.cs:31-67`).
- Risk/health data is *authored* in Continuity markdown, *parsed* by Continuity, and *counted* in Middle — so "project risk" has three touch points and **no owner that computes it.**

---

## 3. Strategic Gap Analysis (highest-leverage first)

Ranked by *importance × unowned-ness × architectural significance*, and tagged by whether the gap is a clean absence or sits against a **deliberate boundary** (§4) that would need a conscious decision to cross.

### 3.1 The Learning Loop is open — preservation produces no compounding return *(highest leverage)*
The most expensive thing the project built — certified reasoning trajectory preservation — is **write-only**. Nothing consumes it to change future behavior. `IReasoning*` appears in **zero** files under `CommandCenter.Decisions`; `DecisionEvolution` is only an event-family label (`ReasoningEnums.cs:9,76`); the nearest feedback, `AnalyzeRepeatedGovernanceFindings`, only notes a finding recurred (`DecisionGovernanceService.cs:858`). **A perfect memory that never informs the next decision is a museum, not an intelligence.** *Boundary tension: low — a consuming/advisory service does not violate "reasoning creates no new authority"; it reads reasoning to advise planning/decisions.*

### 3.2 Active Project Steering is entirely unowned — the most "Command-Center-shaped" gap *(cleanest gap)*
There is no model of project **goals**, no detection of **drift**, no **prioritization/sequencing**, and no computed **health/risk**. Readiness is `plan.md exists? milestones exist?` (`PlanningService.cs:28-38`). The system can certify the past in exhaustive detail and cannot answer *"is this project on track to its objective?"* — because no objective is modeled. *Boundary tension: none — `Planning` is a real bounded context that is simply underbuilt; goals/health/risk are nowhere excluded.*

### 3.3 Certification certifies *faithfulness*, never *merit*
Outcome Certification proves a record is immutable, provenance-preserving, and reproducible — and is explicitly forbidden from certifying *that a decision is correct or a finding enforced* (`reasoning-authority-boundary.md:45`). Execution "quality" is handoff-file-presence + exit code (`HandoffService.cs:45-59`); the monitor records output *"without interpreting quality or intent"* (`architecture.md:49`). **Nothing in the system ever asks whether the work was good.** *Boundary tension: high — directly crosses two deliberate exclusions; requires a conscious scope decision.*

### 3.4 Execution is a single-shot manual executor, not a long-horizon operator
A human picks one milestone (`ExecutionSessionService.cs:134-138`), one session runs (`:145`), and failure means `Failed` (`:57-62`) — no retry, replan, remediation, or stuck-detection; one hardcoded provider with no fallback. For *long-horizon* operation this is the load-bearing gap: the system cannot keep a multi-step project moving without a human in the loop at every boundary. *Boundary tension: medium — recovery/sequencing are additive; only "interpreting quality" brushes a boundary.*

### 3.5 Governance is a detector with one actuator
Ten analyzers detect contradictions, coverage gaps, and conflicting directives (`DecisionGovernanceService.cs:57-68`), but reasoning *"does not enforce"* findings (`reasoning-ownership-boundaries.md:25`) and the only enforcement anywhere is projection exclusion (`DecisionProjectionService.cs:93-96`). Findings that should block promotion or force resolution are advisory. *Boundary tension: low–medium — enforcement can live in Decisions/Execution without touching reasoning's boundary.*

### 3.6 Cross-Project Intelligence does not exist — every repository is a silo
All persistence and all services are repository-scoped by construction (`reasoning-taxonomy.md:3`); ~30 call sites list all repositories only to select one by ID. Nothing learned in repo A can ever benefit repo B; there is no portfolio view, no shared pattern library, no cross-project risk comparison. *Boundary tension: high — repository-scoping is foundational; this is the next frontier, not a quick add.*

### 3.7 Human oversight is pull-based and reactive
The only `IHostedService` is crash recovery; there is no escalation engine, notification/alert dispatch, review queue, or approval-gate service (sweeps for `escalat|notify|alert|webhook|SignalR` → empty). Risk surfaces *only if a human opens the right panel.* For autonomous long-horizon operation, oversight must **come to the human**, not wait to be found. *Boundary tension: low — escalation surfaces existing signals; it adds no new authority.*

---

## 4. Why This Is Deliberate, Not Accidental

The gaps above are coherent because the architecture excludes active cognition *by explicit, repeated policy.* This must be acknowledged: the candidate epics in §5 are not "the architecture forgot X" — they are **"crossing a boundary the architecture intentionally drew."**

| Excluded capability | Verbatim boundary | Source |
| --- | --- | --- |
| Execution quality judgment | "records provider output and activity **without interpreting quality or intent**" | `architecture.md:49` |
| Understanding correctness / drift correction | "**must not** perform … correctness judgment, confidence scoring, **automatic drift correction**" | `operational-context-schema.md:178` |
| Governance enforcement | reasoning "may preserve the history and impact of those findings, but it **does not enforce them**" | `reasoning-ownership-boundaries.md:25` |
| Certifying merit | "**must not certify that a decision is correct**, that a governance finding is enforced" | `reasoning-authority-boundary.md:45` |
| New steering authority | OC "**does not become a new workflow authority or repository state machine**" | `architecture.md:99` |
| Background automation | "**must not** add filesystem watchers, background polling, or automatic rescans" | `architecture.md:31` |
| Cross-repository scope | scope is repository-scoped throughout; **no cross-repository ambition is stated** | `reasoning-taxonomy.md:3` |

**Implication for the roadmap:** the strategic decision is singular and explicit — *should Command Center remain a faithful recorder, or become an active operator?* Everything in §3 follows from that one choice. The audit's job is to surface it, not to pre-decide it.

---

## 5. Candidate Epics (generated only from demonstrated gaps)

Ordered by leverage. Each names the boundary it crosses so the decision is made with eyes open. The first two are recommended as the entry points because they are **highest-leverage with the least conflict** with existing deliberate boundaries.

### Epic A — Close the Learning Loop *(recommended first)*
- **Problem:** Certified reasoning/decision/understanding history is write-only; it never improves future behavior (`IReasoning*` imported nowhere in `Decisions`; `DecisionGovernanceService.cs:858` only flags recurrence).
- **Responsibility:** A consuming service that reads preserved trajectories and surfaces *advisory* lessons — recurring decision rework, repeated governance findings, abandoned alternatives — into new planning and decision discovery.
- **Why the existing architecture doesn't solve it:** Reasoning is deliberately bounded to *preserve*, not *act* (`reasoning-authority-boundary.md:33`). Nothing is allowed — or assigned — to read it back. The loop is structurally open.
- **Expected outcome:** The preservation investment begins compounding; the system demonstrably stops repeating its own mistakes within a repository. *Crosses no authority boundary (advisory-only).*

### Epic B — Active Project Steering *(recommended second)*
- **Problem:** No project goal/objective is modeled; readiness is a file-presence check; there is no drift, prioritization, or health/risk computation (`PlanningService.cs:28-38`; `Artifact.cs:3-11`).
- **Responsibility:** A first-class *objective/charter* artifact plus a steering service that computes on-track/at-risk/drifting signals and proposes work sequencing against the goal.
- **Why the existing architecture doesn't solve it:** `Planning` only checks artifact existence; `RepositoryProjectionService` only displays human-authored risk counts (`:58-59`). No component owns "where should this project go and is it getting there?"
- **Expected outcome:** Command Center can answer the question its name implies — *is this project on course?* — and steer toward an objective. *No deliberate boundary excludes this; cleanest gap.*

### Epic C — Execution Autonomy & Recovery
- **Problem:** Single human-selected milestone, single session, single provider; failure ends in `Failed` with no retry/replan and no stuck-detection (`ExecutionSessionService.cs:57-145`; `CodexExecutionProvider.cs:11-13`).
- **Responsibility:** Execution sequencing/allocation, failure remediation (retry/replan), stuck/anomaly detection, and multi-provider selection/fallback.
- **Why the existing architecture doesn't solve it:** Execution is explicitly a disposable single-shot worker; the monitor records *without interpreting* (`architecture.md:49`). True long-horizon operation needs an operator above the session.
- **Expected outcome:** A long-horizon project advances across many slices without a human at every boundary. *Crosses the "no interpretation / no background automation" boundary — a conscious decision.*

### Epic D — Quality & Enforcement (merit, not just faithfulness)
- **Problem:** Decision quality is presence-of-fields (`DecisionGovernanceService.cs:572-586`); execution "quality" is file-presence + exit code; governance has one actuator (`DecisionProjectionService.cs:93-96`).
- **Responsibility:** Score decision quality (evidence sufficiency, reversibility, risk); evaluate execution against acceptance criteria; turn governance findings into gates that block promotion/resolution until resolved.
- **Why the existing architecture doesn't solve it:** Certification is forbidden from judging merit (`reasoning-authority-boundary.md:45`); enforcement is excluded (`reasoning-ownership-boundaries.md:25`). Detection without an actuator is by design.
- **Expected outcome:** The system distinguishes *faithfully recorded* from *actually good*, and bad work cannot silently advance. *Highest boundary tension; sequence after A–C.*

### Epic E — Proactive Oversight & Escalation
- **Problem:** Oversight is pull-based; no escalation/notification/review-queue/approval-gate service exists (only `ExecutionSessionRecoveryHostedService`).
- **Responsibility:** An escalation engine that pushes at-risk projects, blocking findings, and decisions-awaiting-human to the operator, with explicit approval gates.
- **Why the existing architecture doesn't solve it:** All risk signals already exist but are surfaced only on demand in UI panels; nothing routes them to a human proactively.
- **Expected outcome:** Risk reaches the human before harm, enabling trustworthy unattended operation. *Low boundary tension (surfaces existing signals).* 

### Epic F — Cross-Project Intelligence *(frontier; not near-term)*
- **Problem:** Every repository is an isolated silo (`Middle/Projections/RepositoryProjectionService.cs:31-67`); no portfolio, no shared lessons, no cross-project risk.
- **Responsibility:** A scope above the repository that compares projects, transfers learned patterns, and reports portfolio health.
- **Why the existing architecture doesn't solve it:** Repository-scoping is foundational (`reasoning-taxonomy.md:3`); nothing is permitted to reason across repositories.
- **Expected outcome:** Lessons and risk patterns compound across the whole portfolio. *Highest architectural cost; depends on Epics A–B first.*

---

## 6. Roadmap Recommendation

```text
The following major responsibilities remain:
  1. Learning loop (reasoning → future behavior)        [unowned; low boundary tension]
  2. Active project steering (goals / health / drift)   [unowned; no boundary tension]
  3. Execution autonomy & recovery                      [unowned; medium boundary tension]
  4. Quality & enforcement (merit, not faithfulness)    [partial; high boundary tension]
  5. Proactive oversight & escalation                   [partial; low boundary tension]
  6. Cross-project intelligence                         [unowned; frontier]
```

**Justification from repository evidence.** Command Center's *articulated* vision — preserve reasoning so settled understanding and its history both survive (`backlog.md:8-9`) — is **complete and certified**, and `audit.md` is right that no *failure* of it remains. But the charter of this audit is the broader vision implied by the system's own surface area: a *Command* Center for long-horizon, agent-driven software projects. Against that surface, the evidence shows the system owns the **passive-cognition hemisphere in full and the active-cognition hemisphere almost not at all** (§2). The missing responsibilities are not scattered nice-to-haves; they are a single coherent half of the mission — *steer, judge, recover, enforce, learn, generalize* — left unowned by a consistent and explicit set of authority boundaries (§4).

**The one decision that gates everything:** *Should Command Center extend its authority from faithful recorder to active operator?* If **no**, the architecture is **strategically complete as scoped** and the §3 items should be closed as deliberate exclusions. If **yes**, the highest-leverage, lowest-conflict entry points are **Epic A (close the learning loop)** and **Epic B (active steering)** — they unlock the most value, contradict no existing boundary, and make the expensive preservation layer finally compound. Epics C–F follow once that authority extension is ratified.

**Disposition:** Do not close this audit as "complete." Escalate the single authority-scope decision above to the human owner; it is the genuine fork the system cannot resolve for itself — and, fittingly, exactly the kind of undecided strategic fork Command Center will one day be expected to surface on its own.
