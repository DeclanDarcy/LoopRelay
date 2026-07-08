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

(See ./milestones/m1-establish-characterization-coverage.md)

### Phase 2: Extract Shared Services Without Moving Transitions

(See ./milestones/m2-extract-shared-services.md)

### Phase 3: Extract Simple Linear Transitions

(See ./milestones/m3-extract-simple-linear-transitions.md)

### Phase 4: Extract Active Epic Promotion Transitions

(See ./milestones/m4-extract-active-epic-promotion-transitions.md)

### Phase 5: Extract Epic Preparation Audit

(See ./milestones/m5-extract-epic-preparation-audit.md)

### Phase 6: Extract Split Epic

(See ./milestones/m6-extract-split-epic.md)

### Phase 7: Extract Milestone Deep Dive Generation

(See ./milestones/m7-extract-milestone-deep-dive-generation.md)

### Phase 8: Extract Completion Certification

(See ./milestones/m8-extract-completion-certification.md)

### Phase 9: Slim `RoadmapStateMachine`

(See ./milestones/m9-slim-roadmap-state-machine.md)

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
