# Roadmap CLI Transition Extraction Plan

## Objective

Move Roadmap CLI transition-specific orchestration out of `RoadmapStateMachine` and into named transition handlers while preserving every observable behavior: prompt names, artifact paths, state summaries, journal events, decision ledger entries, lifecycle mutations, recovery intents, console phase text, exception behavior, and caller-visible outcomes.

The implementation target is an extraction and readability refactor, not a workflow redesign. The Roadmap CLI should keep the same commands, state enum values, prompt contracts, projection paths, artifact schemas, and resume/unblock semantics.

## Baseline

Current verification command:

```powershell
dotnet test LoopRelay.slnx --no-restore
```

Current result on July 8, 2026: all test projects pass, with 965 passed tests and 5 skipped tests.

## Non-Goals

- Do not add new CLI commands.
- Do not change prompt text, projection prompt text, prompt contract keys, or projection registry keys.
- Do not rename existing artifact paths under `.agents`.
- Do not change `RoadmapState`, `TransitionStatus`, journal event names, decision text, or transition intent names.
- Do not change the behavior of startup planning, resume planning, unblock planning, execution preparation, or completion routing except where existing transition calls are moved behind handlers.
- Do not roll back partially materialized artifacts unless the current implementation already does.

## Codebase Map

Primary orchestration:

- `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapCliComposition.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapStateMachineSelectionTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/TestDoubles.cs`

Shared transition collaborators already present:

- `RoadmapArtifacts`
- `PromptContractRegistry`
- `ProjectionCache`
- `RoadmapPromptContextBuilder`
- `TransitionInputResolver`
- `RoadmapPromptRunner`
- `RoadmapStateStore`
- `ProjectionManifestStore`
- `DecisionLedgerStore`
- `TransitionJournalStore`
- `ArtifactLifecycleStore`
- `SelectionProvenanceService`
- `ArtifactPromotionService`
- `BundleFileExtractor`
- `BundleManifestWriter`
- `SplitEpicBundleInterpreter`
- `SplitFamilyStore`
- `ExecutionPreparationProvenanceService`
- `InvariantValidator`
- `CompletionCertificationPolicy`
- `CompletionCertificationRouter`
- `ICompletedEpicArchiveService`

## Target Structure

Create a transition-focused folder in the Roadmap CLI project:

```text
src/LoopRelay.Roadmap.Cli/Transitions/
```

Add shared services first:

- `RoadmapTransitionPersistence`
  - Owns the existing `SaveStateAsync`, active artifact row calculation, projection manifest count calculation, split-family count, last-decision lookup, default next-transition mapping, output path parsing, and workflow-failure persistence helpers that handlers need.
- `RoadmapPromptTransitionRunner`
  - Owns the existing prompt envelope variants:
    - normal transitions: `TransitionStarted` plus `TransitionCompleted`, started state at the target state, decisions `Pending` and `Completed`
    - promotion-candidate transitions: `TransitionStarted` plus `PromptCompleted`, current state remains the source state until promotion, decisions `Prompt Started` and `Prompt Completed`
    - runtime failure: `TransitionFailed`, `EvidenceBlocked`, status `Failed`, `ResolveTransitionFailure`, and already-persisted exception
- `ActiveSelectionReader`
  - Owns the existing current-selection read plus `SelectNextEpic` projection presence check, state load, current-cycle capture, freshness validation, and stale-selection error message.
- `DecisionRecorder`
  - Owns current `AppendDecisionAsync` behavior without changing ledger fields.
- `HitlArtifactCapture`
  - Wraps optional `ExplicitHitlNonImplementationRequestCaptureService`; remains a no-op when the service is absent or content is blank.
- `ActiveEpicPromotionCoordinator`
  - Owns current `PromoteActiveEpicAsync` behavior around `ArtifactPromotionService`: active epic promotion, HITL capture, `ArtifactPromoted`, `ArtifactPromotionBlocked`, final state persistence, blocker rows, and `ResolveArtifactPromotionBlocker`.
- `SelectionSuperseder`
  - Owns selection provenance superseding plus `.agents/selection.md` lifecycle `Superseded` writes.

Keep `RoadmapStateMachine` responsible for command dispatch, status, preflight, startup/resume/unblock routing, and transition chaining. After extraction, its main flow should read as calls to named handlers rather than transition internals.

## Handler Inventory

Add these handlers:

- `BootstrapRoadmapCompletionContextTransition`
- `SelectNextEpicTransition`
- `CreateNewEpicTransition`
- `ActiveEpicRewriteTransition`
- `EpicPreparationAuditTransition`
- `SplitEpicTransition`
- `GenerateMilestoneDeepDivesTransition`
- `CompletionCertificationTransition`
- `RoadmapCompletionContextUpdateTransition` as a helper used by completion close routes

`ActiveEpicRewriteTransition` should support both `RealignEpic` and `ReimagineEpic` through explicit configuration: prompt name, source state, phase text, projection key, and lifecycle note. Thin wrappers or factory methods may expose `Realign` and `Reimagine` entry points.

## Implementation Sequence

### Phase 1: Establish Characterization Coverage

Keep existing state-machine integration tests. Add focused tests only where the current behavior is not directly pinned.

Add or confirm tests for:

- selection parse failure after `.agents/selection.md`, selection evidence, provenance, and lifecycle are already written
- `Insufficient Evidence` audit output persisting audit evidence and audit decision before throwing
- bootstrap runtime failure preserving output path `.agents/core/roadmap-completion-context.md`
- `CreateNewEpic` prompt failure using status `Failed`, decision `Runtime Failure`, and intent `ResolveTransitionFailure`
- `RealignEpic` fallback to current selection only when `.agents/epic.md` is missing
- milestone post-prompt bundle failure writing `milestone-spec-generation-failed.NNNN.md`
- completion evaluation parse failure after evaluation evidence is written, without converting it to invalid-certification blocker state
- close-route completion-context update superseding active selection after the context rewrite

Run:

```powershell
dotnet test tests/LoopRelay.Roadmap.Cli.Tests/LoopRelay.Roadmap.Cli.Tests.csproj --no-restore
dotnet test LoopRelay.slnx --no-restore
```

### Phase 2: Extract Shared Services Without Moving Transitions

Move existing private helper behavior into the shared services listed above. Keep `RoadmapStateMachine` calling the same operations through the new services, but do not move transition bodies yet.

Acceptance:

- `RoadmapStateMachine` behavior is unchanged.
- Existing constructor wiring is updated in `RoadmapCliComposition` and `StateMachineFactory`.
- No handler is introduced until shared helper tests pass.
- Roadmap CLI tests pass after each helper extraction.

Important equivalence checks:

- `SaveStateAsync` still preserves existing retired epics, blockers, transition intent, active artifacts, projection manifest counts, split-family count, and last decision id.
- Default `NextTransitions` values remain identical.
- Runtime prompt failures still bypass generic failure overwrite by throwing `RoadmapStepException.AlreadyPersisted`.
- `OperationCanceledException` is not caught by prompt failure blocks.

### Phase 3: Extract Simple Linear Transitions

Move the smallest single-purpose flows first.

#### Bootstrap Roadmap Completion Context

Handler method:

```csharp
Task ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

Entry remains inside `RunFromCoreReadyAsync` after the caller verifies `.agents/core/roadmap-completion-context.md` is not `Present`.

Preserve:

- phase `Bootstrap roadmap completion context`
- prompt `CreateRoadmapCompletionContext`
- state `CoreReady -> RoadmapCompletionContextReady`
- projection `.agents/projections/roadmap-completion.md`
- output `.agents/core/roadmap-completion-context.md`
- completed-epic archive glob `.agents/archive/epics/*.md`
- optional archive input behavior
- `TransitionStarted`, `TransitionCompleted`, and `TransitionFailed`
- started decision `Pending`
- completed decision `Completed`
- runtime failure intent `ResolveTransitionFailure`
- verbatim write of prompt output to `.agents/core/roadmap-completion-context.md`
- optional HITL capture
- lifecycle `Ready` for `.agents/core/roadmap-completion-context.md`

#### Select Next Epic

Handler method:

```csharp
Task<SelectionDecision> ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

Preserve:

- phase `Select next strategic initiative`
- prompt `SelectNextEpic`
- state `RoadmapCompletionContextReady -> SelectNextStrategicInitiative`
- projection `.agents/projections/select-next-epic.md`
- output `.agents/selection.md`
- empty secondary input
- context section order from `BuildSelectionContextAsync`
- started state before `TransitionStarted`
- `TransitionCompleted` before completed state
- completed state before `.agents/selection.md` write
- `.agents/selection.md` write before HITL capture
- HITL capture before numbered selection evidence
- selection evidence before provenance recording
- provenance before selection lifecycle `Ready`
- parsing after lifecycle
- decision-ledger append after parsing
- parse failures not being converted into `TransitionFailed`

`RunSelectionAndFollowingAsync` should still call this handler and then call `ContinueAfterSelectionAsync`.

### Phase 4: Extract Active Epic Promotion Transitions

Introduce `ActiveEpicPromotionCoordinator` before moving these handlers, because `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`, and `SplitEpic` all depend on the same final promotion behavior.

#### Create New Epic

Handler method:

```csharp
Task<ArtifactPromotionResult> ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

Preserve:

- phase `Create new epic`
- fresh active selection validation
- prompt `CreateNewEpic`
- state `NewEpicProposed -> ActiveEpicReady`
- projection `.agents/projections/create-new-epic.md`
- secondary input equal to selection content
- prompt start state `NewEpicProposed` / `Started`, decision `Prompt Started`
- prompt-completed state `NewEpicProposed` / `PromptCompleted`, decision `Prompt Completed`
- success state `ActiveEpicReady` / `Completed`, decision `Artifact Promoted`
- failure state `EvidenceBlocked` / `Failed`, decision `Runtime Failure`
- promotion-blocked state `EvidenceBlocked` / `Paused`
- no decision-ledger entry from this transition
- caller continues to milestone generation only when `Promoted == true`

#### Realign and Reimagine Active Epic

Handler method:

```csharp
Task<ArtifactPromotionResult> ExecuteAsync(
    string prompt,
    RoadmapState sourceState,
    ProjectContext projectContext,
    string auditPath,
    CancellationToken cancellationToken)
```

Allowed prompt/state pairs:

- `RealignEpic`, `RoadmapState.RealignEpic`
- `ReimagineEpic`, `RoadmapState.ReimagineEpic`

Preserve:

- phase text exactly equal to the prompt name
- `.agents/epic.md` read when present
- current selection fallback when `.agents/epic.md` is absent
- selection fallback freshness validation
- required audit evidence at `auditPath`
- audit evidence passed in context and as secondary input
- transition input roles for projection, active epic or selection, and audit evidence
- prompt start and prompt-completed states
- active epic write only after classifier and validator pass
- blocked output preserved under `.agents/evidence/blockers/active-epic-promotion.NNNN.md`
- active epic lifecycle `Ready` note `Promoted by {prompt}.`
- blocked evidence lifecycle `Blocked`
- no decision-ledger entry from the rewrite transition itself

### Phase 5: Extract Epic Preparation Audit

Handler method:

```csharp
Task<EpicPreparationResult> ExecuteAsync(
    SelectionDecision selectionDecision,
    ProjectContext projectContext,
    CancellationToken cancellationToken)
```

Preserve:

- phase `Audit selected epic`
- fresh active selection validation before audit prompt execution
- prompt `EpicPreparationAudit`
- state `ExistingEpicSelected -> EpicPreparationAudit`
- projection `.agents/projections/epic-preparation-audit.md`
- audit evidence stem `epic-preparation-audit`
- HITL capture from audit evidence
- audit decision ledger entry before branch routing
- retire branch:
  - create `RetiredEpic` from selection and audit
  - upsert retired epics
  - append retire decision
  - save `RetireEpic` / `Completed`
  - supersede selection provenance with `RetiredEpicStateDrift`
  - mark `.agents/selection.md` `Superseded`
  - return `EpicPreparationResult.Retired`
- insufficient-evidence branch:
  - throw after audit evidence and audit decision persistence
  - do not write a new durable blocker inside the branch
- realign and reimagine branches:
  - delegate to `ActiveEpicRewriteTransition`
  - return `ActiveEpicReady` when promoted
  - return `Blocked` when not promoted

`ContinueAfterSelectionAsync` should keep mapping `Retired` and `Blocked` to `RoadmapOutcome.Paused`; `ActiveEpicReady` should still fall through to milestone generation.

### Phase 6: Extract Split Epic

Handler method:

```csharp
Task<ArtifactPromotionResult> ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

Preserve:

- phase `Split epic`
- fresh active selection validation
- prompt `SplitEpic`
- state `SplitEpicProposed -> SplitChildSelection`
- projection `.agents/projections/split-epic.md`
- output path `.agents/splits`
- bundle extraction policy `RepositorySafe`
- invalid extraction converted to split-bundle interpretation failure
- split interpreter requiring `.agents/epic-N.md` paths
- whole-bundle rejection before any child file is written
- selected child remains the first validated child by numeric child order
- child writes after complete validation only
- `.agents/bundle-manifest.md` shape and validation result `Valid`
- child lifecycle `Draft` with note `Validated split child epic.`
- HITL capture for each child
- split family JSON under `.agents/splits/split-family-{id}.json`
- family id shape `Guid.NewGuid().ToString("N")[..8]`
- child promotion uses selected child content, not raw prompt output
- original prompt correlation id, timing, and input snapshot reused for promotion journal records
- successful promotion writes `.agents/epic.md`, appends `ArtifactPromoted`, and saves `ActiveEpicReady`
- split-output blocker:
  - evidence stem `split-epic-output`
  - decision `Split Epic Blocked` for blocked interpretation
  - decision `Split Bundle Rejected` otherwise
  - event `SplitBundleRejected`
  - lifecycle `Blocked`
  - state `EvidenceBlocked` / `Paused`
  - intent `ResolveSplitEpicBlocker`
- active-epic promotion rejection:
  - event `ArtifactPromotionBlocked`
  - state `EvidenceBlocked` / `Paused`
  - intent `ResolveArtifactPromotionBlocker`
- runtime prompt failure:
  - state `EvidenceBlocked` / `Failed`
  - intent `ResolveTransitionFailure`

Caller behavior remains unchanged: promoted split proceeds to milestone generation; not promoted pauses.

### Phase 7: Extract Milestone Deep Dive Generation

Handler method:

```csharp
Task ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

Preserve:

- phase `Generate milestone deep dives`
- prompt `GenerateMilestoneDeepDivesForEpic`
- state `ActiveEpicReady -> MilestoneSpecsReady`
- projection `.agents/projections/milestone-deep-dive.md`
- required input `.agents/epic.md`
- empty secondary input
- prompt envelope uses `TransitionStarted` and `PromptCompleted`, not `TransitionCompleted`
- output path before materialization `.agents/specs`
- raw prompt output not stored on success
- extracted milestone files under `.agents/specs/*.md`
- `.agents/specs/bundle-manifest.md`
- lifecycle `Ready` for each spec
- optional HITL capture for each spec
- execution-preparation generator id `GenerateMilestoneDeepDivesForEpic:v1`
- invariant validation after spec materialization and execution-preparation provenance
- final event `MilestoneSpecsMaterialized`
- final state `MilestoneSpecsReady` / `Completed`, decision `Milestone Specs Ready`
- no decision-ledger entry on success
- no operational context or execution prompt generated here

Failure branches:

- runtime prompt failure remains `EvidenceBlocked` / `Failed`, decision `Runtime Failure`, intent `ResolveTransitionFailure`
- bundle extraction or materialization failure writes `.agents/evidence/blockers/milestone-spec-generation-failed.NNNN.md`, event `MilestoneSpecGenerationFailed`, state `EvidenceBlocked` / `Paused`, intent `ResolveMilestoneSpecGenerationFailure`
- invariant failure uses validator evidence or fallback evidence, event `InvariantFailed`, prompt `PostMilestoneInvariantValidation`, intent `ResolveInvariantViolation`
- no rollback of files already written before post-prompt or invariant failures

### Phase 8: Extract Completion Certification

Handler method:

```csharp
Task<RoadmapOutcome> ExecuteAsync(
    ProjectContext projectContext,
    DateTimeOffset executionStarted,
    string executionEvidencePath,
    CancellationToken cancellationToken,
    bool persistCompletionClaim = true,
    ExecutionDispositionRoute? completionRoute = null)
```

Preserve both call paths:

- normal execution completion, where the completion claim state is persisted before evaluation
- resume from persisted completion claim, where `persistCompletionClaim` is false and the evidence path was recovered before entry

Preserve:

- optional non-implementation completion-review gate before evaluation prompt
- gate blocker evidence stem `non-implementation-completion-review-blocked`
- prompt `EvaluateEpicCompletionAndDrift`
- state `EpicCompletionDetected -> CompletionEvaluationAndContextUpdate`
- projection `.agents/projections/epic-completion-evaluation.md`
- evaluation evidence stem `epic-completion-and-drift`
- HITL capture from evaluation evidence before parsing
- parse failure after evidence write is not converted to invalid-certification blocker
- completion policy validation after parse
- evaluation decision ledger append
- invalid certification:
  - blocker evidence stem `invalid-completion-certification`
  - event `CompletionCertificationRejected`
  - prompt `CompletionCertificationRouting`
  - contract key `CompletionCertificationPolicy`
  - state `EvidenceBlocked` / `Paused`
  - decision `Invalid Completion Certification`
  - intent `ResolveInvalidCompletionCertification`
  - return `RoadmapOutcome.Paused`
- valid route mapping through `CompletionCertificationRouter` and `RoadmapCompletionRouteMapper`
- close routes:
  - archive completed execution workspace before completion-context update
  - synthesize completed epic
  - prompt `UpdateRoadmapCompletionContext`
  - projection `.agents/projections/roadmap-completion-update.md`
  - state `CompletionEvaluationAndContextUpdate -> SelectNextStrategicInitiative`
  - rewrite `.agents/core/roadmap-completion-context.md`
  - HITL capture from rewritten completion context
  - write numbered `roadmap-completion-update` evidence
  - supersede active selection because completion context changed
  - append `Roadmap Completion Context Updated` decision
  - final route output list includes evaluation path, `.agents/core/roadmap-completion-context.md`, and synthesis path, but not the numbered update evidence
- active epic lifecycle update according to route
- final route persistence:
  - close routes target `SelectNextStrategicInitiative`, status `Completed`, next `SelectNextEpic`
  - continue route target `ExecutionLoop`, status `Paused`, next `ContinueExecution`
  - reopen route target `EpicPreparationAudit`, status `Paused`, next `EpicPreparationAudit`
  - gather-more-evidence route target `EvidenceGathering`, status `Paused`, next `GatherAdditionalEvidence` and `EvaluateEpicCompletionAndDrift`
- prompt runtime failure remains already persisted
- archive and synthesis failures are not converted into invalid-certification blockers

Move `UpdateRoadmapCompletionContextAsync` into `RoadmapCompletionContextUpdateTransition` and call it only from the completion handler.

### Phase 9: Slim `RoadmapStateMachine`

After handlers are wired:

- Remove extracted private methods from `RoadmapStateMachine`.
- Keep command dispatch, status, unblock recovery, startup preflight, resume-plan execution, terminal selection route persistence, cancellation persistence, and generic error reporting.
- Keep simple selection terminal routes in `ContinueAfterSelectionAsync` unless a later refactor extracts them. They are not required for this change.
- Ensure `RoadmapStateMachine` constructor only receives handlers and high-level planners for paths it still owns.

Expected shape:

```text
RunFromCoreReady
-> if completion context missing: bootstrap handler
-> selection-and-following

RunSelectionAndFollowing
-> select-next handler
-> continue-after-selection

ContinueAfterSelection
-> existing epic: epic-preparation handler
-> new epic: create-new handler
-> split epic: split handler
-> terminal selection outcomes: existing state/decision persistence
-> if active epic ready: milestone handler
```

## Test Plan

Run targeted tests after every phase:

```powershell
dotnet test tests/LoopRelay.Roadmap.Cli.Tests/LoopRelay.Roadmap.Cli.Tests.csproj --no-restore
```

Run full solution tests after shared-service extraction, after all handlers are wired, and after final cleanup:

```powershell
dotnet test LoopRelay.slnx --no-restore
```

Keep or add direct handler tests where they reduce setup noise, but preserve existing state-machine integration tests because they validate chaining, resume planning, and final CLI outcomes.

Minimum acceptance matrix:

| Area | Required coverage |
|---|---|
| Bootstrap | success, runtime failure, existing context skip through caller |
| Selection | success, runtime failure, parse failure after materialization, terminal selections |
| Epic preparation | retire, insufficient evidence, realign success, reimagine success, promotion blocked |
| Create new epic | success, blocked output, ambiguous output, prompt failure |
| Split epic | valid split, invalid child, direct active-epic target rejection, blocked split output, promotion blocked |
| Milestones | success, runtime failure, bundle failure, invariant failure |
| Completion | review gate blocked, invalid certification, close route, continue route, reopen route, gather-more-evidence route, prompt failure |
| Shared persistence | state summaries preserve active artifacts, manifest counts, split-family count, retired epics, blockers, transition intents, and last decision id |
| Cancellation | operation cancellation reaches outer cancellation state writer |

## Migration Notes

- Update `RoadmapCliComposition.Create` to instantiate shared transition services and handlers once, then pass handlers to `RoadmapStateMachine`.
- Update `StateMachineFactory.Create` in tests with the same wiring pattern. Prefer a small test helper builder for the shared services to avoid repeating constructor churn.
- Keep new classes `internal sealed` unless tests require direct access through the existing test assembly visibility.
- Avoid changing public model constructors or serialized record shapes.
- Use existing validators, parsers, and prompt context builders rather than duplicating parsing rules.
- Do not convert helper names into broader abstractions until at least two handlers need the same behavior.

## Risks And Controls

- Risk: state persistence order changes.
  - Control: move `SaveStateAsync` behavior first with characterization tests, then call it from handlers without reordering.
- Risk: prompt completion state is mistaken for artifact completion.
  - Control: keep normal prompt transitions and promotion-candidate prompt transitions as distinct methods in `RoadmapPromptTransitionRunner`.
- Risk: parse failures become durable transition failures by accident.
  - Control: keep parse calls after the same artifact writes and outside prompt failure catch blocks.
- Risk: split writes partial children on invalid output.
  - Control: keep whole-bundle interpretation before `WriteExtractedFilesAsync`.
- Risk: completion close route includes the wrong output set.
  - Control: assert final route output list separately from update evidence writes.
- Risk: constructor churn hides behavior changes in tests.
  - Control: update test factory first, then keep state-machine integration tests unchanged where possible.

## Definition Of Done

- `RoadmapStateMachine` no longer contains the transition bodies for bootstrap, selection, epic preparation, create new epic, active epic rewrite, split epic, milestone generation, or completion certification.
- Each extracted handler has one clear entry method and owns only its transition-specific flow.
- Shared prompt and state persistence behavior is centralized and covered by tests.
- Existing artifact paths, state values, journal events, decision entries, lifecycle rows, transition intents, console phases, and caller outcomes remain unchanged.
- `dotnet test LoopRelay.slnx --no-restore` passes with no new failures.
