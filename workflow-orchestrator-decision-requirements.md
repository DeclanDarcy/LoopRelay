# Workflow Orchestrator Decision Requirements

This document is an architectural discovery audit for a future Workflow Orchestrator.

It does not design orchestration behavior, recommend an architecture, define an algorithm, propose APIs, or introduce a workflow model. It records the decisions a future orchestrator would need to make and the repository facts, authorities, constraints, dependencies, ambiguities, invariants, and unknowns that govern those decisions.

Evidence convention:

- **Evidence**: Directly observed in repository source, tests, prompt contracts, README text, current workspace state, or issue records.
- **Inference**: A decision requirement implied by multiple observed facts.
- **Uncertainty**: A material gap, conflict, missing authority, or unresolved product boundary.

Primary evidence sources include:

- CLI entry points: `src/LoopRelay.Roadmap.Cli/Program.cs`, `src/LoopRelay.Plan.Cli/Program.cs`, `src/LoopRelay.Cli/Program.cs`.
- Roadmap workflow: `RoadmapStateMachine`, `RoadmapStartupPlanner`, `RoadmapResumePlanner`, `RoadmapUnblockPlanner`, `RoadmapState`, `RoadmapTransitionPersistence`, `RoadmapPromptTransitionRunner`, `PromptContractRegistry`.
- Plan workflow: `PlanPipeline`, `PreflightGate`, `PlanSession`, `ReviewStep`, `PermissionedArtifactOperationStep`, `OneShotSteps`.
- Execution workflow: `LoopRunner`, `DecisionSession`, `ExecutionStep`, `MilestoneGate`, `CommitGate`, `WorkingTreeChangeDetector`, `LoopArtifacts`.
- Completion and review: `CompletionCertificationService`, `CompletionCertificationPolicy`, `CompletionCertificationRouter`, `CompletionCertificationTransition`, `NonImplementationCompletionReviewService`.
- Persistence and observability: `LoopRelayWorkspaceDatabase`, `WorkspaceSqlitePersistence`, `WorkspaceSyncService`, `WorkspaceVerificationService`, `WorkflowPersistenceCoordinator`, `RoadmapLogicalArtifactServices`.
- Projection and artifact validity: `ProjectionCache`, `ProjectionValidator`, `ProjectionFreshnessEvaluator`, `ProjectContextLoader`, `ArtifactPromotionService`, `InvariantValidator`, `ExecutionPreparationProvenanceService`.
- Known issue records under `issues/`.

## 1. Executive Summary

**Evidence**

- The repository currently exposes three executable workflow surfaces:
  - `LoopRelay.Roadmap.Cli [status|run|unblock|storage-init|storage-import|storage-export|storage-sync|storage-verify] <REPO_DIR> ...`.
  - `LoopRelay.Plan.Cli <REPO_DIR>`.
  - `LoopRelay.Cli <REPO_DIR>`.
- No current CLI command named `looprelay` was observed, and no single current command chains Roadmap -> Plan -> Execution end to end.
- "Traditional Roadmap Workflow" and "Eval-Driven Roadmap Workflow" are not current command names, enum values, or persisted workflow identities. Roadmap selection/preparation and completion-evaluation behavior are implemented inside the Roadmap CLI and shared Completion services.
- Roadmap has explicit persisted state, transition journals, lifecycle state, prompt contracts, projection manifests, decision ledger entries, blocker evidence, and storage sync/verification.
- Plan is a fixed pipeline with clean-start preflight and no durable Plan state document.
- Execution is a serial loop over artifacts, Git state, decision-session resume state, milestone checkboxes, non-implementation review, and completion certification; most execution stage state is implicit.
- Runtime persistence can be SQLite-backed under `.LoopRelay/persistence/looprelay.sqlite3` or file-backed depending on database validation. The README says SQLite structured runtime state is canonical, while Roadmap and Execution code fall back when the database is missing or not classified as imported/canonical.

**Inference**

- A future orchestrator must make decisions across several authority layers rather than from a single state file: CLI intent, persisted state, `.agents` artifacts, SQLite integrity, lifecycle records, projections, journals, Git, prompt contract outputs, completion policy, and user/HITL evidence.
- Workflow selection and stage selection cannot be inferred from artifact existence alone. The same artifact can be current, stale, superseded, archived, blocked, partial, or merely a compatibility export.
- Completion is a compound decision. Checked milestone boxes create a completion claim; closure requires completion review, certification policy, routing, archive/update effects, and evidence.
- Recovery decisions are constrained by durable evidence. Roadmap has explicit recovery intent for some blockers; Plan and Execution have fewer explicit recovery contracts.

**Uncertainty**

- The future meaning of `looprelay` is not defined in current code.
- The boundary between "Traditional Roadmap" and "Eval-Driven Roadmap" is semantic, not encoded as a durable identity.
- The handoff from Roadmap `MilestoneSpecsReady` to Plan's `.agents/specs/epic.md` input is not encoded as a single current command.
- Plan partial-output recovery and main execution rerun-after-archive behavior are not fully specified.
- Known issue records document unresolved risks in completion archive transactionality, archive index allocation, scoped artifact operation certification, and permission-policy hard-deny behavior.

## 2. Orchestrator Decision Inventory

| ID | Decision | Evidence | Inference | Uncertainty |
| --- | --- | --- | --- | --- |
| D01 | Determine current executable surface requested or implied. | Three CLIs and Roadmap storage commands are parsed separately in their `Program.cs` and `CliArguments` files. | A future orchestrator must distinguish Roadmap, Plan, Execution, Completion, Storage, and status/report requests. | `looprelay` has no observed implementation. |
| D02 | Determine whether storage authority is SQLite, filesystem, or invalid/conflicting. | `WorkspaceSqliteStore.ValidateAsync`, `RoadmapCliComposition`, `LoopWorkspaceDatabase.HasUsableLoopHistoryDatabase`, README. | Store selection affects every downstream fact read. | Global precedence between SQLite and filesystem exports is not centralized. |
| D03 | Determine whether a Roadmap workflow exists, is active, paused, blocked, failed, cancelled, or absent. | `RoadmapStateDocument`, `RoadmapStartupPlanner`, `RoadmapWorkflowStateClassifier`. | Roadmap state is the strongest current stage signal for roadmap decisions. | Current checkout has no `.LoopRelay` and no visible `.agents` contents, so only code contracts are observable here. |
| D04 | Decide whether Roadmap run should initialize, resume, report, or block. | `RoadmapStartupPlanner.Plan`, `RoadmapResumePlanner.PlanAsync`. | Resume requires preflight and artifact/projection safety. | Some states have no safe resume rule. |
| D05 | Decide whether Project Context is valid enough for Roadmap transitions. | `ProjectContextSourceContract` requires nine canonical files; `ProjectContextLoader` rejects missing or extra numbered files. | Roadmap prompt decisions cannot proceed without valid Project Context. | Whether other workflows should enforce the same preflight globally is unspecified. |
| D06 | Decide whether a roadmap prompt/projection contract is ready. | `PromptContractRegistry`, `ProjectionCache`, `ProjectionValidator`, `ProjectionFreshnessEvaluator`. | Required inputs, required outputs, parser identity, allowed decisions, and stale policy govern prompt readiness. | Projection repair vs regeneration is user/manual outside current state machine. |
| D07 | Decide whether an existing selection can be reused or must be regenerated. | `RoadmapResumePlanner.ValidateActiveSelectionFreshnessAsync`, `SelectionProvenanceService`. | Selection is reusable only when current cycle/provenance and lifecycle are usable. | Exact user-facing handling of stale but present selection is not globally specified. |
| D08 | Decide next roadmap initiative outcome. | `SelectNextEpicTransition`, `SelectionDecision`, allowed decisions in `PromptContractRegistry`. | Outcomes include existing epic, new intermediary epic, split epic, strategic investigation, roadmap revision, or no suitable initiative. | Traditional vs eval-driven classification is not explicit. |
| D09 | Decide selected existing epic preparation route. | `EpicPreparationAuditTransition`, `EpicPreparationAuditDecision`. | Outcomes include Retire, Realign, Reimagine, or Insufficient Evidence. | Insufficient Evidence is thrown as a step exception in this path rather than always durable blocker evidence. |
| D10 | Decide whether generated/revised/split active epic output can be promoted. | `ArtifactPromotionService`, `ActiveEpicPromotionCoordinator`, `EpicArtifactValidator`. | Prompt completion does not authorize downstream use until classification, validation, lifecycle update, and state persistence pass. | Automatic repair for promotion blockers is explicitly unsupported by `RoadmapUnblockPlanner`. |
| D11 | Decide whether split output is valid and which child becomes active. | `SplitEpicTransition`, `SplitEpicBundleInterpreter`, `SplitLineagePersistence`. | Split requires bundle extraction, child validation, lineage persistence, and selected child promotion. | Split blocker recovery is explicitly unsupported. |
| D12 | Decide whether milestone specs are materialized and fresh. | `GenerateMilestoneDeepDivesTransition`, `ExecutionPreparationProvenanceService`, `InvariantValidator`. | `MilestoneSpecsReady` is eligible only when specs are fresh and belong to `.agents/epic.md`. | Roadmap currently pauses at `MilestoneSpecsReady`; downstream chaining is external. |
| D13 | Decide whether Roadmap should evaluate an execution completion claim. | `RoadmapResumePlanner` maps `EpicCompletionDetected` to `EvaluateCompletionClaim`; `CompletionCertificationTransition`. | Completion claim evaluation is a Roadmap/Completion branch, not simple execution continuation. | Main CLI also performs completion certification, so ownership boundary is shared. |
| D14 | Decide completion certification route. | `CompletionCertificationPolicy`, `CompletionCertificationRouter`, `RoadmapCompletionRouteMapper`. | Closure recommendations route to close, close with follow-up, continue, reopen, or gather more evidence. | Which path is authoritative if main CLI and Roadmap evidence disagree is not centralized. |
| D15 | Decide whether non-implementation review blocks completion. | `NonImplementationCompletionReviewService`, `NonImplementationCompletionReviewResult`. | Unresolved confirmed/uncertain non-implementation entries require human decisions before completion. | Scope of HITL blockers across workflows is not globally defined. |
| D16 | Decide Plan workflow eligibility. | `PreflightGate` requires `.agents/specs/epic.md` and blocks existing plan, operational context, details, and milestone files. | Plan is clean-start only in current code. | No durable Plan resume/status/repair command observed. |
| D17 | Decide Plan stage progression and completion. | `PlanPipeline`, `PlanSession`, `ReviewStep`, `PermissionedArtifactOperationStep`, `OneShotSteps`. | Completion requires verified plan, operational context, details, milestone files, checklist presence, and publication. | Partial outputs after failure are ambiguous. |
| D18 | Decide Execution workflow eligibility. | `LoopRunner`, `ExecutionStep`, `LoopArtifacts`, `MilestoneGate`. | Execution consumes plan/details/operational context/milestones, but code does not expose a preflight equivalent to Plan. | A missing live plan after archive can fall through to execution, documented in issue `005`. |
| D19 | Decide execution slice route: direct first execution, pending decisions execution, or decision proposal. | `LoopRunner` checks live `decisions.md` and latest handoff. | Live decisions are executed directly; no handoff starts first execution; handoff triggers decision session. | Crashes around live handoff rotation are tolerated by comments but not represented as formal state. |
| D20 | Decide decision-session route: continue or transfer. | `DecisionSessionRouter`, `DecisionRoute`, `DecisionSession`. | Router outputs Continue or Transfer based on cost/capacity inputs; `DecisionSession` downgrades Transfer until seeded. | This is decision-session-specific, not workflow-wide resume. |
| D21 | Decide whether to resume decision-session thread. | `SqliteDecisionSessionResumeStore`, `DecisionSession.OpenOrResumeSessionAsync`, `DecisionResumeComposition`. | Resume requires enabled env flag, usable SQLite state, fresh decision projection, and successful Codex thread resume. | App-server thread resume is marked experimental by code comments. |
| D22 | Decide handoff prompt type after execution work. | `ExecutionStep`, `WorkingTreeChangeDetector`, `MilestoneGate.GetUntickedItemsAsync`. | Real non-`.agents` changes use normal handoff; no real changes use no-changes handoff with unticked milestone items. | Handoff content quality is not machine-validated beyond file existence. |
| D23 | Decide real progress, commit/push, or stall. | `CommitGate`, `WorkingTreeChangeDetector`, `MilestoneGate`. | Progress is non-`.agents` Git changes or reduced unchecked milestone items; stall after more than two no-progress iterations. | Stall counter is in memory and resets on process restart. |
| D24 | Decide whether milestone gate claims epic completion. | `MilestoneGate.IsEpicCompleteAsync`. | Requires at least one strict checkbox across `.agents/milestones/m*.md` and all strict checkboxes checked; fenced code ignored. | Archive removes live milestone files, creating rerun ambiguity in issue `005`. |
| D25 | Decide whether main CLI may report `EpicCompleted`. | `LoopRunner` calls completion review and `CompletionCertificationService`; result must be completed. | `EpicCompleted` requires certified closure, not milestone boxes alone. | Archive/update partial failure can strand retry state, issue `004`. |
| D26 | Decide cancellation, failure salvage, and abnormal exit outcome. | `LoopRunner` salvage publish; Roadmap cancellation state; Plan cancellation outcome. | Cancellation is distinct from failure and can preserve recovery intent or best-effort `.agents` publication. | Plan has no durable cancellation state. |
| D27 | Decide storage command outcome. | `WorkspaceSyncService`, `WorkspaceVerificationService`, `WorkspaceStorageResultCategory`. | Outcomes include initialized, imported, exported, unchanged, stale export, conflict, unsupported version, validation failure, verification failed. | Whether storage sync should ever run automatically is unspecified. |
| D28 | Decide permission/elevation requirement. | `CliSettingsLoader` in Plan/Main/Roadmap composition, Roadmap `--elevated`; issue records for permission gaps. | Permission policy and elevation affect whether agent/tool operations can proceed. | Issues document unresolved trust gaps. |
| D29 | Decide whether user/HITL input is required. | Non-implementation decision file, HITL capture services, Roadmap blockers and terminal strategic states. | Human decisions include review rows, strategic investigation, roadmap revision, evidence repair, permission/elevation. | No single user interaction protocol spans all workflows. |
| D30 | Decide whether outputs are invalidated by changed inputs. | Projection freshness, derived artifact freshness, execution preparation provenance, lifecycle, storage markers. | Prompt outputs and derived artifacts have bounded validity tied to hashes/provenance. | Plan outputs do not have an equivalent durable manifest. |
| D31 | Decide whether concurrent operations are safe. | Serial loops in `LoopRunner` and `PlanPipeline`; storage verify mutation guard; workflow transaction markers. | Concurrent mutation is not established as supported. Read-only status/verify are the only clearly non-mutating surfaces. | No cross-process lock or global concurrency contract observed. |
| D32 | Decide whether a workflow is complete, obsolete, superseded, or already closed. | Lifecycle states, completion archives, roadmap state, issue records. | Completion/obsolete decisions need lifecycle plus archive/evidence, not only current live files. | Durable completed-state gate for main CLI rerun is unresolved. |

## 3. Decision Inputs

| Decision IDs | Required inputs | Optional/supporting inputs | Evidence |
| --- | --- | --- | --- |
| D01 | CLI args, command names, repo path existence. | Environment variables, default command behavior. | `CliArguments` for all CLIs. |
| D02, D27 | SQLite existence, schema version, `workspace_metadata.persistence_state`, domain rows, sync markers, filesystem export snapshot. | Force flags, selected domains, full roundtrip flag. | `LoopRelayWorkspaceDatabase`, `WorkspaceSqliteStore`, `WorkspaceSyncService`, `WorkspaceVerificationService`. |
| D03-D04 | Roadmap persisted state document, last transition, transition intent, blockers, next valid transitions. | Lifecycle rows, decision ledger id, split count, projection manifest counts. | `RoadmapStateDocument`, `RoadmapTransitionPersistence`. |
| D05-D06 | Nine Project Context source files, prompt contract, projection body, projection manifest entry, prompt/source hashes, project context hash. | Optional prompt inputs and blocking output headings. | `ProjectContextSourceContract`, `ProjectContextLoader`, `PromptContractRegistry`, `ProjectionCache`. |
| D07-D12 | Selection artifact/provenance, active epic artifact/lifecycle, roadmap source files, retired epics, split lineage, milestone specs, execution-preparation manifest. | HITL captures, evidence paths, bundle manifests. | Roadmap transition classes, `ExecutionPreparationProvenanceService`. |
| D13-D15, D25 | Active epic, milestone files, execution completion claim/evidence, completion evaluation, non-implementation ledger/review/decision file, archive synthesis, roadmap completion context. | Projection content for completion/update, review evidence paths. | `CompletionCertificationService`, `CompletionCertificationTransition`, `NonImplementationCompletionReviewService`. |
| D16-D17 | `.agents/specs/epic.md`, absence of `.agents/plan.md`, `.agents/operational_context.md`, `.agents/details.md`, and milestone files. | Other `.agents/specs/*.md`, adversarial plan review projection, `.agents` submodule publication status. | `PreflightGate`, `PlanPipeline`, `OneShotSteps`. |
| D18-D24 | `.agents/plan.md`, optional details, operational context, live/latest decisions, live/latest handoff, milestone files, Git status excluding `.agents`, decision resume state. | Decision projection freshness, usage telemetry, operational deltas, no-change count. | `LoopRunner`, `DecisionSession`, `ExecutionStep`, `CommitGate`. |
| D26 | Cancellation token, last persisted transition, live `.agents` writes. | Submodule publication success/failure. | CLI `Program.cs`, `LoopRunner`, `RoadmapStateMachine.WriteCancelledStateAsync`. |
| D28-D29 | Permission settings, Roadmap elevation flags/reasons, HITL ledger entries, non-implementation decisions, blocker evidence. | Known permission issue records. | `LoopCliComposition`, `PlanCliComposition`, `RoadmapCliComposition`, `NonImplementationCompletionReviewService`, `issues/`. |
| D30-D32 | Lifecycle states, projection freshness, derived artifact freshness, storage sync markers, archive directories, workflow transaction markers. | Git history, issue records, legacy filesystem exports. | `ArtifactLifecycleStore`, `InvariantValidator`, `WorkflowPersistenceCoordinator`, `CompletedEpicArchiveService`. |

## 4. Decision Outputs

**Evidence**

- Roadmap command outcomes: `Completed`, `Paused`, `PreflightBlocked`, `Cancelled`, `Failed`.
- Plan outcomes: `Completed`, `PreflightBlocked`, `Failed`, `Cancelled`.
- Execution outcomes: `EpicCompleted`, `CompletionBlocked`, `Cancelled`, `Failed`, `Stalled`.
- Storage categories: `Initialized`, `Imported`, `Exported`, `Unchanged`, `StaleExport`, `Conflict`, `UnsupportedVersion`, `ValidationFailure`, `VerificationFailed`.
- Completion certification outcomes: `Completed`, `Blocked`, `Failed`.
- Decision-session routes: `Continue`, `Transfer`.
- Completion closure recommendations: `Close Epic`, `Close With Follow-Up`, `Continue Epic`, `Reopen Epic`, `Gather More Evidence`.
- Roadmap selection outcomes: `Select Existing Epic`, `Select New Intermediary Epic`, `Select Split Epic`, `Strategic Investigation Required`, `Roadmap Revision Required`, `No Suitable Initiative`.
- Epic audit dispositions: `Realign`, `Reimagine`, `Retire`, `Insufficient Evidence`.

**Inference**

- A future orchestrator decision output vocabulary must include at least: eligible, not eligible, run, resume, rerun, report-only, blocked, waiting-for-human, waiting-for-storage, waiting-for-permission, repair-required, cancelled, failed, stalled, complete, obsolete/superseded, ambiguous, and no-action.

**Uncertainty**

- The repository does not define one shared output enum across all workflows.
- "Traditional Roadmap" and "Eval-Driven Roadmap" do not have encoded output sets separate from Roadmap state and prompt outputs.

## 5. Decision Preconditions

| Decision | Preconditions | Evidence | Inference/Uncertainty |
| --- | --- | --- | --- |
| Roadmap run/resume | Persisted state may be absent or present; Project Context preflight required unless state is report-only. | `RoadmapStartupPlanner`, `RoadmapStateMachine.RunAsync`. | Roadmap cannot safely resume active states without Project Context and contracts. |
| Roadmap prompt transition | Valid prompt contract, projection validation, projection freshness under stale policy, required inputs artifact-ready. | `PromptContractRegistry`, `ProjectionCache`, `RoadmapResumePlanner.ValidatePromptReadiness`. | All Roadmap prompt decisions are provenance-sensitive. |
| Active epic preparation | Selection must exist and be fresh; selected/audited output must pass parser and validation. | `ActiveSelectionReader`, `EpicPreparationAuditTransition`, `ArtifactPromotionService`. | Selection is proposal evidence, not final authority. |
| Milestone spec readiness | Active epic must exist, lifecycle usable, active epic structurally valid, specs fresh, specs refer to active epic. | `RoadmapResumePlanner.ValidateExecutionPreparationAsync`, `ValidateSpecsAsync`. | `MilestoneSpecsReady` is a terminal pause in current Roadmap CLI. |
| Plan start | `.agents/specs/epic.md` exists; plan, operational context, details, milestones absent/empty. | `PreflightGate`. | Existing outputs block a fresh run rather than prove resumability. |
| Plan completion | Plan exists after write/revise, review returns output, operational context seeded, details exists, milestones exist with strict checkboxes, publish/parent gitlink as applicable. | `PlanPipeline`, `PermissionedArtifactOperationStep`, `OneShotSteps`. | No whole-pipeline completion marker exists. |
| Execution slice | Loop is not complete by milestone gate; operational context ensured if plan exists; route determined from decisions/handoff. | `LoopRunner`, `LoopArtifacts.EnsureOperationalContextAsync`. | Missing plan is not preflight-blocked by current code; this is an eligibility ambiguity. |
| Completion certification | Active epic present, at least one milestone file, roadmap completion context present or bootstrappable, evaluation parse/policy/route valid. | `CompletionCertificationService`. | Checked boxes are only a trigger for certification. |
| Storage sync/export/import | DB readable when exporting; filesystem snapshot importable; domain dependencies valid; drift markers compatible unless force flag supplied. | `WorkspaceSyncService`. | Storage is a distinct precondition for trusting persisted facts. |
| Unblock | Persisted state is `EvidenceBlocked`, `Failed`, or `ExecutionBlocked`; transition intent has a supported handler; evidence paths match expected domains. | `RoadmapUnblockPlanner`. | Many intents are explicitly unsupported. |

## 6. Decision Authority

| Authority | Scope | Authority level | Evidence | Limits |
| --- | --- | --- | --- | --- |
| CLI parser/entry point | Process invocation and command family. | Decisive for invoked process. | `Program.cs`, `CliArguments`. | Does not describe workspace truth. |
| SQLite integrity classifier | Whether SQLite-backed stores are usable. | Decisive for store selection in Roadmap/Main where checked. | `WorkspaceSqliteStore.ValidateAsync`, `LoopWorkspaceDatabase.HasUsableLoopHistoryDatabase`. | README-level canonicality and fallback behavior are not a single global rule. |
| Roadmap persisted state | Roadmap current state, last transition, blocker, intent. | Decisive when store is selected and state is valid. | `RoadmapStateDocument`, `RoadmapTransitionPersistence`. | Must be checked against artifacts/projections for resume safety. |
| Roadmap startup/resume/unblock planners | Whether to initialize, resume, report, or block roadmap. | Decisive for Roadmap CLI. | `RoadmapStartupPlanner`, `RoadmapResumePlanner`, `RoadmapUnblockPlanner`. | Does not cover Plan/Main execution status globally. |
| Prompt contracts | Required inputs, outputs, parsers, decisions, stale projection policy. | Decisive for Roadmap prompt readiness. | `PromptContractRegistry`. | Plan/Main prompts do not use the same explicit registry. |
| Projection validator/freshness | Projection usability and invalidation. | Decisive for projection-dependent Roadmap and decision-session reuse. | `ProjectionValidator`, `ProjectionFreshnessEvaluator`, `ProjectionCache`. | Some workflows have weaker projection manifest coverage. |
| Artifact lifecycle/provenance | Artifact usability, freshness, supersession. | Strong for Roadmap artifacts. | `ArtifactLifecycleStore`, `ExecutionPreparationProvenanceService`. | Plan outputs lack equivalent lifecycle metadata. |
| Artifact validators/promoters | Whether model output becomes active artifact. | Decisive for active epic promotion and split child promotion. | `ArtifactPromotionService`, `ActiveEpicPromotionCoordinator`. | Automatic repair often unsupported. |
| Milestone gate | Main execution completion claim trigger. | Decisive for entering completion path. | `MilestoneGate`. | Not closure authority. |
| Completion certification policy/router | Whether closure is allowed and where to route. | Decisive for certified closure. | `CompletionCertificationPolicy`, `CompletionCertificationRouter`. | Shared ownership with Roadmap route mapper is not globally resolved. |
| Git change detector/commit gate | Real progress and stall detection. | Decisive for main execution commit/stall. | `WorkingTreeChangeDetector`, `CommitGate`. | `.agents` changes intentionally excluded except milestone checkbox reduction. |
| Decision-session router/resume store | Continue vs Transfer; resume thread reuse. | Decisive within decision session only. | `DecisionSessionRouter`, `SqliteDecisionSessionResumeStore`. | Not general workflow resume authority. |
| Human/operator | HITL decisions, permission/elevation, roadmap revision/investigation, evidence repair. | Decisive where automation blocks. | Non-implementation decision file, Roadmap terminal states, permission policy. | No unified human-decision protocol across workflows. |
| Issue records | Known unresolved risks. | Qualifying evidence. | `issues/*.md`. | Issues include proposals; this audit records only risk/uncertainty, not recommendations. |

## 7. Decision Confidence Requirements

**Must be certain**

- Storage authority before trusting SQLite-backed vs filesystem-backed state. Evidence: `ValidateAsync` classifications and store-selection code.
- Roadmap prompt readiness when stale policy is `Block`. Evidence: `PromptContractRegistry` and `ProjectionCache`.
- Active epic promotion and milestone spec readiness. Evidence: validators, lifecycle, execution-preparation provenance.
- Completion closure. Evidence: `CompletionCertificationPolicy` and `CompletionCertificationService` block on malformed/invalid/non-closing routes.
- Plan clean-start eligibility. Evidence: `PreflightGate` blocks on any violation.
- Main execution real progress/stall. Evidence: `CommitGate` and `WorkingTreeChangeDetector` use specific Git/milestone rules.

**Can tolerate inference**

- Workflow identity when only artifact sets exist and no persisted state exists. Evidence: Plan and Execution lack durable state documents.
- Execution continuation from handoffs/decisions/history. Evidence: `LoopRunner` and `LoopArtifacts` derive route from live/latest artifacts.
- Traditional vs eval-driven roadmap label. Evidence: no first-class encoded label; behavior inferred from prompt semantics and completion evaluation.

**Must defer or block**

- Roadmap incomplete transition with no durable outputs or missing output artifacts. Evidence: `RoadmapResumePlanner.ValidateIncompleteTransition`.
- Stale invalid projection under blocking policy. Evidence: `ProjectionCache`.
- Unsupported unblock intents. Evidence: `RoadmapUnblockPlanner`.
- Non-implementation completion review with unresolved entries and no valid human decisions. Evidence: `NonImplementationCompletionReviewService`.
- Storage conflict/divergence without force flags. Evidence: `WorkspaceSyncService`.

**Uncertainty**

- Plan partial-output recovery confidence is low because no durable Plan state or journal was observed.
- Main execution rerun-after-close confidence is low because live milestone inputs are archived and issue `005` documents a rerun ambiguity.
- Archive retry confidence is qualified by issue `004`.

## 8. Repository Observability

| Observable fact | How it becomes observable | Evidence | Uncertainty |
| --- | --- | --- | --- |
| Roadmap state exists | `.agents/state.json` or SQLite `roadmap_state`. | `RoadmapStateStore`, `SqliteRoadmapStateStore`, schema. | Current checkout has no visible active state. |
| Roadmap state is report-only | State classifier marks terminal/report states. | `RoadmapWorkflowStateClassifier`. | Legacy execution states remain readable but not advanced. |
| Project Context valid | All nine canonical `.agents/ctx/*.md` files exist and no extra numbered files. | `ProjectContextLoader`. | Whether Plan/Main must require it globally is unspecified. |
| Projection fresh/invalid/stale | Projection manifest plus current provenance comparison. | `ProjectionFreshnessEvaluator`, `ProjectionManifestEntry`. | Missing manifest yields unknown provenance, not necessarily regenerate. |
| Prompt contract emitted | `.agents/contracts/prompt-contracts.md` written during Roadmap preflight. | `PromptContractRegistry.EmitSnapshotAsync`. | File export may be stale if SQLite is canonical. |
| Active epic usable | `.agents/epic.md` exists, lifecycle Ready/Executing, validation passes. | `RoadmapResumePlanner`, `EpicArtifactValidator`. | Multiple lifecycle entries can violate invariant. |
| Selection current | Selection provenance current-cycle hash matches roadmap/completion/projection/retired-epic inputs. | `SelectionProvenanceService` via resume planner. | Present selection can be stale or lifecycle-unusable. |
| Milestone specs fresh | Execution-preparation manifest and hashes match active epic. | `ExecutionPreparationProvenanceService`. | Plan-created milestones lack same provenance. |
| Plan clean-start eligible | Preflight violations list is empty. | `PreflightGate`. | Existing outputs are ambiguous partial/completed/stale state. |
| Execution has pending decisions | `.agents/decisions/decisions.md` exists. | `LoopRunner`. | Live decisions may reflect interrupted prior run. |
| Execution has handoff history | live handoff or highest numbered history exists. | `LoopArtifacts.ReadLatestHandoffAsync`. | History may be file or SQLite-backed depending storage. |
| Real implementation changes exist | `git status --porcelain` excluding `.agents`. | `WorkingTreeChangeDetector`. | Git command failure blocks evaluation. |
| Milestone completion claim | Strict checkboxes in `.agents/milestones/m*.md` all checked and at least one exists. | `MilestoneGate`. | Archive removes live evidence after close. |
| Completion blocked | Blocker evidence under `.agents/evidence/blockers` and blocked result. | `CompletionCertificationService`, Roadmap transition. | Ownership across Main/Roadmap is shared. |
| HITL needed | Non-implementation decisions file required or Roadmap terminal strategic states. | `NonImplementationCompletionReviewService`, `RoadmapState`. | No single HITL queue. |
| Storage conflict/stale export | Sync marker hash drift or verification finding. | `WorkspaceSyncService`, `WorkspaceVerificationService`. | Force flags can override specific storage operations. |
| Workflow persistence partial/corrupt | Workflow transaction markers classified. | `WorkflowPersistenceCoordinator.ClassifyAsync`. | Markers only exist in SQLite-backed coordinated phases. |

## 9. Observable Events

**Evidence**

- Roadmap journal events include `TransitionStarted`, `TransitionCompleted`, `PromptCompleted`, `TransitionFailed`, `ArtifactPromoted`, `ArtifactPromotionBlocked`, `SplitBundleRejected`, `MilestoneSpecsMaterialized`, `MilestoneSpecGenerationFailed`, `CompletionCertificationRejected`, `UnblockReviewCompleted`, `UnblockReviewBlocked`.
- Roadmap state events include state save/update, blocker persistence, transition intent persistence, decision ledger append, lifecycle upsert, split family persistence, milestone spec provenance record.
- Plan events include preflight pass/fail, write plan, generate adversarial projection, adversarial review, revise plan, seed operational context, collect details, extract milestones, extract details, publish `.agents`, record parent gitlink.
- Execution events include milestone gate checked, decision session proposed/resumed/transferred, decisions persisted/retired, handoff generated, post-execution review captured, `.agents` published, Git commit/push, stall count updated, completion review/certification, decision resume cleared.
- Storage events include initialize, import, export, sync, verify, marker write, verification finding.
- Completion events include main completion claim written, evaluation evidence written, archive materialized, synthesis generated, roadmap completion context updated, blocker evidence written.
- Human events include filling non-implementation decisions, supplying Roadmap elevation reason, resolving blockers, performing roadmap revision or investigation.

**Inference**

- Observable events matter when they change eligibility, invalidate outputs, or explain a prior decision.

**Uncertainty**

- Plan and Main execution do not emit Roadmap-style transition journals, so their event traceability is artifact/Git/telemetry based rather than uniform.

## 10. Workflow Eligibility Requirements

### Traditional Roadmap Workflow

**Evidence**

- Roadmap CLI can initialize from no persisted state, requires Project Context preflight, bootstraps roadmap completion context if missing, selects next strategic initiative, audits/promotes active epics, and generates milestone specs.
- Roadmap selection uses roadmap completion context, `.agents/roadmap/*.md`, Project Context, projections, retired epics, and prompt contracts.

**Inference**

- Traditional Roadmap is eligible when Roadmap storage is usable, Project Context is valid, prompt contracts/projections are usable, roadmap source/completion context exist or can be bootstrapped, and no report-only/blocked state prevents run.

**Uncertainty**

- "Traditional Roadmap" is not encoded as a workflow name.

### Eval-Driven Roadmap Workflow

**Evidence**

- Evaluation-driven behavior appears in completion/drift evaluation, completion certification routing, roadmap completion context update, project context evaluation files, and selection prompt/projection semantics.

**Inference**

- Eval-driven roadmap is eligible when execution completion evidence or drift/completion evaluation is available and completion certification/update can run.

**Uncertainty**

- No separate eval-driven CLI/state machine was observed.

### Plan Workflow

**Evidence**

- Eligible only when `.agents/specs/epic.md` exists and planning outputs do not already exist.

**Inference**

- Plan is not eligible for a fresh run when downstream planning artifacts are present.

**Uncertainty**

- No Plan resume eligibility contract exists.

### Execution Workflow

**Evidence**

- Execution CLI starts from repo path, checks milestone completion, ensures operational context from plan when possible, routes by decisions/handoff, runs execution, reviews, publishes, commits, stalls or certifies.

**Inference**

- Execution is eligible when there is enough operational artifact context to run safely: plan/details/operational context, milestone files, decision/handoff state as applicable, Git command availability, and permission/runtime readiness.

**Uncertainty**

- The current code does not preflight all of those inputs before opening execution.

### Storage Workflow

**Evidence**

- Roadmap storage commands bypass normal roadmap state-machine execution.

**Inference**

- Storage commands are eligible independently of Roadmap workflow stage, subject to database/filesystem integrity and domain dependency validation.

**Uncertainty**

- Automatic storage repair/sync before workflow decisions is unspecified.

## 11. Stage Eligibility Requirements

| Stage | May execute when | Must not execute when | Requires recovery/user interaction when |
| --- | --- | --- | --- |
| Roadmap preflight | Active run/resume requires Project Context. | Report-only states require no preflight. | Missing/extra Project Context files. |
| Bootstrap completion context | `CoreReady` and context missing. | Completion context already present. | Projection/prompt failure blocks. |
| Select next epic | Completion context ready, roadmap sources and projection ready. | Selection state is report-only/blocked or projection stale. | Strategic investigation/roadmap revision/no suitable initiative. |
| Continue selection decision | Selection artifact present, lifecycle usable, provenance fresh, contract outputs ready. | Selection stale or invalid. | Regeneration needed or blocker. |
| Epic audit | Existing epic selected and selection readable. | Selection missing/stale. | Insufficient evidence, invalid audit. |
| Create/rewrite/split active epic | Selection/audit context ready and projection usable. | Output fails promotion/validation. | Promotion/split blockers. |
| Generate milestone specs | Active epic ready, projection ready. | Active epic invalid/stale. | Bundle empty/malformed, invariant failure. |
| Roadmap `MilestoneSpecsReady` | Specs fresh and belong to active epic. | Specs missing/stale/mismatched. | Repair spec provenance or active epic. |
| Plan preflight | Required epic input exists; outputs absent. | Any output collision or missing epic input. | User cleanup/authoring. |
| Plan write/revise | Preflight passed; session turn completes; plan nonempty. | Agent turn fails/cancels. | Failed PlanStepException. |
| Plan details/milestones | Scoped operation inputs exist; permissions allow; required outputs pass gates. | Deletes, missing outputs, unchanged plan, no checklist. | Scoped operation repair. |
| Execution first slice | No latest handoff and no live decisions. | Completion gate already true. | Missing runtime/preconditions not uniformly detected. |
| Execution decision proposal | Latest handoff exists and no live decisions. | Live decisions already exist. | Decision turn failure or resume failure. |
| Decision transfer | Router selects Transfer and session is seeded. | Session not seeded. | Scoped transfer failure/rollback. |
| Execution work | Context published; execution session opens. | Cancellation, permission/runtime failure. | Missing handoff after turn fails. |
| Completion certification | Milestone gate true; review ready; active epic/milestones present. | Review blocked, malformed/invalid evaluation, non-closing route. | Human decisions or evidence repair. |
| Storage sync/export/import | Domain dependencies valid; drift compatible or force flag supplied. | Conflict/stale export without force. | User reconciliation. |

## 12. Workflow Dependency Inventory

| Dependency | Classification | Evidence | Reason |
| --- | --- | --- | --- |
| Roadmap -> Plan via prepared epic/spec material | Required for intended chain, but file-level adapter uncertain. | Roadmap writes active epic/specs; Plan requires `.agents/specs/epic.md`. | Plan cannot start without its epic spec input. |
| Roadmap selection -> Project Context/projections | Required and freshness-sensitive. | `ProjectContextLoader`, `ProjectionCache`. | Roadmap prompt decisions depend on current context/projection identity. |
| Active epic -> milestone specs | Required, freshness-sensitive, invalidating. | `ExecutionPreparationProvenanceService`, `InvariantValidator`. | Specs are derived from active epic hash/provenance. |
| Plan -> Execution artifacts | Required by intended chain. | Plan writes plan/context/details/milestones; Execution consumes them. | Execution needs operational artifact set. |
| Execution -> Completion certification | Required for closure. | `LoopRunner`, `CompletionCertificationService`. | Milestone gate only creates claim. |
| Completion certification -> Roadmap completion context | Required for closing routes. | `CompletionCertificationService`, `CompletionCertificationTransition`. | Roadmap selection uses updated completion context. |
| Storage SQLite -> logical artifact resolution | Optional/authority-sensitive. | `RoadmapLogicalArtifactServices`, `LoopHistoryStoreFactory`. | Imported/canonical SQLite changes where history/evidence are read. |
| Git -> execution progress | Required for main execution progress/commit. | `WorkingTreeChangeDetector`, `CommitGate`. | Non-`.agents` Git changes define real progress. |
| Human decisions -> non-implementation completion | Required when unresolved review entries exist. | `NonImplementationCompletionReviewService`. | Completion blocks until decisions are valid. |
| Permission policy -> agent/tool execution | Required runtime dependency. | `AddAgents(settings.Permissions)`, issues. | Permission decisions can block or allow operations. |

## 13. Workflow Invalidation Inventory

**Evidence**

- Project Context hash drift invalidates Roadmap invariants and projection freshness.
- Projection prompt identity/source hash drift, project context drift, and causal input drift make projections stale.
- Active epic hash drift invalidates milestone specs and downstream execution-preparation provenance.
- Milestone spec drift invalidates operational context, execution prompt, and compatibility artifacts.
- Decision ledger drift participates in operational context provenance.
- Storage sync marker drift yields stale export or conflict.
- Lifecycle states can mark artifacts `Archived`, `Superseded`, `Blocked`, or not usable.
- Completed archive movement deletes live plan/details/context/milestones and moves them under archive paths.
- Git working-tree changes alter execution progress and completion review classification.

**Inference**

- What becomes invalid:
  - Roadmap selection: invalidated by roadmap source, roadmap completion context, retired epic state, selection projection/context drift.
  - Active epic-derived specs: invalidated by active epic drift or spec artifact hash drift.
  - Plan outputs: semantically invalidated by changes to upstream epic/specs, but no durable Plan invalidation manifest exists.
  - Execution decisions/handoffs: invalidated by operational context drift, handoff consumption, or live decisions retirement.
  - Completion closure: invalidated or blocked by malformed/invalid evaluation, non-closing route, or unresolved non-implementation review.
  - Filesystem exports: invalidated by database/export drift.

**Uncertainty**

- Plan invalidation is not machine-recorded in the same way as Roadmap projections/derived artifacts.
- Main execution already-closed state is not durable enough to survive archive cleanup, per issue `005`.

## 14. Decision Ambiguity Inventory

| Ambiguity | Evidence | Source | Current behavior |
| --- | --- | --- | --- |
| Traditional vs Eval-Driven Roadmap identity | No command/enum names; semantics spread across prompts/projections/completion. | Semantic labels not encoded. | Treat as Roadmap-domain behavior variants. |
| Roadmap -> Plan handoff | Roadmap active epic/specs vs Plan `.agents/specs/epic.md`. | No unified command. | Roadmap pauses at `MilestoneSpecsReady`; Plan preflight expects its own input. |
| Plan partial outputs | Plan has no state doc/journal. | Artifact-only stage evidence. | Fresh Plan blocks on existing outputs. |
| Execution preflight | Main CLI does not expose status/preflight. | Execution inputs consumed inline. | Missing inputs may fail later or produce weak prompts. |
| Completion owner | Main CLI and Roadmap transition both certify/update. | Shared completion services. | Main CLI certifies from milestone gate; Roadmap certifies from `EpicCompletionDetected`. |
| SQLite vs filesystem authority | README says SQLite canonical; code falls back. | Dual persistence. | Roadmap/Main choose SQLite only when imported/canonical. |
| Recovery intent coverage | Several unblock intents unsupported. | `RoadmapUnblockPlanner`. | Unsupported intents report/persist review failure. |
| Archive retry safety | Issue `004`; archive service deletes live sources before synthesis/update complete. | Partial side effects. | Failure can strand retry inputs. |
| Archive index allocation | Issue `006`; count+1 can collide. | Non-contiguous directories. | Completion can hard fail on collision. |
| Permission trust | Issues `001`, `008`, `009`, `010`. | Parser/protocol uncertainties. | Some approval surfaces may be over- or under-permissive. |

## 15. Human Decision Inventory

**Evidence**

- Non-implementation completion review writes `.agents/review/non-implementation-decisions.md` and blocks until every unresolved row has a valid human decision.
- Allowed file decisions in that path are `Keep`, `Delete`, `ResolveFalsePositive`, `Defer`; synthesis decisions include `KeepSynthesis`, `DiscardSynthesis`, `DeferSynthesis`.
- Roadmap selection can pause at `StrategicInvestigationRequired`, `RoadmapRevisionRequired`, `NoSuitableInitiative`, or `EvidenceGathering`.
- Roadmap blockers carry required next steps and evidence paths.
- Roadmap elevation flags require a non-empty reason.
- Permission policy and Codex approval requests can require human approval/denial.

**Inference**

- Human authority currently covers: strategic judgment, roadmap revision, evidence investigation, blocker repair, non-implementation acceptance/deletion/defer decisions, permission/elevation, and manual storage conflict reconciliation.

**Uncertainty**

- There is no single queue, state type, or global lifecycle for all human decisions.

## 16. Concurrency Constraints

**Supported by evidence**

- `Roadmap status` is read/report oriented: it loads persisted state and prints status/blockers.
- `storage-verify` is intended read-only and checks for unexpected database mutation with a hash guard.
- Plan and Main execution each run serially inside their process.
- Decision and execution sessions are sequenced by `LoopRunner`; Plan pipeline steps are ordered by `PlanPipeline`.

**Unsupported or unsafe by evidence**

- Concurrent workflow mutations against the same `.agents`, SQLite database, or Git repo are not shown as supported.
- Storage import/export/sync can mutate authority surfaces and should not be assumed safe alongside active workflow mutation.
- Main execution and Plan both mutate `.agents` and publish the submodule; no cross-process lock was observed.

**Unknown**

- Whether future `looprelay` status can safely run concurrently with active Plan/Execution/Roadmap beyond read-only behavior.
- Whether SQLite workflow transaction markers are intended as recovery markers only or also as concurrency coordination evidence. Current code records markers but does not implement a general lock.

## 17. Decision Lifetime

| Decision/fact | Lifetime | Evidence | Invalidation |
| --- | --- | --- | --- |
| CLI parse result | Single process invocation. | `Program.cs`. | New invocation. |
| Roadmap startup/resume plan | Until state/artifacts/projections change or process advances. | Planners compute from current facts. | State save, artifact mutation, projection drift. |
| Projection freshness | Until prompt/source/project/causal input hash changes. | `ProjectionFreshnessEvaluator`. | Drift reasons. |
| Active selection reuse | Current selection cycle only. | `SelectionProvenanceService` usage. | Roadmap source/completion/retired/projection drift. |
| Active epic promotion | Until superseded, archived, blocked, lifecycle changed, or content drift invalidates dependents. | Lifecycle/provenance services. | Lifecycle/state/hash changes. |
| Milestone specs readiness | Until active epic/spec/provenance changes. | `ExecutionPreparationProvenanceService`. | Active epic/spec drift. |
| Plan preflight decision | Instantaneous at run start. | `PreflightGate`. | Any relevant file created/deleted. |
| Execution decision proposal | Until live `decisions.md` is consumed/retired or superseded by new proposal. | `LoopArtifacts`, `LoopRunner`. | Execution success retires live decisions. |
| Decision session resume state | Across runs until cleared, stale, failed, disabled for read, or epic closes. | `SqliteDecisionSessionResumeStore`, `DecisionSession`, `LoopRunner`. | Projection staleness, resume failure, successful completion clear. |
| Stall counter | Current process lifetime. | `CommitGate` field. | Process restart or progress reset. |
| Completion claim | Until certification closes/blocks/fails, but archive can move live evidence. | Completion claim evidence. | Certification/archival side effects. |
| Storage sync marker | Until database/export hash drift. | `WorkspaceSyncService`. | Import/export/sync changes. |
| Human decision row | Until ledger target hash/status changes or decision applied. | `NonImplementationCompletionReviewService`. | File hash/status drift, ledger update. |

## 18. Decision Traceability

**Traceable decisions**

- Roadmap transitions: transition journal, state document, transition intent, blocker rows, input snapshots, output paths.
- Roadmap selection/completion decisions: decision ledger entries with prompt/projection/output/confidence/rationale.
- Projection decisions: projection manifest validation/freshness status and stale reasons.
- Artifact promotion: lifecycle entries, promotion/blocker evidence, journal events.
- Milestone spec readiness: execution-preparation manifest and invariant evidence.
- Storage authority: validation result, sync markers, verification findings.
- Execution decision history: live/numbered decisions, handoffs, deltas, SQLite loop history when available.
- Completion certification: completion claim, evaluation evidence, blocker evidence, archive synthesis, update evidence.
- Non-implementation human decisions: ledger entries and decision file hash metadata.

**Weakly traceable decisions**

- Plan stage completion: artifacts and `.agents` publication commits, but no Plan state/journal.
- Main execution stage route: artifacts and console flow, but no named persisted execution state.
- Stall count: in-memory only.

**Uncertainty**

- A future architect cannot reconstruct every Plan/Main execution branch from a single canonical ledger today.

## 19. Decision Invariants

**Evidence and inference**

1. Repository-relative persistence paths must not escape the repository. Evidence: `LoopRelayWorkspaceDatabase.Resolve`.
2. `.agents` is the behavior-bearing artifact root. Evidence: `OrchestrationArtifactPaths`.
3. `.agents` submodule changes do not count as main execution real progress. Evidence: `WorkingTreeChangeDetector`, `CommitGate`.
4. Main execution progress is non-`.agents` Git changes or reduced unticked milestone items. Evidence: `CommitGate`.
5. Milestone completion requires at least one strict checkbox and all strict checkboxes checked outside fenced code. Evidence: `MilestoneGate`.
6. Milestone completion is not epic closure. Evidence: `CompletionCertificationService`.
7. Plan fresh start requires absent downstream outputs and present `.agents/specs/epic.md`. Evidence: `PreflightGate`.
8. Roadmap Project Context requires the canonical nine files and no extra numbered files. Evidence: `ProjectContextSourceContract`.
9. Roadmap projections require validation and freshness under stale policy. Evidence: `ProjectionCache`.
10. Every Roadmap projection must have a prompt contract. Evidence: `PromptContractRegistry` constructor check.
11. Prompt output must pass parser/validator/promotion before downstream use. Evidence: transition classes and `ArtifactPromotionService`.
12. Active epic lifecycle must not have duplicate Ready/Executing active epics. Evidence: `InvariantValidator`.
13. Milestone specs must belong to the active epic. Evidence: `ValidateSpecsAsync`, `InvariantValidator`.
14. `MilestoneSpecsReady` is a Roadmap terminal pause in current CLI. Evidence: `RoadmapResumePlanner`.
15. Decision resume state is scoped to the decision session and must be cleared on stale projection/resume failure/epic completion. Evidence: `DecisionSession`, `LoopRunner`.
16. Storage sync must respect domain dependency validation. Evidence: `WorkspaceSyncDependencyValidator`.
17. Completion closing routes require roadmap completion context update. Evidence: `CompletionCertificationRouter`.
18. Non-implementation completion blockers require human decisions before certification proceeds. Evidence: `NonImplementationCompletionReviewService`.
19. Cancellation is distinct from failure in all CLI exit mappings. Evidence: CLI `Program.cs` files.
20. Known archive/idempotency issue records must qualify completion recovery confidence. Evidence: `issues/004`, `005`, `006`.

## 20. Architectural Constraints

These are observed constraints for any future decision engine; they are not design recommendations.

**Evidence/inference constraints**

- The decision engine must preserve existing CLI boundaries unless a future product decision changes them.
- It must report whether a conclusion is based on evidence, inference, or uncertainty.
- It must resolve persistence authority before trusting SQLite or filesystem-derived facts.
- It must allow multiple relevant workflow identities in one workspace, such as Roadmap paused at `MilestoneSpecsReady` and Plan eligible next.
- It must treat prompt completion, artifact existence, and workflow completion as separate facts.
- It must preserve Roadmap projection freshness and prompt contract authority.
- It must not treat Plan partial outputs as safe resume evidence without additional proof.
- It must not treat main execution milestone completion as epic closure.
- It must account for `.agents` exclusion from real Git progress.
- It must preserve human/HITL blockers and permission/elevation requirements as first-class decision constraints.
- It must preserve storage conflict/stale/export verification findings rather than silently choosing a side.
- It must treat known issue records as confidence-lowering evidence for recovery/idempotency/permission decisions.
- It must keep cancellation, failure, paused, blocked, stalled, completed, and ambiguous distinct.
- It must not invent current commands or flags that are not in repository evidence.

## 21. Unknowns

1. What exact product behavior should `looprelay` expose?
2. Should "Traditional Roadmap" and "Eval-Driven Roadmap" become explicit workflow identities or remain inferred Roadmap behavior variants?
3. What is the authoritative adapter from Roadmap active epic/spec outputs to Plan's `.agents/specs/epic.md` input?
4. Should Roadmap -> Plan -> Execution ever be automatically chained by default?
5. Is SQLite intended to be mandatory before orchestration decisions, or should fallback remain valid per domain?
6. What is the global precedence rule when SQLite canonical state and filesystem exports disagree?
7. What are Plan partial-output recovery semantics?
8. What are Main execution partial-state recovery semantics beyond live decisions/handoff artifacts?
9. What durable completed-state marker should survive completion archive cleanup?
10. How should archive retry behave after partial archive materialization?
11. How should completed epic archive indexes be allocated after gaps or non-numeric directories?
12. Which completion certification path is globally authoritative when Main CLI and Roadmap evidence differ?
13. Which Roadmap unblock intents are intentionally unsupported versus not yet implemented?
14. How should HITL blockers be scoped across workflow boundaries?
15. What concurrency model should govern simultaneous status, storage verify, storage sync, Plan, Roadmap, Execution, and human edits?
16. What release evidence is required before scoped artifact app-server operations are trusted without sandbox fallback?
17. How should permission-policy hard-deny invariants be protected from configurable parser bypasses?
18. Should telemetry absence ever affect decision confidence?
19. Are legacy Roadmap execution-preparation states permanent compatibility states or future active states?
20. What traceability level is required for Plan and Main execution to match Roadmap transition journaling?
