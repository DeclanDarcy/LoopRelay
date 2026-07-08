# Semantic Architecture Migration Implementation Plan

## Purpose

This plan adapts `semantic-architecture-migration-roadmap.md` into an actionable implementation sequence.

This is Track B: migration from the current Roadmap CLI state-machine implementation toward the semantic architecture. It does not replace `plan.md`, which remains Track A: executable semantic realization of the constitution.

The roadmap answers:

```text
How do we transform the current implementation into the semantic architecture?
```

This plan answers:

```text
What code, artifacts, tests, and gates are required to recover, wrap, replace, and retire each current path?
```

The implementation strategy is conservative. First make the current state-machine behavior inspectable as it exists. Then define ownership and migration seams. Then wrap one path through the Track A semantic model while preserving all externally visible behavior. Only after equivalence is proven should any legacy path become secondary or be retired.

## Inputs

- `semantic-constitution.md`
- `canonical-semantic-architecture-roadmap.md`
- `plan.md`
- `semantic-architecture-migration-roadmap.md`
- `state-machine-refactor-audit.md`
- Current Roadmap CLI implementation under `src/LoopRelay.Roadmap.Cli`
- Current Roadmap CLI tests under `tests/LoopRelay.Roadmap.Cli.Tests`

## Track Boundary

| Track | Governing question | Primary artifact | Implementation rule |
| --- | --- | --- | --- |
| Track A: Semantic Realization | How do we implement the constitution? | `plan.md` | Build stable semantic behavior for `RepositoryWork` from the constitution outward. |
| Track B: Migration | How do we transform the current implementation safely? | `migration-plan.md` | Recover and wrap existing behavior without turning migration convenience into semantic authority. |

Track B may create inventories, catalogs, compatibility records, comparison harnesses, and temporary adapters. Those artifacts are migration aids. They must not become constitutional primitives.

## Migration Posture

Use this order of force:

```text
recover current behavior
  -> make transition routing inspectable
  -> map ownership and mixed concerns
  -> define one migration seam
  -> wrap one current path semantically
  -> prove behavior equivalence
  -> make semantic path primary
  -> retire legacy path and compatibility seam
```

Rules:

- Keep `status`, `run`, and `unblock` behavior unchanged until a semantic replacement has behavior-equivalence evidence.
- Do not add migration report writes as hidden side effects of existing commands during recovery phases.
- Prefer explicit catalogs over inferred knowledge hidden in switch statements.
- Prefer one narrow wrapper over a generic compatibility framework.
- Treat persisted state, prompt output, parser output, report text, and file writes as legacy evidence or compatibility effects, not semantic authority.
- Retire compatibility only when blockers, recovery, reports, state, lifecycle, journal, and artifacts are represented by the semantic path.

## Current Implementation Surfaces

Track B starts from these concrete surfaces:

| Concern | Current implementation |
| --- | --- |
| CLI command admission | `CliArguments`, `RoadmapCliCommand` with `Status`, `Run`, and `Unblock` |
| Startup routing | `RoadmapStartupPlanner` |
| Resume routing | `RoadmapResumePlanner` |
| Runtime orchestrator | `RoadmapStateMachine` |
| Persisted workflow state | `RoadmapStateStore`, `RoadmapStateDocument`, `.agents/state.json`, `.agents/state.md` |
| Transition journal | `TransitionJournalStore`, `TransitionJournalRecord`, `.agents/journal/transitions.jsonl` |
| Prompt contracts | `PromptContractRegistry`, `.agents/contracts/prompt-contracts.md` |
| Projection freshness | `ProjectionCache`, `ProjectionManifestStore`, `ProjectionProvenanceFactory` |
| Transition inputs | `TransitionInputResolver`, `TransitionInputSnapshot` |
| Decisions | `DecisionLedgerStore`, `DecisionLedgerEntry`, `.agents/decision-ledger.json` |
| Artifact lifecycle | `ArtifactLifecycleStore`, `.agents/artifacts/lifecycle.json` |
| Artifact promotion | `ArtifactPromotionService`, `EpicAuthoringOutputClassifier`, `EpicArtifactValidator` |
| Blockers and recovery | `RoadmapBlockedArtifact`, `RoadmapUnblockPlanner`, unblock recovery methods |
| Completion routing | `CompletionCertificationPolicy`, `CompletionCertificationRouter`, `RoadmapCompletionRouteMapper` |

The initial migration implementation should extend these surfaces only where needed to make current behavior explicit. It should not first reorganize namespaces or split projects.

## Migration Artifact Layout

Add migration artifacts under a new non-authoritative area:

```text
.agents/migration/
  current-state-inventory.md
  current-state-inventory.json
  transition-catalog.md
  transition-catalog.json
  state-next-transition-index.md
  effect-inventory.md
  ownership-map.md
  ownership-map.json
  seams/
    select-next-epic.md
    select-next-epic.json
  compatibility/
    select-next-epic-equivalence.json
  status.json
```

Implementation target:

- Add migration artifact constants to `RoadmapArtifactPaths` or a small adjacent `RoadmapMigrationArtifactPaths`.
- Use existing `RoadmapArtifacts` and `IArtifactStore` abstractions for writes.
- Use JSON for machine verification and Markdown for HITL review.
- Mark every generated migration artifact as non-authoritative in its header.

## Milestone Template

Every migration milestone must specify:

```text
Executable Outcome
What can now run or be inspected that could not before?

Code Changes
Which concrete files, classes, or tests change?

Durable Artifacts
Which records are created and where?

Behavior Preservation
How do existing command behavior and persisted compatibility remain unchanged?

Evaluation Gate
How can HITL or tests tell the milestone worked?

Retirement Rule
What condition allows any temporary migration structure to be removed?
```

## First Migration Path

The first semantic wrapper should target `SelectNextEpic`.

Reasons:

- It is central to Roadmap CLI routing.
- It already has a prompt contract, projection freshness rule, input snapshot, source inputs, selection provenance, parser, decision ledger write, lifecycle write, state write, and downstream branching.
- It is narrower than completion certification or split epic handling.
- It exercises the semantic boundaries Track A needs first: `RepositoryWork` subject, intent, source capture, protocol admission, interaction, observation, evidence, decision, artifact effect, state effect, and report.

Legacy path to preserve:

```text
RoadmapCompletionContextReady or RetireEpic or SelectNextStrategicInitiative
  -> RoadmapResumePlanner returns SelectNextStrategicInitiative or ContinueSelectionDecision
  -> RoadmapStateMachine.SelectNextInitiativeAsync
  -> PromptContractRegistry.Get("SelectNextEpic")
  -> ProjectionCache.EnsureAsync("SelectNextEpic")
  -> RoadmapPromptContextBuilder.BuildSelectionContextAsync
  -> RunPromptTransitionWithCompletionAsync
  -> write .agents/selection.md
  -> write .agents/evidence/selection/selection-*.md
  -> SelectionProvenanceService.RecordActiveSelectionAsync
  -> ArtifactLifecycleStore marks selection ready
  -> SelectionParser.Parse
  -> DecisionLedgerStore append
  -> ContinueAfterSelectionAsync routes to audit, create, split, pause, or no suitable initiative
```

The first wrapper must preserve those writes, journal records, parser decisions, lifecycle changes, state changes, and downstream routing before it can become primary.

## Phase 0: Current State Machine Recovery

### Goal

Recover the current Roadmap CLI state machine as implementation evidence, without changing `status`, `run`, or `unblock`.

### Executable Outcome

A recovery writer can produce current-state and current-transition inventory artifacts from explicit catalog data and current implementation evidence. It is callable from tests or a dedicated non-default inspection entry point.

### Code Changes

Add narrow migration recovery models under `src/LoopRelay.Roadmap.Cli`:

- `RoadmapMigrationArtifactPaths`
- `RoadmapStateInventory`
- `RoadmapTransitionInventory`
- `RoadmapStateMachineRecoveryWriter`
- `RoadmapMigrationReportRenderer`

Initial inventory entries should be manually explicit, not broad reflection output. Reflection may be used only as a coverage check against `RoadmapState`.

### Durable Artifacts

Generate:

- `.agents/migration/current-state-inventory.md`
- `.agents/migration/current-state-inventory.json`

Each state inventory entry must include:

- `RoadmapState` value;
- current category: active, report-only pause, blocked, terminal, cancelled recovery, legacy retained, or unsupported;
- startup behavior from `RoadmapStartupPlanner`;
- resume behavior from `RoadmapResumePlanner`;
- known next transitions from `RoadmapStateMachine.NextTransitions`;
- persisted fields that matter for the state;
- blocker and recovery handling;
- code owner method or class;
- test coverage reference.

### Initial State Categories

Start with this categorization and update only from code evidence:

| Category | States |
| --- | --- |
| Active startup or resume | `CoreReady`, `BootstrapRoadmapCompletionContext`, `RoadmapCompletionContextReady`, `RetireEpic`, `SelectNextStrategicInitiative`, `ActiveEpicReady`, `GenerateMilestoneDeepDives`, `EpicCompletionDetected` |
| Active but intermediate inside one run | `ExistingEpicSelected`, `NewEpicProposed`, `SplitEpicProposed`, `EpicPreparationAudit`, `RealignEpic`, `ReimagineEpic`, `CreateNewEpic`, `SplitEpic`, `SplitChildSelection`, `CompletionEvaluationAndContextUpdate` |
| Ready pause | `MilestoneSpecsReady` |
| Report-only pause | `StrategicInvestigationRequired`, `RoadmapRevisionRequired`, `NoSuitableInitiative`, `EvidenceGathering`, `ExecutionBlocked` |
| Blocked recovery | `EvidenceBlocked`, `Failed` |
| Cancellation recovery | `Cancelled` |
| Terminal | `Completed` |
| Legacy retained and no longer advanced by Roadmap CLI | `GenerateOperationalContext`, `OperationalContextReady`, `GenerateExecutionPrompt`, `ExecutionPromptReady`, `ExecutionLoop` |

### Tests

Add `RoadmapStateMachineRecoveryTests`:

- every `RoadmapState` has exactly one inventory entry;
- every active state names a code owner;
- every report-only and legacy-retained state names the reason it is not advanced;
- startup categories agree with `RoadmapStartupPlannerTests`;
- resume categories agree with `RoadmapResumePlannerTests`;
- generated JSON round-trips.

### Behavior Preservation

Existing commands must not write migration artifacts by default in this phase. Existing tests for `RoadmapStateMachine*`, `RoadmapResumePlannerTests`, `RoadmapStartupPlannerTests`, `RoadmapStateStoreTests`, and `TransitionJournalTests` must continue to pass unchanged.

### Evaluation Gate

HITL can select any persisted `RoadmapState` and find:

- whether Roadmap CLI can advance it;
- which planner handles it;
- which method owns the next action;
- what evidence or artifact readiness is required;
- what blocker or report-only behavior applies.

## Phase 1: Explicit Transition Catalog

### Goal

Turn recovered behavior into one authoritative current transition catalog.

### Executable Outcome

The recovery writer can generate a transition catalog, state-to-next-transition index, and effect inventory that answer "what happens next?" without reading `RoadmapStateMachine` end to end.

### Code Changes

Add:

- `RoadmapTransitionDescriptor`
- `RoadmapTransitionCatalog`
- `RoadmapTransitionEffectDescriptor`
- `RoadmapTransitionCatalogWriter`

Each `RoadmapTransitionDescriptor` should include:

- stable transition id;
- command condition: `status`, `run`, `unblock`, or internal continuation;
- source state or source-state family;
- admission guard;
- prompt contract, if any;
- projection path and stale policy, if any;
- transition input context;
- observation source;
- parser or classifier;
- accepted decision source;
- artifact effects;
- lifecycle effects;
- state persistence effect;
- transition journal events;
- decision ledger effect;
- report output;
- blocker behavior;
- recovery target;
- legacy status: legacy, cataloged, seamed, wrapped, semantic-primary, retired.

### Durable Artifacts

Generate:

- `.agents/migration/transition-catalog.md`
- `.agents/migration/transition-catalog.json`
- `.agents/migration/state-next-transition-index.md`
- `.agents/migration/effect-inventory.md`

### Minimum Transition Families

The first catalog must cover at least:

| Family | Current code path |
| --- | --- |
| Status report | `StatusAsync` |
| Fresh initialization | `RoadmapStartupPlanner` plus `RoadmapResumePlan.InitializeCoreReady` |
| Completion context bootstrap | `BootstrapRoadmapCompletionContextAsync` |
| Selection generation | `SelectNextInitiativeAsync` |
| Selection decision continuation | `ContinueAfterSelectionAsync` |
| Existing epic preparation audit | `AuditAndPrepareExistingEpicAsync` |
| Active epic rewrite | `RewriteActiveEpicAsync` and `PromoteActiveEpicAsync` |
| New epic creation | `CreateNewEpicAsync` and `PromoteActiveEpicAsync` |
| Split epic | `SplitEpicAsync`, `BlockSplitEpicAsync`, `SplitFamilyStore` |
| Milestone spec generation | `GenerateMilestoneSpecsAsync` |
| Completion certification | `RunCompletionCertificationAsync`, `PersistCompletionRouteAsync`, invalid certification handling |
| Unblock review | `UnblockAsync`, `RoadmapUnblockPlanner`, recovery methods |
| Runtime failure persistence | `RunPromptTransitionWithCompletionAsync`, `RunPromptForPromotionAsync` failure branches |
| Invariant failure persistence | `PersistInvariantFailureAndThrowAsync`, `PersistWorkflowFailureAsync` |
| Cancellation | `WriteCancelledStateAsync` |
| Legacy execution preparation pause | `RoadmapResumePlanner` legacy states |

### Tests

Add `RoadmapTransitionCatalogTests`:

- every prompt in `PromptContractRegistry.All` appears in at least one catalog transition;
- every `RoadmapResumeAction` maps to one or more transition descriptors or terminal/report descriptors;
- every `TransitionJournalRecord.Event` emitted by Roadmap CLI has a catalog owner;
- every descriptor with a prompt contract names required inputs and outputs from `PromptContractRegistry`;
- every descriptor with artifact writes names lifecycle and state effects;
- `state-next-transition-index.md` is derivable from catalog JSON.

### Behavior Preservation

The catalog documents current behavior; it must not be used for runtime routing yet.

### Evaluation Gate

An engineer can answer for every supported current state:

- the next possible transition;
- required inputs and freshness checks;
- code owner;
- prompt, parser, or classifier;
- output paths;
- state and lifecycle effects;
- blocker path and recovery target.

## Phase 2: Ownership and Concern Boundary Map

### Goal

Make mixed concerns explicit before extracting or wrapping anything.

### Executable Outcome

The migration writer can generate an ownership map for every cataloged transition.

### Code Changes

Add:

- `RoadmapTransitionOwnership`
- `RoadmapOwnershipMap`
- `RoadmapOwnershipMapWriter`

For each transition, record the current owner or missing owner for:

- command admission;
- startup or resume routing;
- prompt contract admission;
- projection freshness;
- input snapshot;
- execution;
- observation capture;
- parser/classifier validation;
- decision acceptance;
- artifact mutation;
- artifact lifecycle movement;
- state persistence;
- journal persistence;
- decision ledger persistence;
- report output;
- blocker creation;
- recovery planning.

### Durable Artifacts

Generate:

- `.agents/migration/ownership-map.md`
- `.agents/migration/ownership-map.json`

### Mixed-Concern Findings To Capture

Initial findings should include:

- `RoadmapStateMachine` currently owns orchestration, prompt execution, parser interpretation, artifact writes, lifecycle writes, decision ledger appends, state persistence, journal appends, and many blocker branches.
- `RoadmapResumePlanner` owns a mixture of state routing, projection freshness, artifact readiness, selection freshness, and legacy-state reporting.
- `PromptContractRegistry` is the strongest existing admission table but does not own semantic authority.
- `TransitionInputSnapshot` is a good compatibility evidence source but is not semantic evidence until Track A binds it.
- `DecisionLedgerEntry` persists selected results but does not yet separate suggestion, validation, authority source, and effect consumers.
- `ArtifactPromotionService` preserves candidate output and lifecycle state but currently promotes directly to target paths after local validation.

### Tests

Add `RoadmapOwnershipMapTests`:

- every transition descriptor has ownership entries for all required concern slots;
- entries may be `missing` only when the finding includes a migration action;
- ownership map references real classes or explicit migration notes;
- Markdown and JSON render the same transition ids.

### Behavior Preservation

No runtime routing changes. No ownership extraction yet.

### Evaluation Gate

HITL can inspect `SelectNextEpic`, `CreateNewEpic`, `GenerateMilestoneDeepDivesForEpic`, and `UnblockReview` and distinguish:

- routing owner;
- evidence owner;
- parser or classifier owner;
- decision owner;
- artifact mutation owner;
- lifecycle owner;
- state owner;
- recovery owner.

## Phase 3: SelectNextEpic Migration Seam

### Goal

Define the smallest seam where Track A semantic behavior can wrap the current `SelectNextEpic` path.

### Executable Outcome

The migration artifacts describe exactly where the semantic wrapper begins and ends, which legacy behavior remains inside, what compatibility writes must be preserved, and what equivalence evidence is required.

### Code Changes

Add:

- `RoadmapMigrationSeamDescriptor`
- `SelectNextEpicMigrationSeam`
- `RoadmapMigrationStatus`
- `RoadmapMigrationStatusStore`

Do not route production execution through the seam yet.

### Durable Artifacts

Generate:

- `.agents/migration/seams/select-next-epic.md`
- `.agents/migration/seams/select-next-epic.json`
- `.agents/migration/status.json`

### Seam Definition

The `SelectNextEpic` seam starts after:

```text
RoadmapResumePlanner has selected RoadmapResumeAction.SelectNextStrategicInitiative
```

or after:

```text
RoadmapResumePlanner has selected RoadmapResumeAction.ContinueSelectionDecision
```

The semantic wrapper must own:

- `RepositoryWork` subject identity;
- subject-bound selection intent;
- source capture for roadmap completion context, roadmap source files, projection, prompt context, secondary input, retired epic state, and current persisted state summary;
- protocol admission for the selection operation;
- interaction envelope;
- observation capture of raw prompt output or existing selection content;
- validation boundary for parser output;
- evidence binding for accepted selection observation;
- accepted decision record;
- authorized compatibility writes.

The legacy code inside the seam initially remains responsible for:

- `ProjectionCache.EnsureAsync("SelectNextEpic")`;
- `RoadmapPromptContextBuilder.BuildSelectionContextAsync`;
- `RoadmapPromptRunner.RunRuntimePromptAsync`;
- writing `.agents/selection.md`;
- writing selection evidence under `.agents/evidence/selection`;
- recording selection provenance;
- lifecycle upsert for `.agents/selection.md`;
- `SelectionParser.Parse`;
- legacy `DecisionLedgerEntry`;
- existing `SaveStateAsync` behavior where selection pause states are reached.

### Compatibility Contract

The wrapper must preserve:

- selection artifact content;
- selection evidence path and content;
- selection provenance freshness result;
- lifecycle state for `.agents/selection.md`;
- decision ledger decision text, confidence, and rationale excerpt;
- transition journal event sequence, normalized for timestamp and correlation id;
- `RoadmapStateDocument` current state, transition summary, transition intent, blockers, and next valid transitions;
- downstream route selected by `ContinueAfterSelectionAsync`;
- console outcome.

### Tests

Add `SelectNextEpicMigrationSeamTests`:

- seam descriptor references an existing prompt contract;
- seam descriptor references only real artifact paths;
- every compatibility write has a legacy owner and planned semantic owner;
- every retirement condition is testable;
- status JSON reports `SelectNextEpic` as `seamed`.

### Behavior Preservation

No production execution path changes. The seam is documentation plus machine-readable migration state.

### Evaluation Gate

HITL can see:

- where semantic behavior will begin;
- what legacy behavior stays inside the wrapper;
- which writes must remain compatible;
- which records will prove equivalence;
- when the seam can be retired.

## Phase 4: First Semantic Wrapper

### Goal

Run `SelectNextEpic` through the Track A semantic model while preserving legacy behavior.

### Dependencies

This phase requires Track A to provide at least:

- `RepositoryWork` subject identity;
- subject-bound intent capture;
- source capture;
- protocol admission;
- interaction envelope;
- observation capture;
- evidence binding;
- decision record or report-only classification;
- authorized effect record.

If Track A does not yet provide those contracts, stop at Phase 3.

### Executable Outcome

A non-default execution path can run `SelectNextEpic` through a semantic wrapper and produce both:

- the same legacy-compatible artifacts and state as the current path;
- semantic interaction, evidence, decision, and effect records for `RepositoryWork`.

### Code Changes

Extract the legacy path just enough to call it from a wrapper:

- `ISelectNextEpicLegacyPath`
- `SelectNextEpicLegacyPath`
- `SelectNextEpicSemanticWrapper`
- `SelectNextEpicCompatibilityRecorder`
- `SelectNextEpicEquivalenceComparer`

Keep extraction mechanical. Do not rewrite selection logic while extracting it.

Expected extraction target:

```text
RoadmapStateMachine.SelectNextInitiativeAsync
  -> SelectNextEpicLegacyPath.RunAsync
```

The legacy path should return a result object containing:

- `SelectionDecision`;
- output artifact path;
- evidence path;
- projection path;
- transition input snapshot;
- journal correlation id;
- lifecycle effect summary;
- decision ledger id;
- state effect summary, when applicable.

### Durable Artifacts

Generate semantic records in the Track A artifact area and compatibility evidence under:

- `.agents/migration/compatibility/select-next-epic-equivalence.json`

The compatibility record must include:

- normalized legacy result;
- normalized wrapper result;
- ignored fields such as timestamp and correlation id;
- comparison status;
- mismatches, if any;
- test or run identity.

### Wrapper Flow

```text
load RepositoryWork subject
  -> capture SelectNextEpic intent
  -> capture legacy sources
  -> admit SelectNextEpic protocol
  -> start semantic interaction
  -> call SelectNextEpicLegacyPath
  -> capture raw selection output as observation
  -> validate parser result and source freshness
  -> bind evidence
  -> persist semantic decision
  -> authorize compatibility writes already performed by legacy path
  -> persist semantic effect record
  -> render compatibility equivalence record
```

### Tests

Add `SelectNextEpicSemanticWrapperTests`:

- valid selection path produces the same `SelectionDecision` as legacy;
- strategic investigation path preserves state and decision ledger behavior;
- roadmap revision path preserves state and decision ledger behavior;
- no suitable initiative path preserves state and decision ledger behavior;
- stale selection regeneration behavior remains unchanged;
- missing required input blocks at the same boundary as legacy unless semantic admission blocks earlier with equivalent report-only outcome;
- raw prompt output is observation until validation succeeds;
- parser output is not persisted as semantic decision until evidence binding succeeds.

Add `SelectNextEpicEquivalenceTests`:

- run legacy and wrapped paths in isolated `TempRepo` instances with the same scripted runtime;
- compare normalized `.agents/selection.md`;
- compare selection evidence content;
- compare selection provenance manifest;
- compare lifecycle entries;
- compare decision ledger entries;
- compare transition journal event names, prompt, projection, inputs, outputs, results, and parser decisions;
- compare persisted state;
- ignore timestamps and correlation ids only.

### Behavior Preservation

The semantic wrapper must remain non-default until equivalence tests pass for all supported `SelectNextEpic` branches.

### Evaluation Gate

HITL can inspect one wrapped selection run and see:

- subject identity;
- intent;
- sources;
- protocol admission;
- interaction;
- observation;
- validation;
- evidence;
- accepted decision;
- authorized compatibility effects;
- matching legacy artifacts and state.

## Phase 5: Semantic-Primary SelectNextEpic and Legacy Retirement

### Goal

Make the semantic wrapper primary for `SelectNextEpic`, keep a rollback path until real compatibility is proven, then retire the legacy seam.

### Executable Outcome

`RoadmapStateMachine` uses the semantic wrapper for `SelectNextEpic` by default, with a controlled rollback option for the selected path only.

### Code Changes

After Phase 4 equivalence:

- route `RoadmapResumeAction.SelectNextStrategicInitiative` to `SelectNextEpicSemanticWrapper`;
- route `RoadmapResumeAction.ContinueSelectionDecision` through wrapper validation when an existing selection is reused;
- keep `SelectNextEpicLegacyPath` callable only as rollback and test oracle;
- update migration status from `wrapped` to `semantic-primary`;
- add a retirement migration that removes rollback after acceptance.

### Durable Artifacts

Update:

- `.agents/migration/status.json`
- `.agents/migration/transition-catalog.json`
- `.agents/migration/ownership-map.json`
- `.agents/migration/compatibility/select-next-epic-equivalence.json`

When retired, write:

- `.agents/migration/seams/select-next-epic-retired.md`

### Tests

Extend existing tests so default Roadmap CLI behavior goes through the semantic wrapper:

- `RoadmapStateMachineSelectionTests`
- `RoadmapStateMachineResumeTests`
- `TransitionJournalTests`
- `SelectionProvenanceTests`
- `DecisionLedgerTests`
- `RoadmapStateStoreTests`

Add retirement tests:

- no runtime route calls the retired compatibility seam;
- persisted states from legacy selection runs can still resume;
- migration status marks `SelectNextEpic` as `retired`;
- catalog still preserves historical lineage for the retired path.

### Behavior Preservation

Do not retire `SelectNextEpicLegacyPath` until:

- wrapper is default;
- rollback has been exercised in tests;
- existing persisted state fixtures resume correctly;
- all downstream branches from `ContinueAfterSelectionAsync` still behave the same;
- HITL can trace semantic lineage and legacy compatibility effects.

### Evaluation Gate

HITL can identify `SelectNextEpic` as:

```text
legacy recovered
  -> cataloged
  -> ownership mapped
  -> seamed
  -> wrapped
  -> semantic-primary
  -> legacy retired
```

and can inspect the evidence for each status transition.

## Phase 6: Repeatable Per-Transition Migration

### Goal

Apply the proven pattern to the remaining transition families without creating ambiguous shared ownership.

### Migration Order

After `SelectNextEpic`, migrate in this order:

1. `CreateRoadmapCompletionContext`
2. Active epic promotion paths: `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`
3. `SplitEpic`
4. `GenerateMilestoneDeepDivesForEpic`
5. Completion certification: `EvaluateEpicCompletionAndDrift`, completion route persistence, roadmap completion context update
6. Unblock review and recovery paths
7. Invariant failure and runtime failure persistence
8. Cancellation recovery
9. Legacy execution preparation states

This order keeps the most frequently used planning path first, then artifact promotion, then bundle/materialization flows, then certification and recovery.

### Per-Transition Checklist

For each transition:

- recover actual current behavior in the catalog;
- update ownership map;
- define seam;
- choose semantic protocol and subject intent;
- extract the legacy path only enough to wrap it;
- add equivalence tests;
- make wrapper non-default;
- prove equivalence;
- make wrapper primary;
- preserve rollback;
- retire rollback and compatibility artifacts.

### Acceptance Fields

Each transition status update must include:

- transition id;
- previous status;
- new status;
- evidence artifact;
- equivalence summary;
- rollback availability;
- retirement condition;
- reviewer or test gate.

### Evaluation Gate

At any point, HITL can query the migration status and distinguish:

- legacy only;
- recovered;
- cataloged;
- ownership mapped;
- seamed;
- wrapped;
- semantic primary;
- retired.

No transition may skip from cataloged to semantic-primary.

## Cross-Phase Verification

Run the focused test suite after every implementation phase:

```text
dotnet test tests/LoopRelay.Roadmap.Cli.Tests/LoopRelay.Roadmap.Cli.Tests.csproj
```

When shared primitives are touched, also run:

```text
dotnet test LoopRelay.slnx
```

Minimum focused tests by concern:

| Concern | Tests |
| --- | --- |
| Startup and resume routing | `RoadmapStartupPlannerTests`, `RoadmapResumePlannerTests`, `RoadmapStateMachineResumeTests` |
| State persistence | `RoadmapStateStoreTests`, `RoadmapFailurePersistenceTests` |
| Journal and input lineage | `TransitionJournalTests`, `TransitionInputResolverTests` |
| Selection path | `RoadmapStateMachineSelectionTests`, `SelectionProvenanceTests`, `DecisionLedgerTests` |
| Artifact promotion | `ArtifactPromotionServiceTests`, `RoadmapStateMachinePromotionTests`, `EpicArtifactPromotionTests` |
| Split path | `RoadmapStateMachineSplitTests`, `SplitEpicBundleInterpreterTests`, `SplitFamilyStoreTests` |
| Completion path | `CompletionCertificationPolicyTests`, `CompletionCertificationServiceTests`, `RoadmapStateMachineUnblockTests` |
| Projection freshness | `ProjectionCacheTests`, `ProjectionManifestTests`, `ProjectionProvenanceTests`, `ProjectionValidatorTests` |

## Human Navigability Gate

No migration milestone is complete unless an engineer can answer from migration artifacts:

- What state or command admits the transition?
- What guard blocks it?
- What sources and artifacts are required?
- What prompt, parser, classifier, or policy produces observation?
- Which observation becomes evidence?
- Which decision is accepted, if any?
- Which artifact, lifecycle, journal, state, decision ledger, report, or blocker effect occurs?
- Which owner is responsible for each effect?
- How can the transition recover?
- Whether it is legacy, wrapped, semantic-primary, or retired.

## Implementation Acceptance Baseline

Track B is sufficient when the system can demonstrate this path for at least `SelectNextEpic`:

```text
recover current transition behavior
  -> publish explicit transition catalog
  -> publish ownership map
  -> define migration seam
  -> wrap through RepositoryWork semantic interaction
  -> prove behavior equivalence
  -> make semantic wrapper primary
  -> retire the legacy path and compatibility seam
```

The first pass may cover only `SelectNextEpic`. The important property is that the migration is inspectable, reversible while incomplete, behavior-preserving, and grounded in both current implementation evidence and the constitutional semantic target.

## Explicit Non-Goals

- Do not rewrite the whole Roadmap CLI state machine before one path is wrapped.
- Do not move code only to match the catalog.
- Do not make migration catalogs runtime authority.
- Do not treat prompt contracts as semantic protocols until Track A admission exists.
- Do not treat `TransitionInputSnapshot` as semantic evidence until Track A evidence binding exists.
- Do not let compatibility writes become the long-term semantic persistence model.
- Do not retire legacy state values without explicit persisted-state migration.
- Do not collapse distinct transitions into broad categories that hide blocker, parser, lifecycle, or state differences.
