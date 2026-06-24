# audit-02.md — Roadmap Recovery & Architectural Reconciliation

> Bridge between architecture archaeology and roadmap regeneration.
> Reality wins. The repository is authoritative; `.agents/roadmap.md` is intent; `audit-01.md` is evidence.
> Generated 2026-06-24 against branch `dev` @ `050f76d`.
> Question answered: **given what now exists, what roadmap should exist?** — not *how do we finish the old plan?*

---

# Executive Summary

## What the roadmap intended

`.agents/roadmap.md` ("Session Economics & Continuity Infrastructure Roadmap") sets out to **recover the
responsibilities originally carried by the Decision Session Registry and Session Router** — recast as
*session economics* and *continuity infrastructure* in service of workflow replacement. Across eleven
milestones (M0–M10) it intends to build: an economics domain, an authoritative multi-category session
registry, session observability, a continuity cost model, session **routing** (reuse/replace/continue/
create), continuity **transfer**/bootstrap, efficiency analysis, routing diagnostics, workflow
integration, economics governance, and end-to-end certification.

## What reality produced

Two facts dominate every finding below:

1. **The roadmap's founding premise is historically false.** There was never a "Decision Session
   Registry" or "Session Router" in code — on any branch, in any commit. Pickaxe search
   (`git log --all -S`) returns **zero** source hits for `SessionRouter`, `DecisionSessionRegistry`,
   `SessionRegistry`, `SessionEconomics`, `ContinuityCost`, or `ContinuityRouter`. Worse for the
   "recovery" framing: **session routers and session registries were explicitly declared *non-goals***
   in two prior epic plans (`.agents/archive/epics/03/plan.md:176-180`,
   `.agents/archive/epics/06/plan.md:919-923`), grouped with "session reuse" as things the project
   consciously decided **not** to build. This roadmap is not *recovering* lost capability — it is
   *proposing new capability under a recovery narrative*.

2. **A real, adjacent baseline already exists — and it is timestamps-only.** The genuine session
   subsystem is `ExecutionSession` (Epic 02): identity, a 5-state lifecycle, a file-backed store, and
   startup recovery. But it records **only timestamps and a computed `Duration`** — no token usage, no
   age, no availability, no cost. And it does **not route**: `ExecutionSessionService.StartAsync`
   enforces a *hard guard* — one active session per repository, else it throws — which is the precise
   opposite of the reuse/replace/transfer policy the roadmap's M4 wants.

## What changed

The roadmap was written as if it were the next step after `audit-01.md`. It is not. `audit-01.md`
recommended a **surfacing** roadmap for the already-built Workflow engine (transport → UI workspace →
workspace summary). The Session Economics roadmap instead opens a **second, deeper-infrastructure
frontier** that is ~90% net-new, premised on a capability that was historically rejected, and which
**collides with Execution's session-admission authority**. The project now has two competing futures:
finish surfacing the engine that is done, or build an economics/routing stack that is barely begun.

## What must happen next

**Revise the roadmap onto reality.** Keep the genuinely valuable and genuinely missing parts (session
*economics* and *observability* — cheap, high-value, and buildable on existing infrastructure), drop
the mythical "recovery" framing, and **quarantine the routing/transfer half behind an explicit
Execution-authority precondition** because it cannot be added as a pure observation layer. Do not let
this roadmap silently cancel the workflow-surfacing frontier audit-01 identified; the two must be
sequenced deliberately, not by accident.

---

# Capability Reconciliation

Reconciliation verdicts: **Satisfied · Partially Satisfied · Unsatisfied · Superseded · Replaced.**
Future direction: **Preserve · Expand · Replace · Remove.**

## Capability — Session Economics Domain (roadmap M0)

### Original Intent
Establish an economics domain: `SessionEconomics`, `ContinuityEconomics`, `ContinuityCost`,
`ContinuityBenefit`, `ContinuityDecision`, with metrics for cost, latency, reuse opportunity, context
size, lifecycle duration.

### Current Reality
**None of these types exist.** `DecisionCost` (`src/CommandCenter.Decisions/Models/DecisionCost.cs`)
is *decision-option* economics, unrelated to sessions. The only token counting anywhere is a word
tokenizer in `src/CommandCenter.Continuity/Services/UnderstandingCompressionService.cs:346`. No
session-cost, session-benefit, or economics decision type is present.

### Reconciliation
**Unsatisfied.**

### Future Direction
**Expand** — but build it as a *derived/observational* domain over `ExecutionSession`, not a new
authority. The valuable primitives (lifecycle duration, context size, reuse opportunity) are derivable
from data that already exists or nearly exists.

### Notes
This is the cleanest, highest-value, lowest-risk part of the entire roadmap: it has no historical
baggage and no authority conflict.

---

## Capability — Session Registry (roadmap M1)

### Original Intent
An authoritative registry tracking session identity, type, state, lifetime, ownership, across
**Execution**, **Decision**, and **future** session categories, with persistence.

### Current Reality
A *single-category* registry exists for execution sessions: `IExecutionSessionService` +
`FileSystemExecutionSessionStore` (`src/CommandCenter.Execution/...`) persist `ExecutionSession[]` as
one JSON file (`%AppData%/CommandCenter/execution-sessions.json` or
`COMMAND_CENTER_EXECUTION_SESSIONS_PATH`), with identity, a 5-value `ExecutionSessionState`
(`Created/Executing/Completed/Failed/Cancelled`), ownership-by-`RepositoryId`, and timestamps.
**Decision sessions do not exist** — the only `DecisionSession*`-shaped references are foreign keys
(`Guid ExecutionSessionId`) on decision-influence types. There is no generic multi-category registry
and no first-class "registry" abstraction (it is a flat list store).

### Reconciliation
**Partially Satisfied** (execution category only; not a generalized registry).

### Future Direction
**Preserve and Expand** — treat `ExecutionSession` + its store as the registry baseline. Generalize
*only if* a second session category proves real; do not invent "decision sessions" speculatively
(Epics 03/06 already rejected them).

### Notes
The roadmap's "Session Identity/Type/State/Lifetime/Ownership" list is ~80% already modeled on
`ExecutionSession`. "Type" and a registry-of-registries abstraction are the only genuinely missing
pieces — and may be premature.

---

## Capability — Session Observability (roadmap M2)

### Original Intent
Make session economics visible: age, duration, usage, lifecycle state, activity — with current,
historical, and lifecycle-history reporting.

### Current Reality
Lifecycle observability **largely exists**: `IExecutionSessionService` exposes
`GetRepositorySessionSummaryAsync`, `GetRepositorySessionHistoryAsync`, `GetSessionAsync`;
`src/CommandCenter.Middle/.../RepositoryProjectionService.cs` projects `ActiveExecutionSession` +
`ExecutionSummary` + `ExecutionHistory` into dashboard/workspace read models; the UI renders
`features/execution/ExecutionSessionPanel.tsx` + `ExecutionHistoryPanel.tsx` (state, timestamps,
**duration**). What is **absent**: **age**, **token usage**, and **activity** — those fields do not
exist on `ExecutionSession`/`ExecutionSessionSummary`, so nothing downstream can show them.

### Reconciliation
**Partially Satisfied** (lifecycle/duration/history yes; age/usage/activity no).

### Future Direction
**Expand** — the projection→Middle→UI pipe is built and proven; adding age/usage/activity is an
incremental field addition through an existing seam, not new architecture.

### Notes
This milestone is "1 schema change + 3 surfaces already wired," not a greenfield build. Strong ROI.

---

## Capability — Continuity Cost Model (roadmap M3)

### Original Intent
Understand continuity cost: context growth, context size, decision-context cost, operational-context
cost, workflow cost; with cost/growth/usage trends.

### Current Reality
A **context-size proxy exists**, mislabeled elsewhere: the Continuity domain measures
operational-context document growth — `ContinuityDiagnosticsService`, `UnderstandingCompressionService`
(`CompressionTrend`, byte/revision growth), surfaced in `features/continuity/ContinuityDiagnosticsPanel.tsx`.
This covers "context growth/size" for *operational-context documents*. There is **no cost *model*** —
no monetary/token cost, no decision-context vs operational-context vs workflow cost decomposition.

### Reconciliation
**Partially Satisfied** (context growth/size for op-context docs; the cost *model* itself unsatisfied).

### Future Direction
**Expand** — reuse `ContinuityDiagnostics` + `CompressionTrend` as the growth half; add a true cost
model only once economics primitives (M0) exist. Beware double-counting: "continuity cost" in the
roadmap conflates *document growth* (exists) with *session token cost* (absent).

### Notes
The roadmap treats "continuity cost" as one thing; reality splits it into a **document-growth**
concern (Epic 03, built) and a **session-token** concern (absent). Plan them separately.

---

## Capability — Session Routing Infrastructure (roadmap M4)

### Original Intent
Reintroduce routing: decide reuse / replacement / continuation / creation / transfer from age, usage,
availability, cost, continuity state.

### Current Reality
**Absent, and actively contradicted by existing policy.** `ExecutionSessionService.StartAsync`
(`src/CommandCenter.Execution/Services/ExecutionSessionService.cs:~147`) *refuses* a second active
session per repository (`"Repository already has an active execution session."`). `ReplaceSessionAsync`
is a list upsert-by-Id for state transitions — **not** a reuse-vs-replace *policy*. There is no router,
no reuse decision, no transfer. Historically, "session routers" were a **declared non-goal**
(epics 03/06).

### Reconciliation
**Unsatisfied** (and **Superseded by a deliberate anti-routing guard**).

### Future Direction
**Replace the framing / Defer the build.** Routing cannot be a pure observation layer — it requires
changing Execution's *session-admission authority* (today: at most one active session). That is a
cross-domain authority change, the single largest architectural tension in this roadmap. Gate it
behind an explicit Execution-domain decision; do not plan it as a workflow-style derived projection.

### Notes
This is where "recovery" is most misleading: there is nothing to recover, the closest thing in code is
a guard that forbids the very behavior routing requires, and the concept was previously rejected.

---

## Capability — Continuity Transfer Infrastructure (roadmap M5)

### Original Intent
Context transfer, decision transfer, continuity transfer, bootstrap; with transfer cause/result/
metadata diagnostics.

### Current Reality
**Absent.** No transfer or bootstrap types exist in any domain. (The Continuity domain *promotes*
operational-context proposals to authoritative — `OperationalContextLifecycleService` — but that is
document promotion within one repository, not session/continuity transfer across sessions.)

### Reconciliation
**Unsatisfied.**

### Future Direction
**Defer** — depends entirely on routing (M4) existing first; meaningless without a multi-session world.

### Notes
Transfer presupposes that sessions can be replaced/superseded — which today they cannot. Strictly
downstream of the M4 authority decision.

---

## Capability — Continuity Efficiency Analysis (roadmap M6)

### Original Intent
Analyze transfer/reuse/replacement/bootstrap frequency; efficiency/cost/continuity reports.

### Current Reality
**Absent** for sessions. (Continuity *reporting* exists — `ContinuityReportService`,
`Models/ContinuityReport.cs`, `ContinuityTrend.cs` — but reports on operational-context documents, not
session reuse/transfer frequency.)

### Reconciliation
**Unsatisfied** (reporting *mechanism* exists and is reusable; the *subject matter* does not exist).

### Future Direction
**Defer / Reuse** — when M4/M5 exist, reuse the `*ReportService` pattern (Continuity and Workflow both
have one) rather than inventing a new reporting stack.

### Notes
Downstream of M4/M5. Its only present asset is a report-generation *pattern* to copy.

---

## Capability — Routing Diagnostics (roadmap M7)

### Original Intent
Explain every routing outcome: decision, reason, inputs, consequences.

### Current Reality
**Absent for routing** — but the **explainability pattern is fully built and proven elsewhere**:
`WorkflowInfluenceTrace` + `WorkflowProjectionDiagnostics` + `WorkflowHealthService`
(`src/CommandCenter.Workflow/...`), and `WorkflowGateEvidence` already model
decision→reason→inputs→consequences for workflow gates. The Decisions domain has
`DecisionInfluenceTrace` for the same shape.

### Reconciliation
**Unsatisfied capability / Already-solved mechanism.**

### Future Direction
**Reuse** — when routing exists, model its diagnostics on `WorkflowInfluenceTrace`/`WorkflowGateEvidence`.
Do not design a new diagnostics vocabulary.

### Notes
Strong reuse seam. The "explain a decision with inputs and consequences" problem is solved twice
already in this repo.

---

## Capability — Workflow Integration (roadmap M8)

### Original Intent
Expose session state, routing state, transfer state, continuity state to workflow orchestration.

### Current Reality
**The integration seam already exists and carries session + continuity state today.** Workflow injects
`IExecutionSessionService` in `WorkflowExecutionService`, `WorkflowGitService`, `WorkflowHandoffService`,
`WorkflowPreparationService` and consumes `GetRepositorySessionSummaryAsync` (state/`RepositoryState`/
`SessionId`); `WorkflowOperationalContextService` consumes Continuity proposals. What is **not** exposed
is **routing/transfer/economics state — because those do not exist yet.**

### Reconciliation
**Partially Satisfied** (session + continuity integration done; routing/transfer/economics integration
pending their own existence).

### Future Direction
**Preserve and Expand** — the wiring pattern is proven; feed it new state as M0/M4/M5 produce it. No
new integration architecture required.

### Notes
This milestone is *mechanism-complete*; it is blocked only by the non-existence of the state it would
carry. It should not be planned as new work — only as "extend an existing injection."

---

## Capability — Economics Governance (roadmap M9)

### Original Intent
Identify expensive/inefficient/high-cost/low-value workflows; generate economics/efficiency/governance
reports.

### Current Reality
**Absent for economics** — but governance reporting is a **solved pattern**: `WorkflowReportService`
already emits `HumanGovernanceReport`, `WorkflowProgressionReport`, `WorkflowReadinessReport`,
`RepositoryWorkflowReport`.

### Reconciliation
**Unsatisfied capability / Already-solved mechanism.**

### Future Direction
**Reuse** — extend the existing report service family with an economics report; do not build a parallel
governance stack.

### Notes
Downstream of M0–M3 producing economics data to govern.

---

## Capability — Infrastructure Certification (roadmap M10)

### Original Intent
Certify the registry→observability→cost→routing→transfer→workflow chain; failure-test restart /
provider failure / workflow failure / transfer failure; certify long-horizon economics.

### Current Reality
**Absent for economics** — but the repo has a **deep, repeated certification capability**:
`WorkflowCertificationService` + end-to-end fixture (restart/idempotency/recovery),
`IReasoningCertificationService`, decision lifecycle certification, and the archived long-horizon
certification milestones (`epics/03/.../m8-long-horizon-certification.md`, etc.). Recovery-first
startup with **per-repository error isolation** is built in both Workflow and Execution
(`WorkflowRecoveryHostedService`, `ExecutionSessionRecoveryHostedService`). All certification evidence
is **runtime-only** (no committed artifacts), consistent with audit-01.

### Reconciliation
**Unsatisfied subject / Already-solved mechanism (strong).**

### Future Direction
**Reuse** — model economics certification on `WorkflowCertificationService` + the end-to-end fixture
pattern; reuse the recovery hosted-service + fingerprint-idempotency scaffolding for failure testing.

### Notes
The single biggest reuse opportunity in the roadmap. Certification, recovery, and idempotency are
solved infrastructure here, not greenfield.

---

# Architectural Reconciliation

## Discovery — Session lifecycle authority lives in Execution, and forbids routing

### Original Assumption
A "Session Router" can route reuse/replace/transfer of sessions as an infrastructure/observation layer.

### Actual Reality
`ExecutionSession` is owned by the Execution domain, which **admits at most one active session per
repository** and throws on a second. Session admission is an *authoritative domain policy*, not a
derivable projection.

### Impact
Routing (M4) and transfer (M5) cannot be implemented in the additive, domains-own-truth style that the
Workflow engine used. They require **changing Execution's authority**, which the architecture's own
rules make a deliberate, reviewed act.

### Roadmap Consequence
Routing/transfer must be planned as a **cross-domain authority change with an Execution-domain owner
and decision**, sequenced *after* economics/observability — never as a silent infrastructure layer.

## Discovery — "Continuity" is document continuity, not session continuity

### Original Assumption
Continuity infrastructure is about session/context transfer and continuity economics.

### Actual Reality
`CommandCenter.Continuity` (Epic 03) is **operational-context document management**
(generate/review/promote/diff/compress) + decision analysis + diagnostics/reporting. It references only
`CommandCenter.Core`, deliberately not Execution.

### Impact
The roadmap's "continuity cost / continuity transfer / continuity efficiency" reads the word
"continuity" as session-continuity, but the codebase's Continuity domain is about *understanding
documents*. These are different problems sharing a name.

### Roadmap Consequence
Disambiguate the term. "Context growth/size" maps to the existing Continuity diagnostics; "session
token cost / reuse / transfer" is a new session-economics concern. Planning them as one capability will
cause double-counting and false reuse claims.

## Discovery — Reasoning is decision-provenance, not "Brainstorm/continuity-research"

### Original Assumption
The roadmap fears the system "evolving into a continuity-research or Brainstorm-oriented initiative."

### Actual Reality
`CommandCenter.Reasoning` (Epic 06) is a **decision-reasoning provenance/traceability graph** (events →
threads → relationships → graph → reconstruction), requiring `UserSupplied`/`ManualCapture` provenance.
It depends on Core only — not Continuity, not Execution. Zero hits for "brainstorm" / "exploration" /
"continuity research."

### Impact
The roadmap's stated fear is aimed at a domain that does not embody it. Reasoning is disciplined
provenance, not open-ended ideation.

### Roadmap Consequence
Drop the defensive "don't become Brainstorm" framing as a misread of the codebase. If anything,
Reasoning's event/graph/reconstruction model and certification are *reuse assets*, not a threat.

## Discovery — The derived-layer scaffold is a reusable asset, repeated verbatim

### Original Assumption
Economics, routing, transfer, certification are each separate engineering efforts.

### Actual Reality
The Workflow engine already unifies a single `evaluate → persist → recover → certify → act` scaffold
(lifted verbatim from continuation into preparation per audit-01), one canonical fingerprint
idempotency key, recovery-first startup with per-repository isolation, projection→Middle→UI surfacing,
and a `*ReportService` family.

### Impact
Most of M2/M6/M7/M8/M9/M10's *mechanisms* already exist as proven, reusable patterns.

### Roadmap Consequence
The economics layer should be assembled mostly from existing scaffolds; net-new design is concentrated
in M0 (economics primitives) and M4 (routing authority).

## Discovery — The audit-01 surface gap is still entirely open

### Original Assumption
(Implicit) This roadmap is the agreed next step.

### Actual Reality
audit-01's identified frontier — Tauri bridge workflow commands (0/27), dedicated workflow UI
workspace, `RepositoryWorkflowSummary` in Middle — remains **0% built**. `RepositoryWorkflowSummary` is
still absent from `src` entirely.

### Impact
The project is poised to start a second, harder infrastructure track while the first track's
nearly-done engine remains unreachable by any human.

### Roadmap Consequence
The next roadmap must make an explicit, conscious choice between (or sequencing of) **surfacing the
done engine** vs **building the economics stack** — not drift into the latter by default.

---

# Completed Capability Recovery

Treat these as **completed baseline**; do not re-plan them.

| Capability | Evidence | Why effectively complete |
|---|---|---|
| Execution session lifecycle + identity + ownership + state machine | `ExecutionSession.cs`, `ExecutionSessionState.cs`, `ExecutionSessionService.cs` | Full create/accept/reject/commit/push/query lifecycle, shipped & used (Epic 02). Satisfies most of M1's "identity/state/lifetime/ownership." |
| Session persistence + startup recovery | `FileSystemExecutionSessionStore.cs`, `ExecutionSessionRecoveryHostedService.cs` | Durable JSON store + recovery hosted service. Satisfies M1 persistence and much of M10 failure-recovery. |
| Session lifecycle observability (state/duration/history) | `GetRepositorySessionSummaryAsync`/`GetRepositorySessionHistoryAsync`; `RepositoryProjectionService.cs`; `ExecutionSessionPanel.tsx`/`ExecutionHistoryPanel.tsx` | Current + historical + lifecycle-history already projected to UI. Satisfies the lifecycle half of M2. |
| Operational-context growth/size diagnostics | `ContinuityDiagnosticsService.cs`, `UnderstandingCompressionService.cs` (`CompressionTrend`), `ContinuityDiagnosticsPanel.tsx` | Context growth/size + trends already measured & surfaced. Satisfies the document-growth half of M3. |
| Workflow ↔ session/continuity integration seam | `WorkflowExecutionService.cs`, `WorkflowOperationalContextService.cs` consuming `IExecutionSessionService`/Continuity | Workflow already observes session + continuity state. Satisfies the *mechanism* of M8. |
| Explainability / diagnostics pattern | `WorkflowInfluenceTrace`, `WorkflowProjectionDiagnostics`, `WorkflowGateEvidence`, `DecisionInfluenceTrace` | decision→reason→inputs→consequences already modeled. Satisfies the *mechanism* of M7. |
| Governance/efficiency reporting pattern | `WorkflowReportService` (4 reports), `ContinuityReportService` | Report-generation stack exists. Satisfies the *mechanism* of M6/M9. |
| Certification + recovery + idempotency infrastructure | `WorkflowCertificationService` + e2e fixture, `IReasoningCertificationService`, fingerprint idempotency, per-repo recovery isolation | Proven certification & failure-testing scaffolding. Satisfies the *mechanism* of M10. |

---

# Obsolete Roadmap Recovery

Work that should **not** be planned again as written.

| Item | Classification | Why |
|---|---|---|
| "Recover the Decision Session Registry & Session Router" framing | **Obsolete (mythical baseline)** | Never existed in code; explicitly declared non-goals in epics 03/06. There is nothing to recover. Re-scope as *new* capability or drop. |
| "Decision Sessions" as a registry category (M1) | **Obsolete / Indirectly Satisfied** | No decision-session type exists; decisions reference *execution* sessions by FK. The registry concern is already met by `ExecutionSession` for the only real category. Do not invent decision sessions speculatively. |
| Standalone "continuity transfer/efficiency" as Continuity-domain work (M5/M6) | **Replaced** | The Continuity domain is operational-context documents, not session transfer. These belong (if at all) to a session-economics concern, downstream of routing — not to `CommandCenter.Continuity`. |
| Defensive "don't become Brainstorm/continuity-research" guardrail | **Obsolete (misread)** | Aimed at `CommandCenter.Reasoning`, which is decision-provenance, not ideation. The risk it guards against is not present. |
| Net-new diagnostics/report/certification vocabularies (M7/M9/M10) | **Merged** | Already solved by Workflow/Reasoning/Decisions infrastructure; planning them fresh is churn. Reuse, don't rebuild. |

---

# Missing Capability Recovery

Genuinely missing, genuinely valuable.

## Capability — Session economics primitives & metrics (token usage, age, activity, cost)

### Why It Still Matters
The roadmap's actual mission — *throughput economics for workflow replacement* — is impossible to even
measure today: `ExecutionSession` records no token usage, no age, no activity, no cost. Without these,
no routing, governance, or efficiency claim can ever be evidenced.

### Dependency Analysis
Depends on nothing new. Age/activity are derivable from existing timestamps + event streams; token
usage requires capturing provider usage at session boundaries (Execution already owns the
provider-invocation seam).

### Existing Infrastructure It Can Reuse
`ExecutionSession` model (add fields), the projection→Middle→UI pipe (`RepositoryProjectionService`,
`ExecutionSessionPanel`), the `*ReportService` pattern, and certification/recovery scaffolding.

### Smallest Correct Future Increment
Add economics fields to `ExecutionSession`/`ExecutionSessionSummary`; capture token usage at session
completion; project age/usage/activity through the existing read models; surface in the existing
execution panels. One domain change, three already-wired surfaces.

## Capability — Session activity/age observability surfacing (M2 remainder)

### Why It Still Matters
Lifecycle is visible; *economics* is not. Age/usage/activity are the metrics that make sessions
*economic* rather than merely *lifecycle* objects.

### Dependency Analysis
Strictly downstream of the economics-primitives increment above.

### Existing Infrastructure It Can Reuse
The entire existing session observability pipe (queries → Middle projections → UI panels).

### Smallest Correct Future Increment
Extend `ExecutionSessionSummary` + the two execution panels with the new fields. No new architecture.

## Capability — Session-admission policy that permits routing (precondition for M4/M5/M6)

### Why It Still Matters
Every routing/transfer/efficiency capability is blocked by Execution's one-active-session guard.
Without an Execution-owned decision to relax/parameterize admission, the back half of the roadmap is
unbuildable.

### Dependency Analysis
This is the **gating** item. It is an *authority change in the Execution domain*, not a workflow
projection. Must precede M4/M5/M6/M7-routing.

### Existing Infrastructure It Can Reuse
`ExecutionSessionService`'s state-transition + `ReplaceSessionAsync` upsert machinery; the decision
must be captured via the existing Decisions/Reasoning provenance.

### Smallest Correct Future Increment
A scoped Execution-domain decision + spike: *under what conditions may a repository hold more than one
session, and who owns reuse-vs-replace?* No code until that authority question is answered. Treat as a
research/decision milestone, explicitly gated.

## Capability — `RepositoryWorkflowSummary` in Middle (carried over from audit-01)

### Why It Still Matters
Still the missing dashboard aggregation for workflow; relevant because economics summaries would ride
the same projection seam.

### Dependency Analysis
Independent of economics; shared *mechanism*.

### Existing Infrastructure It Can Reuse
`RepositoryProjectionService`, the existing `RepositoryContinuitySummary`/`RepositoryReasoningSummary`
siblings.

### Smallest Correct Future Increment
Add the summary type alongside its existing siblings — but note this belongs to the **surfacing**
roadmap (audit-01), not the economics roadmap. Flagged here to prevent duplicate planning.

---

# Roadmap Reformation Inputs

Themes, not milestones.

## Theme — Session Economics (the real core)

### Objective
Make session cost, token usage, age, and activity first-class, measurable properties of
`ExecutionSession`.

### Existing Assets
`ExecutionSession` model + store + recovery; the provider-invocation seam in Execution.

### Missing Assets
Economics fields; token-usage capture at session boundaries; an economics primitive set
(`SessionEconomics` and metrics).

### Risks
Conflating document-growth ("continuity cost") with session-token cost; over-modeling before a single
metric is captured.

### Architectural Constraints
Economics is *derived/observational*; Execution remains the session authority. Do not let economics
mutate lifecycle.

## Theme — Session Observability Surfacing

### Objective
Surface session economics through the projection→Middle→UI pipe that already shows lifecycle.

### Existing Assets
`RepositoryProjectionService`, dashboard/workspace projections, `ExecutionSessionPanel`/`ExecutionHistoryPanel`.

### Missing Assets
Economics fields on `ExecutionSessionSummary`; UI rendering of age/usage/activity.

### Risks
Low. Mostly additive through proven seams.

### Architectural Constraints
Read-model only; no new transport needed (this path already reaches the UI, unlike the workflow API).

## Theme — Session Routing & Admission Authority (gated, high-risk)

### Objective
Decide and, if approved, implement reuse/replace/continue/create/transfer of sessions.

### Existing Assets
`ExecutionSessionService` state machine; Decisions/Reasoning provenance to capture the authority change.

### Missing Assets
A relaxed/parameterized session-admission policy; a router; transfer/bootstrap; routing diagnostics.

### Risks
**Highest.** Cross-domain authority change; collides with the one-active-session guard; historically a
rejected non-goal. Easy to over-build.

### Architectural Constraints
Owned by Execution, not Workflow. Must follow the authority-change discipline (explicit decision +
certification). Strictly downstream of economics + an admission decision.

## Theme — Economics Governance & Certification (reuse-heavy)

### Objective
Govern and certify session economics over long horizons.

### Existing Assets
`WorkflowReportService` family, `WorkflowCertificationService` + e2e fixture, recovery + fingerprint
idempotency, archived long-horizon certification patterns.

### Missing Assets
An economics report; an economics certification scenario.

### Risks
Low-to-medium; mostly composition of existing scaffolds. Risk is building it before there is economics
data to govern.

### Architectural Constraints
Reuse the report/certification families; no parallel stacks; runtime-only evidence is acceptable
(consistent with the established model).

## Theme — Workflow Surfacing (carried from audit-01; sequencing decision required)

### Objective
The still-open frontier from audit-01: Tauri bridge workflow commands, dedicated workflow UI workspace,
`RepositoryWorkflowSummary`.

### Existing Assets
A ~95%-complete, certified Workflow engine with 26/27 HTTP endpoints.

### Missing Assets
Transport (0/27 bridge commands), UI workspace, Middle summary.

### Risks
Opportunity cost: starting economics while this remains unreachable strands a finished engine.

### Architectural Constraints
This is a *surfacing* effort, not an *engine* effort; it shares the projection/transport seams the
economics surfacing theme uses.

---

# Architectural Constraints For Future Planning

Mandatory for the next roadmap. Derived from repository reality.

```text
Execution owns session lifecycle and session admission — do not duplicate or override it.
Economics is derived/observational — it must never become authoritative over a session.
Routing/transfer require an Execution authority change — never plan them as a pure projection.
Do not invent "decision sessions" — only one real session category (execution) exists; it was a non-goal.
Disambiguate "continuity": document-continuity (Continuity domain) ≠ session-continuity (new economics).
Reuse the evaluate→persist→recover→certify→act scaffold; do not rebuild it.
Reuse fingerprint idempotency as the universal idempotency key.
Reuse recovery-first startup with per-repository error isolation.
Reuse the *ReportService family for governance/efficiency reports.
Reuse WorkflowCertificationService + the e2e fixture pattern for certification.
Reuse the projection→Middle→UI pipe for any new session surface (it already reaches the UI).
Accept runtime-only evidence; do not plan committed certification artifacts at rest.
Do not let this roadmap silently cancel the audit-01 workflow-surfacing frontier — sequence explicitly.
```

---

# Roadmap Generation Guidance

## Must Build Upon

- `ExecutionSession` + its store + recovery hosted service — the real session registry baseline.
- The session observability pipe (`IExecutionSessionService` queries → `RepositoryProjectionService` →
  execution UI panels).
- Continuity document-growth diagnostics (`ContinuityDiagnosticsService`, `CompressionTrend`).
- The Workflow integration seam (already consuming session + continuity state).
- The report, certification, recovery, idempotency, and influence-trace infrastructure.

## Must Not Rebuild

- A session registry from scratch (extend `ExecutionSession`'s).
- Diagnostics/explainability vocabulary (reuse `WorkflowInfluenceTrace`/`WorkflowGateEvidence`).
- Report and certification stacks (reuse the existing families).
- Recovery + idempotency scaffolding.

## Must Reconcile

- The **false "recovery" premise** — there was no Session Router/Decision Session Registry; they were
  non-goals. Re-author the roadmap's objective as *new economics capability*, not recovery.
- The **routing/authority collision** — Execution's one-active-session guard versus M4's reuse/replace.
- The **"continuity" double meaning** — document-growth vs session-token cost.
- The **two-frontier conflict** — economics stack vs audit-01's workflow surfacing; pick a sequence.

## Must Preserve

- The authority model and the six architecture rules (proven by Workflow certification): domains own
  truth; derived layers observe, project, recover, explain, certify.
- Per-repository error isolation in recovery.
- Runtime-derived, disposable evidence.

## Must Add

1. Session economics primitives + token/age/activity capture on `ExecutionSession`.
2. Economics surfacing through the existing projection→UI pipe.
3. An explicit, gated **Execution session-admission decision** before any routing/transfer work.
4. (Carried) `RepositoryWorkflowSummary` + workflow surfacing — assigned to whichever roadmap owns the
   surface frontier, planned once.

---

# Final Recommendation

## Verdict: **Revise Roadmap** (re-scope onto reality; split and gate).

Not **Continue** — continuing as written builds on a mythical baseline ("recover the Session Router")
and plans routing/transfer as derived infrastructure when they require an Execution authority change.

Not **Replace** — the roadmap's *core economic intent* (make session cost/throughput measurable to
serve workflow replacement) is sound, genuinely unmet, and buildable on existing infrastructure.
Discarding it would discard a real gap.

**Revise**, specifically:

1. **Re-author the objective.** Drop "recover the Decision Session Registry and Session Router." State
   the true goal: *first-class session economics over the existing `ExecutionSession`, in service of
   workflow-replacement throughput.*
2. **Collapse the completed mechanisms into baseline.** M1 (registry), the lifecycle half of M2, the
   document-growth half of M3, the integration *mechanism* of M8, and the *mechanisms* of M6/M7/M9/M10
   already exist — record them as reuse, not build.
3. **Front-load the cheap, high-value core:** Session Economics primitives → Economics Observability
   surfacing. These reuse proven seams and reach the UI today.
4. **Quarantine routing/transfer (M4/M5/M6/M7-routing) behind an explicit Execution session-admission
   decision.** No router code until that authority question is answered and certified. Treat the
   decision itself as the gating milestone.
5. **Make the two-frontier choice explicit.** Decide and document whether economics or audit-01's
   workflow surfacing goes first; do not let one cancel the other by drift.

## Recommended Next Roadmap Scope

A **Session Economics & Observability roadmap** (reality-based), in three gated bands:

```text
Band 1 — Economics Foundation & Observability  (build now; reuses everything)
    Session economics primitives on ExecutionSession (token usage, age, activity, cost)
    Token-usage capture at session boundaries (Execution-owned)
    Economics surfacing via the existing projection → Middle → execution-UI pipe
    Economics report via the existing *ReportService family
    Economics certification via the existing WorkflowCertificationService + e2e fixture pattern

Band 2 — Admission Authority Decision  (gate; decision before code)
    Execution-domain decision: may a repository hold >1 session? who owns reuse vs replace?
    Captured via Decisions + Reasoning provenance; certified

Band 3 — Routing & Transfer  (only if Band 2 approves; high-risk, downstream)
    Session routing (reuse/replace/continue/create) over the relaxed admission policy
    Continuity/context transfer + bootstrap
    Routing diagnostics (reuse WorkflowInfluenceTrace pattern)
    Efficiency analysis + economics governance (reuse report family)
```

Band 1 delivers the roadmap's true objective at low risk and high reuse. Band 2 converts the roadmap's
single largest hidden assumption (that routing is "infrastructure") into an explicit, owned authority
decision. Band 3 is real but speculative, historically rejected, and must not be built until Bands 1–2
make it both measurable and authorized.

The next roadmap is an **economics-and-observability** roadmap built on `ExecutionSession`, **not** a
recovery of infrastructure that never existed.
