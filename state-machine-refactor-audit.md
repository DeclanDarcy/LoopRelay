# State Machine Refactor Audit

Scope: this audit covers the roadmap state-machine implementation centered on `src/LoopRelay.Roadmap.Cli`. Adjacent services are included only where they participate in roadmap execution authority, transition selection, persistence, validation, or recovery. This is an audit of current behavior, not a redesign.

## Executive Summary

The current roadmap state machine is explicit in name but distributed in practice. `RoadmapStateMachine` is the central orchestrator, but the actual execution path is decided by a chain of command dispatch, startup planning, resume planning, persisted state, artifact presence, artifact lifecycle, projection freshness, prompt output parsing, policy route tables, and unblock intent strings.

The clearest evidence is structural:

- `RoadmapStateMachine` has a primary constructor with 27 collaborators: artifacts, project context, contracts, manifests, projections, context building, transition input resolution, completion policy/router/archive, prompt running, state store, startup/resume/unblock planners, selection provenance, decision ledger, journal, lifecycle, promotion, bundle handling, split family handling, execution preparation, invariants, and console (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:6`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:33`).
- Composition manually constructs and passes those collaborators in one place (`src/LoopRelay.Roadmap.Cli/RoadmapCliComposition.cs:52`-`src/LoopRelay.Roadmap.Cli/RoadmapCliComposition.cs:138`).
- There are at least 24 direct `SaveStateAsync(...)` call sites in `RoadmapStateMachine`, plus direct journal, lifecycle, and decision ledger writes throughout the same class (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:165`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:225`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:279`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:366`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:536`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:540`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:544`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:632`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:790`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:927`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:982`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1035`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1188`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1197`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1205`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1261`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1290`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1352`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1361`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1369`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1432`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1535`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1644`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1820`).
- Some transitions are explicit route tables, such as execution disposition (`src/LoopRelay.Roadmap.Cli/ExecutionDispositionProtocol.cs:35`-`src/LoopRelay.Roadmap.Cli/ExecutionDispositionProtocol.cs:55`) and completion certification (`src/LoopRelay.Completion/CompletionCertificationRouter.cs:5`-`src/LoopRelay.Completion/CompletionCertificationRouter.cs:31`), while many others are direct switch branches and ad hoc `SaveStateAsync` calls.
- Execution-related states still exist in `RoadmapState`, but the resume planner reports several as legacy states no longer advanced by Roadmap CLI (`src/LoopRelay.Roadmap.Cli/RoadmapState.cs:24`-`src/LoopRelay.Roadmap.Cli/RoadmapState.cs:29`, `src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:206`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:214`).

The implementation is difficult to understand because no single file or table answers "what happens next?" with authority. The answer depends on the persisted state, last transition status, projection manifest, artifact lifecycle metadata, derived provenance, prompt contracts, parser output, and specific helper path currently executing.

Historical reasons for this shape cannot be determined from the implementation. What can be determined is that responsibilities accumulated inside the state machine because transition execution methods directly perform orchestration, artifact mutation, persistence, journaling, parser decisions, lifecycle changes, recovery state construction, and console reporting in the same control path.

## Execution Flow

### Entrypoint and initialization

1. `Program.cs` parses CLI arguments and creates the composition (`src/LoopRelay.Roadmap.Cli/Program.cs:14`-`src/LoopRelay.Roadmap.Cli/Program.cs:24`).
2. `Program.cs` wires Ctrl+C to cancel a shared token and then calls `machine.ExecuteAsync(invocation.Command, cts.Token)` (`src/LoopRelay.Roadmap.Cli/Program.cs:26`-`src/LoopRelay.Roadmap.Cli/Program.cs:38`).
3. The process exit code is derived from `RoadmapOutcome` (`src/LoopRelay.Roadmap.Cli/Program.cs:40`-`src/LoopRelay.Roadmap.Cli/Program.cs:50`).
4. `RoadmapCliComposition.Create` constructs the agent runtime, artifact store, projection registry/cache, context builders, stores, planners, policy/router services, promotion/bundle/split services, invariant validator, and finally the state machine (`src/LoopRelay.Roadmap.Cli/RoadmapCliComposition.cs:33`-`src/LoopRelay.Roadmap.Cli/RoadmapCliComposition.cs:140`).

### Command dispatch

`RoadmapStateMachine.ExecuteAsync` dispatches only three commands: `Status`, `Run`, and `Unblock` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:35`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:43`).

### Status flow

`StatusAsync` loads persisted state, asks `RoadmapStartupPlanner` for a startup plan, prints current state and transition intent, reports blockers, then returns the planner's report outcome or `Paused` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:46`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:65`).

### Run flow

1. `RunAsync` loads persisted state and asks `RoadmapStartupPlanner.Plan` what startup action applies (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:99`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:108`).
2. If preflight is required, it loads project context and emits the prompt contract snapshot (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:110`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:121`).
3. It asks `RoadmapResumePlanner.PlanAsync` to decide the resume action, then calls `ExecuteResumePlanAsync` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:124`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:128`).
4. `OperationCanceledException` writes a cancelled state and returns `Cancelled` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:130`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:133`).
5. Already-persisted workflow errors are logged as failed without rewriting state; other exceptions are reported as ephemeral blockers (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:135`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:150`).

### Startup planning

`RoadmapStartupPlanner` determines whether to initialize, resume, or report only:

- No persisted state means fresh initialization to `CoreReady` with preflight required (`src/LoopRelay.Roadmap.Cli/RoadmapStartupPlanner.cs:5`-`src/LoopRelay.Roadmap.Cli/RoadmapStartupPlanner.cs:14`).
- `EvidenceBlocked`, terminal pause states, `Completed`, and `Failed` become report-only plans without preflight (`src/LoopRelay.Roadmap.Cli/RoadmapStartupPlanner.cs:16`-`src/LoopRelay.Roadmap.Cli/RoadmapStartupPlanner.cs:43`).
- Other states resume after preflight (`src/LoopRelay.Roadmap.Cli/RoadmapStartupPlanner.cs:45`-`src/LoopRelay.Roadmap.Cli/RoadmapStartupPlanner.cs:51`).

### Resume planning

`RoadmapResumePlanner.PlanAsync` captures an artifact snapshot, handles null state, report-only states, cancellation recovery, incomplete transition safety, projection safety, and then delegates to `PlanForStateAsync` (`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:14`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:77`).

`PlanForStateAsync` contains the main persisted-state routing switch:

- `CoreReady` and `BootstrapRoadmapCompletionContext` continue from core readiness (`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:88`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:95`).
- `RoadmapCompletionContextReady` and `RetireEpic` route to selection if prompt readiness is safe (`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:96`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:107`).
- `SelectNextStrategicInitiative` may reuse active selection, regenerate selection, or block based on selection provenance, lifecycle, and prompt outputs (`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:109`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:162`).
- `ActiveEpicReady` and `GenerateMilestoneDeepDives` route to milestone generation after active epic and prompt-readiness validation (`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:164`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:187`).
- `MilestoneSpecsReady` is terminal paused after spec validation (`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:189`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:204`).
- legacy execution preparation/execution states are paused and no longer advanced by Roadmap CLI (`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:206`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:214`).
- `EpicCompletionDetected` resumes completion certification (`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:216`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:220`).
- several states are terminal paused (`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:221`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:229`).
- unknown states block because no safe resume rule is registered (`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:231`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:235`).

### Resume execution

`ExecuteResumePlanAsync` switches on `RoadmapResumeAction` and calls the corresponding state-machine method (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:154`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:214`). This is a second routing layer after `RoadmapResumePlanner`.

### Core-to-selection path

1. `RunFromCoreReadyAsync` bootstraps the roadmap completion context if absent, then proceeds to selection (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:478`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:488`).
2. `BootstrapRoadmapCompletionContextAsync` gets a prompt contract, ensures a projection, runs a prompt transition, writes the roadmap completion context, and marks lifecycle ready (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:552`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:571`).
3. `SelectNextInitiativeAsync` ensures projection, builds selection context, runs a prompt transition, writes selection and selection evidence, records selection provenance, updates lifecycle, parses the selection output, and appends a decision ledger entry (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:573`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:601`).

### Selection branching

`ContinueAfterSelectionAsync` branches on `selection.RecommendedOutcome` string values (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:498`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:550`). The parser defines allowed output strings separately (`src/LoopRelay.Roadmap.Cli/SelectionParser.cs:5`-`src/LoopRelay.Roadmap.Cli/SelectionParser.cs:13`).

Branches:

- `"Select Existing Epic"` runs audit and preparation (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:505`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:517`).
- `"Select New Intermediary Epic"` creates and promotes a new active epic (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:518`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:525`).
- `"Select Split Epic"` splits and promotes a selected child (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:526`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:533`).
- `"Strategic Investigation Required"`, `"Roadmap Revision Required"`, and default/no suitable initiative append decisions, save terminal pause states, and return paused (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:534`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:545`).
- Successful active-epic branches continue to milestone spec generation (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:548`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:549`).

### Existing epic preparation

`AuditAndPrepareExistingEpicAsync` reads selection, ensures projection, builds audit context, runs a prompt transition, writes audit evidence, parses the audit, appends a decision, then branches:

- `Retire` updates retired epic state, appends decision, saves `RetireEpic`, supersedes active selection, and pauses (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:625`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:636`).
- `Insufficient Evidence` throws a step exception (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:639`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:642`).
- `Realign` routes through `RewriteActiveEpicAsync` to promotion (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:644`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:647`).
- otherwise it routes through `ReimagineEpic` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:650`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:651`).

### Prompt transition execution

The generic prompt transition helper:

1. Resolves transition inputs and hashes (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1332`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1348`).
2. Generates a correlation ID and saves state as `Started` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1349`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1353`).
3. Runs the runtime prompt through `RoadmapPromptRunner` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1355`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1358`).
4. Journals completion and saves the target state as completed (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1359`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1362`).
5. On non-cancellation exception, journals failure, saves `EvidenceBlocked` with `ResolveTransitionFailure`, and throws an already-persisted exception (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1364`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1385`).

Promotion transitions use a different helper: `RunPromptForPromotionAsync` saves the source state as `Started`, then `PromptCompleted`, and leaves final state assignment to `PromoteActiveEpicAsync` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1167`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1221`). This is a materially different transition lifecycle from `RunPromptTransitionWithCompletionAsync`.

### Artifact promotion

`PromoteActiveEpicAsync` delegates classification/validation/write to `ArtifactPromotionService`, then either journals and saves `ActiveEpicReady` or saves `EvidenceBlocked` with `ResolveArtifactPromotionBlocker` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1224`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1305`). The promotion service itself classifies, validates, writes the target artifact, and updates lifecycle or preserves blocked evidence (`src/LoopRelay.Roadmap.Cli/ArtifactPromotion.cs:72`-`src/LoopRelay.Roadmap.Cli/ArtifactPromotion.cs:129`).

### Split epic

`SplitEpicAsync` runs a prompt transition, extracts a bundle, interprets it, writes child files and manifests, updates lifecycle, writes a split family record, then promotes the selected child (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:687`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:751`). Invalid extraction or interpretation goes to `BlockSplitEpicAsync`, which writes blocker evidence, journals rejection, saves `EvidenceBlocked`, and returns not promoted (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:753`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:807`).

### Milestone spec generation

`GenerateMilestoneSpecsAsync` runs a promotion-style prompt transition, extracts a bundle, writes specs and a bundle manifest, updates lifecycle, records execution-preparation provenance, validates invariants, journals materialization, and saves `MilestoneSpecsReady` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:863`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:940`). Failure in post-processing persists `EvidenceBlocked` with `ResolveMilestoneSpecGenerationFailure` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:955`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:997`).

### Completion certification

Completion certification is reachable from resume planning when persisted state is `EpicCompletionDetected` (`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:216`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:220`) or through recovery flows.

`RunCompletionCertificationAsync` can first persist an execution completion claim, then runs `EvaluateEpicCompletionAndDrift`, parses the evaluation, validates certification policy, routes through the completion router, archives/synthesizes if the route requires a completion-context update, updates active epic lifecycle, and persists the completion route (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1022`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1110`).

The shared completion route table maps recommendations to intents (`src/LoopRelay.Completion/CompletionCertificationRouter.cs:5`-`src/LoopRelay.Completion/CompletionCertificationRouter.cs:31`). The roadmap-specific mapper maps those recommendations to roadmap states, transition statuses, CLI outcomes, lifecycle states, and next-transition text (`src/LoopRelay.Roadmap.Cli/RoadmapCompletionRoute.cs:23`-`src/LoopRelay.Roadmap.Cli/RoadmapCompletionRoute.cs:63`).

Invalid certification writes `EvidenceBlocked` with `ResolveInvalidCompletionCertification` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1451`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1553`).

### Unblock flow

1. `UnblockAsync` loads state, asks `RoadmapUnblockPlanner` for a plan, reports unsupported/failed plans, and dispatches successful plans by `RoadmapUnblockAction` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:68`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:96`).
2. `RoadmapUnblockPlanner` only runs for blocked recovery states and then switches on persisted `TransitionIntent.Intent` string (`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:22`-`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:66`).
3. Supported intents include preflight blocker recovery, malformed execution output repair, invalid completion certification repair, and a legacy runtime failure path (`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:42`-`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:47`).
4. Several intent strings are explicitly unsupported because automatic recovery is not safely modeled (`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:48`-`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:65`).

### Failure, cancellation, retry, cleanup

- Prompt-transition failure persists `EvidenceBlocked` and `ResolveTransitionFailure` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1364`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1385`).
- Promotion failure persists `EvidenceBlocked` and `ResolveArtifactPromotionBlocker` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1274`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1305`).
- Invariant failure is converted into `RoadmapWorkflowFailure`, journaled, saved, then thrown as already persisted (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1554`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1582`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1623`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1659`).
- Cancellation is caught only at the `RunAsync` boundary and written by `WriteCancelledStateAsync` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:130`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:133`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1802`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1834`).
- No generic retry loop was found. `RoadmapUnblockPlanner` explicitly says prompt transition failures are not automatically retried because retry safety and prompt idempotency are not modeled (`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:54`-`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:56`).
- Cleanup is composition disposal of agent sessions and service provider (`src/LoopRelay.Roadmap.Cli/RoadmapCliComposition.cs:143`-`src/LoopRelay.Roadmap.Cli/RoadmapCliComposition.cs:151`).

## Responsibility Map

### `RoadmapStateMachine.cs`

Responsibilities present:

- command dispatch
- run/status/unblock orchestration
- preflight error handling
- resume plan execution
- selection branch execution
- prompt transition execution
- artifact promotion handling
- split bundle handling
- milestone spec post-processing
- completion certification routing
- unblock recovery persistence
- cancellation persistence
- transition journaling
- decision ledger writes
- artifact lifecycle writes
- state persistence
- blocker evidence rendering
- invariant failure persistence
- console reporting

Why it lives there: the implementation calls all of these collaborators directly inside transition methods. Historical intent is not knowable from the code alone.

Isolation: not isolated. Most transition methods combine multiple responsibilities in one call path.

### `RoadmapResumePlanner.cs`

Responsibilities present:

- persisted-state-to-resume-action routing
- artifact snapshot capture
- lifecycle inspection
- prompt contract readiness validation
- projection freshness validation
- incomplete transition safety
- cancelled transition recovery target selection
- active selection freshness validation
- execution preparation readiness validation
- active epic/spec validation

Evidence: planning begins by capturing `RoadmapArtifactSnapshot` and validating safety before `PlanForStateAsync` (`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:14`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:77`). `PlanForStateAsync` then switches over `RoadmapState` (`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:79`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:235`).

Isolation: partially isolated from execution, but intertwined with artifact and provenance rules.

### `RoadmapUnblockPlanner.cs`

Responsibilities present:

- blocked-state eligibility
- persisted transition intent routing
- project context preflight during unblock
- evidence hashing
- execution disposition parsing and policy validation
- completion evaluation parsing, policy validation, and routing
- recovery plan construction
- unsupported recovery classification

Evidence: the planner switches directly on string `TransitionIntent.Intent` (`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:42`-`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:66`) and hashes/validates evidence in recovery-specific methods (`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:139`-`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:319`).

Isolation: routing is isolated from `RoadmapStateMachine`, but recovery validation, evidence collection, and policy interpretation are intertwined.

### `RoadmapStateDocument.cs` and `RoadmapStateStore.cs`

Responsibilities present:

- current state model
- active artifact summary
- last transition summary
- blocker rows
- projection manifest counts
- transition intent
- next-valid-transition text
- retired epics
- JSON persistence DTOs
- legacy markdown migration/parsing

Evidence: `RoadmapStateDocument` aggregates many concepts in one persisted document (`src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs:3`-`src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs:49`). `RoadmapStateStore.LoadAsync` can load JSON or migrate legacy markdown (`src/LoopRelay.Roadmap.Cli/RoadmapStateStore.cs:21`-`src/LoopRelay.Roadmap.Cli/RoadmapStateStore.cs:47`).

Isolation: persistence format is isolated, but the persisted state contains multiple architectural concerns.

### `ProjectionCache.cs`

Responsibilities present:

- projection lookup
- prompt execution for missing projections
- validation
- hashing
- freshness evaluation
- manifest updates
- blocked evidence writing

Evidence: `EnsureAsync` reads or generates content, validates it, computes provenance/freshness, upserts manifest, writes blocked evidence on invalid/stale projections, and writes generated projections (`src/LoopRelay.Roadmap.Cli/ProjectionCache.cs:10`-`src/LoopRelay.Roadmap.Cli/ProjectionCache.cs:84`).

Isolation: projection behavior is isolated from the state-machine file but still mixes execution, validation, persistence, and blocking.

### `TransitionInputs.cs`

Responsibilities present:

- transition input routing by runtime prompt string
- artifact dependency resolution
- required/optional input distinction
- hash capture
- snapshot hash construction

Evidence: `AddPromptInputsAsync` switches on runtime prompt names to decide artifact inputs (`src/LoopRelay.Roadmap.Cli/TransitionInputs.cs:46`-`src/LoopRelay.Roadmap.Cli/TransitionInputs.cs:86`). The accumulator reads artifacts and throws on missing required inputs (`src/LoopRelay.Roadmap.Cli/TransitionInputs.cs:261`-`src/LoopRelay.Roadmap.Cli/TransitionInputs.cs:291`).

Isolation: input snapshotting is isolated, but transition dependency ownership is tied to prompt strings rather than state objects.

### `PromptContractRegistry.cs`

Responsibilities present:

- prompt contract table
- required inputs/outputs
- allowed decisions
- artifact writer ownership text
- stale projection policy
- parser name metadata
- snapshot artifact emission

Evidence: the constructor builds the registry with hard-coded contracts (`src/LoopRelay.Roadmap.Cli/PromptContractRegistry.cs:11`-`src/LoopRelay.Roadmap.Cli/PromptContractRegistry.cs:23`) and `EmitSnapshotAsync` writes a markdown artifact (`src/LoopRelay.Roadmap.Cli/PromptContractRegistry.cs:44`-`src/LoopRelay.Roadmap.Cli/PromptContractRegistry.cs:60`).

Isolation: contract data is centralized but not authoritative for actual state transitions.

### `InvariantValidator.cs`

Responsibilities present:

- project context drift detection
- prompt contract coverage check
- projection manifest/freshness validation
- artifact lifecycle duplicate-active-epic check
- active epic validation
- execution preparation freshness validation
- spec ownership validation
- execution prerequisite validation
- failure evidence creation

Evidence: `ValidateAsync` performs all checks in one method (`src/LoopRelay.Roadmap.Cli/InvariantValidator.cs:16`-`src/LoopRelay.Roadmap.Cli/InvariantValidator.cs:135`), and `FailAsync` writes blocker evidence while returning validation results (`src/LoopRelay.Roadmap.Cli/InvariantValidator.cs:314`-`src/LoopRelay.Roadmap.Cli/InvariantValidator.cs:349`).

Isolation: validation is separated from main execution, but validation and evidence writing are mixed.

### `ExecutionPreparationProvenanceService.cs`

Responsibilities present:

- record milestone spec provenance
- record operational context provenance
- record execution prompt provenance
- record compatibility artifact provenance
- evaluate freshness for derived artifacts
- compute expected execution milestone paths
- hash artifacts
- mutate execution preparation manifest

Evidence: recording methods mutate manifest state (`src/LoopRelay.Roadmap.Cli/ExecutionPreparationProvenanceService.cs:25`-`src/LoopRelay.Roadmap.Cli/ExecutionPreparationProvenanceService.cs:183`) and evaluation methods compute freshness across derived artifacts (`src/LoopRelay.Roadmap.Cli/ExecutionPreparationProvenanceService.cs:198`-`src/LoopRelay.Roadmap.Cli/ExecutionPreparationProvenanceService.cs:387`).

Isolation: provenance is isolated from the state machine, but it is both recorder and validator.

### `RoadmapExecutionBridge.cs` and `RoadmapExecutionOutcomeInterpreter.cs`

Responsibilities present:

- execution prompt transport through agent runtime
- trust posture evidence writing
- one-shot vs persistent execution selection
- transport result classification
- execution disposition parsing
- protocol validation
- execution evidence rendering

Evidence: bridge execution chooses persistent or one-shot based on sandbox approval and writes trust posture evidence (`src/LoopRelay.Roadmap.Cli/RoadmapExecutionBridge.cs:20`-`src/LoopRelay.Roadmap.Cli/RoadmapExecutionBridge.cs:68`). The outcome interpreter parses and validates execution disposition (`src/LoopRelay.Roadmap.Cli/RoadmapExecutionOutcomeInterpreter.cs:10`-`src/LoopRelay.Roadmap.Cli/RoadmapExecutionOutcomeInterpreter.cs:32`).

Current wiring evidence: `RoadmapExecutionBridge` is defined and tested, but source usage only shows direct construction in tests and no construction in `RoadmapCliComposition` (`src/LoopRelay.Roadmap.Cli/RoadmapExecutionBridge.cs:13`, `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapExecutionBridgeTests.cs:46`, `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapExecutionBridgeTests.cs:76`, `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapExecutionBridgeTests.cs:95`). The current roadmap CLI composition does not inject it into `RoadmapStateMachine`.

## Mixed Concern Inventory

| File | Major responsibilities mixed | Why understanding is difficult | Why modification is risky |
|---|---|---|---|
| `RoadmapStateMachine.cs` | execution, routing, persistence, journaling, lifecycle, decisions, recovery, cancellation, console output, artifact mutation | A reader must understand many helper methods and collaborator side effects to know one transition | Changes to one transition can affect state persistence, journal shape, blocker recovery, lifecycle, and resume behavior |
| `RoadmapResumePlanner.cs` | resume routing, artifact inspection, lifecycle, projection freshness, prompt contracts, provenance, active epic/spec validation | "Can resume?" depends on persisted state plus artifacts plus manifest/lifecycle/provenance | Adding/changing states requires updating resume rules and safety validations |
| `RoadmapUnblockPlanner.cs` | unblock routing, evidence hashing, project context validation, execution disposition parsing, completion policy/routing | Recovery is keyed by persisted intent strings, not a typed transition model | New blockers need intent strings, evidence shape, planner support, state-machine recovery persistence, and tests |
| `ProjectionCache.cs` | projection generation, cache read/write, validation, manifest update, stale policy, blocker evidence | A projection read can run an agent and mutate manifests or write blockers | Changing projection policy can alter runtime flow, state blocking, and manifest semantics |
| `TransitionInputs.cs` | prompt-to-input routing, artifact reading, required/optional validation, hashes | Dependencies are keyed by runtime prompt names in a switch | New prompts require edits here plus prompt contracts, projection registry, context builder, and state machine |
| `InvariantValidator.cs` | validation, freshness checks, lifecycle checks, active epic/spec checks, failure evidence writing | Multiple invariants with different failure states live in one validator | Adding one invariant can change blocking state, evidence artifacts, and recovery intent |
| `ExecutionPreparationProvenanceService.cs` | recording, freshness evaluation, manifest mutation, hash capture | Provenance model doubles as readiness gate | Changes to derived artifact generation affect resume planning and invariants |
| `RoadmapStateStore.cs` | JSON persistence and legacy markdown migration/parsing | Loading state can migrate and save, not only read | Persistence changes can affect backward compatibility and resume behavior |
| `PromptContractRegistry.cs` | contract metadata and artifact emission | Registry is partly documentation and partly runtime readiness data | Contract edits need matching changes in transition inputs, projections, parsers, and workflow methods |
| `RoadmapPromptContextBuilder.cs` | context assembly, artifact reads, raw context guard | Prompt context construction hides artifact dependencies not fully represented in state transitions | Context changes can affect hashes, transition inputs, and prompt behavior |

## Transition Analysis

### Explicit transitions

The implementation has explicit transition-like data in several places:

- `RoadmapState` enum lists state-like values (`src/LoopRelay.Roadmap.Cli/RoadmapState.cs:3`-`src/LoopRelay.Roadmap.Cli/RoadmapState.cs:34`).
- `RoadmapResumePlanner.PlanForStateAsync` maps persisted state to resume action (`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:79`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:235`).
- `ExecutionDispositionProtocol.Routes` maps execution status and command to outcome kind, target state, and workflow transition (`src/LoopRelay.Roadmap.Cli/ExecutionDispositionProtocol.cs:35`-`src/LoopRelay.Roadmap.Cli/ExecutionDispositionProtocol.cs:55`).
- `CompletionCertificationRouter.DefaultRoutes` maps closure recommendations to completion intents (`src/LoopRelay.Completion/CompletionCertificationRouter.cs:5`-`src/LoopRelay.Completion/CompletionCertificationRouter.cs:31`).
- `RoadmapCompletionRouteMapper` maps completion routes to roadmap target state, transition status, CLI outcome, lifecycle state, and next transitions (`src/LoopRelay.Roadmap.Cli/RoadmapCompletionRoute.cs:23`-`src/LoopRelay.Roadmap.Cli/RoadmapCompletionRoute.cs:63`).
- `NextTransitions` maps some states to display next-transition text (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1775`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1788`).

### Distributed transitions

Many transitions are defined by code paths rather than a table:

- Selection branches are string switches in `ContinueAfterSelectionAsync` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:503`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:545`).
- Audit branches are string comparisons in `AuditAndPrepareExistingEpicAsync` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:625`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:651`).
- Prompt transitions are encoded by each call to `RunPromptTransitionAsync`, `RunPromptTransitionWithCompletionAsync`, or `RunPromptForPromotionAsync`.
- Failure transitions are encoded by each `SaveStateAsync(... EvidenceBlocked ...)` call in catch or blocker paths.
- Unblock transitions are encoded by persisted `TransitionIntent.Intent` strings and `RoadmapUnblockPlanner` switch arms (`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:42`-`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:66`).

### Inferred/runtime-generated transitions

Some transitions are inferred from runtime data:

- Selection branch is inferred from model output parsed by `SelectionParser` (`src/LoopRelay.Roadmap.Cli/SelectionParser.cs:24`-`src/LoopRelay.Roadmap.Cli/SelectionParser.cs:34`).
- Completion route is inferred from model output parsed into `CompletionEvaluationDecision`, validated by policy, and routed by `CompletionCertificationRouter` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1067`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1081`).
- Execution disposition routes are inferred from execution output when that path is used (`src/LoopRelay.Roadmap.Cli/RoadmapExecutionOutcomeInterpreter.cs:20`-`src/LoopRelay.Roadmap.Cli/RoadmapExecutionOutcomeInterpreter.cs:30`).
- Resume action is inferred from persisted state plus artifact/lifecycle/projection/provenance freshness (`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:20`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:77`).

### `NextValidTransitions` is not execution authority

`NextValidTransitions` exists in persisted state (`src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs:12`-`src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs:14`) and is written by `SaveStateAsync` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1753`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1754`). Search evidence shows runtime reads in source are limited to persistence/migration and one unblock failure append; tests assert values, but planners do not use it as a guard. Therefore current execution authority is not this field.

## Implicit Architecture

These concepts exist in code but are not consistently first-class transition concepts:

- Lifecycle phases: console phase labels such as "Project Context preflight", "Select next strategic initiative", and "Generate milestone deep dives" are strings in execution methods (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:113`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:576`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:866`).
- Prompt transition lifecycle: generic transitions have `Started -> Completed/Failed`, while promotion transitions have `Started -> PromptCompleted -> promotion result` (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1167`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1221`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1332`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1385`).
- Transition intent: persisted as string plus dispatch state and evidence paths (`src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs:33`-`src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs:39`), then interpreted by string switch in unblock planning (`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:42`-`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:66`).
- Artifact lifecycle: readiness and safety depend on lifecycle metadata (`src/LoopRelay.Roadmap.Cli/ArtifactLifecycleStore.cs:40`-`src/LoopRelay.Roadmap.Cli/ArtifactLifecycleStore.cs:49`, `src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:680`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:710`).
- Projection freshness: projection readiness depends on provenance and stale policy (`src/LoopRelay.Roadmap.Cli/ProjectionCache.cs:40`-`src/LoopRelay.Roadmap.Cli/ProjectionCache.cs:81`).
- Selection cycle freshness: active selection reuse depends on provenance capture and freshness (`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:305`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:326`).
- Execution preparation readiness: a separate provenance model determines whether specs, operational context, execution prompt, and compatibility artifacts are fresh (`src/LoopRelay.Roadmap.Cli/ExecutionPreparationProvenanceService.cs:198`-`src/LoopRelay.Roadmap.Cli/ExecutionPreparationProvenanceService.cs:250`).
- Terminal pause/report-only state: classification is separate from resume planning (`src/LoopRelay.Roadmap.Cli/RoadmapWorkflowStateClassifier.cs:5`-`src/LoopRelay.Roadmap.Cli/RoadmapWorkflowStateClassifier.cs:45`).
- Legacy execution boundary: execution states remain in the enum, but current resume planning does not advance them (`src/LoopRelay.Roadmap.Cli/RoadmapState.cs:24`-`src/LoopRelay.Roadmap.Cli/RoadmapState.cs:29`, `src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:206`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:214`).

## Navigation Pain Points

An engineer cannot answer "what happens next?" by reading one file unless the question is limited to a very small local branch.

For a fresh run through milestone spec generation, mandatory files include at least:

- `Program.cs`
- `RoadmapCliComposition.cs`
- `RoadmapStateMachine.cs`
- `RoadmapStartupPlanner.cs`
- `RoadmapResumePlanner.cs`
- `RoadmapStateDocument.cs`
- `RoadmapStateStore.cs`
- `RoadmapArtifacts.cs`
- `PromptContractRegistry.cs`
- `ProjectionCache.cs`
- `RoadmapPromptContextBuilder.cs`
- `TransitionInputs.cs`
- `RoadmapPromptRunner.cs`
- `SelectionParser.cs`
- `ArtifactPromotion.cs`
- `ExecutionPreparationProvenanceService.cs`
- `InvariantValidator.cs`

Specific branches add more mandatory files:

- Existing epic audit adds `EpicPreparationAuditParser`, `RetiredEpic`, and promotion validators.
- Split epic adds `BundleFileExtractor`, `SplitEpicBundleInterpreter`, `BundleManifestWriter`, and `SplitFamilyStore`.
- Completion certification adds `CompletionEvaluationParser`, `CompletionCertificationPolicy`, `CompletionCertificationRouter`, `RoadmapCompletionRoute`, and `CompletedEpicArchiveService`.
- Unblock adds `RoadmapUnblockPlanner`, execution disposition parser/policy, completion policy/router, and evidence hashing paths.

The biggest navigation bottleneck is that the state machine contains both high-level routing and low-level persistence/evidence work. A reader moves between workflow branches, helpers, stores, and policy files to reconstruct one state transition.

## Flow Fragmentation Inventory

Execution jumps across:

- command switch: `Program.cs` -> `RoadmapStateMachine.ExecuteAsync`
- planner dispatch: `RoadmapStateMachine.RunAsync` -> `RoadmapStartupPlanner` -> `RoadmapResumePlanner` -> `ExecuteResumePlanAsync`
- prompt execution: state machine -> `ProjectionCache` -> `RoadmapPromptRunner` -> agent runtime
- context assembly: state machine -> `RoadmapPromptContextBuilder` -> artifacts/provenance
- transition input capture: state machine -> `TransitionInputResolver` -> artifacts/provenance
- model output interpretation: state machine -> parser -> policy/router -> state-machine continuation
- persistence: state machine -> `SaveStateAsync` -> `RoadmapStateStore` -> structured store/artifacts
- history: state machine -> `TransitionJournalStore`
- lifecycle: state machine/promotion services -> `ArtifactLifecycleStore`
- recovery: state machine -> `RoadmapUnblockPlanner` -> evidence readers/parsers/policies -> state-machine recover methods

The flow is highly fragmented. The fragmentation is not only file count; it is authority fragmentation. Different files decide different parts of "next": startup plan, resume action, selection outcome, prompt contract, transition inputs, promotion result, completion route, execution disposition route, invariant failure state, and unblock action.

## Modification Pain Points

### Adding a state

Likely touch points:

- `RoadmapState`
- `RoadmapResumePlanner.PlanForStateAsync`
- `RoadmapWorkflowStateClassifier` if terminal/report-only behavior changes
- `RoadmapStateMachine` branch method and `SaveStateAsync` call sites
- `NextTransitions`
- prompt contracts/projection registry/artifact paths if prompt-backed
- `TransitionInputResolver` if new prompt inputs exist
- tests across resume, state machine, state store, and failure persistence

Regression risk: high, because a state needs runtime behavior, resume behavior, display/report behavior, persistence expectations, and recovery behavior.

### Adding a transition

Likely touch points:

- a branch in `RoadmapStateMachine`
- prompt contract registry
- projection registry/path if prompt-backed
- prompt context builder
- transition input resolver
- parser/policy/router if output-driven
- journal and state persistence semantics
- unblock intent if failure can be recovered

Regression risk: high, because transitions are not owned by a single transition table.

### Changing execution order

Likely touch points:

- `RunFromCoreReadyAsync`
- `RunSelectionAndFollowingAsync`
- `ContinueAfterSelectionAsync`
- `RoadmapResumePlanner`
- `NextTransitions`
- prompt contract inputs/outputs and transition inputs
- tests that assert persisted state and next transitions

Regression risk: high, because resume and fresh execution must stay aligned.

### Adding validation

Validation can live in several places today:

- prompt output parsers
- `ProjectionCache`
- `RoadmapResumePlanner`
- `InvariantValidator`
- `ArtifactPromotionService`
- completion/execution policy classes
- post-processing methods in `RoadmapStateMachine`

Regression risk: medium to high, because validation failure must decide failure state, blocker evidence, transition intent, journal event, and resume behavior.

### Adding cancellation behavior

Cancellation currently bubbles to `RunAsync`, which writes a single cancelled state. A more granular cancellation change would need to account for prompt helpers, promotion helpers, post-processing helpers, state persistence, and recovery state (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:130`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:133`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1802`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1834`).

### Adding retries

No generic retry mechanism is present. The unblock planner explicitly marks prompt transition failures as not automatically retried because retry safety and prompt idempotency are not modeled (`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:54`-`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:56`). Retry support would need transition idempotency, output overwrite rules, journal semantics, and recovery behavior.

### Adding persistence, telemetry, checkpointing, or progress reporting

Persistence and history are currently embedded in transition helpers and many direct call sites. Any new checkpoint or telemetry concept would need to be threaded through `SaveStateAsync`, `TransitionJournalStore`, prompt helpers, promotion helpers, failure persistence, and likely tests.

## Architectural Bottlenecks

### `RoadmapStateMachine.cs` as mega coordinator

It is the largest bottleneck. It decides command flow, executes prompts, parses outputs, writes artifacts, updates lifecycle, writes state, writes journals, appends decisions, handles cancellation, and constructs recovery evidence. The file's constructor and direct method responsibilities make it the universal executor for roadmap progression.

### `RoadmapResumePlanner.cs` as hidden route authority

The actual next action after startup is mostly decided here, not by `NextValidTransitions`. It combines state routing with artifact/lifecycle/projection/provenance safety, so execution order is partially hidden behind readiness validation.

### `RoadmapUnblockPlanner.cs` as recovery authority

Recovery depends on string intents persisted in state. The planner contains both routing and evidence validation, which makes recovery behavior hard to extend without understanding state persistence, evidence files, parsers, and policies.

### `SaveStateAsync` as persistence bottleneck

`SaveStateAsync` does more than save current state. It reloads existing state, manifest, active artifacts, last decision, retired epics, blockers, split family count, projection manifest counts, transition intent, and next-transition text (`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1718`-`src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1756`). This makes every state write implicitly coupled to repository inspection and reporting metadata.

### Prompt strings as architectural keys

Runtime prompt names key contracts, projections, transition inputs, prompt rendering, parser behavior, and state-machine calls. Evidence appears in `PromptContractRegistry`, `TransitionInputResolver`, `ProjectionCache`, and state-machine prompt methods. This creates a hidden dependency graph around strings rather than state/transition ownership.

## Implicit State Machine Inventory

| Concept | Representation | Evidence |
|---|---|---|
| State | `RoadmapState` enum | `src/LoopRelay.Roadmap.Cli/RoadmapState.cs:3`-`src/LoopRelay.Roadmap.Cli/RoadmapState.cs:34` |
| Transition status | `TransitionStatus` enum | `src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs:41`-`src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs:49` |
| Last transition | `RoadmapTransitionSummary` | `src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs:22`-`src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs:31` |
| Transition intent | `RoadmapTransitionIntent` string plus dispatch state and evidence | `src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs:33`-`src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs:39` |
| Next valid transitions | persisted list of strings | `src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs:12`-`src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs:14` |
| Startup phase | `RoadmapStartupPlan` | `src/LoopRelay.Roadmap.Cli/RoadmapStartupPlanner.cs:63`-`src/LoopRelay.Roadmap.Cli/RoadmapStartupPlanner.cs:85` |
| Resume action | `RoadmapResumeAction` | `src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:535`-`src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs:544` |
| Unblock action/status | `RoadmapUnblockAction`, `RoadmapUnblockPlanStatus` | `src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:647`-`src/LoopRelay.Roadmap.Cli/RoadmapUnblockPlanner.cs:661` |
| Artifact lifecycle | `ArtifactLifecycleState` stored through lifecycle store | `src/LoopRelay.Roadmap.Cli/ArtifactLifecycleStore.cs:40`-`src/LoopRelay.Roadmap.Cli/ArtifactLifecycleStore.cs:49` |
| Projection lifecycle/freshness | projection manifest entries and stale policy | `src/LoopRelay.Roadmap.Cli/ProjectionCache.cs:33`-`src/LoopRelay.Roadmap.Cli/ProjectionCache.cs:81` |
| Selection decision | prompt output strings parsed by `SelectionParser` | `src/LoopRelay.Roadmap.Cli/SelectionParser.cs:5`-`src/LoopRelay.Roadmap.Cli/SelectionParser.cs:34` |
| Execution disposition | status/command route table | `src/LoopRelay.Roadmap.Cli/ExecutionDispositionProtocol.cs:35`-`src/LoopRelay.Roadmap.Cli/ExecutionDispositionProtocol.cs:55` |
| Completion recommendation | policy and route table | `src/LoopRelay.Completion/CompletionCertificationPolicy.cs:23`-`src/LoopRelay.Completion/CompletionCertificationPolicy.cs:50`, `src/LoopRelay.Completion/CompletionCertificationRouter.cs:5`-`src/LoopRelay.Completion/CompletionCertificationRouter.cs:31` |
| Checkpoint/history | transition journal JSONL | `src/LoopRelay.Roadmap.Cli/TransitionJournal.cs:3`-`src/LoopRelay.Roadmap.Cli/TransitionJournal.cs:18`, `src/LoopRelay.Roadmap.Cli/TransitionJournalStore.cs:9`-`src/LoopRelay.Roadmap.Cli/TransitionJournalStore.cs:14` |
| Invariants | validator output plus failure evidence | `src/LoopRelay.Roadmap.Cli/InvariantValidator.cs:16`-`src/LoopRelay.Roadmap.Cli/InvariantValidator.cs:135` |

## Execution Authority

Execution authority is fragmented:

- CLI command authority: `ExecuteAsync` command switch.
- Startup authority: `RoadmapStartupPlanner`.
- Resume authority: `RoadmapResumePlanner`.
- Branch authority: parser output strings and state-machine switches.
- Prompt execution authority: prompt helper methods.
- Completion authority: completion policy/router plus roadmap mapper.
- Execution disposition authority: execution disposition policy/table, where currently used.
- Recovery authority: persisted transition intent strings plus `RoadmapUnblockPlanner`.
- Persistence authority: direct `SaveStateAsync` call sites.

The ultimate "what executes next" answer is not centralized. For active runs it is mostly `RoadmapStateMachine` plus `RoadmapResumePlanner`. For recovery it is `RoadmapUnblockPlanner`. For output-driven branches it is parser/policy/router logic. For completion closure it is shared completion routing mapped into roadmap states. For display, `NextTransitions` exists but is not execution authority.

## Linear Understandability

A partial happy path can be made linear:

1. Parse CLI and construct state machine.
2. Dispatch `run`.
3. Load state and startup plan.
4. Preflight project context and prompt contracts.
5. Build resume plan.
6. If no state, persist `CoreReady`.
7. Bootstrap roadmap completion context if absent.
8. Select next strategic initiative.
9. Branch on selection output.
10. Produce/promote active epic or pause on terminal selection outcome.
11. Generate milestone specs.
12. Persist `MilestoneSpecsReady`.
13. Return paused.

Full execution cannot be understood linearly because:

- Branching depends on model output parsed at runtime.
- Resume behavior depends on persisted state plus artifact/lifecycle/provenance/projection freshness.
- Prompt transition helper behavior differs for promotion and non-promotion transitions.
- Completion routing uses separate shared and roadmap-specific route tables.
- Execution states exist but are treated as legacy/report-only by current resume planning.
- Failure paths can write several different blocker states and recovery intents.
- `NextValidTransitions` is persisted but not used as a guard for execution authority.

## Refactor Readiness Assessment

The implementation already contains natural phases, but they are not consistently represented as architectural boundaries:

- startup/preflight
- resume planning
- projection readiness
- prompt context construction
- transition input snapshot
- prompt execution
- output interpretation
- artifact materialization/promotion
- invariant validation
- state persistence
- journaling
- blocker/recovery
- completion certification
- cancellation recovery

Evidence for natural phases is visible in distinct helper groups and repeated patterns, especially prompt transition helpers, promotion handling, planner decisions, route tables, state persistence, and journal records. The opportunity is not to invent phases; it is to make already-existing phases explicit and owned.

## Refactor Opportunities

These are architectural opportunities revealed by the audit, not implementation prescriptions:

- Make state, step, transition, and next state explicit concepts rather than dispersed `SaveStateAsync` calls and string-driven branches.
- Separate execution authority from persistence/reporting metadata so a transition decision can be read independently of how the state document is assembled.
- Separate prompt execution lifecycle from artifact promotion lifecycle; today generic transitions and promotion transitions encode different lifecycles.
- Make recovery ownership explicit; today recovery is distributed across transition intent strings, unblock planner switch arms, and state-machine recovery methods.
- Make lifecycle stages explicit; current code has startup, resume, preflight, prompt, promotion, validation, persistence, completion, and cancellation phases, but they are implicit in method order.
- Clarify whether legacy execution states are active, external, or retired. Current code still models them but resume planning does not advance them.
- Consolidate transition dependency ownership; prompt contracts, transition input resolver, projection paths, context builder, parsers, and state-machine methods currently share prompt-name keyed responsibilities.
- Treat `NextValidTransitions` as either authoritative or reporting-only. Current evidence shows it is reporting/persistence data, not a decision guard.
- Isolate state mutation from journal writing and blocker evidence generation so failure behavior can be reasoned about without reading the entire orchestrator.
- Make model-output decision points explicit: selection outcomes, audit dispositions, completion recommendations, execution disposition pairs, and blocker classifications all affect state but are represented in different places.

No framework or library recommendation is implied. The opportunity is to expose the architecture the code already suggests: explicit states, explicit phases, explicit transition ownership, and smaller focused handlers that preserve current behavior while making execution order navigable.
