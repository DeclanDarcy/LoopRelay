# LoopRelay.Plan.Cli and State Machine Refactor Discovery Audit

This audit records the current architecture and behavioral constraints of `LoopRelay.Plan.Cli`, the roadmap state machine in `LoopRelay.Roadmap.Cli`, and adjacent components that materially constrain future simplification. It is intentionally observational. It does not define a redesign, roadmap, extraction sequence, or implementation plan.

Evidence labels refer to the inspected source files in this repository. Inference labels describe conclusions drawn from that evidence. Uncertainty labels identify questions that remain unresolved from static inspection.

## 1. Executive Summary

Evidence:

- `LoopRelay.Plan.Cli` is a linear orchestration pipeline, not a durable state machine. `Program.cs` parses one repository argument, wires cancellation, invokes `PlanPipeline.RunAsync`, and maps outcomes to process exit codes.
- `PlanPipeline` orders preflight, plan authoring, projection, adversarial review, plan revision, artifact operations, `.agents` publication, and parent gitlink recording. The file comment explicitly says it mirrors the reference CLI `LoopRunner` while containing no agent logic itself.
- `LoopRelay.Roadmap.Cli` contains the durable state machine. `RoadmapStateMachine` handles run/status/unblock, startup and resume planning, durable state writes, transition persistence, unblock recovery, cancellation state, and reporting.
- `RoadmapState` contains both active domain states and historical or legacy execution-preparation states. `RoadmapWorkflowStateClassifier` classifies several execution-preparation states as terminal pauses because legacy execution preparation is no longer advanced.
- Main execution is now constrained by `LoopRelay.Cli`, especially `LoopRunner`, `ExecutionStep`, `DecisionSession`, `CommitGate`, and completion certification. These components define operational behavior that a future state-machine simplification must preserve or consciously account for.

Inference:

- The strongest current architectural assets are explicit prompt contracts, durable transition journals, artifact lifecycle records, projection freshness checks, operation-scoped permission gates, evidence preservation, and clearly observable exit-code behavior.
- The largest liabilities are concentration of orchestration decisions in `RoadmapStateMachine`, large manual composition roots, dual file/SQLite persistence behavior, legacy execution states that remain visible in the enum, and hidden coupling between prompt contracts, projections, artifact lifecycle, decision ledgers, and resume rules.
- Major simplification opportunities exist around separating durable domain state from orchestration mechanics, reducing pass-through work inside the state machine, clarifying which states are domain-visible versus implementation artifacts, and reconciling roadmap state with the separate main execution loop. These are observations, not prescriptions.

Uncertainty:

- Static inspection cannot prove which historical behaviors are still relied on by users or external automation.
- The intent behind retaining legacy execution-preparation states is only partly documented by comments and classifier messages.

## 2. State Machine Purpose

Evidence:

- `RoadmapStateMachine.ExecuteAsync` routes only `status`, `run`, and `unblock` commands. Storage commands are handled earlier in `Roadmap.Cli.Program`.
- `RoadmapStateMachine.RunAsync` loads durable state, loads project context, emits prompt contract snapshots, asks `RoadmapResumePlanner` for an action, and executes the selected workflow branch.
- `RoadmapResumePlanner` decides whether to initialize, continue selection, generate milestone specs, evaluate completion claims, report terminal pause states, or block unsafe resume attempts.
- `RoadmapUnblockPlanner` validates evidence for a narrow set of recovery intents and rejects unsupported unblock routes with explicit reasons.
- `RoadmapTransitionPersistence` centralizes state saves, decision-ledger writes, journal records, projection summaries, active artifact summaries, transition intents, and next-valid-transition derivation.

Intended responsibilities:

- Maintain durable roadmap workflow position.
- Resume safely after interrupted or partial transitions.
- Persist transition results, transition intent, blockers, summaries, and next valid transitions.
- Preserve evidence for review, completion, and recovery.
- Prevent stale projections or invalid artifact state from driving later prompts.

Actual responsibilities:

- Command execution for `run`, `status`, and `unblock`.
- Startup, resume, and unblock planning coordination.
- Project context preflight reporting.
- Prompt contract snapshot emission.
- Transition dispatch and some transition outcome interpretation.
- Durable state persistence and transition intent updates through `RoadmapTransitionPersistence`.
- Some user-visible reporting.
- Cancellation persistence.
- Recovery journal writing.

Historical responsibilities that remain:

- Execution-preparation states remain in `RoadmapState`, but resume planning now treats `GenerateOperationalContext`, `OperationalContextReady`, `GenerateExecutionPrompt`, `ExecutionPromptReady`, and `ExecutionLoop` as terminal pause/report-only states.
- `RoadmapExecutionBridge` and execution-disposition parsing still exist, but the roadmap composition does not wire the bridge into the active state-machine path. Main execution is handled by `LoopRelay.Cli`.

Responsibilities that have drifted into the state machine:

- User-facing status reporting and blocker printing.
- Mapping selection outcomes to state persistence and next transitions.
- Recovery-specific journal record construction.
- Cancellation recovery-state selection.
- Some artifact readiness decisions that also live in snapshot, lifecycle, and resume-planner services.

Responsibilities that appear accidental:

- The state machine knows enough about many transition output paths and evidence paths to recover or report them.
- `RoadmapStateDocument` mixes current state, active artifacts, last transition, blockers, transition intent, projection counts, retired epics, and next-valid-transition presentation.
- Legacy execution-preparation visibility creates domain-looking states that are now mostly orchestration history.

## 3. State Inventory

### Plan CLI Observable Phases

`LoopRelay.Plan.Cli` has no durable enum state. Its meaningful states are pipeline phases in `PlanPipeline`.

| Phase | Purpose, entry, and exit | Owned responsibilities and invariants | Dependencies and observable effects |
| --- | --- | --- | --- |
| Argument validation | Entry is process startup. Exit is a parsed repository path or usage failure. | Requires exactly one existing directory. Assigns a new repository identity. | `CliArguments.TryParse`; invalid input throws usage error. |
| Composition | Entry after argument parse. Exit is a constructed `PlanPipeline`. | Loads settings and prompt policy; wires agents, stores, prompts, projections, review, artifact operations, and publisher. | `PlanCliComposition`; observable logs include repo path and Codex executable. |
| Preflight | Entry is `PlanPipeline.RunAsync`. Exit is continue or `PreflightBlocked`. | Requires absent plan/operational context/details and no existing milestones; requires specs or epic material. | `PreflightGate`; exit code 4 on preflight block. |
| Plan authoring | Entry after preflight. Exit requires completed authoring turn and non-empty `.agents/plan.md`. | Holds one persistent PlanAuthoring session; captures HITL output. | `PlanSession.WritePlanAsync`; writes `.agents/plan.md`. |
| Plan publication | Entry after plan write. Exit after `.agents` publish. | Publishes `.agents` submodule only if changed. | `AgentsSubmodulePublisher.PublishAsync`; git side effects in `.agents`. |
| Adversarial projection | Entry after plan publish. Exit requires projection content and publication. | Generates projected diff or future impact view for review. | `AdversarialProjectionService`; publishes `.agents`. |
| Adversarial review | Entry after projection. Exit requires parseable PASS/CONDITIONAL PASS/FAIL verdict output. | Uses fresh read-only review session and zero permissions. | `ReviewStep`; no publish after review in pipeline. |
| Plan revision | Entry after review. Exit requires same warm authoring session to revise plan and non-empty plan gate. | Keeps plan-authoring context alive for delta-only revision; then closes session. | `PlanSession.ReviseAsync` and `CloseAsync`; writes plan and captures HITL. |
| Operational context seed | Entry after revision. Exit after `.agents/operational_context.md` exists. | Seeds operational context from plan only if missing. | `PlanArtifacts.WriteOperationalContextFromPlanIfAbsentAsync`; writes context. |
| Revision publication | Entry after operational context seed. Exit after `.agents` publish. | Publishes revised plan/context state. | `AgentsSubmodulePublisher.PublishAsync`. |
| Collect details | Entry after publish. Exit requires `.agents/details.md` non-empty and allowed writes changed. | Uses scoped artifact operation session with transaction rollback on failure. | `PermissionedArtifactOperationStep`; HITL prompt `CollectDetails`. |
| Extract milestones | Entry after details. Exit requires milestone glob non-empty, checklist content, and changed files. | Uses scoped artifact operation session; reads plan and milestone glob. | `ExtractMilestonesStep`; writes `.agents/milestones/*.md`. |
| Extract details | Entry after milestones. Exit requires details and milestones valid. | Uses scoped artifact operation session. | `ExtractDetailsStep`; updates details and milestones. |
| Parent gitlink recording | Entry after any `.agents` publication. Exit after parent repo records submodule gitlink. | Commits and pushes parent `.agents` gitlink if submodule commits were published. | `AgentsSubmodulePublisher.RecordParentGitlinkAsync`. |
| Terminal outcome | Entry from completion, cancellation, or failure. | Maps `Completed` to 0, `PreflightBlocked` to 4, `Cancelled` to 130, other failure to 1. | `Plan.Cli.Program`; logs outcome through console. |

### Roadmap Durable States

The durable state enum is `RoadmapState`. The inventory below distinguishes domain-visible states from orchestration, bookkeeping, and historical states.

| State | Purpose and entry | Exit and observable effects | Invariants, dependencies, and ownership |
| --- | --- | --- | --- |
| `CoreReady` | Baseline state after fresh initialization or successful preflight-blocker recovery. | Bootstraps roadmap completion context or resumes from core. | Requires project context to load before run. Persisted through `RoadmapTransitionPersistence`. |
| `BootstrapRoadmapCompletionContext` | In-flight or historical marker for creating completion context. | Resume planner treats it like `CoreReady` and continues from core. | Uncertainty: no inspected transition appeared to persist this as a durable steady state. |
| `RoadmapCompletionContextReady` | Completion context artifact exists and can inform selection. | Runs next strategic initiative selection. | Depends on `.agents/core/roadmap-completion-context.md`, projection readiness, prompt contracts. |
| `SelectNextStrategicInitiative` | Selection prompt has produced selection evidence and a decision. | Continues to existing, new, split, strategic investigation, roadmap revision, or no suitable initiative handling. | Requires selection output, selection provenance, lifecycle readiness, decision ledger entry. |
| `ExistingEpicSelected` | Selection outcome chose an existing epic. | Runs epic preparation audit. | Depends on active selection and roadmap source. |
| `NewEpicProposed` | Selection outcome proposed a new epic. | Runs new-epic creation and promotion. | Depends on selection evidence and projection context. |
| `SplitEpicProposed` | Selection outcome proposed splitting an epic. | Runs split epic bundle generation. | Depends on selected source epic and split prompt output. |
| `EpicPreparationAudit` | Existing epic has been audited. | May proceed to active epic, retire, realign, reimagine, or block for evidence. | Requires audit evidence and parsed audit decision. |
| `RealignEpic` | Audit disposition requires realigning an epic. | Runs active epic rewrite and promotion. | Depends on audit evidence and active selection. |
| `ReimagineEpic` | Audit disposition requires reimagining an epic. | Runs active epic rewrite and promotion. | Depends on audit evidence and active selection. |
| `RetireEpic` | Existing epic was retired. | Returns to selection. | Updates retired-epic tracking and supersedes selection. |
| `EvidenceBlocked` | Durable blocked state for missing, invalid, stale, or untrusted evidence. | Status reports blocker; unblock may recover only supported intents. | Requires blocker text, last transition/intent, evidence path when available. |
| `EvidenceGathering` | Completion route requires more evidence before closing or continuing. | Classified as terminal pause/report-only. | Active epic typically remains ready. Resume planner does not advance it automatically. |
| `CreateNewEpic` | In-flight/historical creation state for a proposed new epic. | Promotes candidate to active epic or blocks. | Promotion validation owns final active-epic effect. |
| `SplitEpic` | In-flight split generation state. | Produces split child selection evidence. | Split family store and lifecycle participate. |
| `SplitChildSelection` | Split output has child candidates and selected child evidence. | Promotes selected child to active epic or blocks. | Depends on split lineage, selected child, and active epic promotion. |
| `ActiveEpicReady` | A usable active epic is ready for execution preparation. | Generates milestone deep dives/specs. | Requires active epic artifact, lifecycle ready/executing/completed compatibility, and validation. |
| `GenerateMilestoneDeepDives` | In-flight marker for spec generation. | Materializes milestone specs or blocks/fails. | Depends on prompt contract, active epic, projection, execution prep manifest. |
| `MilestoneSpecsReady` | Milestone specs are generated and owned by the active epic. | Currently terminal pause; resume planner reports it stops before execution context generation. | Requires fresh specs, execution preparation manifest, and spec ownership. |
| `GenerateOperationalContext` | Legacy execution-preparation state. | Terminal pause/report-only in resume planner. | Historical dependency on operational context generation. |
| `OperationalContextReady` | Legacy state indicating operational context exists. | Terminal pause/report-only in resume planner. | Context is now also used by `LoopRelay.Cli`. |
| `GenerateExecutionPrompt` | Legacy execution-prompt generation state. | Terminal pause/report-only in resume planner. | Historical execution bridge path. |
| `ExecutionPromptReady` | Legacy state indicating execution prompt exists. | Terminal pause/report-only in resume planner. | `RoadmapUnblockPlanner` treats runtime repair as legacy/no longer advanced. |
| `ExecutionLoop` | Historical or externally reached execution state. | May be used for completion claim evaluation if persisted evidence exists; otherwise terminal pause/report-only. | Main execution loop now lives in `LoopRelay.Cli`. |
| `ExecutionBlocked` | Execution disposition reported a blocker. | Report-only and unblock-limited. | `ResolveExecutionBlocker` is marked unsupported in unblock planner. |
| `EpicCompletionDetected` | Execution evidence claims an epic is complete. | Runs completion certification and evaluation. | Depends on execution evidence path and completion policy. |
| `CompletionEvaluationAndContextUpdate` | Completion evaluation has been produced. | Routes to close, continue, reopen, gather evidence, or block. | Depends on completion evaluation parser, policy, route mapper, completion context update. |
| `StrategicInvestigationRequired` | Selection concluded investigation is required. | Terminal pause/report-only. | Decision-ledger and selection evidence preserve why. |
| `RoadmapRevisionRequired` | Selection concluded roadmap revision is required. | Terminal pause/report-only. | Decision-ledger and selection evidence preserve why. |
| `NoSuitableInitiative` | Selection found no suitable initiative. | Terminal pause/report-only. | Decision-ledger and selection evidence preserve why. |
| `Completed` | Terminal success. | Status reports terminal state; run does not preflight. | Represents no more roadmap action required under current workflow. |
| `Failed` | Terminal failure or invariant failure. | Status reports failure; unblock only if supported intent exists. | May preserve failure evidence and transition intent. |
| `Cancelled` | Cancellation marker persisted with a recovery state. | Resume planner maps back to the cancelled recovery state when safe. | Requires `CancelledRecoveryState` in transition intent evidence paths. |

Inference:

- Many enum values are not equally domain-significant. Selection outcomes, active epic readiness, evidence blocking, completion evaluation, and terminal selection results are domain-visible. Several generation and execution-preparation states exist to make orchestration resumable or to preserve historical workflow shape.

## 4. Transition Inventory

### Shared Transition Semantics

Evidence:

- `RoadmapPromptTransitionRunner` wraps prompt transitions with started/completed/failed journal records and state saves.
- Prompt transition failures persist either `EvidenceBlocked` or `Failed` with `ResolveTransitionFailure` intent before throwing `RoadmapTransitionAlreadyPersistedException`.
- Cancellation is handled by `RoadmapStateMachine.RunAsync`, which writes `Cancelled` with a recovery state and returns `Cancelled`.
- `Roadmap.Cli` uses `InputWaitProgressAgentRuntime`, not `GatedAgentRuntime`; usage-limit retry behavior is active in the main loop composition, not in roadmap or plan CLI composition.
- `PermissionedArtifactOperationStep` has transaction rollback for scoped plan artifact mutations.

Inference:

- Most roadmap transitions have durable started/completed/failed journal records. Plan pipeline transitions do not have an equivalent durable state journal.
- Retry behavior is deliberately sparse in these CLIs. Prompt re-entry is mediated mostly by durable state/resume/unblock, not automatic retry.

### Plan CLI Transitions

| Transition | Trigger and destination | Side effects, persistence, and interactions | Cancellation, failure, retry, human recovery |
| --- | --- | --- | --- |
| Process start to preflight | `Program.Main` receives a valid repo path. | Builds composition, logs repo and Codex path. | Invalid usage exits through exception handling path; no retry. |
| Preflight to blocked | Existing plan/context/details/milestones or missing source material. | No plan mutation; returns `PreflightBlocked`. | Exit code 4. Recovery is manual cleanup/input correction. |
| Preflight to write plan | Preflight has no violations. | Opens warm authoring session. | Caller token cancellation returns 130. |
| Write plan to publish | Authoring turn completes and plan gate passes. | Writes `.agents/plan.md`, captures HITL, publishes `.agents`. | Non-empty plan gate failure becomes `PlanStepException`; no automatic retry. |
| Publish to adversarial projection | Plan publish completes. | Projection generated and `.agents` published. | Projection errors become pipeline failure. |
| Projection to review | Projection exists. | Fresh read-only review session writes review output in memory/HITL capture. | Review output must be completed and parseable. |
| Review to revise plan | Review verdict exists. | Same warm authoring session receives revision prompt; plan gate reruns. | Failure closes through pipeline exception handling; no durable resume. |
| Revise to seed operational context | Revision completed. | Warm authoring session closes; operational context is seeded from plan if absent. | Close is guarded once; seed failure fails pipeline. |
| Seed to publish revision | Operational context exists. | Publishes `.agents`. | Git/submodule failures wrapped as plan step failures. |
| Publish to collect details | Revision publish completed. | Scoped artifact operation can write details only; transaction captured. | On operation failure, transaction restore is attempted. Human interaction flows through approval gateway. |
| Collect details to extract milestones | Details output gate passes. | Scoped operation writes milestone files and requires checklist/glob content. | Output gate and changed guard protect empty/no-op transitions. |
| Extract milestones to extract details | Milestone gate passes. | Scoped operation updates details and milestones. | Same permission and rollback behavior. |
| Artifact operations to parent gitlink | At least one `.agents` publish occurred. | Parent repo records submodule gitlink commit/push. | Git failures fail pipeline after submodule work. |
| Any phase to terminal | Completion, cancellation, or exception. | Maps outcome to exit code; disposes composition. | No durable plan-resume state exists. |

Classification:

- Domain transitions: plan authoring, adversarial review, plan revision, detail extraction, milestone extraction.
- Orchestration transitions: publishing, projection generation, session open/close, operational-context seeding.
- Bookkeeping transitions: parent gitlink recording, exit-code mapping.
- Implementation artifacts: warm authoring session reuse, ChangedGuard-specific checks, scoped artifact operation transaction boundaries.

### Roadmap State Transitions

| Transition | Trigger and destination | Side effects, persistence, and interactions | Cancellation, failure, retry, human recovery |
| --- | --- | --- | --- |
| No state to `CoreReady` | `run` on a repo with no durable state. | Saves initial durable state after project context preflight. | Project context failure returns `PreflightBlocked` without a normal transition. |
| `CoreReady` to `RoadmapCompletionContextReady` | Startup/resume planner selects continue-from-core and completion context is missing or stale. | Runs `CreateOrUpdateRoadmapCompletionContext`; writes completion context, HITL capture, lifecycle ready, journal/state records. | Prompt failure persists blocked/failed state through transition runner. |
| `RoadmapCompletionContextReady` to `SelectNextStrategicInitiative` | Selection is required. | Ensures projection, writes selection, selection evidence, provenance, lifecycle, decision ledger. | Stale projection or invalid manifest blocks through projection/resume checks. |
| `SelectNextStrategicInitiative` to `ExistingEpicSelected` | Parsed selection recommends existing epic. | Saves selection decision context and continues to audit. | Invalid or stale selection blocks resume. |
| `SelectNextStrategicInitiative` to `NewEpicProposed` | Parsed selection recommends new epic. | Saves proposed new epic path/state and continues to creation. | Promotion later determines active-epic effect. |
| `SelectNextStrategicInitiative` to `SplitEpicProposed` | Parsed selection recommends split. | Saves split proposal state and continues to split transition. | Split parser/policy owns later blocking. |
| `SelectNextStrategicInitiative` to terminal pause states | Selection recommends strategic investigation, roadmap revision, or no suitable initiative. | Saves decision and terminal pause state. | No automatic recovery except future run/status behavior. |
| `ExistingEpicSelected` to `EpicPreparationAudit` | Existing epic selected. | Runs audit prompt, writes numbered audit evidence, captures HITL, appends decision. | Insufficient evidence path may throw; static inspection leaves exact durable failure path uncertain. |
| `EpicPreparationAudit` to `ActiveEpicReady` | Audit says epic can proceed. | Active epic is validated/promoted and lifecycle updated. | Promotion failure persists `EvidenceBlocked`. |
| `EpicPreparationAudit` to `RetireEpic` | Audit disposition says retire. | Retired epic is upserted, selection superseded, state saved. | Resume returns to selection. |
| `EpicPreparationAudit` to `RealignEpic` or `ReimagineEpic` | Audit disposition requires rewrite. | Runs rewrite prompt and candidate promotion. | Promotion block persists evidence. |
| `NewEpicProposed`/`CreateNewEpic` to `ActiveEpicReady` | New-epic prompt produces promotable candidate. | Writes active epic, lifecycle, HITL, journal, state. | Invalid candidate preserves numbered evidence and blocks. |
| `SplitEpicProposed`/`SplitEpic` to `SplitChildSelection` | Split prompt produces candidate bundle. | Writes split family artifacts and selected child evidence. | Invalid extraction or interpretation blocks with `ResolveSplitEpicBlocker`. |
| `SplitChildSelection` to `ActiveEpicReady` | Selected child is promotable. | Promotes child as active epic; persists split lineage. | Promotion block persists evidence. |
| `ActiveEpicReady`/`GenerateMilestoneDeepDives` to `MilestoneSpecsReady` | Milestone specs prompt succeeds and postprocessing validates. | Writes milestone spec files, manifest, lifecycle, execution preparation manifest, journal, state. | Postprocessing or invariant failure persists `EvidenceBlocked` or `Failed`; no automatic retry. |
| `MilestoneSpecsReady` to pause | Resume planner sees specs ready. | Reports terminal pause: stops before execution context generation. | No transition is advanced by current roadmap state machine. |
| Legacy execution-preparation states to pause | Resume planner sees `GenerateOperationalContext`, `OperationalContextReady`, `GenerateExecutionPrompt`, `ExecutionPromptReady`, or `ExecutionLoop`. | Reports legacy pause/no active advancement. | Unblock for runtime repair reports legacy no longer advanced. |
| `ExecutionLoop` to `EpicCompletionDetected` | Completion certification is invoked with persisted execution evidence. | Persists completion claim and transition intent when configured. | Requires execution evidence path; invalid disposition can block. |
| `EpicCompletionDetected` to `CompletionEvaluationAndContextUpdate` | Completion claim evaluation runs. | Optional non-implementation review; evaluation prompt; evidence write; HITL; parser/policy validation; decision ledger. | Invalid certification persists `EvidenceBlocked` with `ResolveInvalidCompletionCertification`. |
| `CompletionEvaluationAndContextUpdate` to `SelectNextStrategicInitiative` | Completion route closes epic. | Archives completed epic, updates roadmap completion context, marks active epic completed, state saved. | Completion context update failures become transition failures. |
| `CompletionEvaluationAndContextUpdate` to `ExecutionLoop` | Completion route says continue epic. | Active epic remains executing; state pauses for continued execution. | Execution loop itself is outside roadmap state machine. |
| `CompletionEvaluationAndContextUpdate` to `EpicPreparationAudit` | Completion route reopens epic. | Active epic ready; state returns to audit path. | Depends on evaluation route validity. |
| `CompletionEvaluationAndContextUpdate` to `EvidenceGathering` | Completion route requests more evidence. | Active epic ready; terminal pause. | Manual evidence gathering expected outside this transition. |
| Any transition to `EvidenceBlocked` or `Failed` | Prompt failure, parser failure, invariant failure, stale projection, invalid promotion, or policy failure. | Writes blocker evidence, transition intent, journal records, and state. | `unblock` can recover only supported intents. |
| Any cancellable run to `Cancelled` | Caller cancellation token fires. | Saves cancelled state, recovery target, blocker text, and next transition. | Resume planner uses `CancelledRecoveryState` to plan recovery. |
| `EvidenceBlocked`/`Failed`/`ExecutionBlocked` to recovery state | `unblock` command with supported intent and valid evidence. | Appends review/journal; saves target state or reports failed recovery. | Unsupported recovery intents append blocker/review and remain paused or failed. |

Classification:

- Domain transitions: selection outcome routing, audit disposition routing, active epic promotion, split child selection, completion route mapping, terminal strategic investigation/roadmap revision/no-suitable outcomes.
- Orchestration transitions: prompt started/completed markers, projection generation, context construction, completion context refresh, HITL capture, resume planning.
- Bookkeeping transitions: decision-ledger append, lifecycle update, projection manifest update, transition journal append, next-valid-transition derivation, storage sync/import/export.
- Implementation artifacts: legacy execution-preparation states, warm/cold session distinctions, prompt-contract snapshot emission as part of run startup, SQLite workflow marker coordination.

## 5. Responsibility Mapping

| Responsibility | Current owner | Logical owner observed from cohesion | Producers and consumers | Lifecycle, coupling, and mixed concerns |
| --- | --- | --- | --- | --- |
| CLI argument parsing and exit codes | `Program.cs` in each CLI | CLI shell | User/process produces args; shell/automation consumes exit codes | Cohesive; tightly coupled to public behavior. |
| Plan orchestration order | `PlanPipeline` | Plan workflow sequencer | Plan session, review, projection, artifact ops, publisher | Cohesive ordering, but git publication and artifact gates are interleaved with domain phases. |
| Plan authoring session lifecycle | `PlanSession` | Plan authoring workflow | Agent runtime produces turns; plan artifacts consume output | Cohesive; warm-session reuse is a hidden behavioral dependency. |
| Scoped plan artifact mutation | `PermissionedArtifactOperationStep` | Artifact operation service | Prompt runtime writes; gates/transaction consume | Mixed permissions, transaction, prompt, output validation, HITL capture. |
| Roadmap command routing | `Roadmap.Cli.Program` plus `RoadmapStateMachine` | CLI plus state-machine facade | User commands produce actions; state/report consumers | Storage commands bypass machine; run/status/unblock enter it. |
| Startup planning | `RoadmapStartupPlanner` | State/resume planning service | State store and artifact snapshot produce facts; state machine consumes plan | Cohesive. |
| Resume planning | `RoadmapResumePlanner` | Resume safety service | State, artifacts, lifecycle, projections produce facts; machine consumes action | High cohesion around safety, high coupling to artifact shape and prompt contracts. |
| Unblock planning | `RoadmapUnblockPlanner` | Recovery validation service | Evidence files, state intent, project context produce facts; machine consumes recovery action | Cohesive but narrow; unsupported cases are explicit. |
| Durable state persistence | `RoadmapStateStore`, `RoadmapTransitionPersistence`, SQLite stores | Persistence layer | Transitions produce documents; resume/status consume | Mixed file/SQLite support raises synchronization complexity. |
| Transition journaling | `RoadmapPromptTransitionRunner`, `TransitionJournalStore`, coordinated stores | Event log | Transitions produce journal records; status/debug/recovery consume | Cohesive event model; journal construction also appears in unblock recovery. |
| Prompt transition execution | `RoadmapPromptTransitionRunner`, transition classes | Prompt orchestration | Context builder, prompt runner, parser, policy | Strong behavioral cohesion; failure persistence is embedded in runner. |
| Projection freshness and manifests | `ProjectionCache`, `ProjectionRegistry`, `PromptContractRegistry` | Projection service | Prompt outputs produce projections; resume and prompts consume | Strong boundary but hidden coupling to prompt keys and stale policy. |
| Context loading | `ProjectContextLoader`, `RoadmapPromptContextBuilder` | Context service | Repo/project artifacts produce context; prompts consume | Context builder owns important safety rule: no raw project context marker leakage. |
| Artifact lifecycle | `ArtifactLifecycleStore`, `ArtifactPromotionService`, `ActiveEpicPromotionCoordinator` | Artifact governance | Prompt candidates produce artifacts; resume/invariants consume lifecycle | Cohesive but cross-cuts state persistence and journals. |
| Split lineage | `SplitEpicTransition`, `SplitFamilyStore` | Split domain service | Split prompt produces family; promotion/resume consume | Mostly cohesive. |
| Completion certification | `CompletionCertificationTransition`, `RoadmapCompletionRouteMapper`, completion policies | Completion domain service | Execution evidence and review produce route; state machine consumes outcome | Crosses roadmap and main execution boundary. |
| Non-implementation review | Review services under roadmap and loop compositions | Review subsystem | Completion artifacts produce review input; completion transition consumes result | Cohesive but manually composed in multiple CLIs. |
| Main execution loop | `LoopRunner`, `ExecutionStep`, `DecisionSession` | Operational execution subsystem | Operational context/epic/specs produce prompts; git and `.agents` consume outputs | External to roadmap state machine but constrains it heavily. |
| Git publication | `AgentsSubmodulePublisher`, `CommitGate` | Infrastructure/git boundary | `.agents` and repo changes produce commits; users/remotes consume | Observable behavior; failure semantics must be preserved. |
| Agent process/session protocol | `AgentRuntime`, `CodexAppServerSession`, argument builder | Agent infrastructure | CLIs create sessions; prompts consume turns | Strong low-level boundary; protocol version comments are hidden constraints. |
| Permission evaluation | `PermissionGateway`, operation handlers, adapters | Permission subsystem | Codex approval requests produce decisions; sessions consume responses | Strong boundary; operation-scoped permissions are critical invariant protection. |
| Telemetry and usage retry | `GatedAgentRuntime`, telemetry composition | Runtime monitoring service | Agent turns produce telemetry; logs/SQLite consume | Present in main loop, absent from plan/roadmap compositions. |

Mixed concerns highlighted:

- `RoadmapStateMachine` combines command orchestration, state routing, persistence calls, reporting, and recovery-specific event construction.
- `RoadmapCliComposition` constructs many unrelated subsystems and encodes file-vs-SQLite store selection.
- `RoadmapStateDocument` combines current state, transition metadata, artifact summary, projection counts, blockers, and display-oriented next transitions.
- `PermissionedArtifactOperationStep` combines prompt execution, security policy, transaction rollback, output validation, and HITL capture.

## 6. Coupling Analysis

High coupling:

- `RoadmapStateMachine` has many constructor dependencies and branches across transition services, state persistence, resume planning, unblock planning, status reporting, and cancellation.
- `RoadmapCliComposition` manually wires agents, stores, projections, transition classes, lifecycle stores, non-implementation review, completion archives, and workflow coordinators.
- `RoadmapResumePlanner` couples durable state, artifact snapshot, lifecycle, projection manifests, prompt contracts, active selection provenance, execution prep manifests, and spec ownership.
- `CompletionCertificationTransition` couples execution evidence, non-implementation review, completion parsing/policy, route mapping, archive effects, completion context updates, lifecycle, decision ledger, and state persistence.

Hidden coupling:

- Prompt contract keys must match projection registry entries, prompt registry behavior, parser expectations, and transition output paths.
- `RoadmapStateDocument.TransitionIntent` determines unblock behavior and cancellation recovery, but it is embedded in state rather than visible at the command boundary.
- Selection provenance and lifecycle records must stay synchronized with selection output for resume to continue safely.
- Main execution completion behavior depends on roadmap completion certification services even though operational execution is in `LoopRelay.Cli`.

Temporal coupling:

- Plan authoring must keep the same warm session for write and revise, then close it before scoped artifact operations.
- Plan pipeline must publish `.agents` before projection/review and again after revision before artifact extraction.
- Roadmap run must emit prompt contract snapshots before prompt transitions.
- Projection freshness must be established before prompts or resume paths use projected context.
- Completion certification optionally performs non-implementation review before evaluating completion.

Lifecycle coupling:

- Agent sessions must be registered and closed through `AgentSessionRegistry`.
- Operation-scoped sessions depend on approval contexts and rollback transactions.
- SQLite workflow markers coordinate multi-domain writes and classify incomplete workflow units.
- Parent gitlink recording depends on previous `.agents` submodule publication.

Ordering constraints:

- Plan details are collected before milestones are extracted, and milestone checklists are required before detail extraction.
- Roadmap completion context precedes selection.
- Selection precedes audit/new/split paths.
- Active epic readiness precedes milestone spec generation.
- Completion route side effects precede state route persistence.

Circular or near-circular dependencies:

- State persistence stores artifact summaries derived from artifacts that are themselves produced because of state transitions.
- Resume validation depends on projection manifests generated by transitions that are selected by resume validation.
- Completion context updates are both inputs to selection and outputs of completion routing.

Implicit dependencies:

- File paths under `.agents` are part of the contract between CLIs.
- Exit codes are part of the external automation contract.
- Review verdict strings and execution disposition headings are parser contracts.
- Sandbox and approval settings encode security posture that is not obvious from transition names.

## 7. State Ownership

| Mutable state | Owner | Lifetime and mutation locations | Synchronization, recovery, consumers, duplication |
| --- | --- | --- | --- |
| `.agents/state.json` | `RoadmapStateStore` and `RoadmapTransitionPersistence` | Durable per repo; saved on roadmap transitions, cancellation, recovery, failures. | File-backed or SQLite-backed equivalent; consumed by status/resume/unblock. Duplicates summary facts from artifact stores. |
| Legacy `.agents/state.md` | `RoadmapStateStore` migration parser | Read when JSON state is absent; parsed and migrated. | Recovery depends on strict markdown table parsing. Historical duplication of JSON state. |
| Transition journal | `TransitionJournalStore` or SQLite journal store | Append-only per transition, unblock, invariant, and promotion events. | File JSONL or SQLite; consumed by diagnostics/recovery. |
| Decision ledger | Decision ledger stores | Appended by selection, audit, completion, and context update transitions. | File or SQLite; last decision is summarized into state. |
| Artifact lifecycle | `ArtifactLifecycleStore` or SQLite lifecycle store | Updated when artifacts become ready, blocked, executing, completed, or superseded. | Resume and invariant validators consume it. Duplicates existence/readiness facts available from files. |
| Projection manifest | Projection manifest stores | Updated by projection cache. | Resume, prompt readiness, invariant validation consume it. Hidden contract with prompt keys. |
| Selection provenance manifest | Selection provenance store | Written after selection. | Resume validates active selection freshness. |
| Execution preparation manifest | Execution prep manifest store | Written after milestone specs and historical execution prep. | Resume and invariant validation consume it. |
| Split family state | Split family store | Written by split transition. | Promotion and resume consume lineage. File/SQLite variants. |
| Numbered evidence directories | `RoadmapArtifacts` | Written by review, audit, split, completion, blockers, loop histories. | Recovery and human review consume. Execution evidence can be redirected to SQLite store. |
| Completion context | Roadmap completion transitions | Durable `.agents/core/roadmap-completion-context.md`. | Selection prompt consumes; completion update mutates. |
| Selection artifact | Selection transition | Durable `.agents/selection.md`. | Selection parser, resume, audit/new/split transitions consume. |
| Active epic artifact | Promotion services and lifecycle | Durable `.agents/epic.md` or equivalent active path. | Milestone generation, execution, completion consume. |
| Milestone specs | Milestone deep dive transition | Durable spec files and manifest. | Execution preparation, main loop, completion evaluation consume. |
| Plan artifacts | `PlanArtifacts` and plan steps | `.agents/plan.md`, details, milestones, operational context. | Plan pipeline produces; main loop consumes operational context and milestones. |
| Loop live/historical handoffs and decisions | `LoopArtifacts`, `DecisionSession`, `ExecutionStep` | Mutated throughout `LoopRelay.Cli` execution. | Main loop, decision router, completion review consume. |
| Agent sessions and threads | `AgentRuntime`, `CodexAppServerSession`, session registry | Process lifetime; persistent sessions may be resumed. | CLIs consume session IDs; close/dispose required. |
| Decision resume state | `DecisionSession` and resume store | Persists warm decision session identity. | Decision flow consumes for resume; cleared on close except disposal path. |
| Telemetry logs | Telemetry composition/recorder | Per turn; SQLite or JSONL compatible logs. | Diagnostics consume. Not present in plan/roadmap compositions. |
| `.agents` submodule git state | Infrastructure publisher | Mutated on publish and parent gitlink recording. | Git remotes/users consume; failure can leave stranded submodule commits handled by publisher. |
| SQLite workspace database | `WorkspaceSqliteStore` and domain stores | Optional canonical/imported persistence. | Storage commands and runtime composition consume. Workflow markers coordinate domains. |

Inference:

- State ownership is intentionally redundant in places: durable state keeps summary fields to make status/resume fast, while authoritative details remain in artifact, manifest, ledger, and lifecycle stores.
- The redundancy increases recovery capability but also raises synchronization risk.

## 8. Execution Flow

### Plan CLI Startup to Termination

1. Process starts and `CliArguments.TryParse` validates a single repository directory.
2. `PlanCliComposition` loads settings and prompt policy, wires agent services and all plan pipeline dependencies.
3. Ctrl+C registers cancellation through a `CancellationTokenSource`.
4. `PlanPipeline.RunAsync` evaluates preflight. Violations produce `PreflightBlocked`.
5. A persistent PlanAuthoring session writes `.agents/plan.md`; the plan gate requires non-empty content.
6. `.agents` is published, adversarial projection is generated, and `.agents` is published again.
7. A fresh read-only review session produces an adversarial verdict.
8. The same warm plan-authoring session revises the plan and then closes.
9. Operational context is seeded from the plan if absent, then `.agents` is published.
10. Scoped artifact operation sessions collect details, extract milestones, and extract details, each with allowed-read/write gates and rollback on failure.
11. If `.agents` was published, parent gitlink recording commits/pushes the parent repository.
12. Program maps outcome to exit code 0, 4, 130, or 1 and disposes composition.

Retries:

- No automatic usage-limit retry is wired in the Plan CLI composition.
- Scoped artifact operations roll back on failure but do not automatically retry.

User interaction:

- HITL capture records plan/review/artifact prompts.
- Operation-scoped sessions can respond to approval requests through permission handling.

### Roadmap CLI Startup to Termination

1. `RoadmapCliInvocation.TryParse` parses command and repo.
2. Storage commands (`storage-init`, `storage-import`, `storage-export`, `storage-sync`, `storage-verify`) run before state-machine composition and return storage-specific exit codes.
3. Non-storage commands create `RoadmapCliComposition`, which selects file or SQLite-backed stores depending on database validation.
4. Ctrl+C registers cancellation.
5. `RoadmapStateMachine.ExecuteAsync` routes `status`, `run`, or `unblock`.
6. `status` loads state and reports current state, startup reason, transition intent, and blockers.
7. `run` asks startup and resume planners for the next action, emits prompt contracts, then dispatches transition branches.
8. Prompt transitions use `RoadmapPromptTransitionRunner` for started/completed/failed journal records and state persistence.
9. Promotion, split, milestone, and completion transitions perform postprocessing and may persist their own blocker or invariant failure states.
10. Cancellation writes `Cancelled` with a recovery target.
11. `unblock` validates state/intent/evidence, appends review/journal records, and saves a recovered or failed state.
12. Program maps `Completed` and `Paused` to 0, `PreflightBlocked` to 4, `Cancelled` to 130, and default failure to 1.

Loops and recursion:

- `RunFromCoreReadyAsync` may bootstrap completion context and then immediately run selection.
- `RunSelectionAndFollowingAsync` selects and immediately continues into audit/new/split handling.
- `ContinueAfterSelectionAsync` may perform several transition steps before pausing at milestone specs or a terminal selection outcome.
- The main operational execution loop is not advanced by roadmap run; it lives in `LoopRelay.Cli.LoopRunner`.

### Main Execution Loop Constraint

Evidence:

- `LoopRelay.Cli.Program` maps `EpicCompleted` to 0, `CompletionBlocked` to 4, `Cancelled` to 130, `Stalled` to 3, and failure to 1.
- `LoopRunner` loops over operational execution, decision routing, handoff rotation, `.agents` publication, commit/push, stall detection, non-implementation review, and completion certification.
- `ExecutionStep` holds an operational Codex session for execution and handoff generation.
- `DecisionSession` holds or resumes a warm read-only decision session and can perform scoped artifact operations.

Inference:

- Roadmap state simplification is constrained by behaviors now implemented outside `Roadmap.Cli`, especially completion certification, milestone completion gates, decision routing, and git publishing.

## 9. Cognitive Complexity

High-complexity areas:

- `RoadmapCliComposition`: A large manual composition root wires settings, agents, stores, workflow coordinators, prompt registries, transition classes, completion services, SQLite/file variants, and the state machine. Understanding runtime behavior requires following many constructor arguments.
- `RoadmapStateMachine`: It combines command routing, resume dispatch, transition continuation, completion recovery, unblock failure persistence, cancellation persistence, reporting, and state saves. The same file contains both high-level workflow and low-level journal construction.
- `RoadmapResumePlanner`: Safety checks are valuable but dense. It navigates artifact snapshots, projection manifests, prompt contracts, lifecycle records, selection provenance, execution prep manifests, and spec ownership before allowing a transition.
- `RoadmapTransitionPersistence`: State persistence includes active artifact summaries, projection counts, last decision IDs, retired epics, blockers, transition intent, next transitions, decision ledger append, and workflow coordination. The method names are clear, but the persisted document aggregates many concerns.
- Completion certification: Completion evidence, optional non-implementation review, evaluation prompt, parser/policy, route mapper, archive effects, lifecycle updates, context updates, journal entries, and state saves are all intertwined.
- Dual persistence modes: Many stores have file and SQLite variants, with coordination wrappers active only in SQLite mode. A reader must reason about both modes.
- Legacy execution states: The enum suggests execution preparation is in the state machine, while resume planning reports those states as no longer advanced.
- Plan artifact operation steps: Security, transaction rollback, HITL, prompt execution, output validation, and changed guards are locally mixed.

Why these are expensive:

- Engineers must keep artifact files, lifecycle records, manifests, prompt contracts, transition intent, and state enum values aligned mentally.
- Behavior is often distributed across composition, transition class, runner, persistence helper, parser/policy, and resume planner.
- Several important behaviors are negative behaviors: states that intentionally do not advance, unblock routes that intentionally fail, prompts that intentionally cannot write, and projections that intentionally block stale output.

## 10. Architectural Boundaries

| Boundary | Assessment | Evidence and notes |
| --- | --- | --- |
| CLI shell to workflow service | Mostly strong | Programs handle args, cancellation, exit codes. Roadmap storage commands bypass state machine by design. |
| Plan pipeline to plan steps | Moderate | `PlanPipeline` orders steps cleanly, but publication and artifact operations are interleaved with workflow semantics. |
| Roadmap state machine to transition services | Weak to moderate | Transitions are separate classes, but the state machine still interprets many outcomes and saves many states directly. |
| Transition runner to prompts | Strong | `RoadmapPromptTransitionRunner` centralizes prompt transition journal/state failure behavior. |
| Prompt contracts/projections | Strong but leaking | Registries provide explicit contracts, but many callers know prompt keys, output paths, and stale policies. |
| Artifact lifecycle | Moderate | Dedicated stores/services exist; state documents still duplicate lifecycle-derived summary facts. |
| Persistence file/SQLite | Artificial and leaking | Store interfaces hide much, but composition and workflow coordination branch on SQLite validation. |
| Agent runtime | Strong | Agent process/session protocol is isolated under `LoopRelay.Agents`. Callers still choose persistent vs one-shot and sandbox posture. |
| Permissions | Strong | Permission gateway and operation handler centralize approval decisions. Operation profiles are passed from higher-level steps. |
| Main loop vs roadmap | Leaking | Main loop uses roadmap completion certification services; roadmap retains execution states that main loop now effectively owns. |
| Git infrastructure | Moderate | Git publisher and commit gate isolate commands, but workflow sequencing depends on git side effects. |
| Human interaction | Moderate | HITL capture is service-like, but prompts, transitions, and reviews decide when to capture. |

## 11. Dependency Graph

Current major dependency relationships:

```text
LoopRelay.Plan.Cli.Program
  -> CliArguments
  -> PlanCliComposition
     -> settings and prompt policy
     -> LoopRelay.Agents
     -> LoopRelay.Projections
     -> PlanSession
     -> ReviewStep
     -> PermissionedArtifactOperationStep
     -> AgentsSubmodulePublisher
     -> PlanPipeline

LoopRelay.Roadmap.Cli.Program
  -> RoadmapCliInvocation
  -> storage command services
  -> RoadmapCliComposition
     -> settings and prompt policy
     -> agents, permissions, prompt runner
     -> project context and projections
     -> file or SQLite domain stores
     -> workflow coordinator
     -> transition persistence
     -> transition classes
     -> startup/resume/unblock planners
     -> RoadmapStateMachine

RoadmapStateMachine
  -> RoadmapStartupPlanner
  -> RoadmapResumePlanner
  -> RoadmapUnblockPlanner
  -> RoadmapTransitionPersistence
  -> transition classes
  -> active selection reader
  -> state store, journal store, lifecycle store
  -> prompt contract registry

Transition classes
  -> RoadmapPromptTransitionRunner
  -> RoadmapPromptContextBuilder
  -> RoadmapPromptRunner
  -> ProjectionCache
  -> parsers and policies
  -> artifact stores and lifecycle stores
  -> transition persistence

LoopRelay.Cli
  -> LoopRunner
     -> ExecutionStep
     -> DecisionSession
     -> MilestoneGate
     -> CommitGate
     -> non-implementation review
     -> CompletionCertificationTransition
     -> AgentsSubmodulePublisher

Agent infrastructure
  -> Codex process launcher
  -> app-server session protocol
  -> permission gateway
  -> telemetry and usage-limit retry when wrapped by GatedAgentRuntime
```

Dependency hotspots:

- `RoadmapCliComposition`
- `RoadmapStateMachine`
- `RoadmapResumePlanner`
- `RoadmapTransitionPersistence`
- `CompletionCertificationTransition`
- `LoopRunner` and `DecisionSession`

## 12. Lifecycle Analysis

Execution lifecycle:

- Plan CLI: one process, one warm planning session, one read-only review session, several scoped artifact sessions, then git publication and disposal.
- Roadmap CLI: one process, one command, possibly several prompt transitions, then pause/completion/failure/cancellation.
- Main loop: potentially repeated execution/decision iterations until epic completion, completion block, stall, cancellation, or failure.

Session lifecycle:

- Persistent sessions use `codex app-server --listen stdio://` through `AgentRuntime.OpenSession`.
- One-shot sessions use `codex exec --json`.
- `PlanSession` holds a persistent authoring session across write and revise.
- `ReviewStep` opens a fresh read-only persistent review session and closes it after one turn.
- `ExecutionStep` holds an operational session for execution and handoff generation.
- `DecisionSession` can resume a persisted decision thread and clears resume state on normal close.

Command lifecycle:

- Plan has one implicit command: generate plan artifacts.
- Roadmap has `run`, `status`, `unblock`, and storage commands.
- Main loop has run behavior with separate outcome taxonomy.

Review lifecycle:

- Plan adversarial review happens after projection and before revision.
- Roadmap completion may include non-implementation review before completion evaluation.
- Main loop performs post-execution non-implementation review and completion review.

Approval lifecycle:

- Read-only review and planning prompts generally run with no write permissions.
- Operation-scoped artifact sessions require approval and route requests through `OperationPermissionHandler`.
- Operation permission denies user input, network, command execution, deletes, parent traversal, unknown paths, and out-of-scope writes.

Retry lifecycle:

- Main loop can wrap runtime with `GatedAgentRuntime`, which retries usage-limit failures in persistent sessions up to a fixed limit.
- Plan and roadmap compositions use `InputWaitProgressAgentRuntime`, so equivalent usage-limit retry is not wired there.
- SQLite workflow markers can classify retryable/corrupt incomplete workflow units, but active state-machine recovery remains intent/evidence driven.

Cancellation lifecycle:

- CLIs install Ctrl+C cancellation tokens.
- Plan pipeline catches `OperationCanceledException` and returns `Cancelled`.
- Roadmap state machine writes durable `Cancelled` state with a recovery target.
- Main loop salvages `.agents` publication on cancellation/failure where possible.

Persistence lifecycle:

- Roadmap transitions save started/completed/failed state and journal records.
- File stores persist structured documents; SQLite stores persist canonical JSON or normalized rows.
- Storage commands initialize/import/export/sync/verify workspace database state.

Recovery lifecycle:

- Resume planning validates state, artifacts, projection freshness, incomplete transitions, active selection, active epic, and spec readiness.
- Unblock planning validates specific transition intents and evidence hashes before recovery.
- Unsupported unblock routes are intentionally reported and persisted as continued blockers.

Shutdown lifecycle:

- CLI compositions dispose pipelines, sessions, registries, and service providers.
- Agent sessions deregister and dispose Codex app-server processes.
- Git publication and parent gitlink commits are attempted before normal Plan CLI termination.

## 13. Failure Analysis

| Failure mode | Detection | Persistence and cleanup | Recovery behavior |
| --- | --- | --- | --- |
| Invalid CLI usage | Argument parser | Usage exception/output; no durable state | User reruns with valid args. |
| Plan preflight violation | `PreflightGate` | No durable plan state; returns `PreflightBlocked` | Manual artifact cleanup/input correction. |
| Empty plan/details/milestones | Plan gates and changed guards | Plan step failure; scoped transaction may restore writes | No automatic retry. |
| Scoped artifact write violation | Permission gateway or output verifier | Denied approval or operation failure; transaction restore attempted | Manual rerun after correction. |
| `.agents` git failure | Publisher wrapper | Plan step failure; submodule may have partial commits/push state | Publisher has stranded-commit push handling, but pipeline returns failure. |
| Project context preflight failure | `ProjectContextLoader` in roadmap run | Ephemeral blocker report and `PreflightBlocked`; normal state transition not necessarily written | User fixes project context and reruns. |
| Prompt transition failure | `RoadmapPromptTransitionRunner` | Journal failed event; state saved as `EvidenceBlocked` or `Failed` with intent | Resume/unblock depends on intent. |
| Projection invalid/stale | `ProjectionCache`, resume planner, invariant validator | Blocker evidence and blocked/failed state | Unblock only if supported by intent. |
| Selection provenance stale | Resume planner | Blocks safe resume | Regeneration or manual recovery path depends on state/action. |
| Promotion candidate invalid | `ArtifactPromotionService`/coordinator | Candidate evidence preserved; lifecycle blocked; state `EvidenceBlocked` | Unsupported or intent-specific unblock. |
| Split bundle invalid | `SplitEpicTransition` | Split blocker evidence, journal, `EvidenceBlocked` | `ResolveSplitEpicBlocker` is noted by unblock planner as unsupported. |
| Milestone spec postprocessing failure | `GenerateMilestoneDeepDivesTransition` | Blocker evidence and state `EvidenceBlocked` or `Failed` | Depends on persisted intent; no automatic retry. |
| Invariant failure | `InvariantValidator` | Orchestration evidence and failed/blocked state through transition persistence | Recovery requires inspecting persisted blocker/evidence. |
| Completion certification invalid | Completion certification transition/policy | Evidence blocked with `ResolveInvalidCompletionCertification` | Supported by unblock planner when evidence validates. |
| Execution disposition malformed | Execution outcome interpreter/unblock planner | `ResolveMalformedExecutionOutput` intent when used | Supported recovery from exact execution evidence path. |
| Execution runtime failure | Roadmap unblock planner | Runtime repair currently returns failed because legacy execution prep is no longer advanced | No active roadmap recovery path observed. |
| Cancellation | Ctrl+C token | Plan returns cancelled; roadmap writes `Cancelled` with recovery state; loop attempts salvage | Roadmap resume uses cancelled recovery state. |
| SQLite workflow interruption | Workflow coordinator markers | Markers classify retryable/corrupt units | Storage verification exposes status; active transition recovery remains separate. |

Inference:

- The architecture strongly favors evidence preservation over blind retry.
- Rollback is localized: plan artifact operations have transaction restore; roadmap mostly persists blocked/failure evidence rather than reverting previous durable facts.

Uncertainty:

- `EpicPreparationAuditTransition` throws for insufficient evidence; static inspection did not fully prove which outer persistence path always captures that failure.
- File-backed multi-document writes are less visibly atomic than SQLite-coordinated workflow units.

## 14. Hidden Architectural Constraints

Ordering assumptions:

- Plan revision must happen in the same authoring session as initial plan creation.
- Plan projection and adversarial review depend on the first `.agents` publish.
- Roadmap selection depends on a current completion context.
- Completion route effects must update archive/context/lifecycle consistently before final route state is trusted.

Singleton assumptions:

- One active selection, one active epic, one current state document, and one current completion context are assumed in many resume and invariant paths.
- `.agents` is treated as the canonical planning submodule location.

Timing assumptions:

- Usage-limit waits are only handled where `GatedAgentRuntime` is composed.
- Agent app-server sessions must remain alive across multi-turn workflows.
- Ctrl+C cancellation can happen mid-transition; roadmap relies on cancellation state recovery.

Persistence assumptions:

- Structured document schema versions must validate before loading.
- Legacy markdown state migration must remain available for old repos.
- File and SQLite stores are intended to represent equivalent domains, but coordination semantics differ.
- Transition intent and evidence paths are part of recovery contracts.

Session assumptions:

- `codex exec` is one-shot and cannot provide multi-turn behavior; persistent app-server is required for held-open sessions.
- Resume validation for Codex app-server sessions is eager in `AgentRuntime.OpenSession`.
- Operation-scoped sessions must pass explicit permission profiles.

Workflow assumptions:

- Some roadmap states are report-only by current design even though their names sound executable.
- Main execution loop owns operational progress after milestone specs.
- Completion certification bridges roadmap and loop domains.

Execution assumptions:

- Milestone checkboxes and handoff files define loop progress and stall behavior.
- CommitGate excludes `.agents` from regular repo commits.
- Completion review changes may be committed/pushed separately.

## 15. Human Readability Assessment

Navigability:

- Plan CLI is comparatively navigable: `Program` -> `PlanCliComposition` -> `PlanPipeline` -> step classes.
- Roadmap CLI requires longer navigation: `Program` -> composition -> state machine -> planner -> transition -> runner -> context builder -> parser/policy -> persistence/store.

Locality:

- Plan pipeline ordering is local in one method.
- Roadmap behavior is intentionally dispersed across planners, transition classes, runner, persistence, stores, validators, and artifact services.

Discoverability:

- Enum state names are discoverable, but whether a state is active, terminal, legacy, or report-only requires reading `RoadmapResumePlanner` and `RoadmapWorkflowStateClassifier`.
- Prompt contracts and projection registries improve discoverability for prompt behavior.
- SQLite/file behavior is difficult to discover without reading composition and store implementations together.

Naming quality:

- Most class names are descriptive and domain-specific.
- Some state names describe actions (`GenerateMilestoneDeepDives`) while others describe durable facts (`MilestoneSpecsReady`) or outcomes (`NoSuitableInitiative`), mixing conceptual levels.

Conceptual consistency:

- The architecture consistently preserves evidence, validates artifacts, and records transitions.
- Consistency is weaker around execution: roadmap retains execution states, while operational execution has moved to the main loop.

Transition clarity:

- Prompt transition runner behavior is clear once found.
- Selection and completion routes are clear in their mappers/parsers.
- State-machine dispatch is harder to read because it combines continuation, persistence, reporting, and recovery.

Architectural coherence:

- The repository has a coherent evidence-first workflow style.
- Coherence is reduced by historical layering and duplicated representations of readiness/state across files, lifecycle, manifests, and summaries.

## 16. Architectural Debt

Accidental complexity:

- Large manual composition roots.
- Display/report fields stored together with durable workflow facts.
- Repeated store selection between file-backed and SQLite-backed variants.

Historical layering:

- Legacy execution-preparation states remain in the state enum and resume logic.
- Roadmap execution bridge exists but is not part of active roadmap composition.
- State migration from legacy markdown remains necessary.

Duplicated concepts:

- Readiness appears in file existence, lifecycle records, state summaries, prompt readiness checks, and invariant validation.
- Execution evidence can exist as filesystem evidence or SQLite logical evidence.
- Projection facts appear in manifests, state projection counts, and prompt readiness checks.

Orchestration complexity:

- State machine, transition runner, transition classes, and persistence helper all participate in transition control.
- Completion certification combines several subdomains.

State complexity:

- Enum values mix domain states, in-flight operations, terminal pauses, legacy states, and failure/cancellation states.
- Transition intent is a second state-like axis embedded in the state document.

Lifecycle complexity:

- Warm sessions, one-shot sessions, operation-scoped sessions, resumable decision sessions, and main execution sessions each have distinct behavior.
- Git publication lifecycle crosses `.agents` submodule and parent repo.

Dependency complexity:

- Resume and invariant checks depend on many stores and manifests.
- Main loop depends on roadmap completion services while roadmap retains execution history.

Readability complexity:

- Understanding a single outcome often requires reading parser, policy, route mapper, transition, persistence, and resume behavior.

## 17. Existing Refactoring Seams

Observations only:

- CLI programs already isolate argument parsing, cancellation setup, command routing, and exit-code mapping.
- `PlanPipeline` is a narrow sequencing surface over plan services.
- `PlanSession`, `ReviewStep`, and `PermissionedArtifactOperationStep` are already separated by session posture and artifact authority.
- `RoadmapStartupPlanner`, `RoadmapResumePlanner`, and `RoadmapUnblockPlanner` already isolate planning decisions from raw command parsing.
- `RoadmapPromptTransitionRunner` already centralizes prompt transition persistence semantics.
- Transition classes already group many domain prompt workflows.
- Parser and policy classes already separate text interpretation from transition execution for selection, execution disposition, completion evaluation, and audit flows.
- `RoadmapTransitionPersistence` already centralizes durable state writes and decision recording.
- Store interfaces already separate file and SQLite persistence at many call sites.
- `ProjectionCache`, `ProjectionRegistry`, and `PromptContractRegistry` already create a boundary around prompt/projection readiness.
- `ArtifactPromotionService` and `ActiveEpicPromotionCoordinator` already isolate promotion validation and blocked evidence preservation.
- Permission gateway and operation permission handler already isolate security decisions from prompt code.
- Agent runtime already isolates Codex process protocol from workflow code.
- Git publisher and commit gate already isolate most git command details.
- Completion route mapping is already separated from completion prompt execution.

Inference:

- These seams are meaningful because they already carry behavior and tests can observe their inputs/outputs. This audit does not prescribe moving code across them.

## 18. Regression Risk Inventory

Behavioral coupling risks:

- Changing state enum meaning can break resume, status, unblock, and legacy repo migration.
- Changing prompt keys or output paths can break prompt contracts, projection manifests, parsers, and resume validation.
- Changing `.agents` publication order can affect review projections, submodule state, and parent gitlink behavior.
- Changing main loop completion behavior can affect roadmap completion certification expectations.

Hidden invariant risks:

- Active selection freshness, lifecycle status, projection freshness, and spec ownership must remain synchronized.
- Operation-scoped permission denials must remain strict.
- Completion route validity depends on parser, policy, route mapper, archive, and context update effects.

Sequencing risks:

- Plan write/revise warm-session reuse.
- Plan details/milestones/details operation ordering.
- Roadmap completion context before selection.
- Selection before audit/new/split.
- Active epic before milestone specs.
- Non-implementation review before completion evaluation when enabled.

Persistence interaction risks:

- JSON schema validation and legacy markdown migration.
- File and SQLite store equivalence.
- Workflow marker classification and partial writes.
- Transition intent evidence paths used by unblock.
- State summaries duplicated from underlying artifacts.

Recovery risks:

- Unsupported unblock intents are deliberate behavior.
- Cancellation recovery uses transition intent and last transition.
- `RoadmapTransitionAlreadyPersistedException` prevents double persistence after failed transitions.
- Evidence hashes in unblock planning must match stored evidence.

Cancellation risks:

- Cancelling during prompt transitions, git operations, or multi-file persistence can leave partially completed external effects.
- Roadmap cancellation is durable; Plan cancellation is outcome-only.

Retry risks:

- Usage-limit retry exists only where `GatedAgentRuntime` is wired.
- Adding or moving retry behavior could duplicate prompt effects unless transition persistence remains idempotent.

State synchronization risks:

- Lifecycle, decision ledger, projection manifest, execution prep manifest, active artifacts, and durable state document can drift if writes are reordered or partially skipped.

## 19. Behavior Preservation Inventory

Externally observable behaviors to preserve after any successful refactor:

- Plan CLI accepts exactly one repository directory argument and rejects invalid input.
- Plan CLI logs repository path and Codex executable path.
- Plan CLI returns exit codes: completed 0, preflight blocked 4, cancelled 130, failed 1.
- Roadmap CLI storage commands run before state-machine composition and preserve their documented success/failure exit-code behavior.
- Roadmap CLI returns exit codes: completed/paused 0, preflight blocked 4, cancelled 130, failed 1.
- Main loop returns exit codes: epic completed 0, completion blocked 4, cancelled 130, stalled 3, failed 1.
- Ctrl+C cancellation remains supported.
- `.agents` submodule publication and parent gitlink recording behavior remains observable.
- Plan preflight continues to block when pre-existing plan/context/details/milestones conflict or source material is missing.
- Plan authoring and revision preserve warm-session behavior.
- Plan adversarial review remains read-only and verdict-parsed.
- Scoped artifact operations continue to enforce allowed reads/writes, no deletes, rollback on failure, output gates, and changed guards.
- Plan outputs remain `.agents/plan.md`, `.agents/details.md`, `.agents/milestones/*.md`, and `.agents/operational_context.md`.
- Roadmap state JSON schema validation and legacy markdown migration remain compatible.
- Roadmap status continues to report current state, startup reason, transition intent, blockers, and next valid transitions.
- Prompt contract snapshots remain emitted during roadmap run.
- Projection freshness and stale-policy behavior remain enforced.
- Transition journal records continue to include correlation, state, prompt, projection, contract, hashes, output paths, duration, result, parser decision, and errors.
- Selection, audit, split, milestone, and completion evidence remain durably written.
- Decision ledger entries remain appended for selection, audit, completion, and context updates.
- Artifact lifecycle statuses continue to drive resume and invariant behavior.
- Unsupported unblock routes remain explicit rather than silently advancing.
- `Cancelled` roadmap state preserves recovery information.
- Legacy execution-preparation states remain report-only unless their public behavior is deliberately changed in a future design.
- Completion certification preserves non-implementation review, parser/policy validation, route mapping, archive effects, and completion context updates.
- Operation permission handler continues to deny out-of-scope paths, deletes, command execution, network, parent traversal, unknown paths, and user input.
- Agent runtime preserves persistent app-server versus one-shot exec behavior.
- Main loop preserves milestone gate semantics, handoff/decision rotation, stall detection, repo commit behavior, `.agents` publication, and completion review behavior.
- Telemetry and usage-limit retry behavior remains present where currently wired and absent where not currently wired, unless intentionally changed.

## 20. Unknowns

Unanswered architectural questions requiring additional investigation:

- Which legacy roadmap execution-preparation states are still present because of persisted user repositories versus future intended use?
- Is `RoadmapExecutionBridge` retained for planned reintegration, backwards compatibility, or dead historical layering?
- What exact external automation relies on current exit codes, log messages, and status report text?
- Are file-backed and SQLite-backed stores expected to be behaviorally identical in all failure/interruption cases?
- What operational process is expected when storage workflow markers classify a unit as retryable or corrupt?
- Which unblock intents are intentionally unsupported long term, and which are placeholders awaiting future recovery behavior?
- What tests or fixtures cover migration from legacy `.agents/state.md`?
- How often do real repositories contain partially written artifacts that resume planning must tolerate?
- What is the authoritative source of truth when durable state summaries disagree with lifecycle manifests or artifact files?
- Are HITL capture formats and paths considered public contracts?
- How stable are Codex app-server protocol assumptions referenced in agent-session comments?
- Are usage-limit retries intentionally excluded from Plan and Roadmap CLIs?
- Does the insufficient-evidence path in `EpicPreparationAuditTransition` always persist durable recovery evidence through an outer catch path?
- Which completion certification paths are shared between roadmap and main loop by necessity versus historical convenience?
- What compatibility guarantees exist for prompt-owned runtime prompt policy hashes?
- How should external users interpret report-only states that sound like active workflow stages?
- What observability is required for background services such as telemetry, SQLite workflow markers, and Codex session registries?
- Which `.agents` files are user-editable contracts versus internal implementation artifacts?
- Which git failure states are acceptable to leave for manual repair after submodule publication or parent gitlink recording?
- What is the minimum evidence set a future roadmap author needs to validate before changing state enum shape?
