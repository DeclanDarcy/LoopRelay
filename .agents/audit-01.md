# Audit-01 — Governed Workflow Orchestration Roadmap

**Subject roadmap:** `.agents/roadmap.md` ("Governed Workflow Orchestration Roadmap", Milestones 0–10)
**Audited against:** current `C:\kernritsu\CommandCenter` repository state (branch `dev`, HEAD `02bddad`)
**Audit type:** implementation-aware. The repository is treated as authoritative. The roadmap is treated as a historical artifact that may be partially wrong.

---

## Orientation — What The Repository Actually Is Now

The roadmap proposes building a **single, central Governed Workflow Engine**: a first-class `Workflow` domain (`Workflow`, `WorkflowStage`, `WorkflowTransition`, `WorkflowState`, `WorkflowExecution`, `GovernanceGate`), a generic workflow state machine, unified workflow persistence/recovery, a cross-cutting `GovernanceGate` framework, per-domain "integration" into that engine, an auto-advancing continuation engine, and a final end-to-end workflow certification.

The repository did **not** evolve toward a central engine. It evolved into **seven completed epics**, each delivering an independent, internally-orchestrated, separately-certified domain:

| Epic | Domain delivered | Project |
| --- | --- | --- |
| 01 | Repository / artifact / planning foundation | `CommandCenter.Core` |
| 02 | Execution lifecycle + handoff + git + repeatable loop | `CommandCenter.Execution` |
| 03 | Operational context (continuity) lifecycle | `CommandCenter.Continuity` |
| 04 | Frontend / workspace UI + authority audits | `CommandCenter.UI` |
| 05 | Decision lifecycle | `CommandCenter.Decisions` |
| 06 | Reasoning trajectory substrate | `CommandCenter.Reasoning` |
| 07 | Automated decision generation | `CommandCenter.Decisions` (generation layer) |

**Three facts dominate this entire audit:**

1. **The roadmap's core vocabulary does not exist in code.** A search across every `.cs` file for `Workflow`, `WorkflowStage`, `WorkflowTransition`, `WorkflowExecution`, `WorkflowState`, `GovernanceGate`, `orchestrat*`, `StateMachine`, `Continuation`, `AwaitingGovernance`, `AutoAdvance` returns **zero matches**. The only file in the repository containing this vocabulary is `roadmap.md` itself. There is no central orchestrator: `Program.cs` wires independent domain services and maps independent endpoint groups with nothing coordinating them.

2. **The roadmap's *stages* all exist already**, fully built and individually certified — but as independent capabilities, each with its own state primitives, its own persistence, its own restart recovery, its own authority boundary, and its own certification. The thing that is missing is *only the unifying engine*, not the stages.

3. **The repository has a deliberate, certified authority doctrine that directly contradicts the roadmap's thesis.** Epic 02 M8 explicitly recorded "Keep automatic milestone progression out of scope." Epic 04's `m0-closure-authority-matrix.md` certifies that **every** workflow-mutating transition requires an explicit human action. Epic 06's boundary docs establish that advisory layers "may not override decisions, become authority, or replace governance." The system was deliberately designed so that **humans operate each stage explicitly**, and that explicitness is treated as an architectural value to preserve — the opposite of the roadmap's goal of moving humans "from operation to governance" via auto-advance.

This means most roadmap milestones are not "incomplete" in the way the roadmap assumes. They are **implicitly completed in substance but architecturally drifted in shape**, and the one milestone that is genuinely unbuilt (M9, continuation) is unbuilt **on purpose** and conflicts with the current governance model.

A note on provenance: `roadmap.md` is **untracked** (`?? .agents/roadmap.md` in git status) and has never been committed, while the seven epics are fully archived under `.agents/archive/epics/`. The evidence strongly suggests the roadmap was authored *after or in parallel with* an architecture it does not describe.

---

## 1. Architectural Compatibility

### Milestone 0 — Workflow Domain Foundation

Original Assumption:
Workflow orchestration is greenfield. A new first-class domain (`Workflow`, `WorkflowStage`, `WorkflowTransition`, `WorkflowState`, `WorkflowExecution`, `GovernanceGate`) with a single shared lifecycle (`Pending/Running/Blocked/AwaitingGovernance/Completed/Failed/Cancelled`) can be introduced cleanly.

Current Reality:
There is no generic workflow domain, but there are already **multiple mature, domain-specific lifecycle models** that collectively cover the same ground: `RepositoryExecutionState` (`Ready/Executing/AwaitingAcceptance/Accepted/AwaitingCommit/AwaitingPush/Failed/Cancelled`), `ExecutionSessionState` (`Created/Executing/Completed/Failed/Cancelled`), `DecisionState`, `DecisionProposalState`, `DecisionReviewState`, `DecisionCandidateState`, `OperationalContextProposalStatus`, `OperationalContextReviewState`. Each domain also owns its own persistence, recovery, and certification.

Assessment:
**Refactor Required / Drifted.**

Reasoning:
Introducing a generic `Workflow`/`WorkflowState` abstraction now is not a foundation step — it is a **retrofit over six domains that each already define authoritative state**. Done literally it would create a second source of lifecycle truth competing with each domain's existing state (duplicate authority paths), which the codebase explicitly guards against. The lifecycle enums the roadmap proposes (`AwaitingGovernance`, `Blocked`) have no home: there is no governance-blocking state anywhere because progression is human-initiated, not engine-gated.

### Milestone 1 — Workflow State Machine

Original Assumption:
No authoritative engine exists; a single state machine must be built to sequence Execution Context Resolution → Execution → Handoff Review → Decision Generation → Decision Review → Decision Resolution → Operational Context Review → Commit → Push, with valid/invalid transition enforcement.

Current Reality:
A validated state machine **already exists for the execution slice** of that chain. `RepositoryExecutionState` transitions are enforced with explicit guards (Epic 02 M5: "Prevent accept/reject outside `AwaitingAcceptance`"; accept → `AwaitingCommit` if changes else `Ready`; reject → `Ready`). Commit/push extend it (`AwaitingCommit → AwaitingPush → Ready`). The Decision, Operational Context, and Reasoning stages are *also* state machines, but **separate ones**, not nodes in a single chain.

Assessment:
**Partially Compatible / Drifted.**

Reasoning:
The roadmap models one linear 9-stage machine. Reality is ~5 of those stages (`context resolution → execution → handoff/acceptance → commit → push`) inside one execution machine, plus the decision/context stages living in parallel domains that are *not* inline transitions of the execution flow. A literal single machine would have to absorb and re-sequence transitions that already belong to other domains.

### Milestone 2 — Workflow Persistence

Original Assumption:
Workflow state (current/previous stage, transition history, governance history, failure history) and restart recovery do not exist and must be built once, centrally.

Current Reality:
Persistence and restart recovery are **pervasive and per-domain**: `FileSystemExecutionSessionStore` + `ExecutionSessionRecoveryHostedService` (execution sessions survive restart; "Accepted state persists after restart"), `FileSystemDecisionRepository`, `FileSystemReasoningRepository`, `FileSystemOperationalContextProposalStore`, plus `ExecutionHistory` projection for audit. Recovery from repository artifacts is certified (Epic 05 M9: "recovery after reload", "artifact reconstruction"; Epic 02 M8: restart-between-executions certified).

Assessment:
**Compatible in capability / Drifted in shape.**

Reasoning:
The *capability* (durable state + restart recovery) is fully present. What does not exist is a *single unified workflow record* holding a cross-domain transition/governance/failure history. Building one would duplicate the per-domain stores that already hold this information under `.agents/*`.

### Milestone 3 — Governance Gate Framework

Original Assumption:
Human authority is unmodeled and must be introduced as a new cross-cutting `GovernanceGate` abstraction with actions `Approve/Reject/Modify/Pause/Cancel` and who/why/when metadata.

Current Reality:
The **term "governance" is already taken and means something different.** `IDecisionGovernanceService` / `DecisionGovernanceService` (Epic 05 M7) is an **advisory, non-mutating health analyzer** of the decision ecosystem ("Governance does not modify decisions, proposals, operational context, or execution state"). Meanwhile the *control-point* semantics the roadmap wants already exist, realized as **explicit per-stage human commands**: `accept/reject_execution_handoff`, `accept/edit/reject/promote operational context proposal`, decision `resolve/supersede/archive`, `commit/push`. Who/why/when is captured per-domain (resolver metadata, review notes, acceptance timestamp+note, promotion metadata).

Assessment:
**Conflict / Refactor Required.**

Reasoning:
Building a new `GovernanceGate` framework would (a) collide head-on with the existing, opposite meaning of "governance" (advisory diagnostics vs. control gates) and (b) duplicate authority that already lives in each domain's explicit commands. The functional need is already satisfied; the abstraction is what is missing, and it cannot adopt the name "governance" without semantic damage.

### Milestone 4 — Execution Workflow Integration

Original Assumption:
Execution (context generation, launch, monitoring, completion, failure) must be integrated *into* a workflow engine.

Current Reality:
Execution is **fully built and is itself the de-facto orchestrator** of its slice: `ExecutionContextService`, `ExecutionSessionService`, `ExecutionMonitoringService`, providers (`Codex/Fake/Noop`), `ExecutionEvent`, failure/cancel states, and recovery hosted service. This is the entirety of Epic 02.

Assessment:
**Compatible (already delivered); "integration target" does not exist.**

Reasoning:
There is no separate engine to integrate into — the execution domain already plays that role for execution→push. The milestone's substance is done; only its framing (subordinate to a central engine) is invalid.

### Milestone 5 — Handoff Workflow Integration

Original Assumption:
Handoff detection/validation/evaluation/governance must be added and wired into the engine, producing "workflow decisions" and "governance requests".

Current Reality:
`HandoffService` + the acceptance workflow (Epic 02 M4/M5) deliver detection, validation, and the accept/reject "handoff governance" gate, with history preserved. Accepted handoffs feed directly into the git stage.

Assessment:
**Compatible (already delivered) / Drifted framing.**

Reasoning:
The one part not literally true: handoffs do not auto-emit Decision-domain "decisions". Decision discovery/capture (`DecisionReasoningCaptureService`, `DecisionDiscoveryService`) is a separate, deliberately-decoupled path.

### Milestone 6 — Decision Workflow Integration

Original Assumption:
Decision Discovery/Generation/Review/Resolution is one milestone of work, with a governance gate at resolution.

Current Reality:
This is **massively over-delivered** by two whole epics (05 lifecycle + 07 automated generation): discovery → candidate → option → tradeoff → recommendation → package generation, refinement directives, quality assessment/signals/trends, human-authoring-burden analysis, advisory governance, human-gated resolution, influence projection into execution, and full certification. Resolution authority is human-only and certified ("execution and governance cannot resolve decisions").

Assessment:
**Compatible / Hidden Completion (far beyond roadmap scope).**

Reasoning:
The roadmap underestimates this area by roughly two epics. Nothing here needs "integration"; it needs to be *recognized as already exceeding* the milestone.

### Milestone 7 — Operational Context Workflow Integration

Original Assumption:
Context Proposal/Review/Acceptance/Promotion with Accept/Edit/Reject gates before promotion must be built.

Current Reality:
Built in full (Epic 03): `OperationalContextProposal`, review with accept/edit/reject states, `OperationalContextLifecycleService.PromoteOperationalContextAsync` with strict preconditions, archival of prior revisions, history. The Accept/Edit/Reject-before-promotion gate exists **exactly as specified**.

Assessment:
**Compatible / Hidden Completion.**

Reasoning:
This milestone is essentially already satisfied verbatim.

### Milestone 8 — Git Workflow Integration

Original Assumption:
Commit Preparation/Review/Execution and Push Preparation/Execution with commit/push approval gates must be built.

Current Reality:
Built in full (Epic 02 M6 git-lifecycle): `GitService`, `CommitPreparation`, `CommitRequest/Result`, commit scope selection, push, integrated into the execution state machine (`AwaitingCommit → AwaitingPush → Ready`). Commit and push are explicit human-gated actions (`commit_execution` requires "Commit Selected"; `push_execution` requires "Push Commit").

Assessment:
**Compatible / Hidden Completion.**

### Milestone 9 — Workflow Continuation Engine

Original Assumption:
Stages should **auto-advance** (Execution → Handoff → Decision → Context → Commit → Push) with the engine evaluating completion and only stopping at governance gates, replacing "human clicks button → next stage".

Current Reality:
This is the **one genuinely unbuilt capability — and it was deliberately excluded.** Epic 02 M8 explicitly states "Keep automatic milestone progression out of scope," delivering instead a *repeatable but human-initiated* loop. Epic 04's closure-authority-matrix certifies that every transition requires explicit human action and that backend commands remain authoritative for state transitions.

Assessment:
**Incompatible as written / Refactor + Re-justification Required.**

Reasoning:
Auto-advance contradicts a certified architectural invariant. It cannot simply be "built"; it would require deliberately *reversing* a documented authority decision. This is the roadmap's true remaining intent and its single largest point of conflict with reality.

### Milestone 10 — Workflow Certification

Original Assumption:
A new end-to-end certification must prove the full loop auto-progresses, gates are enforced, it survives restart/failure, and "humans govern rather than operate."

Current Reality:
Certification is **everywhere, per-domain**: Epic 02 M8 (repeatable execution loop), Epic 03 M8 (long-horizon continuity), Epic 05 M9 (decision lifecycle, restart/artifact recovery, authority boundaries), Epic 06 M8 (reasoning outcome), Epic 07 M10 (automated generation, incl. an *executive report on whether human authorship has been replaced by generation + governance*). What no certification covers is **cross-domain automatic stage progression**, because it does not exist.

Assessment:
**Partially Compatible / one criterion Obsolete.**

Reasoning:
Most M10 criteria (survives restart, survives failure, gates enforced, authority preserved) are already certified per-domain. But the criterion "stages progress automatically" / "humans govern rather than operate" is contradicted by the certified, intentional reality that humans operate each stage. That criterion is obsolete unless M9's authority reversal is first accepted.

---

## 2. Semantic Alignment

### Milestone 0
Original Meaning: introduce "workflow" as the system's central organizing concept.
Current Meaning: the system's organizing concepts are **domains with authority boundaries** (Execution, Decisions, Continuity, Reasoning), not a single workflow object.
Assessment: **Misaligned.** "Workflow" is not the domain language the system adopted.

### Milestone 1
Original Meaning: one linear stage machine.
Current Meaning: several domain state machines, with the execution slice already linear and validated.
Assessment: **Partially Aligned.**

### Milestone 2
Original Meaning: a unified workflow persistence record.
Current Meaning: per-domain durable artifacts under `.agents/*` with certified recovery.
Assessment: **Partially Aligned** (capability aligns, structure does not).

### Milestone 3
Original Meaning: "governance" = control gates that block progression.
Current Meaning: "governance" = **advisory, non-mutating health analysis**; human authority is expressed as explicit commands, not gates.
Assessment: **Misaligned** (direct vocabulary collision — the most dangerous in the roadmap).

### Milestone 4
Original Meaning: subordinate execution to an engine.
Current Meaning: execution *is* the orchestrator of its slice.
Assessment: **Partially Aligned** (capability present, hierarchy inverted).

### Milestone 5
Original Meaning: handoff governance + emit workflow decisions.
Current Meaning: handoff acceptance gate exists; decision emission is a separate decoupled path.
Assessment: **Partially Aligned.**

### Milestone 6
Original Meaning: a single decision integration step.
Current Meaning: decisions are a two-epic automated-generation-plus-lifecycle domain.
Assessment: **Aligned in intent, under-scoped in language.**

### Milestone 7
Original Meaning: context proposal lifecycle with accept/edit/reject gates.
Current Meaning: identical — built verbatim.
Assessment: **Aligned.**

### Milestone 8
Original Meaning: git lifecycle with commit/push approval.
Current Meaning: identical — built verbatim.
Assessment: **Aligned.**

### Milestone 9
Original Meaning: replace human stage-operation with automatic progression.
Current Meaning: human stage-operation is a **certified value**, not a problem to remove.
Assessment: **Misaligned** (the goal opposes the architecture's stated principle).

### Milestone 10
Original Meaning: certify autonomous-loop replacement of human operation.
Current Meaning: certification proves authority boundaries and recovery *and* that humans remain operators where required.
Assessment: **Partially Aligned / partly Misaligned** (the "humans govern not operate" framing conflicts).

---

## 3. Hidden Completion

### Milestone 0
Already Completed: domain lifecycle modeling for every relevant stage.
Evidence: `RepositoryExecutionState`, `ExecutionSessionState`, `DecisionState`, `DecisionProposalState`, `DecisionReviewState`, `OperationalContextProposalStatus`, `OperationalContextReviewState`.
Remaining Work: only a *generic unifying abstraction* — which may be unwanted (see Refactor).

### Milestone 1
Already Completed: validated transition enforcement for context-resolution → execution → acceptance → commit → push.
Evidence: Epic 02 M5/M6/M8 milestone records; guard rules ("Prevent accept/reject outside `AwaitingAcceptance`").
Remaining Work: only cross-domain unification (decision/context stages are separate machines).

### Milestone 2
Already Completed: durable state + restart recovery + audit history, per domain.
Evidence: `FileSystemExecutionSessionStore`, `ExecutionSessionRecoveryHostedService`, `ExecutionHistory`, `FileSystemDecisionRepository`, `FileSystemReasoningRepository`, `FileSystemOperationalContextProposalStore`; certified recovery in Epic 05 M9 and Epic 02 M8.
Remaining Work: only a single cross-domain workflow record (likely redundant).

### Milestone 3
Already Completed: the *functional* approve/reject/modify control points + who/why/when metadata.
Evidence: handoff accept/reject (Epic 02 M5), op-context accept/edit/reject/promote (Epic 03 M3/M4), decision resolve/supersede/archive with resolver metadata (Epic 05 M6/M9), commit/push gates (Epic 04 closure matrix).
Remaining Work: only a named gate abstraction (collides with "governance").

### Milestone 4
Already Completed: **all of it.** Evidence: Epic 02 (`ExecutionContextService`, `ExecutionSessionService`, `ExecutionMonitoringService`, providers, recovery).
Remaining Work: none (other than reframing).

### Milestone 5
Already Completed: detection, validation, acceptance gate, history. Evidence: `HandoffService`, Epic 02 M4/M5.
Remaining Work: optional auto-emission of decisions (deliberately decoupled).

### Milestone 6
Already Completed: discovery → generation → review → refinement → resolution → governance → quality → influence → certification. Evidence: `CommandCenter.Decisions` (Epics 05 + 07), Epic 07 M10 executive report.
Remaining Work: none within roadmap scope; reality exceeds it.

### Milestone 7
Already Completed: **all of it.** Evidence: `OperationalContextLifecycleService`, Epic 03 M3/M4.
Remaining Work: none.

### Milestone 8
Already Completed: **all of it.** Evidence: `GitService`, `CommitPreparation`, push flow, Epic 02 M6, closure matrix commands.
Remaining Work: none.

### Milestone 9
Already Completed: a *repeatable* (not automatic) loop, restart-safe.
Evidence: Epic 02 M8 ("repository can repeatedly move through execution, acceptance, commit, push, and ready states").
Remaining Work: automatic progression — **explicitly out of scope by prior decision.**

### Milestone 10
Already Completed: per-domain certification of recovery, failure, authority, and (for decisions) workflow-replacement evidence.
Evidence: Epic 02 M8, Epic 03 M8, Epic 05 M9, Epic 06 M8, Epic 07 M10.
Remaining Work: cross-domain auto-progression certification — blocked on M9.

---

## 4. Obsolescence

### Milestone 0
Status: **Partially Obsolete.** A generic workflow domain duplicates existing per-domain lifecycles. Still-needed portion: at most a thin coordination contract, not a new authoritative domain.

### Milestone 1
Status: **Partially Obsolete.** The execution-slice machine exists; a second generic machine adds little unless it unifies domains (debatable value).

### Milestone 2
Status: **Mostly Obsolete.** Durable state + recovery already exist; a unified workflow-persistence layer would duplicate them.

### Milestone 3
Status: **Partially Obsolete as named.** The control function exists; only a renamed/unified abstraction would remain, and it must not reuse "governance".

### Milestone 4
Status: **Obsolete as a work item** (already delivered). Value of re-implementing: none.

### Milestone 5
Status: **Largely Obsolete** (delivered, minus optional decision auto-emission).

### Milestone 6
Status: **Obsolete as scoped** (reality exceeds it).

### Milestone 7
Status: **Obsolete** (delivered verbatim).

### Milestone 8
Status: **Obsolete** (delivered verbatim).

### Milestone 9
Status: **Still Needed** — this is the roadmap's only substantive unbuilt intent — but it is **superseded-by-decision**: prior work intentionally rejected it. It cannot be treated as routine remaining work.

### Milestone 10
Status: **Partially Obsolete.** Most certification already exists per-domain; the "automatic progression / humans don't operate" criterion is obsolete unless M9's authority reversal is accepted.

---

## 5. Refactor Requirements

### Milestone 0
Refactor Needed: **Yes.** Reason: a generic `Workflow`/`WorkflowState` domain would compete with six domains' authoritative state. Required Adjustment: if pursued, build a *non-authoritative coordination view* over existing domain states — never a second source of state truth.

### Milestone 1
Refactor Needed: **Yes.** Reason: a single linear machine would have to re-own transitions that belong to Decision/Continuity domains. Required Adjustment: model the chain as cross-domain *handoffs of authority*, not as one engine owning all transitions.

### Milestone 2
Refactor Needed: **Yes (if pursued at all).** Reason: avoid duplicating per-domain stores. Required Adjustment: derive any "workflow history" as a *projection* over existing `.agents/*` artifacts.

### Milestone 3
Refactor Needed: **Yes.** Reason: name + semantic collision with advisory `DecisionGovernanceService`; risk of duplicate authority paths. Required Adjustment: rename (e.g. "decision gate" / "approval gate"), and reuse existing explicit commands rather than introducing a parallel gate authority.

### Milestone 4
Refactor Needed: **No** (already built). Adjustment: reframe as "already satisfied".

### Milestone 5
Refactor Needed: **No** for the gate; **Optional** if decision auto-emission is desired (must respect decision-domain authority).

### Milestone 6
Refactor Needed: **No.** Adjustment: rescope the milestone to match the far larger delivered reality.

### Milestone 7
Refactor Needed: **No** (built verbatim).

### Milestone 8
Refactor Needed: **No** (built verbatim).

### Milestone 9
Refactor Needed: **Yes — the most consequential.** Reason: auto-advance violates the certified "explicit human action per transition" invariant and the "automatic progression out of scope" decision. Required Adjustment: do not implement as written. Any continuation must be **opt-in, per-transition, governance-respecting**, and must first explicitly *revisit and overturn* the existing authority doctrine, not bypass it.

### Milestone 10
Refactor Needed: **Yes.** Reason: certifying "humans don't operate" contradicts the system's certified value. Required Adjustment: redefine certification around *authority preservation + recovery + optional assisted progression*, reusing the per-domain certification services rather than building a new monolith.

---

## 6. Dependency Shifts

### Milestone 0
Original Dependencies: none (foundation).
Current Dependencies: would now depend on **all six** existing domains (it must wrap their state).
Assessment: **Shifted** — foundation became a capstone.

### Milestone 1
Original: depends on M0.
Current: the execution-slice machine already exists independent of any M0; a unifying machine depends on all domains.
Assessment: **Shifted.**

### Milestone 2
Original: depends on M0/M1.
Current: persistence already exists ahead of M0/M1; unification depends on all domain stores.
Assessment: **Shifted / inverted.**

### Milestone 3
Original: depends on M0–M2.
Current: control points already exist across domains; only an abstraction layer would depend on them.
Assessment: **Shifted.**

### Milestones 4, 5, 7, 8
Original: depend on M0–M3 being built first.
Current: **already complete and predate the proposed foundation entirely.** The roadmap's ordering is inverted — these "later" integrations are the *most mature* parts of the system.
Assessment: **Inverted.**

### Milestone 6
Original: depends on M0–M3.
Current: complete and independent (Epics 05/07).
Assessment: **Inverted.**

### Milestone 9
Original: depends on M4–M8.
Current: M4–M8 are done, so M9 is *unblocked technically* — but it acquired a **new non-technical dependency**: an explicit reversal of the authority doctrine.
Assessment: **Shifted** (technical deps satisfied; governance dependency added).

### Milestone 10
Original: depends on M9.
Current: depends on M9 *and* on which authority model is chosen; most sub-criteria already certified independently.
Assessment: **Shifted.**

---

## 7. Governance Compatibility

The repository's governance model (current, authoritative): **(a)** "governance" = advisory, non-mutating analysis; **(b)** every state transition requires an explicit human command; **(c)** advisory/reasoning layers may influence but never override or resolve; **(d)** automatic progression was deliberately excluded; **(e)** each domain owns its own authority and is certified to keep it.

### Milestone 0
Assessment: **Conflict (mild).** Reasoning: a central workflow state object risks becoming a competing authority over domain state. Compatible only if non-authoritative.

### Milestone 1
Assessment: **Compatible** if it enforces *existing* domain transitions; **Conflict** if it asserts new transition authority.

### Milestone 2
Assessment: **Compatible** as a projection; **Conflict** if it becomes the authoritative store.

### Milestone 3
Assessment: **Conflict.** Reasoning: directly collides with the advisory meaning of "governance" and could create a parallel approval authority. Must be reconciled with existing explicit-command authority.

### Milestone 4
Assessment: **Compatible** (already conforms).

### Milestone 5
Assessment: **Compatible** (acceptance gate is an explicit human action, consistent with the model).

### Milestone 6
Assessment: **Compatible.** Reasoning: certified that "execution and governance cannot resolve decisions" — human resolution authority is preserved.

### Milestone 7
Assessment: **Compatible** (accept/edit/reject/promote are explicit, review-mediated).

### Milestone 8
Assessment: **Compatible** (commit/push are explicit, gated).

### Milestone 9
Assessment: **Conflict — the central governance conflict of the roadmap.** Reasoning: auto-advance removes the explicit human action that the closure-authority-matrix certifies as required, and reverses the "automatic progression out of scope" decision. Hidden automation across authority boundaries is precisely what the model forbids.

### Milestone 10
Assessment: **Conflict (partial).** Reasoning: certifying "humans govern rather than operate" institutionalizes the M9 conflict. Compatible only if recast to certify authority preservation rather than autonomy.

---

# Audit Summary

## Still Correct

- **M7 (Operational Context)** and **M8 (Git)** intent matches reality verbatim — but they are *already built*, so "correct" means "already satisfied."
- **M6 (Decisions)** intent is correct in direction; reality exceeds it.
- The roadmap's high-level *value* (humans focus on review/approval/resolution; the system handles mechanics) is **partially honored** — the system already automates generation/preparation and reserves judgment points for humans. Only the *auto-advance* expression of that value conflicts.

## Partially Completed

- **M1 (State Machine):** execution slice complete and validated; decision/context stages exist as separate machines; cross-domain unification absent.
- **M3 (Governance Gates):** control function complete via explicit commands + metadata; the unified/named abstraction absent (and name is taken).
- **M5 (Handoff):** acceptance gate complete; optional decision auto-emission absent by design.
- **M9 (Continuation):** repeatable loop complete; automatic progression absent by deliberate decision.
- **M10 (Certification):** restart/failure/authority certified per-domain; cross-domain auto-progression certification absent.

## Implicitly Completed

- **M2 (Persistence/Recovery):** durable state + restart recovery + audit history exist per-domain and are certified.
- **M4 (Execution Integration):** Epic 02 delivered it in full; execution self-orchestrates its slice.
- **M6 (Decision Integration):** Epics 05 + 07 deliver far more than the milestone asks.
- **M7 (Operational Context Integration):** Epic 03 delivered the exact proposal→review→accept/edit/reject→promote lifecycle.
- **M8 (Git Integration):** Epic 02 M6 delivered commit/push with gates, inside the execution state machine.

## Architecturally Drifted

- **M0:** "workflow" is not the system's organizing concept; domains with authority boundaries are.
- **M1/M2:** unified-engine shape vs. distributed per-domain machines and stores.
- **M4/M5/M6/M7/M8:** all real, but as **independent capabilities**, not subordinate "integrations" into a central engine that does not exist.

## Obsolete

- **M4, M7, M8** as *work items* — already delivered; re-implementation adds no value.
- **M6** as *scoped* — reality has outgrown the milestone description.
- **M2** as a *new central store* — would duplicate existing recovery infrastructure.
- The **"humans don't operate stages"** premise embedded in **M9/M10** — superseded by a deliberate, certified design choice.

## Requires Refactor

- **M0:** only as a non-authoritative coordination projection, never a second state authority.
- **M1:** model as cross-domain authority handoffs, not one engine owning all transitions.
- **M3:** rename away from "governance"; reuse existing explicit commands; do not create a parallel approval authority.
- **M9:** do not implement as written; any continuation must be opt-in, per-transition, governance-respecting, and predicated on an *explicit, deliberate reversal* of the current authority doctrine.
- **M10:** recast certification around authority preservation + recovery + assisted (not autonomous) progression, reusing existing per-domain certification services.

## Requires Reordering

- The roadmap's **dependency order is inverted.** It treats M0–M3 (foundation/engine/governance) as prerequisites for M4–M8 (the integrations). In reality **M4–M8 are the most mature, fully-certified parts of the system and predate any foundation.** A corrected roadmap must start from the *existing* integrations and ask whether any unifying layer is warranted on top — not the reverse.
- **M9 is technically unblocked** (M4–M8 done) but **governance-blocked** (requires overturning the no-auto-progression decision). Its true predecessor is a *policy decision*, not a code milestone.

## Highest Risk Roadmap Assumptions

1. **"Workflow orchestration is greenfield."** False. Orchestration exists per-domain; the execution domain already runs a validated, persistent, restart-safe, repeatable loop.
2. **"'Governance' is an available concept to define."** False and dangerous. "Governance" already means advisory, non-mutating analysis. The roadmap's gate semantics collide with it.
3. **"Humans should be moved off operating stages."** Contradicts a *certified architectural value*. The closure-authority-matrix and Epic 02 M8 establish explicit human operation as intentional, not incidental.
4. **"A single central Workflow Engine is the right shape."** Risks creating duplicate authority paths over six mature domains — the exact failure mode the codebase's boundary docs guard against.
5. **"M0 is the foundation everything depends on."** The dependency graph is inverted; the supposed dependents are already built and certified.
6. **"Decision/Context/Git/Handoff stages still need building/integration."** All are complete; some (decisions) exceed the roadmap by two epics.

## Most Important Architectural Discoveries Since Roadmap Creation

1. **No central orchestrator exists or was ever built** — by design. Coordination is distributed across self-contained domains (`Program.cs` wires independent services with nothing on top).
2. **A certified authority-boundary doctrine governs the whole system:** advisory layers never mutate or resolve; every transition is an explicit human command; auto-progression was *explicitly* placed out of scope (Epic 02 M8). This is the single most important fact for any corrected roadmap.
3. **"Governance" is an occupied term** with the *opposite* meaning to the roadmap (advisory diagnostics, not control gates).
4. **The execution domain already realizes M1+M2+M4+M5+M8+most-of-M9**: a validated, persistent, restart-recoverable, repeatable execution→acceptance→commit→push→ready loop.
5. **The Decisions area is two epics deep** (lifecycle + automated generation), with certification that already answers "has human authorship been replaced by generation + governance?" — territory the roadmap's single M6 never imagined.
6. **An entire Reasoning trajectory domain (Epic 06) exists** that the roadmap is blind to. It is the actual cross-cutting layer that spans decisions/execution/continuity — but it is explicitly **non-authoritative** ("may not override decisions, become authority, or replace governance"). It is the closest thing to a cross-domain substrate, and it deliberately is *not* an orchestrator.
7. **Certification is a first-class, per-domain pattern** (`DecisionCertificationService`, `DecisionGenerationCertificationService`, `ReasoningCertificationService`, plus epic certification milestones) — so the roadmap's M10 should compose these, not replace them.
8. **The roadmap artifact is untracked / never committed**, while the divergent architecture is fully archived — corroborating that the roadmap does not reflect the system it targets.
