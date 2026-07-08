# BootstrapRoadmapCompletionContext Transition Extraction Audit

## Audited Transition

Exactly one transition is audited here: `BootstrapRoadmapCompletionContext`, implemented by `BootstrapRoadmapCompletionContextAsync` in `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`.

Runtime prompt: `CreateRoadmapCompletionContext`.

Scope: `RoadmapState.CoreReady` to `RoadmapState.RoadmapCompletionContextReady` when `.agents/core/roadmap-completion-context.md` is missing.

Entry: `RunFromCoreReadyAsync` checks `artifacts.GetStatusAsync(RoadmapArtifactPaths.RoadmapCompletionContext)` and calls `BootstrapRoadmapCompletionContextAsync(projectContext, cancellationToken)` only when the artifact is not `Present`.

Exit: the transition has produced `.agents/core/roadmap-completion-context.md`, marked that artifact `Ready`, persisted state/journal records for `CreateRoadmapCompletionContext`, and returned to `RunFromCoreReadyAsync`. The caller then continues into `SelectNextEpic`; that downstream transition is outside this audit.

Included:

- missing completion-context guard
- prompt contract lookup
- projection cache resolution, generation, validation, manifest update, and projection-blocked failure artifact
- completed-epic archive evidence loading and rendering
- transition input snapshot and hashing
- prompt execution
- transition state and journal persistence
- roadmap completion context artifact write
- HITL request capture from the generated context
- lifecycle update for the generated context
- runtime failure persistence

Excluded:

- Project Context preflight before the transition is entered
- prompt contract snapshot emission before resume planning
- `SelectNextEpic` and all later selection/epic transitions
- redesigning projection, prompt, archive, lifecycle, journal, state, or HITL concepts

## Deliverable 1: Transition Narrative

Current State

The roadmap workflow is at `CoreReady` or resuming through `BootstrapRoadmapCompletionContext`, and the core roadmap completion context artifact is missing or empty. Project Context preflight has already loaded the canonical Project Context files and produced a `ProjectContext`.

Goal

Create the initial durable roadmap completion context from the stable Project Context projection plus any archived completed epic evidence, so later roadmap planning can select the next initiative against the project's current strategic state.

Major Steps

1. Confirm the completion context is absent.
2. Announce the bootstrap phase.
3. Resolve the `CreateRoadmapCompletionContext` contract.
4. Ensure the matching projection exists, is valid, and is fresh enough to use.
5. Build the runtime prompt context from the projection content.
6. Load archived completed epics and render them as secondary prompt input.
7. Capture transition inputs and hashes.
8. Persist transition-start state and journal records.
9. Run the `CreateRoadmapCompletionContext` runtime prompt.
10. Persist transition-completed state and journal records.
11. Write the generated roadmap completion context artifact.
12. Capture HITL request markers from that artifact if capture is configured.
13. Mark the artifact lifecycle `Ready`.
14. Return to the caller, which proceeds to selection.

Completion

The transition is complete when `.agents/core/roadmap-completion-context.md` contains the runtime prompt output, `.agents/artifacts/lifecycle.json` marks it `Ready`, `.agents/state.json` records `RoadmapCompletionContextReady` / `Completed` for `CreateRoadmapCompletionContext`, and `.agents/journal/transitions.jsonl` has matching started/completed records.

## Deliverable 2: Current Execution Trace

This trace starts at the run path that can enter the bootstrap transition and stops before `SelectNextEpic`.

1. `RunAsync` loads persisted state with `stateStore.LoadAsync`.
2. `RunAsync` asks `startupPlanner.Plan(persistedState)` for the startup plan.
3. `RunAsync` prints `Startup plan: ...`.
4. If startup does not require preflight, `RunAsync` returns without entering this transition.
5. `RunAsync` prints phase `Project Context preflight`.
6. `projectContextLoader.LoadAsync` validates the required Project Context files, rejects missing or unexpected numbered files, concatenates them, and computes the Project Context hash.
7. `contractRegistry.EmitSnapshotAsync` writes `.agents/contracts/prompt-contracts.md`.
8. `resumePlanner.PlanAsync` captures an artifact snapshot.
9. If there is no persisted state, `resumePlanner.PlanAsync` returns `ContinueFromCoreReady` with `ShouldPersistCoreReady = true`.
10. If persisted state is `CoreReady` or `BootstrapRoadmapCompletionContext`, `resumePlanner.PlanAsync` returns `ContinueFromCoreReady`.
11. `RunAsync` prints `Resume plan: ...`.
12. `ExecuteResumePlanAsync` enters the `ContinueFromCoreReady` branch.
13. If `ShouldPersistCoreReady` is true, `ExecuteResumePlanAsync` saves a completed `CoreReady` preflight state.
14. `ExecuteResumePlanAsync` calls `RunFromCoreReadyAsync`.
15. `RunFromCoreReadyAsync` calls `artifacts.GetStatusAsync(.agents/core/roadmap-completion-context.md)`.
16. If the artifact is `Present`, bootstrap is skipped and the caller proceeds to selection.
17. If the artifact is `Missing` or `Empty`, `RunFromCoreReadyAsync` calls `BootstrapRoadmapCompletionContextAsync`.
18. `BootstrapRoadmapCompletionContextAsync` sets `runtimePrompt = "CreateRoadmapCompletionContext"`.
19. It prints phase `Bootstrap roadmap completion context`.
20. It loads the prompt contract with `contractRegistry.Get(runtimePrompt)`.
21. It calls `projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken)`.
22. `ProjectionCache` resolves the projection definition from `ProjectionRegistry`.
23. `ProjectionCache` builds current projection provenance from the projection definition and Project Context.
24. `ProjectionCache` reads `.agents/projections/roadmap-completion.md`.
25. If the projection content is missing or blank, `ProjectionCache` runs `ProjectionForCreateRoadmapCompletionContext`.
26. If the projection prompt returns blank output, `ProjectionCache` throws `RoadmapStepException`.
27. `ProjectionCache` validates the projection title, required sections, intended consumer, and forbidden runtime-state headings.
28. `ProjectionCache` hashes the projection content.
29. `ProjectionCache` loads the projection manifest.
30. `ProjectionCache` finds the prior manifest entry, if any.
31. `ProjectionCache` evaluates freshness, or treats newly generated projection content as fresh.
32. `ProjectionCache` upserts the manifest entry.
33. If validation failed, `ProjectionCache` writes numbered `projection-blocked` evidence and throws.
34. If the projection was generated, `ProjectionCache` writes `.agents/projections/roadmap-completion.md`.
35. If projection freshness is stale and policy is `Block`, `ProjectionCache` writes numbered `projection-blocked` evidence and throws.
36. `ProjectionCache` returns the projection definition and content.
37. `BootstrapRoadmapCompletionContextAsync` builds the runtime context string: `# Roadmap Completion Bootstrap`, then `## Projection Content`, then the projection content.
38. It constructs `CompletedEpicEvidenceLoader`.
39. `CompletedEpicEvidenceLoader.RenderAsync` lists `.agents/archive/epics/*.md`.
40. It reads each archived epic in ordinal path order.
41. It skips any listed archived epic that no longer reads.
42. It extracts a title from the first `# ` heading when present.
43. It extracts an epic id from a markdown field table row where `Field` is `Epic ID`.
44. It extracts known evidence sections such as `Strategic Purpose`, `Outcome`, `Completion Evidence`, `Implementation Evidence`, `Drift`, and `Follow-Up`.
45. It falls back to the normalized full content when no known evidence section is found.
46. It limits each archived epic's rendered content to `MaxRenderedContentPerEpic`.
47. It labels evidence quality `Strong`, `Weak`, or `Unclear`.
48. If no completed epics exist, it renders the fixed "No completed epic markdown files..." message.
49. If completed epics exist, it renders a `# Completed Epic Evidence` document with source glob, per-epic metadata, and selected content.
50. It limits total rendered completed-epic evidence to `MaxTotalRenderedCharacters`.
51. `BootstrapRoadmapCompletionContextAsync` calls `RunPromptTransitionAsync` from `CoreReady` to `RoadmapCompletionContextReady`.
52. `RunPromptTransitionAsync` calls `RunPromptTransitionWithCompletionAsync`.
53. `RunPromptTransitionWithCompletionAsync` calls `inputResolver.ResolveAsync`.
54. `TransitionInputResolver` adds the projection path as a required `Projection` input.
55. `TransitionInputResolver` adds each completed epic archive file as an optional `CompletedEpic` input.
56. `TransitionInputAccumulator` reads all input paths in ordinal order.
57. `TransitionInputAccumulator` throws if a required input is missing.
58. `TransitionInputAccumulator` records missing optional inputs as `MissingOptional`.
59. `TransitionInputAccumulator` hashes present input contents.
60. `TransitionInputResolver` computes projection hash, prompt context hash, secondary input hash, and snapshot hash.
61. `RunPromptTransitionWithCompletionAsync` creates a correlation id.
62. It records `started = DateTimeOffset.UtcNow`.
63. It starts a stopwatch.
64. It calls `SaveStateAsync` with current state `RoadmapCompletionContextReady`, status `Started`, from `CoreReady`, to `RoadmapCompletionContextReady`, prompt `CreateRoadmapCompletionContext`, projection path `.agents/projections/roadmap-completion.md`, output `.agents/core/roadmap-completion-context.md`, and decision `Pending`.
65. `SaveStateAsync` loads existing state.
66. `SaveStateAsync` loads the projection manifest.
67. `SaveStateAsync` computes active artifact rows for roadmap completion context, selection, and active epic.
68. `SaveStateAsync` reads the last decision id.
69. `SaveStateAsync` keeps existing retired epics and blockers unless replaced.
70. `SaveStateAsync` counts split-family JSON files.
71. `SaveStateAsync` computes projection manifest counts.
72. `SaveStateAsync` writes `.agents/state.json`.
73. `RunPromptTransitionWithCompletionAsync` appends `TransitionStarted` to `.agents/journal/transitions.jsonl`.
74. It calls `promptRunner.RunRuntimePromptAsync`.
75. `RoadmapPromptRunner` renders the `CreateRoadmapCompletionContext` runtime prompt from the context and completed-epic evidence.
76. `RoadmapPromptRunner` appends the implementation-first prompt policy.
77. `RoadmapPromptRunner` runs a read-only planning agent one-shot turn.
78. If the agent turn state is not `Completed`, `RoadmapPromptRunner` throws a `RoadmapStepException` with diagnostics.
79. `RoadmapPromptRunner` echoes output if the console renderer did not stream it.
80. `RoadmapPromptRunner` returns the runtime output.
81. `RunPromptTransitionWithCompletionAsync` stops the stopwatch.
82. It records `completed = DateTimeOffset.UtcNow`.
83. It appends `TransitionCompleted` to the transition journal.
84. It calls `SaveStateAsync` with current state `RoadmapCompletionContextReady`, status `Completed`, decision `Completed`, and the completed timestamp.
85. It returns `PromptTransitionCompletion`.
86. `RunPromptTransitionAsync` returns `completion.Output`.
87. `BootstrapRoadmapCompletionContextAsync` writes the output to `.agents/core/roadmap-completion-context.md`.
88. It calls `CaptureHitlRequestsAsync(.agents/core/roadmap-completion-context.md, output)`.
89. `CaptureHitlRequestsAsync` returns immediately when HITL capture is not configured or the output is blank.
90. If HITL capture is configured, it captures explicit non-implementation request markers from the generated artifact.
91. `BootstrapRoadmapCompletionContextAsync` calls `lifecycleStore.UpsertAsync(.agents/core/roadmap-completion-context.md, Ready)`.
92. `ArtifactLifecycleStore` loads the existing lifecycle document.
93. `ArtifactLifecycleStore` replaces any lifecycle entry for the context path.
94. `ArtifactLifecycleStore` writes `.agents/artifacts/lifecycle.json`.
95. `BootstrapRoadmapCompletionContextAsync` returns to `RunFromCoreReadyAsync`.
96. `RunFromCoreReadyAsync` calls `RunSelectionAndFollowingAsync`; this starts the separate `SelectNextEpic` transition and is outside this audit.

Failure trace for runtime prompt failure:

1. Steps 1 through 77 have occurred.
2. `RoadmapPromptRunner` throws a non-cancellation exception, or the agent runtime throws one.
3. `RunPromptTransitionWithCompletionAsync` catches it unless it is `OperationCanceledException`.
4. It stops the stopwatch.
5. It appends `TransitionFailed` to `.agents/journal/transitions.jsonl`.
6. It calls `SaveStateAsync` with current state `EvidenceBlocked`, status `Failed`, from `CoreReady`, to `RoadmapCompletionContextReady`, output `.agents/core/roadmap-completion-context.md`, decision `Failed`, blocker `Review the transition failure and rerun.`, transition intent `ResolveTransitionFailure`, and next transition `Resolve blocker and rerun`.
7. It throws `RoadmapStepException.AlreadyPersisted`.
8. `RunAsync` catches the already-persisted failure, prints the error, and returns `RoadmapOutcome.Failed`.

Failure trace for invalid or stale projection:

1. Projection cache has read or generated projection content.
2. Validation or freshness fails.
3. `ProjectionCache` writes numbered blocker evidence under `.agents/evidence/blockers/projection-blocked.NNNN.md`.
4. `ProjectionCache` throws `RoadmapStepException`.
5. No transition-start state has been written yet by `RunPromptTransitionWithCompletionAsync`.
6. `RunAsync` catches the exception as a normal roadmap step failure, prints an ephemeral blocker warning, prints the error, and returns `RoadmapOutcome.Failed`.

## Deliverable 3: Concern Inventory

| Step | Concern | Where concern becomes mixed |
|---|---|---|
| Load persisted state | persistence read | Startup/readiness routing is upstream of the transition. |
| Startup planning | routing | It decides whether any transition can run before bootstrap logic is visible. |
| Project Context preflight | validation, artifact read | Outside transition, but required before bootstrap can execute. |
| Prompt contract snapshot | artifact write, reporting/governance | Written before resume planning, not owned by the bootstrap method. |
| Resume planning | routing, validation, recovery | Determines `ContinueFromCoreReady`; not local to bootstrap. |
| Persist initial `CoreReady` | state mutation, persistence | Happens only for fresh initialization and is adjacent to bootstrap entry. |
| Completion-context status check | routing, artifact read | `RunFromCoreReadyAsync` decides skip vs enter. |
| Phase output | reporting | Mixed into transition method before business work. |
| Prompt contract lookup | validation/config lookup | Mixed into bootstrap method. |
| Projection cache ensure | artifact read/write, prompt execution, validation, manifest persistence, recovery | The helper can run a projection prompt, write projection artifacts, write blocked evidence, and throw. |
| Runtime context build | context construction | Simple string construction in bootstrap method. |
| Completed epic evidence load | artifact read, parsing, normalization | Hidden behind `CompletedEpicEvidenceLoader.RenderAsync`. |
| Completed epic evidence render | context construction, truncation, normalization | Hidden data shaping before prompt execution. |
| Transition input resolution | artifact read, hashing, validation | Hidden inside shared prompt transition runner. |
| Save started state | state mutation, persistence, summary aggregation | `SaveStateAsync` also loads manifests, active artifacts, decision id, split family counts, and existing blockers. |
| Journal start | journaling | Hidden inside shared runner. |
| Runtime prompt render/run | prompt execution, console streaming | `RoadmapPromptRunner` renders, applies policy, runs agent, validates agent state, and echoes output. |
| Journal completion | journaling | Hidden inside shared runner. |
| Save completed state | state mutation, persistence, summary aggregation | Same broad persistence helper as start state. |
| Roadmap context write | artifact write | Directly visible in bootstrap method. |
| HITL capture | parsing, optional side effect, ledger mutation | Hidden behind a null-guard helper. |
| Lifecycle upsert | lifecycle mutation, persistence | Directly visible but persistence details hidden. |
| Runtime failure handling | recovery, journaling, state mutation, persistence | Hidden inside shared runner. |
| Projection-blocked handling | validation, artifact write, recovery | Hidden inside `ProjectionCache`, before transition state starts. |

Concerns become most mixed in three places:

1. `ProjectionCache.EnsureAsync` combines projection lookup, prompt execution, validation, freshness, manifest writes, projection writes, blocker evidence, and exceptions.
2. `RunPromptTransitionWithCompletionAsync` combines input capture, state writes, journal writes, prompt execution, timing, and failure persistence.
3. `SaveStateAsync` combines state mutation with manifest summaries, artifact statuses, decision ledger summary, retired epics, split-family counts, transition intent, and next-transition calculation.

## Deliverable 4: Hidden Steps

The visible bootstrap method hides these concrete steps:

1. Resolve projection definition.
2. Build projection provenance.
3. Read existing projection artifact.
4. Generate projection if missing.
5. Validate projection shape.
6. Update projection manifest.
7. Block on invalid or stale projection.
8. Render bootstrap context.
9. Discover completed epic archive files.
10. Parse completed epic titles.
11. Parse completed epic ids.
12. Extract relevant completed epic evidence sections.
13. Assign evidence quality.
14. Truncate per-epic evidence.
15. Truncate total completed-epic evidence.
16. Capture transition input snapshot.
17. Hash projection input.
18. Hash completed epic archive inputs.
19. Hash rendered prompt context.
20. Hash secondary input.
21. Persist started transition state.
22. Append transition-start journal event.
23. Render runtime prompt.
24. Append prompt policy.
25. Stream or echo prompt output to console.
26. Persist completed transition state.
27. Append transition-completed journal event.
28. Persist failure state and journal on runtime failure.
29. Capture explicit HITL requests from generated output.
30. Upsert artifact lifecycle.

## Deliverable 5: Natural Step Boundaries

Step 1: Entry Guard

Purpose

Decide whether bootstrap is required.

Inputs

- `RoadmapArtifactPaths.RoadmapCompletionContext`
- artifact status from `RoadmapArtifacts.GetStatusAsync`

Outputs

- skip bootstrap when status is `Present`
- enter bootstrap when status is `Missing` or `Empty`

---

Step 2: Projection Readiness

Purpose

Provide a valid, fresh projection for `CreateRoadmapCompletionContext`.

Inputs

- `runtimePrompt = "CreateRoadmapCompletionContext"`
- `ProjectContext`
- prompt contract
- existing projection artifact, if any
- projection manifest

Outputs

- `ProjectionCacheResult`
- updated projection manifest
- generated `.agents/projections/roadmap-completion.md`, when missing
- projection-blocked evidence and exception on invalid/stale projection

---

Step 3: Runtime Input Assembly

Purpose

Build the two strings passed to the runtime prompt.

Inputs

- projection content
- completed epic archive files under `.agents/archive/epics/*.md`

Outputs

- bootstrap runtime context
- completed-epic evidence secondary input

---

Step 4: Transition Execution Envelope

Purpose

Run the prompt with durable transition bookkeeping.

Inputs

- from state `CoreReady`
- to state `RoadmapCompletionContextReady`
- runtime prompt name
- projection path
- runtime context
- completed-epic evidence
- output path `.agents/core/roadmap-completion-context.md`

Outputs

- transition input snapshot
- started state record
- started journal record
- runtime output
- completed state record
- completed journal record
- failure state/journal if prompt execution fails

---

Step 5: Artifact Materialization

Purpose

Make the prompt output the durable roadmap completion context.

Inputs

- runtime output

Outputs

- `.agents/core/roadmap-completion-context.md`

---

Step 6: Post-Write Side Effects

Purpose

Record non-content metadata and optional HITL requests for the generated artifact.

Inputs

- generated context path
- generated context content
- optional HITL request capture service

Outputs

- HITL capture side effects when configured
- lifecycle entry for `.agents/core/roadmap-completion-context.md` marked `Ready`

---

Step 7: Handoff

Purpose

Return control to the core-ready runner.

Inputs

- successful bootstrap completion

Outputs

- caller proceeds to `RunSelectionAndFollowingAsync`

## Deliverable 6: Mixed-Concern Analysis

Step 1: Entry Guard

Reads artifact state and decides routing. This is small, but it means the transition's actual entry condition is not in the bootstrap method itself.

Step 2: Projection Readiness

Reads artifacts, may execute a projection prompt, validates projection structure, updates a manifest, writes the projection artifact, writes blocked evidence, and throws. This makes projection readiness look like a cache call even though it can perform durable workflow work and produce failure artifacts.

Step 3: Runtime Input Assembly

Builds prompt input while also parsing and normalizing archived epic artifacts. The transition reader has to leave the state machine to understand what completed epic history the prompt actually receives.

Step 4: Transition Execution Envelope

Captures input hashes, mutates state, appends journal records, runs the prompt, measures elapsed time, persists failure state, and constructs the return object. The prompt call itself is buried between durable side effects, so behavior cannot be understood by reading the bootstrap method alone.

Step 5: Artifact Materialization

Writes the generated output after the shared runner has already persisted a completed transition state. This ordering matters: state can say the transition completed before the output artifact write and lifecycle write happen.

Step 6: Post-Write Side Effects

Captures HITL markers and updates lifecycle. These are unrelated to generating the content but externally observable, so a future extraction must keep them visible in order.

Step 7: Handoff

The caller immediately starts selection after bootstrap. This makes the run path read like one continuous flow, but the bootstrap transition should end before `SelectNextEpic` begins.

## Deliverable 7: Data Flow

Persisted State

- Origin: `.agents/state.json`, read by `stateStore.LoadAsync`.
- Consumed by: startup planner, resume planner, optional initial `CoreReady` persistence, `SaveStateAsync`.
- Output: `.agents/state.json` records `RoadmapCompletionContextReady` during started/completed bootstrap, or `EvidenceBlocked` on runtime failure.

Project Context

- Origin: `.agents/ctx/01-purpose.md` through `.agents/ctx/08-vocabulary.md`.
- Produced by: `ProjectContextLoader.LoadAsync`.
- Consumed by: `ProjectionCache.EnsureAsync`, projection prompt rendering, runtime prompt rendering.

Prompt Contract

- Origin: `PromptContractRegistry`, contract row for `CreateRoadmapCompletionContext`.
- Consumed by: projection cache for stale projection policy.
- Observable output: `.agents/contracts/prompt-contracts.md` is written before this transition can run.

Projection Definition

- Origin: `ProjectionRegistry`.
- Value: runtime prompt `CreateRoadmapCompletionContext`, projection prompt `ProjectionForCreateRoadmapCompletionContext`, projection path `.agents/projections/roadmap-completion.md`.
- Consumed by: `ProjectionCache`, `RoadmapPromptCatalog`, transition input resolver, journal records, state records.

Projection Content

- Origin: existing `.agents/projections/roadmap-completion.md`, or output from `ProjectionForCreateRoadmapCompletionContext`.
- Consumed by: projection validator, manifest entry, runtime context string, transition input snapshot.
- Output: may be written to `.agents/projections/roadmap-completion.md` when generated.

Completed Epic Archive Files

- Origin: `.agents/archive/epics/*.md`.
- Consumed by: `CompletedEpicEvidenceLoader` for prompt secondary input and `TransitionInputResolver` for optional input hashes.
- Output: rendered completed-epic evidence string.

Rendered Runtime Context

- Origin: bootstrap method combines a fixed heading with projection content.
- Consumed by: transition input snapshot hash and `RoadmapPromptRunner.RunRuntimePromptAsync`.

Secondary Input

- Origin: `CompletedEpicEvidenceLoader.RenderAsync`.
- Consumed by: transition input snapshot hash and `CreateRoadmapCompletionContext` prompt rendering.

Transition Input Snapshot

- Origin: `TransitionInputResolver`.
- Contains: projection identity, artifact inputs, prompt context hash, secondary input hash, snapshot hash.
- Consumed by: transition journal records and failure/success state context.

Runtime Prompt

- Origin: `RoadmapPromptCatalog.RenderRuntime("CreateRoadmapCompletionContext", context, completedEpicEvidence)` plus implementation-first policy.
- Consumed by: read-only planning agent runtime.

Raw Output

- Origin: agent runtime completed output.
- Consumed by: `RunPromptTransitionWithCompletionAsync`, `artifacts.WriteAsync`, `CaptureHitlRequestsAsync`.
- Output: `.agents/core/roadmap-completion-context.md`.

Lifecycle Entry

- Origin: bootstrap method calls `lifecycleStore.UpsertAsync`.
- Consumed by: resume planning and state summary via `ActiveArtifactRowsAsync`.
- Output: `.agents/artifacts/lifecycle.json`.

Transition Journal

- Origin: `RunPromptTransitionWithCompletionAsync`.
- Output: `.agents/journal/transitions.jsonl`.
- Events: `TransitionStarted`, `TransitionCompleted`, or `TransitionFailed`.

Failure Blocker

- Origin: runtime prompt failure inside shared runner, or projection invalid/stale inside projection cache.
- Output: runtime failure persists `EvidenceBlocked` state; projection failure writes numbered projection-blocked evidence and returns a failed run without transition-start state for this prompt.

## Deliverable 8: Human Navigation Audit

Minimum files an engineer must read to understand this transition today:

1. `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`
2. `src/LoopRelay.Roadmap.Cli/RoadmapStartupPlanner.cs`
3. `src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs`
4. `src/LoopRelay.Roadmap.Cli/RoadmapArtifactPaths.cs`
5. `src/LoopRelay.Roadmap.Cli/PromptContractRegistry.cs`
6. `src/LoopRelay.Roadmap.Cli/ProjectionRegistry.cs`
7. `src/LoopRelay.Roadmap.Cli/ProjectionCache.cs`
8. `src/LoopRelay.Roadmap.Cli/ProjectionValidator.cs`
9. `src/LoopRelay.Roadmap.Cli/CompletedEpicEvidenceLoader.cs`
10. `src/LoopRelay.Roadmap.Cli/TransitionInputs.cs`
11. `src/LoopRelay.Roadmap.Cli/RoadmapPromptRunner.cs`
12. `src/LoopRelay.Roadmap.Cli/RoadmapPromptCatalog.cs`
13. `src/LoopRelay.Roadmap.Cli/RoadmapStateStore.cs`
14. `src/LoopRelay.Roadmap.Cli/TransitionJournalStore.cs`
15. `src/LoopRelay.Roadmap.Cli/ArtifactLifecycleStore.cs`
16. `src/LoopRelay.Core/Prompts/Planning/CreateRoadmapCompletionContext.prompt`
17. `src/LoopRelay.Core/Prompts/Projections/ProjectionForCreateRoadmapCompletionContext.prompt`

Helper methods and services on the minimum path:

- `RunAsync`
- `ExecuteResumePlanAsync`
- `RunFromCoreReadyAsync`
- `BootstrapRoadmapCompletionContextAsync`
- `ProjectionCache.EnsureAsync`
- `CompletedEpicEvidenceLoader.RenderAsync`
- `RunPromptTransitionAsync`
- `RunPromptTransitionWithCompletionAsync`
- `TransitionInputResolver.ResolveAsync`
- `RoadmapPromptRunner.RunRuntimePromptAsync`
- `SaveStateAsync`
- `ActiveArtifactRowsAsync`
- `CaptureHitlRequestsAsync`
- `ArtifactLifecycleStore.UpsertAsync`

Models and persistence objects on the minimum path:

- `RoadmapState`
- `RoadmapStateDocument`
- `RoadmapTransitionSummary`
- `RoadmapTransitionIntent`
- `PromptContract`
- `ProjectionDefinition`
- `ProjectionManifest`
- `ProjectionManifestEntry`
- `ProjectionCacheResult`
- `CompletedEpicEvidence`
- `TransitionInputRequest`
- `TransitionInputSnapshot`
- `TransitionJournalRecord`
- `ArtifactLifecycleEntry`

Tests that confirm observable behavior:

- `Missing_completion_context_triggers_bootstrap_before_selection`
- `Missing_completion_context_bootstrap_passes_archived_epic_evidence`
- `Existing_completion_context_skips_bootstrap`
- `Prompt_transition_failures_are_owned_by_the_transition_layer`
- `Create_completion_context_resolves_projection_as_single_artifact_input`
- `Create_completion_context_resolves_archived_epics_as_completed_epic_inputs`
- `Create_completion_context_declares_completed_epic_archive_glob`
- transition journal test that completed epic archives appear in input hashes

## Deliverable 9: Extraction Boundary

Smallest extraction boundary:

`BootstrapRoadmapCompletionContextTransition.ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)`

The handler should begin after the caller has decided the roadmap completion context is missing. It should end after the context artifact is written, HITL capture has run, and lifecycle is marked `Ready`.

The handler should own:

- `CreateRoadmapCompletionContext` prompt contract lookup
- projection readiness for this runtime prompt
- bootstrap context construction
- completed-epic evidence rendering
- transition execution envelope for `CoreReady -> RoadmapCompletionContextReady`
- context artifact materialization
- HITL capture for the generated context
- lifecycle ready mark

The handler should not own:

- startup planning
- Project Context preflight
- prompt contract snapshot emission
- deciding whether a present completion context should skip bootstrap
- selection after bootstrap
- general state store implementation
- general projection cache implementation
- general prompt runner implementation
- general transition journal implementation

Natural seam:

Replace this call:

```text
await BootstrapRoadmapCompletionContextAsync(projectContext, cancellationToken)
```

with:

```text
await bootstrapRoadmapCompletionContextTransition.ExecuteAsync(projectContext, cancellationToken)
```

The caller still decides whether the artifact is missing and still proceeds to `RunSelectionAndFollowingAsync`.

## Deliverable 10: Required Inputs

Required

- `ProjectContext`
- cancellation token
- runtime prompt name `CreateRoadmapCompletionContext`
- from state `CoreReady`
- to state `RoadmapCompletionContextReady`
- output path `.agents/core/roadmap-completion-context.md`
- projection path `.agents/projections/roadmap-completion.md`
- `PromptContractRegistry`
- `ProjectionCache`
- `CompletedEpicEvidenceLoader` capability or `RoadmapArtifacts` access needed to construct it
- shared prompt-transition runner capability, or the underlying `TransitionInputResolver`, `RoadmapPromptRunner`, `RoadmapStateStore`, `ProjectionManifestStore`, `DecisionLedgerStore`, `TransitionJournalStore`, and artifact-summary access currently used by `SaveStateAsync`
- `RoadmapArtifacts` for final context write
- `ArtifactLifecycleStore`

Optional

- `ExplicitHitlNonImplementationRequestCaptureService`, because `CaptureHitlRequestsAsync` is a no-op when it is absent.
- Existing completed epic archive files, because the contract declares them optional and the prompt receives a "no completed epic markdown files" message when absent.

Incidental

- Startup planner.
- Resume planner.
- Project Context loader.
- Prompt contract snapshot emission.
- Selection provenance.
- Decision ledger append behavior, except the last decision id read inside current `SaveStateAsync`.
- Epic promotion services.
- Bundle, split, execution preparation, completion certification, and invariant services.
- Completion archive services.
- Console beyond the phase message and prompt streaming already owned by the prompt runner.

## Deliverable 11: Required Outputs

Externally observable outputs on success:

- console phase `Bootstrap roadmap completion context`
- optional projection prompt console output when projection is generated
- runtime prompt console streaming or echoed output
- `.agents/projections/manifest.json` updated for `CreateRoadmapCompletionContext`
- `.agents/projections/roadmap-completion.md` written if the projection was generated
- `.agents/journal/transitions.jsonl` appended with `TransitionStarted`
- `.agents/state.json` written with `RoadmapCompletionContextReady` / `Started`
- `.agents/journal/transitions.jsonl` appended with `TransitionCompleted`
- `.agents/state.json` written with `RoadmapCompletionContextReady` / `Completed`
- `.agents/core/roadmap-completion-context.md` written with runtime prompt output
- HITL request capture side effects when configured and markers exist
- `.agents/artifacts/lifecycle.json` updated with `.agents/core/roadmap-completion-context.md` at `Ready`
- caller proceeds to `SelectNextEpic`

Externally observable outputs on runtime prompt failure:

- console output produced before failure
- `.agents/journal/transitions.jsonl` appended with `TransitionStarted`
- `.agents/state.json` written with `RoadmapCompletionContextReady` / `Started`
- `.agents/journal/transitions.jsonl` appended with `TransitionFailed`
- `.agents/state.json` written with current state `EvidenceBlocked`, status `Failed`, transition intent `ResolveTransitionFailure`, output `.agents/core/roadmap-completion-context.md`, and next transition `Resolve blocker and rerun`
- `RunAsync` returns `RoadmapOutcome.Failed`

Externally observable outputs on projection validation or freshness failure:

- `.agents/projections/manifest.json` may be updated before the failure
- `.agents/evidence/blockers/projection-blocked.NNNN.md` is written
- console warning from `ReportEphemeralBlockerAsync`
- console error
- `RunAsync` returns `RoadmapOutcome.Failed`
- no `CreateRoadmapCompletionContext` transition-start state is written by `RunPromptTransitionWithCompletionAsync`

Externally observable outputs on cancellation:

- `WriteCancelledStateAsync` writes `Cancelled` state based on the current persisted transition summary.
- `RunAsync` returns `RoadmapOutcome.Cancelled`.

## Deliverable 12: Behavioral Equivalence Contract

Inputs that must remain equivalent:

- Bootstrap runs only when `.agents/core/roadmap-completion-context.md` is not `Present`.
- Runtime prompt name remains `CreateRoadmapCompletionContext`.
- From state remains `CoreReady`.
- To state remains `RoadmapCompletionContextReady`.
- Output path remains `.agents/core/roadmap-completion-context.md`.
- Projection path remains `.agents/projections/roadmap-completion.md`.
- Completed epic archive input glob remains `.agents/archive/epics/*.md`.
- Completed epic archive files remain optional transition inputs.
- Transition input hashing includes projection content, completed epic archive inputs, rendered context, and secondary input.

Outputs that must remain equivalent:

- Generated runtime output is written verbatim to `.agents/core/roadmap-completion-context.md`.
- Projection generation, validation, freshness policy, manifest updates, and projection-blocked evidence behavior remain identical.
- Completed epic evidence rendering remains identical, including sort order, title/id extraction, section extraction, evidence quality, fallback content, no-archive message, and truncation budgets.
- `TransitionStarted`, `TransitionCompleted`, and `TransitionFailed` journal records retain the same event names, states, prompt, projection, output paths, elapsed behavior, decisions, error messages, input hashes, and input snapshot shape.
- Started state remains `RoadmapCompletionContextReady` / `Started` with decision `Pending`.
- Completed state remains `RoadmapCompletionContextReady` / `Completed` with decision `Completed`.
- Runtime failure state remains `EvidenceBlocked` / `Failed` with transition intent `ResolveTransitionFailure`.
- HITL capture remains conditional on configured capture service and nonblank output.
- Lifecycle state for `.agents/core/roadmap-completion-context.md` remains `Ready`.
- Console phase text remains `Bootstrap roadmap completion context`.
- Agent prompt rendering still appends the implementation-first prompt policy.
- Agent runtime still uses read-only planning.
- Non-completed agent turns still fail with diagnostics.
- `OperationCanceledException` is not converted into runtime failure persistence inside the prompt runner; outer cancellation behavior remains responsible.
- The caller still proceeds to `RunSelectionAndFollowingAsync` after successful bootstrap.

Not part of the equivalence contract:

- Internal method names.
- Whether the handler is a private method or a separate class.
- Local variable names.
- Internal grouping of pure string construction, as long as rendered prompt inputs remain byte-for-byte equivalent.
- Private helper boundaries inside completed-epic evidence parsing, as long as rendered secondary input remains equivalent.

## Deliverable 13: Transition Handler Shape

Recovered linear handler shape:

```text
Execute(projectContext, cancellationToken)

-> Set transition constants

-> Report phase

-> Load prompt contract

-> Ensure projection

-> Build runtime context

-> Render completed epic evidence

-> Run transition with durable prompt envelope
   -> Resolve input snapshot
   -> Save started state
   -> Append started journal
   -> Run runtime prompt
   -> Append completed journal
   -> Save completed state
   -> Persist failed state/journal on runtime failure

-> Write roadmap completion context artifact

-> Capture HITL requests

-> Mark lifecycle Ready

-> Return
```

The caller shape remains:

```text
RunFromCoreReady

-> Check roadmap completion context status

-> If missing, execute bootstrap handler

-> Run selection and following transitions
```

## Deliverable 14: Readability Improvements

Extracting this transition would make the entry and exit easier to see. Today the reader must connect `RunFromCoreReadyAsync`, `BootstrapRoadmapCompletionContextAsync`, the shared prompt runner, and lifecycle updates to know when bootstrap actually starts and ends.

It would make the write set explicit. The transition writes or may write projection artifacts, projection manifest, state, journal, roadmap completion context, HITL ledger entries, lifecycle, and projection-blocked evidence. Those effects are currently spread across helpers with broad names.

It would reduce the minimum reading path for this transition. A named handler could show the sequence `ensure projection -> render archive evidence -> run prompt envelope -> write context -> capture HITL -> mark ready` in one place, while leaving implementation details in existing services.

It would isolate the important ordering hazard: completed transition state is persisted before the final context artifact write and lifecycle update. A handler can make that order visible without changing it.

It would separate bootstrap from selection. The current caller immediately continues into `SelectNextEpic`, so a reader can accidentally treat bootstrap and selection as one workflow. A handler with one return point makes the transition boundary clear.

It would make failure behavior easier to compare. Projection failures happen before transition-start state, while runtime failures happen after transition-start state and persist `EvidenceBlocked`. That distinction is currently split between `ProjectionCache` and `RunPromptTransitionWithCompletionAsync`.

It would lower working memory. An engineer could understand this transition by following one named flow and consulting existing helpers only for their established contracts, instead of reconstructing the transition from the state machine plus projection cache plus input resolver plus prompt runner plus persistence helpers.
