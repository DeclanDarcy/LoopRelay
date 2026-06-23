# Command Center — Critical-Path-to-Workflow-Replacement Audit (audit-04)

**Date:** 2026-06-23
**Branch:** `dev`
**Authority anchor:** `founder-intent.md` (authoritative for intent).
**Inherited finding (audit-03, treated as a starting assumption):** Primary mission = **workflow replacement**; secondary mission = **Brainstorm research**; the project is currently optimizing the secondary mission while the primary remains incomplete and contains one structural inversion at its most important step.
**Charter (this audit):** Not "what is architecturally interesting" and not "what helps future Brainstorm." Only: *what capabilities must exist before the founder's workflow can be considered replaced, and which current/proposed efforts are not on that critical path?*
**Method:** The founder's workflow was reconstructed from `founder-intent.md` and re-verified against live source. The three load-bearing facts — the behavior of Decision Generation, the presence of any cross-stage orchestration, and the manual/automatic trigger of each stage — were re-checked directly in the source tree rather than inherited. Claims are grounded `file:line`.

---

## 0. Executive Summary

**The mechanical half of the workflow is replaced. The cognitive half is not, and nothing currently planned closes it.** Command Center automates *moving work through the pipe* (context assembly, execution launch, commit, push) but not *producing the cognition the pipe carries* (decisions) or *advancing the pipe itself* (continuation). The result is a **faster manual cockpit, not a workflow replacement**: the founder presses fewer buttons but is still the decision engine and still the loop driver.

Three facts, each re-verified in source, fix the critical path:

1. **Decision Generation is inverted, not merely incomplete.** `DecisionGenerationService.GenerateProposalAsync` is *fully deterministic* — no model call anywhere in the decision pipeline. It emits an empty proposal form: `option-1 = "Resolve {Title}"`, an optional `option-2 = "Preserve current direction…"` only when the signal is a Conflict/Fork, boilerplate tradeoffs/assumptions interpolating the title, and a recommendation **hardwired to `options[0]`** (`DecisionGenerationService.cs:59-65, 292-363`). Refinement then *requires the human to change the content* and refuses to proceed otherwise (`DecisionRefinementService.cs:49-63`). The system produces the form; the human produces the decision. This is the founder's named **Human Decision Engine Failure** (`founder-intent.md:479-481`).

2. **There is no workflow — there are ten buttons.** The only `IHostedService` registered is crash recovery (`Program.cs:17`; `ExecutionSessionRecoveryHostedService.cs:6-18`). No service chains stages; every stage is an explicit HTTP/Tauri call (`ExecutionEndpoints.cs:18,38`; `ExecutionSessionsEndpoints.cs:86,114`; `GitEndpoints.cs:41,60,80`). One session per repo (`ExecutionSessionService.cs:145-148`); failure ends at `Failed` with manual retry (`:197-207`). The founder's *cyclic* "Next Execution" loop (`founder-intent.md:79-111`) exists nowhere.

3. **The entire forward roadmap is off the critical path.** `.agents/backlog.md` north-star is "Settled conclusions survive. Reasoning does not" (`:7-9`); across 5 epics / 55 milestones the strings "workflow replacement," "throughput," "autonomous," and "automate the workflow" appear **zero** times. Not one milestone advances the primary mission.

**Answer to the charter question — would workflow replacement occur if development continued exactly as planned?**

```text
No.
The mechanical spine is built and convergent.
The one Tier-0 capability (real decision generation) is INVERTED and is on
no roadmap. The loop that makes it a "workflow" rather than a toolkit is MISSING.
All 55 planned milestones serve the secondary mission.
Continuing as planned makes the recorder better and leaves the workflow unreplaced.
```

---

## 1. Objective 1 — Reconstructed Workflow & Implementation State

The founder's workflow (`founder-intent.md:79-111`) reconstructed, with each stage classified **Automated / Partial / Manual** *as implemented today*. "Automated" = the system produces the output; "Manual" = a human produces the substance; "Partial" = system produces mechanics, human produces the judgment/substance.

| # | Stage | Owner (verified) | State | What is automated vs. human-produced |
| --- | --- | --- | --- | --- |
| 1 | **Repository Selection** | `RepositoryService` | **Automated surface / human choice** | Selection UI + registry exist; *choosing which repo* is a governance act, correctly human. |
| 2 | **Context Assembly** | `ExecutionContextService` | **Automated** | Context/prompt resolved deterministically; manually previewed (`ExecutionEndpoints.cs:18`). The founder's "manual prompt/context assembly" (`:54-56`) is genuinely removed. |
| 3 | **Execution** | `ExecutionSessionService`, `CodexExecutionProvider` | **Automated-within / manual trigger** | Provider launch + monitoring automated once started; human selects the milestone and clicks Start (`ExecutionEndpoints.cs:38`); single-shot, one per repo (`ExecutionSessionService.cs:145-148`). |
| 4 | **Handoff Review** | `HandoffService` | **Partial** | *Validation* automated = handoff-file-present + zero exit code → `AwaitingAcceptance` (`HandoffService.cs:45-59`). *Quality judgment* is human: no acceptance-criteria/goal-met evaluation. Accept/reject is a human button (`ExecutionSessionsEndpoints.cs:86,114`). |
| 5 | **Decision Generation** | `DecisionGenerationService` | **Inverted (form Automated / substance Manual)** | Deterministic empty-form generator (`:59-65, 292-363`); the human authors the real decision via refinement (`DecisionRefinementService.cs:49-63`). Discovery is a keyword/trigger-word scan + SHA-256 dedup (`DecisionDiscoveryService.cs:16-31, 256, 279-342`), not state-derived. |
| 6 | **Decision Review** | `DecisionReviewService` | **Automated form / collapses into authoring** | Review state machine works, but it reviews content the human just authored — governance over self-produced substance. |
| 7 | **Decision Resolution** | `DecisionResolutionService` | **Automated form (correctly human)** | Records human choice + rationale. Appropriate governance — but the substance being resolved was human-generated, not system-generated. |
| 8 | **Operational Context Update** | `OperationalContextGenerationService` (+ review/lifecycle) | **Automated + governed** | Generation automated; human accept/reject (`OperationalContextReviewService`). Convergent as a continuity *mechanism*. |
| 9 | **Commit** | `GitService` | **Automated / manual trigger** | prepare/commit wired; human button (`GitEndpoints.cs:41,60`). "Manual commit preparation" (`:61`) genuinely removed. |
| 10 | **Push** | `GitService` | **Automated / manual trigger** | push wired; human button (`GitEndpoints.cs:80`). "Manual push" (`:62`) genuinely removed. |
| — | **Next Execution (the cycle)** | *(none)* | **Missing** | No orchestrator chains stages; no auto-start of the next slice. The workflow's cyclic nature (`:109-111`) is unimplemented. |

**Reading:** stages 1–3, 8–10 are real workflow replacement. Stage 4 is half-replaced (mechanics yes, judgment no). Stages 5–7 are the **inversion zone** — present as ceremony, human-authored in substance. The **loop is absent**.

---

## 2. Objective 2 — Minimum Workflow-Replacement Capability Set

For each stage's *automation*, classified **Required / Helpful / Optional** for achieving workflow replacement (Brainstorm goals excluded by charter), with current status.

| Stage capability | For workflow replacement | Status |
| --- | --- | --- |
| Context assembly automation | **Required** | ✅ Built |
| Execution launch automation | **Required** | ✅ Built |
| Handoff *validation* | **Required** | ✅ Built |
| Handoff *quality/acceptance evaluation* | **Helpful** (lets human review a judgment, not produce one) | ⚠️ Partial (exit-code only) |
| **Real automated Decision Generation** | **Required** | ❌ Inverted |
| Decision Review (human) | **Required** (governance) | ✅ Built |
| Decision Resolution (human) | **Required** (governance) | ✅ Built |
| Operational Context update automation | **Required** | ✅ Built |
| Commit automation | **Required** | ✅ Built |
| Push automation | **Required** | ✅ Built |
| **Autonomous continuation (governed loop)** | **Required** (distinguishes "workflow" from "toolkit") | ❌ Missing |
| Session economics / reuse | **Helpful** (cost, not capability) | ❌ Missing/unowned |
| Multi-provider orchestration | **Optional** | ❌ Missing |

**The minimum set has exactly two unmet *Required* items: real decision generation and a governed continuation loop.** Everything else Required is built; everything missing beyond those two is Helpful or Optional.

---

## 3. Objective 3 — Workflow Bottlenecks (authoring vs. governance)

The founder's design intent is explicit: the human sits **between decision generation and decision application** as *reviewer/governor*, not as *producer* (`founder-intent.md:198-209, 212-232`). The bottleneck test is therefore: **which stages still demand human *authoring / synthesis / reasoning / decision-production* rather than human *review / governance / approval*?**

```text
STAGES WHERE THE HUMAN IS STILL THE PRODUCER  (founder-intent violations)
  Decision Generation   — human authors options, tradeoffs, recommendation in full.
                          The system's output is an empty form. PRIMARY BOTTLENECK.
                          (DecisionGenerationService.cs:292-363; DecisionRefinementService.cs:49-63)
  Decision Review       — nominally governance, but it reviews content the same
                          human just authored, so it collapses back into authoring.
  Handoff quality       — human must read the handoff and synthesize "is this good?";
                          system only checks file-present + exit code. SECONDARY BOTTLENECK.
                          (HandoffService.cs:45-59)
  Next-Execution driving— human manually re-initiates every stage of every cycle;
                          there is no loop to govern, only buttons to press.

STAGES WHERE THE HUMAN IS CORRECTLY THE GOVERNOR  (founder-intent honored)
  Repository Selection, Decision Resolution (records the verdict),
  Operational Context accept/reject, Commit/Push approval.
```

**Finding:** The mechanical bottlenecks the founder enumerated (prompt assembly, context consolidation, commit prep, push — `:54-62`) are **removed**. The cognitive bottleneck — **decision production** — is **not**, and it is the one the founder ranked non-negotiable. The single inversion at stage 5 drags stage 6 with it and forces the human to remain the decision engine for the most important step in the loop. Special-attention triad:

```text
Decision Generation  → BOTTLENECK (human authoring; the core inversion)
Decision Refinement  → BOTTLENECK (the human's authoring channel, mislabeled "refinement")
Decision Resolution  → NOT a bottleneck (governance) — but it governs human-made substance
```

---

## 4. Objective 4 — Is Automated Decision Generation on the Critical Path?

**Question:** Can workflow replacement succeed without automated decision generation?

**Evidence, decisive:**
- The founder defines success as operating projects "without … **manual decision generation**" (`founder-intent.md:134-139, 457-465`).
- "Automated Decision Generation is a core requirement. It is not optional. It is not a convenience. It is not a future enhancement." (`:308-317`)
- "Without automated decision generation: **The human becomes the decision engine.** That outcome is contrary to founder intent." (`:330-336`)
- Verified state: the generator is deterministic boilerplate with a hardwired recommendation; no model is invoked anywhere in the decision pipeline (`DecisionGenerationService.cs:30-91`; confirmed empty on any LLM/inference call in `CommandCenter.Decisions`).

**Answer:** **No — workflow replacement cannot succeed without it, by the founder's own definition of success.** Removing "manual decision generation" is a literal success criterion; the capability that would remove it is the inverted one.

**Classification:**
```text
Decision Generation = CRITICAL PATH (Tier 0)
```
It is not near-critical and not merely supporting. It is the one capability whose absence converts the whole system from "workflow replacement under governance" into "a faster manual decision desk."

---

## 5. Objective 5 — Decision Sessions, by Responsibility

Not "should they exist," but: *what responsibilities were Decision Sessions intended to satisfy, and are those satisfied today?* (`founder-intent.md:172-196, 250-273`)

| Intended responsibility | Source | Status today | Evidence |
| --- | --- | --- | --- |
| **Automated decision production** (the stated reason they exist, `:180`) | `:172-196` | **Unsatisfied** | Generation is a deterministic form (`DecisionGenerationService.cs:292-363`) |
| **Decision lifecycle management** (generate→review→resolve→supersede) | `:182-196` | **Satisfied** (as stateless ceremony) | Discovery/Review/Refine/Resolution/Certification services exist |
| **Session reuse / cost optimization / economics** | `:250-264` | **Unsatisfied** (no owner) | Sessions non-reusable; no router; `backlog.md:1131-1137` admits the owner was lost |
| **Session health / routing diagnostics** | `:258-265` | **Unsatisfied** | No routing layer exists |
| **Continuity carriage** (incidental, *not* permanent memory, `:176`) | `:174-180` | **Partially satisfied** (re-homed into Operational Context) | `Continuity/Services/*` |

**Finding:** The project kept the **skeleton** (lifecycle ceremony) and discarded the **function** (automated production + economics). Dropping the *session mechanism* was a legitimate transitional choice (`founder-intent.md:381-394`); dropping the *production responsibility it carried* was not.

---

## 6. Objective 6 — Roadmap Set vs. Workflow Replacement

Each roadmap family classified **Critical Path / Important / Secondary / Premature** *relative to workflow replacement* (not Brainstorm).

| Roadmap family | Backlog locus | Classification | Basis |
| --- | --- | --- | --- |
| **Reasoning & Decision Lifecycle** | Epic 5 + shipped Epic 06 | **Secondary** (decision *lifecycle* is Important; reasoning lifecycle is Secondary) | Lifecycle ceremony aids governance but is already built; reasoning preservation is evidence-generation (`backlog.md:1-505`) |
| **Reasoning Trajectory Preservation** | Epic 5 | **Secondary** (already shipped) | Pure evidence for Brainstorm; no decision-production content |
| **Continuity Fidelity** | Epic 6 | **Secondary** | Measures transfer success of a continuity *mechanism*; no workflow stage depends on it (`backlog.md:506-1055`) |
| **Continuity Strategy** | Epic 7 | **Important but misframed** | Re-homes the orphaned **session-economics** responsibility (`backlog.md:1131-1137`) — genuinely workflow-adjacent — but frames it as continuity policy, not throughput support |
| **Operational Context Decomposition** | Epic 8 | **Premature** | Decomposes a mechanism the founder said should stay *peripheral* (`founder-intent.md:277-305`); no workflow stage is blocked by OC cohesion (`backlog.md:1622-2119`) |
| **Long-Horizon Research & Brainstorm Evidence** | Epic 9 | **Premature** | Self-described recovery of Brainstorm research ambitions — textbook premature-Brainstorm (`backlog.md:2120-2128`) |

**No roadmap family is Critical Path to workflow replacement.** Exactly one (Continuity Strategy) touches a workflow-relevant responsibility, and only if re-framed from continuity to session economics.

---

## 7. Objective 7 — Investments Currently Off the Critical Path

| Proposed work | Founder need it satisfies | Tier of that need | On critical path? |
| --- | --- | --- | --- |
| Continuity Fidelity (Epic 6) | "Did continuity transfer succeed?" — a continuity-mechanism quality signal | Secondary | **No** |
| Continuity Strategy (Epic 7) | Session economics / continuity cost (orphaned router responsibility) | Helpful (Tier 2) — *if* reframed | **Adjacent**, misframed |
| Operational Context Decomposition (Epic 8) | Internal cohesion of a peripheral mechanism | Secondary / beyond-intent | **No** |
| Research Evidence (Epic 9) | Evidence for future Brainstorm | Secondary (explicitly) | **No (premature)** |
| Reasoning Trajectory (shipped) | "How did reasoning evolve?" | Secondary | **No** (already done) |

**Confirming audit-03's suspicion:** Continuity Fidelity, Continuity Strategy (as continuity), and Research Evidence are **secondary-mission investments**. They consume the project's entire forward capacity (55 milestones) while the primary mission's one Tier-0 gap stays open.

---

## 8. Objective 8 — Missing Critical-Path Capabilities

Classified **Missing / Partial / Incorrect / Inverted**.

| Founder-critical capability | Verdict | Evidence |
| --- | --- | --- |
| **Decision Generation** | **Inverted** — form present, substance absent | `DecisionGenerationService.cs:59-65, 292-363` |
| **Decision Throughput** | **Missing** — a consequence of the inversion; human authors every decision | `DecisionRefinementService.cs:49-63` |
| **Workflow Automation (cross-stage orchestration)** | **Missing** — no service chains stages | `Program.cs:17` (only crash-recovery hosted service) |
| **Autonomous Continuation (the cycle)** | **Missing** — no next-execution trigger; ten manual buttons | `ExecutionEndpoints.cs`, `GitEndpoints.cs`, `ExecutionSessionsEndpoints.cs` |
| **Handoff Quality / acceptance evaluation** | **Partial** — file-present + exit code only | `HandoffService.cs:45-59` |
| **Session Economics / reuse** | **Missing** — unowned since the router was rejected | `backlog.md:1131-1137` |

**The two that block the primary mission outright:** Decision Generation (Inverted) and Autonomous Continuation (Missing).

---

## 9. Objective 9 — Shortest Path to Workflow Replacement (capability tiers)

Not a roadmap — a ranked capability set. **Tier 0** = workflow replacement cannot be claimed without it. **Tier 1** = required to make the automated stages behave as a *workflow* rather than a faster toolkit. **Tier 2** = restores an intended-but-lost support capability.

```text
TIER 0 — without this, "replacement" is false by the founder's own success criteria
  • Real automated Decision Generation.
    Replace the deterministic empty-form generator with a model-backed generator
    that synthesizes fallible-but-real options, tradeoffs, and a reasoned
    recommendation from the decision context — so the human REVIEWS substance
    instead of AUTHORING it. (Closes the inversion at DecisionGenerationService.cs.)

TIER 1 — turns nine automated stages into one governed workflow
  • Governed Autonomous Continuation.
    An orchestrator that advances handoff-accept → decision generation → OC update →
    commit/push → next slice, pausing at the human-governance gates the founder
    defined (decision review/resolution), instead of requiring a button per stage.
  • Handoff Quality / Acceptance Evaluation.
    Evaluate the handoff against acceptance criteria so the human reviews a
    judgment rather than producing one. (Upgrades HandoffService beyond exit-code.)

TIER 2 — recovers an intended capability; improves economics, not capability
  • Session Economics / Continuity-cost ownership.
    Re-home the orphaned Session-Router responsibility (backlog.md:1131-1137) —
    framed as workflow/throughput support, NOT as continuity research.
```

Tier 0 is one capability. Tier 0 + Tier 1 is the **minimum capability set** for workflow replacement. Everything in the current backlog sits below Tier 2 in priority for the primary mission.

---

## 10. Objective 10 — Mission Alignment of Every Effort

`Advances Primary / Secondary / Both / Neither`.

| Effort | Status | Mission alignment |
| --- | --- | --- |
| Context assembly, execution launch, commit, push (built) | Done | **Primary** |
| Human-governance boundary (built) | Done | **Primary** (enabling) |
| Operational Context lifecycle (built) | Done | **Both** (continuity mechanism the workflow uses + Brainstorm evidence) |
| **Fix Decision Generation (Tier 0)** | *Not planned* | **Primary** |
| **Governed continuation loop (Tier 1)** | *Not planned* | **Primary** |
| **Handoff quality gate (Tier 1)** | *Not planned* | **Primary** |
| Reasoning Trajectory Preservation (shipped, Epic 06) | Done | **Secondary** |
| Epic 6 — Continuity Fidelity | Proposed | **Secondary** |
| Epic 7 — Continuity Strategy | Proposed | **Secondary** (→ **Both** only if reframed as session economics) |
| Epic 8 — Operational Context Decomposition | Proposed | **Secondary** |
| Epic 9 — Research & Brainstorm Evidence | Proposed | **Secondary** (premature) |

**Efforts consuming significant effort while advancing only the secondary mission:** the **entire active backlog — 5 epics, 55 milestones** (`backlog.md`), none of which advances the primary mission, plus the already-completed reasoning-trajectory stack. The three Primary-mission capabilities that would actually replace the workflow appear on **no** plan.

---

## 11. Success Criteria — Direct Answers

1. **What workflow is Command Center replacing?** The founder's manual engineering loop: repo selection → context/prompt assembly → execution launch → handoff review → **decision generation** → decision review → resolution → operational-context update → commit → push, operated *cyclically* and continuously (`founder-intent.md:49-111`).
2. **Which workflow stages remain manual?** In *substance*: Decision Generation (human authors it) and Handoff quality judgment. In *triggering*: all of them — every stage is a discrete manual button; the cross-stage loop is fully manual.
3. **Which manual stages are the largest bottlenecks?** **Decision Generation** (the human is the decision engine), followed by the absence of a continuation loop, then handoff quality judgment.
4. **Which capabilities are truly on the critical path?** Real decision generation (Tier 0); governed continuation + handoff quality (Tier 1); session economics (Tier 2). Nothing else.
5. **Is automated decision generation on the critical path?** **Yes — Tier 0.** Its absence makes "workflow replacement" false by the founder's own success criteria (`:457-465`).
6. **Which proposed roadmap work is secondary-mission?** **All of it** — every one of the 5 backlog epics / 55 milestones; the strings "workflow replacement / throughput / autonomous" appear zero times.
7. **Which proposed roadmap work is premature?** Epic 8 (OC Decomposition) and Epic 9 (Research & Brainstorm Evidence) — the latter is premature-Brainstorm by the founder's own test (`founder-intent.md:435-441`).
8. **What capabilities are missing that prevent workflow replacement?** Real decision generation (inverted), a governed continuation loop (missing), and a handoff quality gate (partial).
9. **What is the minimum capability set required for workflow replacement?** Tier 0 + Tier 1: real decision generation, a governed autonomous loop, and acceptance-criteria handoff evaluation — on top of the already-built mechanical spine and governance boundary.
10. **If development continued exactly as currently planned, would workflow replacement occur?** **No.** The plan contains zero workflow-replacement work; the Tier-0 inversion would remain; the loop would remain absent.
11. **If not, what capability gaps prevent it?** The decision-generation inversion, the missing continuation loop, and the partial handoff quality gate — none of which is on any roadmap.
12. **What should become the project's north-star until workflow replacement is achieved?**

```text
"Make the human the governor, not the decision engine —
 then close the loop."

Concretely: a real decision generator (Tier 0) feeding a governed autonomous
continuation loop (Tier 1), so the founder operates Axiom / Vector /
FrontendCompiler end-to-end through Command Center while only reviewing,
approving, and resolving — never authoring, never button-driving.
Freeze secondary-mission (continuity/reasoning/research) epics until this holds.
```

---

## 12. Final Verdict

```text
Command Center has replaced the parts of the workflow that move work
through the pipe, and left unreplaced the part that produces the cognition
the pipe exists to carry.

The founder asked for a system that GENERATES decisions and RUNS the loop
under human governance. The project built a system that FORMATS decisions
and EXPOSES the loop as buttons, then pointed all 55 remaining milestones
at preserving the reasoning the human still has to supply.

Two capabilities stand between today and workflow replacement:
a real decision generator, and a governed loop to run it in.
Neither is on the roadmap. Everything on the roadmap is something else.
```

**Disposition:** Until the Tier-0 decision-generation inversion is closed and a Tier-1 governed loop exists, treat every secondary-mission epic as deferrable and measure progress against *workflow replacement*, not against the backlog's reasoning-preservation north-star.
