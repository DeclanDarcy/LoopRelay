# Audit-02 — Bridging Roadmap Intent to Emerged Architecture

**Subject roadmap:** `.agents/roadmap.md` ("Governed Workflow Orchestration Roadmap", Milestones 0–10)
**Prior audit:** `.agents/audit-01.md` (implementation-aware drift audit)
**Audited against:** current `C:\kernritsu\CommandCenter` repository (branch `dev`, HEAD `02bddad`), with key claims re-verified directly.
**Audit type:** intent-preserving bridge. The roadmap's *intended capability* is authoritative. Audit-01 is evidence. The repository is evidence. Certification proves current behavior works — not that future behavior must stay the same.

---

## Orientation — What This Audit Is For

Audit-01 answered *what exists, what drifted, what is already done*. Its conclusion was structurally correct on one axis (no central engine was built; the stages exist as seven independent, certified domains) but it over-committed on the most consequential question: it treated the roadmap's central intent — **M9 continuation** — as conflicting with a "certified architectural value" that humans must *operate* every stage, and therefore as something that could only proceed by "reversing a documented authority decision."

Audit-02 re-opened that conclusion because it is the single fact that decides whether the roadmap is reachable or dead. Two load-bearing documents were re-read verbatim, and **both say something narrower than audit-01 inferred:**

1. **The closure-authority-matrix asserts backend authority, not human operation.** Verbatim (`.agents/archive/epics/04/audits/m0-closure-authority-matrix.md`): *"Backend commands remain authoritative for state transitions and validation."* The surrounding inventory certifies that **navigation/selection must not silently mutate state** and that mutations happen only through explicit actions that **call backend commands** (e.g. selecting an awaiting-commit repo does *not* call `prepare_commit`; only the explicit action does). This is a **frontend-must-not-mutate** doctrine. It does **not** state that a human must personally trigger every transition. An orchestrator that invokes the same authoritative backend commands does not violate it — it honors it.

2. **The "no automatic progression" decision is about work selection, not mechanical advance.** Verbatim (`.agents/archive/epics/02/decisions/decisions.0024.md`): *"M8 must make the transition from `Ready` to the next intentional execution frictionless but not automatic"*, *"`User selects milestone` remains an explicit act"*, *"the system … must not choose a milestone"*, *"M8 must expose state, not make decisions."* Deferred items are *"Automatic milestone selection / System-selected next milestone / Execution chaining."* The protected value is **the human chooses what to work on next** — which is exactly what the roadmap *also* protects: *"The purpose is not autonomy."*

**The bridge thesis follows directly.** The roadmap's destination is *humans govern at decision points; the system performs the mechanical coordination between them; nothing loses state.* The architecture has already built (a) **every stage**, (b) **every genuine governance control point** as an explicit, authoritative backend command, and (c) **durable, certified, per-domain recovery.** What audit-01 read as a wall between intent and reality is mostly a **conflation of two different authorities** that the founder and the architecture both agree on separately:

- **Governance / work-selection authority** → stays human. (Roadmap agrees: "not autonomy." Architecture agrees: "must not choose a milestone.")
- **Mechanical inter-stage progression** → the roadmap wants it automated *up to the next governance gate*; the architecture never actually forbade this — it only forbade the *frontend* mutating state and the *system choosing the next unit of work*.

Therefore the roadmap is **not** dead and does **not** require overturning a foundational value. It requires (i) re-shaping its "central engine" into a **non-authoritative coordination layer** that calls existing backend commands and pauses at existing governance commands, (ii) **renaming** its "governance gate" abstraction away from the occupied advisory term, and (iii) **inverting** its dependency order, because the supposed dependents (M4–M8) are the most mature parts of the system.

Everything below applies that thesis milestone by milestone.

---

## 1. Capability Preservation

The user-facing capability each milestone was trying to create, whether it is still needed, and what remains.

### Milestone 0 — Workflow Domain Foundation
Original Capability: a first-class, observable "workflow" object — a coordinated lifecycle a human can see and reason about as one thing.
Current Reality: six mature domain lifecycles (`RepositoryExecutionState`, `ExecutionSessionState`, `DecisionState`/`DecisionProposalState`/`DecisionReviewState`, `OperationalContextProposalStatus`/`OperationalContextReviewState`) plus a per-repository projection (`RepositoryProjectionService`). No single object spans them; `CommandCenter.Middle` is the only seam that bridges two domains.
Capability Status: **Partially Satisfied.**
Remaining Gap: a *coordinating view/object* that lets a human (or a progression layer) observe the whole cycle as one lifecycle. The lifecycles exist; the unifying lens does not.

### Milestone 1 — Workflow State Machine
Original Capability: enforced, valid-only progression through the stage chain.
Current Reality: enforced progression exists for the execution slice (`Ready → Executing → AwaitingAcceptance → Accepted → AwaitingCommit → AwaitingPush → Ready`) via explicit commands and service-contract guards; decision/context are separate validated machines.
Capability Status: **Partially Satisfied.**
Remaining Gap: only the *cross-domain* sequencing (what happens between the execution machine ending one phase and a decision/context phase beginning) is unmodeled. Each machine is sound on its own.

### Milestone 2 — Workflow Persistence & Recovery
Original Capability: state survives restart/failure/interruption with no loss.
Current Reality: pervasive and certified — `FileSystemExecutionSessionStore` + `ExecutionSessionRecoveryHostedService`, `FileSystemDecisionRepository`, `FileSystemReasoningRepository`, `FileSystemOperationalContextProposalStore`, `ExecutionHistory` projection.
Capability Status: **Fully Satisfied (per-domain).**
Remaining Gap: none for the capability. Only a *unified cross-domain history record* is absent, and it is better derived than stored.

### Milestone 3 — Governance Gate Framework
Original Capability: human authority modeled as explicit, auditable decision points with who/why/when.
Current Reality: present as concrete commands — accept/reject handoff, accept/edit/reject/promote operational context, resolve/supersede/archive decision, commit/push approval — each with resolver/reviewer metadata and timestamps.
Capability Status: **Partially Satisfied.**
Remaining Gap: only a *unifying abstraction/name* for "these are all governance gates." The function is complete; the concept is unnamed (and the obvious name, "governance," is taken — see §3).

### Milestone 4 — Execution Integration
Original Capability: execution managed as a governed lifecycle (context → launch → monitor → complete/fail).
Current Reality: delivered in full (Epic 02): `ExecutionContextService`, `ExecutionSessionService`, `ExecutionMonitoringService`, providers, failure/cancel, recovery host.
Capability Status: **Fully Satisfied.**
Remaining Gap: none.

### Milestone 5 — Handoff Integration
Original Capability: detect/validate/evaluate a handoff and gate it for human acceptance.
Current Reality: `HandoffService` + accept/reject gate with history (Epic 02 M4/M5).
Capability Status: **Fully Satisfied** (minus optional decision auto-emission, deliberately decoupled).
Remaining Gap: none required; an optional bridge to decision discovery is a *choice*, not a gap.

### Milestone 6 — Decision Integration
Original Capability: discover → generate → review → resolve decisions with human-held resolution.
Current Reality: over-delivered by two epics (05 + 07): candidate/option/tradeoff/recommendation generation, refinement, quality signals, advisory governance reporting, human-only resolution, influence projection, certification.
Capability Status: **Fully Satisfied (exceeds scope).**
Remaining Gap: none; the milestone under-describes reality.

### Milestone 7 — Operational Context Integration
Original Capability: propose → review → accept/edit/reject → promote continuity context.
Current Reality: built verbatim (Epic 03): `OperationalContextLifecycleService.PromoteOperationalContextAsync` with preconditions, archival, history.
Capability Status: **Fully Satisfied.**
Remaining Gap: none.

### Milestone 8 — Git Integration
Original Capability: commit/push as governed, gated lifecycle stages.
Current Reality: built verbatim (Epic 02 M6): `GitService`, `CommitPreparation`, commit-scope selection, push, folded into the execution state machine; commit/push are explicit gated actions.
Capability Status: **Fully Satisfied.**
Remaining Gap: none.

### Milestone 9 — Continuation Engine
Original Capability: stages advance *themselves* to the next governance gate so the human stops *operating* mechanical transitions and only *governs* decision points.
Current Reality: a **repeatable but human-stepped** loop exists; the mechanical hops between stages are still human button-presses. The capability has **not** been built — but, per the orientation, it was never actually forbidden; only *work-selection autonomy* was forbidden.
Capability Status: **Still Needed** (genuinely unbuilt, and still valid).
Remaining Gap: a non-authoritative progression layer that, after a stage reaches a terminal phase, calls the next authoritative backend command and halts at any genuine governance gate. The gates already exist to halt at.

### Milestone 10 — Certification
Original Capability: proof the whole loop progresses, gates hold, and it survives restart/failure.
Current Reality: restart/failure/authority/recovery certified per-domain (Epic 02 M8, 03 M8, 05 M9, 06 M8, 07 M10), the last even certifying whether human authorship was replaced by generation+governance.
Capability Status: **Partially Satisfied.**
Remaining Gap: only **cross-domain progression** certification — and the "humans don't operate" framing must be restated as "humans govern decision points; the system advances mechanical transitions" (see §7).

---

## 2. Architectural Leverage

How the emerged implementation accelerates the roadmap's destination.

### Milestone 0
Reusable Assets: the six domain lifecycle enums; `RepositoryProjectionService`; `ExecutionHistory`; `CommandCenter.Middle` (the only existing cross-domain seam).
What Can Be Eliminated: the premise that a new authoritative `Workflow`/`WorkflowState` domain must be *built first*.
What Should Be Reused: existing domain states as the **source of truth** that a coordination view *reads*, never replaces.

### Milestone 1
Reusable Assets: the execution-slice state machine and its guard rules; per-domain validated transitions.
What Can Be Eliminated: re-implementing transition validation for stages that already enforce it.
What Should Be Reused: each domain's own transition guards — a coordination layer sequences *between* machines, it does not re-own *within* them.

### Milestone 2
Reusable Assets: all `FileSystem*Store` repositories, `ExecutionSessionRecoveryHostedService`, `ExecutionHistory`, `.agents/*` artifacts.
What Can Be Eliminated: any new central workflow store.
What Should Be Reused: existing stores as the recovery substrate; derive cross-domain history as a **projection** over them.

### Milestone 3
Reusable Assets: every explicit command (handoff accept/reject, context accept/edit/reject/promote, decision resolve/supersede/archive, commit/push) and its who/why/when metadata.
What Can Be Eliminated: a parallel approval authority; the assumption that gates must be newly built.
What Should Be Reused: the existing commands *as* the gates — the abstraction merely catalogs and names them.

### Milestone 4 / 5 / 7 / 8
Reusable Assets: the complete Execution, Handoff, Continuity, and Git services and their state integration.
What Can Be Eliminated: all re-implementation; these are finished building blocks.
What Should Be Reused: these domains become the **operands** a progression layer calls — they are the engine's hands.

### Milestone 6
Reusable Assets: the entire two-epic Decisions stack, including advisory `DecisionGovernanceReport` (`Healthy/AdvisoryFindings/Blocked`) and `DecisionCertificationService`/`DecisionGenerationCertificationService`.
What Can Be Eliminated: the idea that decision integration is one unit of remaining work.
What Should Be Reused: the `Blocked` health signal as a **readable precondition** a progression layer consults before advancing past a decision stage.

### Milestone 9
Reusable Assets: the repeatable loop harness (`RepeatableExecutionLoopRebuildsContext…SurvivesRestart`), the explicit backend commands, the recovery host.
What Can Be Eliminated: the assumption that continuation requires new state authority.
What Should Be Reused: the *existing terminal-phase signals* of each stage as triggers, and the *existing commands* as the actions to invoke.

### Milestone 10
Reusable Assets: `DecisionCertificationService`, `DecisionGenerationCertificationService`, `ReasoningCertificationService`, and every epic certification milestone.
What Can Be Eliminated: a monolithic new certification suite.
What Should Be Reused: compose the existing per-domain certifications and add only a cross-domain-progression certification on top.

---

## 3. Semantic Intent

Whether the founder's meaning survives architectural drift.

### Milestone 0
Original Intent: make "workflow" the thing the system coordinates around.
Current Interpretation: the system coordinates around **domains with authority boundaries**; "workflow" is the *relationship between them*, not a new domain.
Intent Preserved: **Partially.** The coordination intent survives; the "new central domain" expression does not.

### Milestone 1
Original Intent: one enforced path through the work.
Current Interpretation: several enforced paths that need *linking*, not replacing.
Intent Preserved: **Partially.**

### Milestone 2
Original Intent: never lose state.
Current Interpretation: state is never lost — already true.
Intent Preserved: **Yes.**

### Milestone 3
Original Intent: model where the human exercises authority.
Current Interpretation: those points are modeled as explicit commands; "governance" the *word* now means **advisory, non-mutating health analysis** (`IDecisionGovernanceService`, `DecisionHealthAssessment`).
Intent Preserved: **Yes for the concept; No for the vocabulary.** The gate concept must adopt a different name to avoid colliding with the occupied advisory meaning.

### Milestone 4
Original Intent: execution is a governed lifecycle.
Current Interpretation: it is — and it self-orchestrates its slice.
Intent Preserved: **Yes** (hierarchy inverted, intent intact).

### Milestone 5
Original Intent: handoff is gated by a human.
Current Interpretation: it is.
Intent Preserved: **Yes.**

### Milestone 6
Original Intent: decisions are generated and human-resolved.
Current Interpretation: delivered well beyond the description.
Intent Preserved: **Yes.**

### Milestone 7 / 8
Original Intent: context and git are governed, gated lifecycles.
Current Interpretation: built verbatim.
Intent Preserved: **Yes.**

### Milestone 9
Original Intent: the human stops doing *mechanical* stage-advancing and only does *judgment*; explicitly **"not autonomy."**
Current Interpretation: audit-01 read this as "remove human operation," which collides with "humans choose the work." But the founder's own words exclude autonomy and target *mechanical* progression only. Re-read correctly, the intent is **advance the machinery between governance gates**, not choose the work.
Intent Preserved: **Yes** — once separated from the work-selection autonomy it never claimed.

### Milestone 10
Original Intent: certify that the human's role became review/approval/resolution.
Current Interpretation: certify authority preservation + recovery + assisted progression; drop the absolutist "humans don't operate" phrasing, which over-reaches the founder's own non-autonomy stance.
Intent Preserved: **Partially** (intent intact; one phrase over-states it).

---

## 4. Architecture Evolution

Where the *current* architecture is an implementation artifact that should be allowed to evolve. Current architecture is a tool, not a destination.

### Milestone 0
Current Architecture: no cross-domain object; coordination is implicit/manual.
Limitation: nothing can observe or reason about "the whole cycle" — including a human.
Recommended Evolution: introduce a **non-authoritative coordination projection** that reads domain states. This is an *additive* evolution, not a rewrite, and does not threaten domain authority.

### Milestone 1
Current Architecture: validated machines that do not know about each other.
Limitation: the seams between machines are unmodeled, so progression across them is manual by omission, not by principle.
Recommended Evolution: model the **inter-machine seams** explicitly (terminal-phase → next-domain-entry) as first-class transitions owned by a coordinator that *calls* domains.

### Milestone 2
Current Architecture: per-domain stores; no unified history.
Limitation: no single timeline of "the cycle."
Recommended Evolution: a **derived projection** timeline; do not promote it to an authoritative store.

### Milestone 3
Current Architecture: "governance" denotes advisory analysis; control points are unnamed commands.
Limitation: the absence of a shared name for the control points makes them invisible as a *set*, and the word that should describe them is taken.
Recommended Evolution: name the control-point set with a **distinct term** (e.g. "approval gate" / "decision gate") and treat existing commands as its members. Do not overload "governance."

### Milestone 4 / 5 / 7 / 8
Current Architecture: self-contained, self-orchestrating domains.
Limitation: none individually; collectively they lack a caller that sequences them.
Recommended Evolution: leave the domains intact; add a caller *above* them. The domains should **not** evolve; the gap is the layer that isn't there.

### Milestone 6
Current Architecture: rich decisions domain with advisory `Blocked` health.
Limitation: `Blocked` is computed but nothing consumes it as a progression precondition.
Recommended Evolution: let a progression layer **read** `DecisionHealthAssessment` to decide whether to halt — advisory stays advisory, but becomes *consulted*.

### Milestone 9
Current Architecture: every mechanical hop is a human button calling a backend command.
Limitation: this is the **artifact**, not the value. The certified value is "backend commands are authoritative" — satisfied equally by a coordinator calling them. Requiring a *human finger* on each mechanical hop is incidental, not principled.
Recommended Evolution: a coordinator that **calls the same authoritative backend commands** for mechanical transitions and **halts at governance gates and at work-selection**. This honors backend authority and the no-work-autonomy value simultaneously.

### Milestone 10
Current Architecture: per-domain certification; UI-characterized "explicit action" tests assume a human caller.
Limitation: the characterization tests encode *today's UI behavior*, not a permanent requirement.
Recommended Evolution: re-characterize "explicit action" as "explicit, authoritative, audited invocation" — satisfiable by an approved coordinator — and certify progression across the seam.

---

## 5. Hidden Progress

How much of each milestone is already done indirectly.

### Milestone 0
Already Achieved: all domain lifecycle modeling; a repository projection; one cross-domain seam (`Middle`).
Still Required: the coordination lens itself.
Estimated Remaining Scope: **Reduced.**

### Milestone 1
Already Achieved: validated execution-slice machine; per-domain validated transitions.
Still Required: seam transitions between machines.
Estimated Remaining Scope: **Reduced.**

### Milestone 2
Already Achieved: durable state + restart recovery + audit history, certified.
Still Required: at most a derived unified timeline.
Estimated Remaining Scope: **Minimal.**

### Milestone 3
Already Achieved: every control point + metadata.
Still Required: a named catalog/abstraction (non-"governance").
Estimated Remaining Scope: **Minimal.**

### Milestone 4 / 5 / 7 / 8
Already Achieved: all of it.
Still Required: nothing (other than being *called* by a coordinator).
Estimated Remaining Scope: **Minimal / none.**

### Milestone 6
Already Achieved: two epics' worth, exceeding scope, including advisory `Blocked`.
Still Required: nothing within scope.
Estimated Remaining Scope: **Minimal.**

### Milestone 9
Already Achieved: a restart-safe repeatable loop; all commands to call; all gates to halt at.
Still Required: the thin coordinator that triggers mechanical hops and halts at gates.
Estimated Remaining Scope: **Reduced** (the hard parts — stages, persistence, gates, recovery — are done; the connective layer is what remains, and it is small *because* everything it calls exists).

### Milestone 10
Already Achieved: restart/failure/authority/recovery certification per-domain; a "is authorship replaced?" executive report pattern.
Still Required: cross-domain progression certification, composed from existing services.
Estimated Remaining Scope: **Reduced.**

---

## 6. Roadmap Transformation

How the roadmap's implementation framing should change (not a new plan — a transformation of stance).

### Milestone 0
Original Strategy: build the foundation domain first; everything depends on it.
Recommended Strategy: treat it as a **capstone coordination projection** over finished domains, added last, owning no state.
Reasoning: the dependents already exist; a "foundation" built now would be a retrofit and a competing authority.

### Milestone 1
Original Strategy: one linear engine owns all transitions.
Recommended Strategy: a **sequencer of authority handoffs** between existing machines.
Reasoning: transitions already have owners; the gap is only the seams.

### Milestone 2
Original Strategy: build central workflow persistence.
Recommended Strategy: **project**, don't persist anew.
Reasoning: durable recovery already exists per-domain; a second store duplicates and risks divergence.

### Milestone 3
Original Strategy: build a `GovernanceGate` framework.
Recommended Strategy: **catalog existing commands** under a new, non-colliding name.
Reasoning: the function exists; only the concept and a safe label are missing.

### Milestone 4 / 5 / 6 / 7 / 8
Original Strategy: integrate each domain *into* an engine.
Recommended Strategy: **recognize as complete building blocks**; reframe "integration" as "being callable by a coordinator."
Reasoning: there is no engine to integrate into; the domains are the most mature part of the system.

### Milestone 9
Original Strategy: build an auto-advance engine that replaces human button-clicks across the board.
Recommended Strategy: build a **bounded mechanical-progression coordinator** that advances *between* governance gates by calling authoritative backend commands, and that never selects work and never overrides a gate.
Reasoning: this is the real founder intent, it honors both protected values (backend authority; human work-selection), and it is small because its operands exist.

### Milestone 10
Original Strategy: a new monolithic end-to-end certification proving "humans don't operate."
Recommended Strategy: **compose** existing per-domain certifications and add a seam-progression + gate-enforcement + recovery certification; certify "humans govern decision points," not "humans don't operate."
Reasoning: most criteria are already certified; only the cross-domain criterion and a corrected success phrase are missing.

---

## 7. Founder Intent Alignment (highest priority)

For each milestone: does the current architecture advance the founder's intended *outcome*, or merely preserve the current *implementation*? When intent conflicts with architecture, intent wins unless the capability itself is invalid.

### Milestone 0
Founder Intent: the workflow becomes a managed, observable lifecycle — a first-class object of attention.
Current Architecture Alignment: **Medium.** The lifecycles exist but cannot be observed as one.
Adjustment Required: add a non-authoritative coordination lens.
Reasoning: the *capability* (observe/coordinate the whole cycle) is valid; only the *mechanism* (a new owning domain) is wrong. Intent wins; mechanism evolves.

### Milestone 1
Founder Intent: work progresses along a valid, enforced path.
Current Architecture Alignment: **Medium-High** within domains, **Low** across them.
Adjustment Required: model the inter-domain seams.
Reasoning: intent is satisfied locally; the cross-domain path is the unbuilt remainder.

### Milestone 2
Founder Intent: the system never loses progress.
Current Architecture Alignment: **High.**
Adjustment Required: none (optionally a derived timeline).
Reasoning: fully advanced already.

### Milestone 3
Founder Intent: human authority is explicit, located, and auditable.
Current Architecture Alignment: **High in function, Low in nameability.**
Adjustment Required: name the gate-set without reusing "governance."
Reasoning: the architecture *advances* the intent; only the vocabulary betrays it.

### Milestone 4 / 5 / 7 / 8
Founder Intent: each stage is a governed lifecycle with human gates.
Current Architecture Alignment: **High.**
Adjustment Required: none but reframing.
Reasoning: intent fully realized; architecture advances the outcome, not merely itself.

### Milestone 6
Founder Intent: decisions are produced for the human, resolved by the human.
Current Architecture Alignment: **High** (exceeds intent), with human-only resolution certified.
Adjustment Required: none.
Reasoning: outcome surpassed.

### Milestone 9
Founder Intent: the founder performs *Review / Approval / Resolution*; the system performs *Execution / Coordination / Progression / Recovery* — explicitly **"not autonomy."**
Current Architecture Alignment: **Low — but the misalignment is mechanical, not principled.** The architecture currently makes the human perform mechanical progression too; that preserves *current implementation*, not founder *outcome*.
Adjustment Required: introduce bounded mechanical progression to governance gates via authoritative backend commands; keep work-selection and gate-resolution human.
Reasoning: **Founder intent wins.** There is no strong evidence the capability is invalid — on the contrary, the only documented objection (Epic 02) targets *work-selection autonomy*, which the founder also rejects. The architecture's protected value (backend authority) is *preserved*, not breached, by a coordinator calling backend commands. Audit-01's "must reverse a certified decision" overstates the case: nothing certified requires a human finger on mechanical hops; it requires backend authority and human work-selection, both retained.

### Milestone 10
Founder Intent: prove the founder's role became governance, and that the system survives restart/failure while gates hold.
Current Architecture Alignment: **Medium.** Survival/authority/recovery proven; "role became governance" not yet, and the original "humans don't operate" phrasing slightly over-reaches the founder's own non-autonomy stance.
Adjustment Required: certify cross-domain progression + gate enforcement; restate success as "humans govern decision points and select work; the system advances mechanical transitions and recovers."
Reasoning: intent is reachable by composition; only the absolutist phrasing needs softening to match the founder's *own* "not autonomy."

---

# Audit-02 Summary

## Capabilities Already Achieved
- Durable state + restart/failure recovery, certified per-domain (M2).
- Full execution lifecycle that self-orchestrates its slice (M4).
- Handoff detection/validation + human acceptance gate (M5).
- Decision generation→review→human resolution, exceeding scope by ~two epics, with advisory health and certification (M6).
- Operational-context propose→review→accept/edit/reject→promote, verbatim (M7).
- Commit/push as gated lifecycle stages inside the execution machine (M8).
- Per-domain certification of recovery, failure, and authority boundaries (most of M10).

## Capabilities Partially Achieved
- A unifying lifecycle *view* over the domains (M0) — lifecycles done, lens missing.
- Cross-domain *sequencing* (M1) — machines done, seams missing.
- A named *gate abstraction* (M3) — gates done as commands, name/catalog missing.
- *Cross-domain progression certification* (M10) — per-domain done, seam unproven.

## Capabilities Still Missing
- A bounded **mechanical-progression coordinator** that advances between governance gates by calling authoritative backend commands (M9) — the roadmap's true remaining intent, and still valid.
- A non-authoritative cross-domain **coordination projection / timeline** (M0/M1/M2 unified view).

## Architectural Assets To Reuse
- The six domain lifecycle state models and their transition guards.
- All `FileSystem*Store` repositories + `ExecutionSessionRecoveryHostedService` + `ExecutionHistory` (recovery substrate / projection source).
- Every explicit human command (handoff, context, decision, commit, push) as the **gate set**.
- `DecisionHealthAssessment` (`Healthy/AdvisoryFindings/Blocked`) as a **consulted progression precondition**.
- `CommandCenter.Middle` (`OperationalContextGenerationService`) as the existing cross-domain seam to generalize from.
- `DecisionCertificationService`, `DecisionGenerationCertificationService`, `ReasoningCertificationService` as composable certification.
- The `RepeatableExecutionLoop…SurvivesRestart` harness as the progression test scaffold.

## Architectural Constraints That Can Be Relaxed
- **"Every transition requires a human button-press."** This is an *implementation artifact* of the current UI, not a certified value. It can be relaxed to "every transition is an authoritative, audited backend invocation."
- **The "no automatic progression" decision, narrowly.** It forbids *work selection / milestone choice / execution chaining* — not mechanical advance to the next gate. Mechanical progression can proceed without overturning it.
- **The "humans don't operate stages" success phrasing.** Over-reaches the founder's own "not autonomy"; can be relaxed to "humans govern decision points and select work."

## Architectural Constraints That Must Remain
- **Backend commands are authoritative for state transitions** (frontend/coordinator may invoke, never bypass).
- **Humans choose what to work on next** (no system-selected milestones, no execution chaining) — founder *and* architecture agree.
- **Advisory layers (`DecisionGovernanceService`, Reasoning) never mutate, resolve, or override.**
- **Decision resolution and gate outcomes are human-only.**
- **Each domain remains the single authority over its own state** — any coordinator reads/calls, never duplicates state.
- **The word "governance" keeps its advisory meaning** — the gate concept must use a different name.

## Highest Leverage Existing Systems
1. The **Execution domain** — already realizes context→execution→acceptance→commit→push as a validated, persistent, restart-safe loop; it is the natural spine for progression.
2. The **explicit command set** — already the complete governance-gate inventory.
3. The **per-domain certification services** — already the composable proof of M10.
4. **`CommandCenter.Middle`** — the one existing cross-domain seam; the prototype for a coordination layer.

## Most Important Founder Intent Signals
- *"The purpose is not autonomy."* — the founder never wanted work autonomy; this dissolves most of audit-01's conflict.
- *"Move humans to approval, review, and resolution."* — the target role, already partially realized.
- *"Humans should govern workflow stages,"* not own them — gates, not operation.
- M9's auto-advance list is **Execution/Handoff/Decision/Context/Commit/Push** — *mechanical pipeline stages*, never "choose the next milestone."

## Most Important Architectural Drift Discoveries
- **No central orchestrator exists** — coordination is distributed across self-contained domains; `Program.cs` wires independents.
- **Roadmap vocabulary is absent from code** (`Workflow*`, `GovernanceGate`, `StateMachine`, `Continuation`, `AwaitingGovernance`, `AutoAdvance`, `orchestrat*` → zero matches).
- **"Governance" is occupied** by an advisory, non-mutating meaning (`DecisionHealthAssessment`).
- **The dependency order is inverted** — M4–M8 are the most mature, certified parts and predate any "foundation."
- **The two "blocking" doctrines are narrower than audit-01 framed them**: the closure matrix protects *backend authority*, and Epic 02 protects *work selection* — neither forbids mechanical progression by an authoritative coordinator.
- A whole **Reasoning domain (Epic 06)** exists, deliberately non-authoritative — the closest thing to a cross-cutting substrate, and pointedly *not* an orchestrator.

## Most Important Roadmap Adjustments Required
- **Invert the order:** start from the finished domains; treat any unifying layer as a capstone, not a foundation.
- **Re-shape M0–M2 from "central authoritative engine/store" to "non-authoritative coordination projection over existing domain state."**
- **Rename M3's gate abstraction** away from "governance"; populate it with the existing explicit commands rather than building new authority.
- **Re-scope M9 from "auto-advance everything" to "bounded mechanical progression between governance gates, via authoritative backend commands, halting at gates and at work-selection"** — and recognize it as *valid, wanted, and largely unblocked*, contrary to audit-01's "must reverse a certified value."
- **Recompose M10** from existing per-domain certifications + a seam-progression test; restate the success criterion to match the founder's own non-autonomy.
- **Recognize M4–M8 as done**, reframed as the operands a coordinator calls.
