# SelectNextEpic Transition Extraction Audit

## Audited Transition

Exactly one transition is audited here: `SelectNextEpic`.

Scope: `RoadmapState.RoadmapCompletionContextReady` -> `RoadmapState.SelectNextStrategicInitiative`, implemented by `SelectNextInitiativeAsync` in `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`.

The audit ends when `SelectNextInitiativeAsync` returns a parsed `SelectionDecision`. Downstream routing in `ContinueAfterSelectionAsync` is boundary context only; `EpicPreparationAudit`, `CreateNewEpic`, `SplitEpic`, and `GenerateMilestoneDeepDivesForEpic` are out of scope.

## 1. Transition Narrative

Current State

The roadmap completion context is ready. Roadmap source files exist under `.agents/roadmap/*.md`. The state machine has a `ProjectContext` from preflight and needs to choose the next strategic initiative.

Goal

Run the `SelectNextEpic` prompt against the current roadmap context, persist the raw selection, record the evidence and provenance for that selection, parse the recommendation, and return the decision to the existing router.

Major Steps

1. Announce the selection phase.
2. Resolve the `SelectNextEpic` prompt contract.
3. Ensure the `SelectNextEpic` projection is present, valid, and fresh.
4. Load existing roadmap state for retired epic context.
5. Build the runtime prompt context from projection content, roadmap completion context, roadmap source references, and retired epics.
6. Capture the transition input snapshot.
7. Persist the transition as started and append a start journal record.
8. Run the runtime prompt through the read-only planning agent.
9. Persist the transition as completed and append a completion journal record.
10. Write the raw selection artifact.
11. Capture optional HITL non-implementation request markers from the selection output.
12. Write numbered selection evidence.
13. Record selection provenance.
14. Mark the selection artifact lifecycle as ready.
15. Parse the selection recommendation.
16. Append a decision ledger entry.
17. Return the parsed `SelectionDecision`.

Completion

The transition is complete when `.agents/selection.md` contains the raw selection output, `.agents/evidence/selection/selection.NNNN.md` contains matching evidence, selection provenance and lifecycle state are updated, state and journal records show the prompt transition completed, the decision ledger has the parsed recommendation, and the parsed decision has been returned to the caller.

## 2. Current Execution Trace

This is the current successful execution order.

1. `ExecuteResumePlanAsync` or `RunFromCoreReadyAsync` calls `RunSelectionAndFollowingAsync`.
2. `RunSelectionAndFollowingAsync` calls `SelectNextInitiativeAsync`.
3. `SelectNextInitiativeAsync` sets `runtimePrompt = "SelectNextEpic"`.
4. `console.Phase("Select next strategic initiative")` writes the phase message.
5. `contractRegistry.Get("SelectNextEpic")` loads the prompt contract.
6. `projectionCache.EnsureAsync("SelectNextEpic", projectContext, contract, cancellationToken)` starts.
7. `ProjectionCache` resolves the projection definition from `ProjectionRegistry`.
8. `ProjectionCache` creates current projection provenance from the projection definition and `ProjectContext`.
9. `ProjectionCache` reads `.agents/projections/select-next-epic.md`.
10. If the projection content is missing or empty, `ProjectionCache` renders `ProjectionForSelectNextEpic`.
11. If generation is needed, `RoadmapPromptRunner.RunProjectionPromptAsync` runs the projection prompt through `RunOneShotAsync`.
12. `RunOneShotAsync` creates a `ConsoleTurnRenderer`.
13. `RunOneShotAsync` calls `runtime.RunOneShotAsync(AgentSpecs.ReadOnlyPlanning(repository), prompt, renderer.Stream, cancellationToken)`.
14. `RunOneShotAsync` requires `AgentTurnState.Completed`; otherwise it throws `RoadmapStepException`.
15. `RunOneShotAsync` echoes silent output if needed and returns the projection output.
16. `ProjectionCache` rejects empty generated projection content.
17. `ProjectionCache` validates projection content with `ProjectionValidator.Validate("SelectNextEpic", content)`.
18. `ProjectionCache` hashes the projection content.
19. `ProjectionCache` loads the projection manifest.
20. `ProjectionCache` finds any prior manifest entry for `SelectNextEpic`.
21. `ProjectionCache` computes validation status.
22. `ProjectionCache` computes freshness as fresh for generated content or via `ProjectionFreshnessEvaluator` for existing content.
23. `ProjectionCache` creates or updates the manifest entry.
24. `ProjectionCache` upserts the manifest entry before later blocking or writing generated projection content.
25. If validation failed, `ProjectionCache` writes `.agents/evidence/blockers/projection-blocked.NNNN.md` and throws `RoadmapStepException`.
26. If projection content was generated, `ProjectionCache` writes `.agents/projections/select-next-epic.md`.
27. If the projection is stale and the contract policy is `Block`, `ProjectionCache` writes `.agents/evidence/blockers/projection-blocked.NNNN.md` and throws `RoadmapStepException`.
28. `ProjectionCache` returns `ProjectionCacheResult`.
29. `stateStore.LoadAsync()` loads existing roadmap state.
30. `contextBuilder.BuildSelectionContextAsync(projection.Content, existing?.RetiredEpics ?? [])` starts.
31. The context builder reads required `.agents/core/roadmap-completion-context.md`.
32. The context builder calls `RenderRoadmapSourceReferencesAsync`.
33. `RenderRoadmapSourceReferencesAsync` calls `RequireRoadmapSourcePathsAsync`.
34. `RequireRoadmapSourcePathsAsync` lists `.agents/roadmap/*.md`.
35. `RequireRoadmapSourcePathsAsync` throws if no roadmap source exists.
36. `RequireRoadmapSourcePathsAsync` reads every roadmap source path to prove it is present and non-empty.
37. The context builder renders roadmap source references as a table; it does not embed roadmap epic bodies.
38. The context builder renders retired epics as `None` or as a table.
39. The context builder builds `# Roadmap Runtime Prompt Context` with sections in this order: projection content, current roadmap completion context, roadmap source references, retired epics.
40. The context builder rejects context containing raw project-context markers.
41. `SelectNextInitiativeAsync` calls `RunPromptTransitionWithCompletionAsync`.
42. `RunPromptTransitionWithCompletionAsync` calls `inputResolver.ResolveAsync`.
43. `TransitionInputResolver` creates an input accumulator.
44. `TransitionInputResolver` adds the projection path as a required input.
45. `TransitionInputResolver` applies `SelectNextEpic` prompt input rules.
46. The resolver adds required `.agents/core/roadmap-completion-context.md`.
47. The resolver calls `RequireRoadmapSourcePathsAsync` again and adds each roadmap source as required input.
48. The resolver snapshots inputs in path order.
49. The snapshot reads each required input.
50. The snapshot throws `RoadmapStepException` if any required input is missing.
51. The snapshot hashes every present input.
52. The resolver extracts the projection hash.
53. The resolver hashes the rendered prompt context.
54. The resolver hashes the secondary input, which is the empty string.
55. The resolver computes the overall snapshot hash.
56. The resolver returns `TransitionInputSnapshot`.
57. `RunPromptTransitionWithCompletionAsync` creates a correlation id.
58. It captures `started = DateTimeOffset.UtcNow`.
59. It starts a stopwatch.
60. It calls `SaveStateAsync` with current state `SelectNextStrategicInitiative`, status `Started`, from `RoadmapCompletionContextReady`, to `SelectNextStrategicInitiative`, prompt `SelectNextEpic`, projection `.agents/projections/select-next-epic.md`, output `.agents/selection.md`, and decision `Pending`.
61. `SaveStateAsync` loads existing state.
62. `SaveStateAsync` loads projection manifest.
63. `SaveStateAsync` reads active artifact statuses for roadmap completion context, selection, and active epic.
64. `SaveStateAsync` reads the last decision id from the decision ledger.
65. `SaveStateAsync` preserves existing retired epics unless a replacement list is passed.
66. `SaveStateAsync` preserves existing blockers unless a replacement list is passed.
67. `SaveStateAsync` lists split family files.
68. `SaveStateAsync` computes projection manifest counts.
69. `SaveStateAsync` saves `.agents/state.json` with `NextValidTransitions(SelectNextStrategicInitiative)`, which is `["SelectNextEpic"]`.
70. `RunPromptTransitionWithCompletionAsync` appends a `TransitionStarted` record to `.agents/journal/transitions.jsonl`.
71. The start journal record includes the correlation id, previous state, attempted state, prompt, projection, prompt contract key, input artifact hashes, output path `.agents/selection.md`, duration `0`, result `Started`, parser decision `None`, no error, and the full input snapshot.
72. `promptRunner.RunRuntimePromptAsync("SelectNextEpic", context, string.Empty, cancellationToken)` starts.
73. The prompt runner renders the runtime prompt with `RoadmapPromptCatalog.RenderRuntime`.
74. The prompt runner appends the implementation-first prompt policy.
75. `RunOneShotAsync` creates a `ConsoleTurnRenderer`.
76. `RunOneShotAsync` runs the prompt through `runtime.RunOneShotAsync(AgentSpecs.ReadOnlyPlanning(repository), prompt, renderer.Stream, cancellationToken)`.
77. `RunOneShotAsync` requires `AgentTurnState.Completed`.
78. `RunOneShotAsync` echoes silent output if needed.
79. `RunOneShotAsync` returns the raw selection markdown.
80. `RunPromptTransitionWithCompletionAsync` stops the stopwatch.
81. It captures `completed = DateTimeOffset.UtcNow`.
82. It appends a `TransitionCompleted` record to `.agents/journal/transitions.jsonl`.
83. The completion journal record reuses the same correlation id, input hashes, and input snapshot.
84. The completion journal record records elapsed milliseconds, result `Completed`, parser decision `None`, no error, and output path `.agents/selection.md`.
85. `RunPromptTransitionWithCompletionAsync` calls `SaveStateAsync` again with current state `SelectNextStrategicInitiative`, status `Completed`, decision `Completed`, and the same transition output path.
86. `SaveStateAsync` repeats its state snapshot reads and saves `.agents/state.json`.
87. `RunPromptTransitionWithCompletionAsync` returns `PromptTransitionCompletion`.
88. `SelectNextInitiativeAsync` writes the raw output to `.agents/selection.md`.
89. `CaptureHitlRequestsAsync` returns immediately if no HITL capture service exists or the output is blank.
90. If the HITL capture service exists, it captures markers from `.agents/selection.md`.
91. `artifacts.WriteNumberedEvidenceAsync(".agents/evidence/selection", "selection", completion.Output)` starts.
92. `WriteNumberedEvidenceAsync` lists existing `.agents/evidence/selection/selection.*.md`.
93. It chooses the next four-digit path.
94. It writes the selection output to `.agents/evidence/selection/selection.NNNN.md`.
95. `selectionProvenance.RecordActiveSelectionAsync` starts.
96. It checks cancellation.
97. It creates derived selection provenance from the transition input snapshot and retired epics.
98. Provenance includes selection cycle hash, prompt context hash, secondary input hash, retired epic state hash, projection input, roadmap completion context input, and roadmap source inputs.
99. It creates a trusted manifest entry for `.agents/selection.md` using the selection content hash.
100. It loads `.agents/selection-provenance-manifest.json`, treating missing or invalid JSON as empty.
101. It upserts the active selection, superseding prior trusted selection entries for the same artifact kind.
102. It saves `.agents/selection-provenance-manifest.json`.
103. `lifecycleStore.UpsertAsync(".agents/selection.md", Ready, evidencePath)` starts.
104. The lifecycle store loads `.agents/artifacts/lifecycle.json`, or migrates legacy markdown.
105. It removes any existing lifecycle entry for `.agents/selection.md`.
106. It appends a ready lifecycle entry with the evidence path as notes.
107. It sorts entries by path and saves `.agents/artifacts/lifecycle.json`.
108. `new SelectionParser().Parse(completion.Output)` starts.
109. The parser reads `## Recommendation Summary`.
110. It requires `Recommended Outcome` to match one of the allowed selection outcomes.
111. It requires `Recommended Initiative`.
112. It requires `Initiative Type` to match one of the allowed initiative types.
113. It requires `Confidence` to match common allowed confidence values.
114. It reads optional `Primary Reason`.
115. If outcome is `Select Existing Epic`, it reads `## If Existing Roadmap Epic Selected`.
116. For an existing epic, it requires `Epic ID` and `Epic Name`.
117. For an existing epic, it rejects a selection where both identity fields are unknown.
118. The parser returns `SelectionDecision`.
119. `AppendDecisionAsync` starts.
120. It asks the decision ledger for the next id.
121. It appends a decision entry to `.agents/decision-ledger.json`.
122. The entry state is `SelectNextStrategicInitiative`.
123. The entry transition and prompt are both `SelectNextEpic`.
124. The entry projection path is `.agents/projections/select-next-epic.md`.
125. The entry input artifact path list is empty.
126. The entry output artifact path list is `[.agents/selection.md]`.
127. The entry decision is `SelectionDecision.RecommendedOutcome`.
128. The entry confidence is `SelectionDecision.Confidence`.
129. The entry rationale is `SelectionDecision.PrimaryReason`.
130. `SelectNextInitiativeAsync` returns the parsed `SelectionDecision`.
131. `RunSelectionAndFollowingAsync` passes that decision to `ContinueAfterSelectionAsync`; that routing is outside this audited transition.

Failure and cancellation ordering:

1. Projection validation or stale projection failure happens before the started state is saved. The projection manifest may already be updated, and a projection blocker evidence file may be written.
2. Prompt execution failure inside `RunPromptTransitionWithCompletionAsync` appends `TransitionFailed`, saves `EvidenceBlocked` state with `ResolveTransitionFailure`, and throws `RoadmapStepException.AlreadyPersisted`.
3. `OperationCanceledException` is not caught by `RunPromptTransitionWithCompletionAsync`; outer `RunAsync` writes cancelled state.
4. Selection parse failure happens after state completion, selection artifact write, evidence write, provenance write, and lifecycle update, but before decision ledger append.

## 3. Concern Inventory

| Execution step | Primary concern | Other concerns present | Where concerns become mixed |
|---|---|---|---|
| Enter through `RunSelectionAndFollowingAsync` | routing | transition invocation | Routing and transition execution are adjacent, but still separable. |
| Announce phase | reporting | none | Not mixed. |
| Load prompt contract | validation/configuration | transition setup | Slightly mixed with orchestration because the whole registry is accessed for one known prompt. |
| Ensure projection | artifact read, prompt execution, validation | manifest persistence, blocker evidence, recovery exception path | Strongly mixed: an input preparation step can run an agent, mutate manifest state, write projection files, write blocker evidence, and throw. |
| Load existing state | state read | retired epic context, preserved blockers/intent later | Mixed because state is read for context but reused indirectly by `SaveStateAsync` later. |
| Build selection context | artifact read, context assembly | input validation, retired epic rendering | Mixed where context rendering also proves source artifacts exist and rejects raw project-context markers. |
| Resolve input snapshot | artifact read, hashing | validation, ordering, provenance preparation | Mixed because it both validates required artifacts and prepares journal/provenance hashes. |
| Save started state | state mutation, persistence | active artifact status, manifest counts, ledger summary, split counts, next transitions | Strongly mixed: a transition state write pulls data from several unrelated persistence surfaces. |
| Append start journal | journaling | input snapshot persistence | Lightly mixed: the journal stores both event and snapshot detail. |
| Run runtime prompt | prompt execution | prompt rendering, policy append, console streaming, runtime state validation | Strongly mixed: prompt execution includes rendering, policy composition, stream reporting, and failure normalization. |
| Append completion journal | journaling | timing, snapshot reuse | Lightly mixed. |
| Save completed state | state mutation, persistence | active artifact status, manifest counts, ledger summary, split counts, next transitions | Same mixing as started state. |
| Write selection artifact | artifact write | none | Not mixed. |
| Capture HITL markers | evidence capture | optional side ledger write | Mixed because a selection artifact write immediately feeds a non-selection review ledger when the optional service exists. |
| Write numbered evidence | artifact write | sequence allocation | Lightly mixed: file naming and persistence happen together. |
| Record selection provenance | provenance persistence | hashing, retired epic state inclusion, superseding older entries | Mixed because selection content and transition input snapshot become durable freshness rules. |
| Upsert lifecycle | lifecycle persistence | evidence path reporting | Lightly mixed: lifecycle state includes evidence path as notes. |
| Parse selection | parsing, validation | decision extraction | Not mixed, but it happens after several writes. |
| Append decision | decision persistence | id allocation, timestamp | Lightly mixed. |
| Return decision | transition result | downstream routing handoff | Boundary point. |

The highest-concern mixing is in projection ensure, shared prompt transition execution, state saving, and post-output materialization before parsing.

## 4. Hidden Steps

These steps are real but hidden inside helper calls:

1. Resolve projection definition for `SelectNextEpic`.
2. Capture projection provenance from `ProjectContext`.
3. Decide whether to reuse or generate the projection.
4. Run a projection prompt if projection content is missing.
5. Validate projection structure.
6. Update projection manifest even when later blocking.
7. Write projection blocker evidence for invalid or stale projection.
8. Read and validate roadmap completion context.
9. List and validate roadmap source paths.
10. Render roadmap source references instead of embedding roadmap content.
11. Render retired epic exclusions into prompt context.
12. Reject raw project-context markers.
13. Resolve transition input hashes.
14. Hash rendered prompt context.
15. Hash empty secondary input.
16. Compute selection-cycle snapshot hash.
17. Allocate transition correlation id.
18. Capture transition start timestamp.
19. Start elapsed-time measurement.
20. Build active artifact status rows.
21. Load projection manifest counts for state.
22. Read last decision id for state.
23. Count split family files for state.
24. Preserve existing blockers and transition intent when saving state.
25. Compute next valid transitions for `SelectNextStrategicInitiative`.
26. Render runtime prompt.
27. Append prompt policy.
28. Stream agent output to console.
29. Echo silent agent output.
30. Reuse the original input snapshot for completion and failure journal records.
31. Allocate numbered selection evidence path.
32. Capture optional HITL request markers.
33. Hash selection content for provenance.
34. Hash retired epic state for provenance.
35. Supersede prior trusted selection provenance entries.
36. Upsert selection lifecycle state.
37. Parse and validate the recommendation summary.
38. Parse existing epic identity only for `Select Existing Epic`.
39. Allocate next decision id.

## 5. Natural Step Boundaries

Step 1: Announce and identify transition

Purpose: Make the CLI phase visible and bind execution to the `SelectNextEpic` prompt.

Inputs: none beyond the selected transition.

Outputs: console phase text, runtime prompt name.

---

Step 2: Ensure projection

Purpose: Produce usable projection content for the runtime prompt.

Inputs: `ProjectContext`, `SelectNextEpic` contract, projection registry, projection file, projection manifest.

Outputs: `ProjectionCacheResult`, optional generated projection file, updated projection manifest, optional blocker evidence and exception.

---

Step 3: Build selection context

Purpose: Render the human-readable input package the runtime prompt will receive.

Inputs: projection content, roadmap completion context, roadmap source path list, existing retired epics.

Outputs: rendered `# Roadmap Runtime Prompt Context`.

---

Step 4: Capture transition inputs

Purpose: Freeze the artifact inputs and hashes used by this transition attempt.

Inputs: prompt name, projection path, rendered context, empty secondary input, roadmap completion context, roadmap sources.

Outputs: `TransitionInputSnapshot`, input artifact hash map, prompt context hash, secondary input hash, snapshot hash.

---

Step 5: Mark transition started

Purpose: Persist that `SelectNextEpic` has begun.

Inputs: from state, to state, prompt, projection path, output path, start time, input snapshot.

Outputs: `.agents/state.json` with status `Started`, `TransitionStarted` journal record.

---

Step 6: Execute prompt

Purpose: Ask the read-only planning agent for the next strategic initiative selection.

Inputs: runtime prompt name, rendered context, empty secondary input, repository, cancellation token.

Outputs: raw selection markdown or an exception.

---

Step 7: Mark transition completed or failed

Purpose: Persist the prompt execution result at the transition level.

Inputs: raw output or exception, start time, completion or failure time, elapsed milliseconds, input snapshot.

Outputs: `TransitionCompleted` and completed state on success; `TransitionFailed`, `EvidenceBlocked` state, blocker row, and `ResolveTransitionFailure` intent on prompt failure.

---

Step 8: Materialize selection artifact

Purpose: Store the raw prompt output as the active selection.

Inputs: raw selection markdown.

Outputs: `.agents/selection.md`.

---

Step 9: Capture ancillary evidence

Purpose: Preserve selection evidence and optional explicitly requested non-implementation markers.

Inputs: selection path and raw selection markdown.

Outputs: optional HITL ledger updates, `.agents/evidence/selection/selection.NNNN.md`.

---

Step 10: Record derived artifact state

Purpose: Make the active selection freshness-checkable and lifecycle-visible.

Inputs: selection markdown, input snapshot, retired epics, evidence path.

Outputs: `.agents/selection-provenance-manifest.json`, `.agents/artifacts/lifecycle.json`.

---

Step 11: Interpret decision

Purpose: Convert selection markdown into the typed decision used by downstream routing.

Inputs: raw selection markdown.

Outputs: `SelectionDecision` or parse exception.

---

Step 12: Record decision

Purpose: Preserve the parsed recommendation in the decision ledger.

Inputs: parsed decision, projection path, selection output path.

Outputs: `.agents/decision-ledger.json`.

---

Step 13: Return

Purpose: Hand the decision to existing downstream routing.

Inputs: parsed decision.

Outputs: returned `SelectionDecision`.

## 6. Mixed-Concern Analysis

Step 1 is clean. It reports the phase and establishes the prompt name.

Step 2 mixes input preparation with projection generation, projection validation, manifest persistence, blocker evidence creation, and exception routing. A reader cannot understand whether selection can run without understanding projection cache state and stale projection policy.

Step 3 mixes context assembly with artifact validation. The method does not only render context; it also enforces that roadmap source files exist and are non-empty, and it rejects raw project-context markers.

Step 4 mixes provenance preparation with validation. The snapshot is needed for journaling and selection provenance, but creating it also re-reads required artifacts and can stop the transition.

Step 5 mixes a started-state write with global state summary refresh. Saving one transition status also reads active artifact statuses, projection manifest counts, decision ledger state, split family counts, existing blockers, existing transition intent, and retired epic records.

Step 6 mixes prompt execution with prompt rendering, policy append, console streaming, runtime result validation, and diagnostic formatting. The actual "run SelectNextEpic" action is surrounded by execution infrastructure.

Step 7 mixes prompt result persistence with recovery behavior. On success it records journal and state completion. On failure it changes the workflow state to `EvidenceBlocked`, creates a blocker row, sets a recovery intent, and throws an already-persisted exception.

Step 8 is clean. It writes the selection artifact.

Step 9 mixes selection persistence with optional HITL capture and evidence sequencing. A reader focused on selection must also account for optional review-ledger side effects.

Step 10 mixes derived artifact provenance, old selection superseding, retired epic state hashing, and lifecycle update. These are all related to the selection artifact, but they are different persistence surfaces.

Step 11 is clean internally, but its position is surprising: parse validation happens after artifact, evidence, provenance, and lifecycle writes.

Step 12 mixes decision id allocation, timestamping, and ledger persistence. It is small but still a persistence concern after parsing.

Step 13 is clean. It returns the typed decision.

## 7. Data Flow

`ProjectContext`

Origin: loaded during preflight before this transition.

Consumed by: projection provenance, projection prompt rendering if projection is generated.

Output influence: projection manifest freshness and generated projection content.

---

`PromptContract`

Origin: `contractRegistry.Get("SelectNextEpic")`.

Consumed by: `ProjectionCache.EnsureAsync`.

Output influence: stale projection policy `Block`, required inputs and outputs as registry data.

---

Projection content

Origin: `.agents/projections/select-next-epic.md` or generated by `ProjectionForSelectNextEpic`.

Consumed by: projection validator, projection manifest hash, selection context builder, transition input snapshot.

Output influence: runtime prompt context, selection-cycle provenance.

---

Roadmap completion context

Origin: `.agents/core/roadmap-completion-context.md`.

Consumed by: selection context builder, transition input snapshot.

Output influence: runtime prompt context hash, snapshot hash, selection provenance.

---

Roadmap source paths and content

Origin: `.agents/roadmap/*.md`.

Consumed by: source reference rendering and transition input snapshot.

Output influence: runtime prompt context, input artifact hashes, snapshot hash, selection provenance.

---

Retired epics

Origin: `RoadmapStateDocument.RetiredEpics` loaded from `.agents/state.json`.

Consumed by: selection context builder and selection provenance.

Output influence: prompt context hash and retired epic state hash in selection provenance.

---

Rendered prompt context

Origin: `RoadmapPromptContextBuilder.BuildSelectionContextAsync`.

Consumed by: transition input snapshot and runtime prompt rendering.

Output influence: prompt context hash, runtime agent output.

---

Secondary input

Origin: `SelectNextInitiativeAsync` passes `string.Empty`.

Consumed by: transition input snapshot and runtime prompt rendering.

Output influence: secondary input hash. It intentionally remains the empty-string input.

---

Transition input snapshot

Origin: `TransitionInputResolver.ResolveAsync`.

Consumed by: start, completion, and failure journal records; selection provenance.

Output influence: journal input hash fields, `InputSnapshot`, selection cycle identity, derived artifact causal inputs.

---

Raw selection output

Origin: `RoadmapPromptRunner.RunRuntimePromptAsync("SelectNextEpic", ...)`.

Consumed by: selection artifact write, HITL marker capture, numbered evidence write, selection provenance hash, lifecycle ready note indirectly through evidence path, selection parser.

Output influence: `.agents/selection.md`, `.agents/evidence/selection/selection.NNNN.md`, `.agents/selection-provenance-manifest.json`, optional HITL ledger, parsed decision.

---

Selection evidence path

Origin: `WriteNumberedEvidenceAsync`.

Consumed by: lifecycle upsert.

Output influence: lifecycle notes for `.agents/selection.md`.

---

Parsed `SelectionDecision`

Origin: `SelectionParser.Parse`.

Consumed by: `AppendDecisionAsync` and returned to `RunSelectionAndFollowingAsync`.

Output influence: `.agents/decision-ledger.json`, downstream routing behavior.

---

Decision id

Origin: `DecisionLedgerStore.NextDecisionIdAsync`.

Consumed by: decision ledger append.

Output influence: ordered decision entry in `.agents/decision-ledger.json`.

## 8. Human Navigation Audit

Minimum files and members an engineer must read to understand only this transition:

1. `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`
   - `RunSelectionAndFollowingAsync`, lines 494-500.
   - `ContinueAfterSelectionAsync`, lines 502-554, only to identify the boundary after return.
   - `SelectNextInitiativeAsync`, lines 578-608.
   - `RunPromptTransitionWithCompletionAsync`, lines 1404-1458.
   - `CaptureHitlRequestsAsync`, lines 1460-1468.
   - `AppendDecisionAsync`, lines 1743-1758.
   - `SaveStateAsync`, lines 1800-1837.
   - `ActiveArtifactRowsAsync` and `NextTransitions`, lines 1840-1868.
2. `src/LoopRelay.Roadmap.Cli/ProjectionCache.cs`
   - `EnsureAsync`, including projection generation, validation, manifest updates, and blocker evidence.
3. `src/LoopRelay.Roadmap.Cli/ProjectionRegistry.cs`
   - the `SelectNextEpic` projection path and projection prompt name.
4. `src/LoopRelay.Roadmap.Cli/PromptContractRegistry.cs`
   - the `SelectNextEpic` contract inputs, outputs, decisions, writer, stale policy, and parser.
5. `src/LoopRelay.Roadmap.Cli/RoadmapPromptContextBuilder.cs`
   - `BuildSelectionContextAsync` and `RenderRoadmapSourceReferencesAsync`.
6. `src/LoopRelay.Roadmap.Cli/TransitionInputs.cs`
   - `ResolveAsync`, `AddPromptInputsAsync`, `AddRoadmapSourceInputsAsync`, and `TransitionInputSnapshot`.
7. `src/LoopRelay.Roadmap.Cli/RoadmapPromptRunner.cs`
   - runtime prompt rendering, policy append, read-only planning agent execution, console streaming, and failure checks.
8. `src/LoopRelay.Roadmap.Cli/RoadmapArtifacts.cs`
   - artifact reads, writes, required reads, roadmap source path resolution, and numbered evidence allocation.
9. `src/LoopRelay.Roadmap.Cli/RoadmapArtifactPaths.cs`
   - selection, projection, journal, lifecycle, state, evidence, and roadmap source paths.
10. `src/LoopRelay.Roadmap.Cli/SelectionProvenance.cs`
    - `RecordActiveSelectionAsync`, provenance input construction, active selection superseding.
11. `src/LoopRelay.Roadmap.Cli/SelectionParser.cs`
    - allowed outcomes, required fields, existing epic identity validation.
12. `src/LoopRelay.Roadmap.Cli/RoadmapStateStore.cs` and `src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs`
    - state load/save shape and transition summary fields.
13. `src/LoopRelay.Roadmap.Cli/TransitionJournalStore.cs` and `src/LoopRelay.Roadmap.Cli/TransitionJournal.cs`
    - JSONL append behavior and journal record fields.
14. `src/LoopRelay.Roadmap.Cli/DecisionLedgerStore.cs` and `src/LoopRelay.Roadmap.Cli/DecisionLedger.cs`
    - decision id allocation and ledger entry shape.
15. `src/LoopRelay.Roadmap.Cli/ArtifactLifecycleStore.cs` and `src/LoopRelay.Roadmap.Cli/ArtifactLifecycle.cs`
    - lifecycle upsert behavior and persisted shape.

Most relevant tests:

1. `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapStateMachineSelectionTests.cs`
   - bootstrap-before-selection behavior, existing context behavior, HITL capture from selection output.
2. `tests/LoopRelay.Roadmap.Cli.Tests/TransitionJournalTests.cs`
   - snapshot reuse for started/completed/failed journal records.
3. `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapFailurePersistenceTests.cs`
   - prompt failure persistence for `SelectNextEpic`.
4. `tests/LoopRelay.Roadmap.Cli.Tests/SelectionProvenanceTests.cs`
   - selection freshness and invalidation.
5. `tests/LoopRelay.Roadmap.Cli.Tests/TransitionInputResolverTests.cs`
   - `SelectNextEpic` input snapshot behavior.
6. `tests/LoopRelay.Roadmap.Cli.Tests/MarkdownParserTests.cs`
   - selection parser behavior.

## 9. Extraction Boundary

Smallest useful extraction boundary:

Extract the body and directly required helper flow of `SelectNextInitiativeAsync` into a named linear transition handler whose single public operation returns `SelectionDecision`.

The handler should own:

1. The `SelectNextEpic` phase.
2. Projection ensure for `SelectNextEpic`.
3. Selection context construction.
4. Transition input snapshot capture.
5. Started/completed/failed state and journal persistence for the prompt transition.
6. Runtime prompt execution.
7. Selection artifact write.
8. Optional HITL capture.
9. Numbered selection evidence write.
10. Selection provenance write.
11. Selection lifecycle update.
12. Selection parsing.
13. Selection decision ledger append.
14. The returned `SelectionDecision`.

Keep outside the handler:

1. `RunSelectionAndFollowingAsync` downstream routing.
2. `ContinueAfterSelectionAsync`.
3. Existing epic audit.
4. New epic creation.
5. Split epic generation.
6. Milestone deep-dive generation.
7. Preflight project context loading.
8. Startup and resume planning.
9. Unblock flows.

This boundary preserves one purpose, one entry, one return value, and the existing execution flow without changing routing behavior.

## 10. Required Inputs

Required runtime values:

1. `ProjectContext`.
2. `CancellationToken`.

Required services or collaborators:

1. `ILoopConsole` for the phase message.
2. `PromptContractRegistry` or the concrete `SelectNextEpic` `PromptContract`.
3. `ProjectionCache`.
4. `RoadmapPromptContextBuilder`.
5. `TransitionInputResolver`.
6. `RoadmapPromptRunner`.
7. `RoadmapArtifacts`.
8. `RoadmapStateStore`.
9. `ProjectionManifestStore`, because `SaveStateAsync` records projection manifest counts.
10. `TransitionJournalStore`.
11. `SelectionProvenanceService`.
12. `ArtifactLifecycleStore`.
13. `DecisionLedgerStore`.

Required existing artifacts:

1. `.agents/core/roadmap-completion-context.md`.
2. At least one non-empty `.agents/roadmap/*.md`.
3. `.agents/projections/select-next-epic.md`, or the ability to generate it from `ProjectionForSelectNextEpic`.
4. Project context files already loaded into `ProjectContext`.

Optional inputs:

1. `ExplicitHitlNonImplementationRequestCaptureService`.
2. Existing `.agents/selection-provenance-manifest.json`.
3. Existing `.agents/artifacts/lifecycle.json`.
4. Existing `.agents/decision-ledger.json`.
5. Existing `.agents/journal/transitions.jsonl`.

Incidental inputs required only to preserve current state snapshots:

1. Existing roadmap state blockers.
2. Existing roadmap transition intent.
3. Existing retired epics.
4. Existing active artifact statuses for `.agents/core/roadmap-completion-context.md`, `.agents/selection.md`, and `.agents/epic.md`.
5. Existing projection manifest entries and counts.
6. Existing decision ledger last id.
7. Existing split family files under `.agents/splits/split-family-*.json`.

## 11. Required Outputs

Returned value:

1. `SelectionDecision`.

Durable successful outputs:

1. `.agents/projections/select-next-epic.md` if generated.
2. `.agents/projections/manifest.json`.
3. `.agents/state.json`, first with `Started`, then with `Completed`.
4. `.agents/journal/transitions.jsonl`, with `TransitionStarted` and `TransitionCompleted`.
5. `.agents/selection.md`.
6. Optional HITL ledger writes from `.agents/selection.md`.
7. `.agents/evidence/selection/selection.NNNN.md`.
8. `.agents/selection-provenance-manifest.json`.
9. `.agents/artifacts/lifecycle.json`.
10. `.agents/decision-ledger.json`.

Console outputs:

1. Phase text: `Select next strategic initiative`.
2. Projection prompt stream if projection generation runs.
3. Runtime prompt stream for `SelectNextEpic`.
4. Silent-output echo when the renderer did not stream output.

Failure outputs:

1. Projection manifest update before projection validation or stale-projection block.
2. `.agents/evidence/blockers/projection-blocked.NNNN.md` for projection validation or freshness block.
3. `TransitionStarted` journal and started state if failure happens during runtime prompt execution.
4. `TransitionFailed` journal and `EvidenceBlocked` state with `ResolveTransitionFailure` if runtime prompt execution fails.
5. Cancelled state from the outer runner if cancellation is thrown.
6. On parse failure after prompt success: completed transition state and journal, selection artifact, selection evidence, provenance, and lifecycle may exist without a decision ledger entry.

## 12. Behavioral Equivalence Contract

Inputs that must remain identical:

1. Runtime prompt name: `SelectNextEpic`.
2. From state: `RoadmapCompletionContextReady`.
3. To state: `SelectNextStrategicInitiative`.
4. Projection path: `.agents/projections/select-next-epic.md`.
5. Selection output path: `.agents/selection.md`.
6. Required input artifacts: roadmap completion context and `.agents/roadmap/*.md`.
7. Secondary input: the empty string.
8. Prompt contract stale projection policy: `Block`.
9. Prompt context section order and headings:
   - `Projection Content`
   - `Current Roadmap Completion Context`
   - `Roadmap Source References`
   - `Retired Epics`
10. Runtime prompt policy append behavior.
11. Read-only planning agent spec.

Outputs that must remain identical:

1. Raw selection output written exactly to `.agents/selection.md`.
2. Numbered evidence content equal to raw selection output.
3. Decision ledger fields:
   - state `SelectNextStrategicInitiative`
   - transition `SelectNextEpic`
   - prompt `SelectNextEpic`
   - projection path `.agents/projections/select-next-epic.md`
   - input artifact paths `[]`
   - output artifact paths `[.agents/selection.md]`
   - decision from parsed recommended outcome
   - confidence from parsed confidence
   - rationale from parsed primary reason
4. Selection lifecycle state `Ready` with the evidence path in notes.
5. Selection provenance causal inputs and selection content hash.
6. Prior trusted selection provenance superseding behavior.
7. Projection manifest update behavior.
8. Journal event names and fields.
9. State transition summary fields.
10. State active artifact rows and summary counts.

Ordering that must remain identical:

1. Projection manifest upsert happens before projection validation or stale projection throws.
2. Generated projection content is written after manifest upsert and validation.
3. Started state is saved before `TransitionStarted` is appended.
4. Runtime prompt execution starts after input snapshot capture and start persistence.
5. `TransitionCompleted` is appended before completed state is saved.
6. Completed state is saved before `.agents/selection.md` is written.
7. `.agents/selection.md` is written before HITL capture.
8. HITL capture runs before numbered selection evidence.
9. Numbered selection evidence is written before selection provenance.
10. Selection provenance is written before lifecycle upsert.
11. Lifecycle upsert happens before parsing.
12. Decision ledger append happens after parsing.

Exception and recovery behavior that must remain identical:

1. Projection validation and stale projection failures throw before selection transition start persistence.
2. Runtime prompt failures are persisted as `EvidenceBlocked` and rethrown as already persisted.
3. Cancellation is not swallowed by the transition runner.
4. Parse errors are not retroactively converted into failed transition state.
5. Downstream routing receives the same `SelectionDecision` and therefore keeps existing behavior, including terminal outcome persistence currently owned by `ContinueAfterSelectionAsync`.

Not part of the contract:

1. Local variable names.
2. Private helper names after extraction.
3. The physical class name of the extracted handler.
4. Any internal grouping that does not alter inputs, outputs, ordering, exceptions, console behavior, or durable persistence.

## 13. Transition Handler Shape

Recovered linear shape:

```text
Execute(projectContext, cancellationToken)

-> Announce phase

-> Load SelectNextEpic contract

-> Ensure SelectNextEpic projection

-> Load existing state for retired epics

-> Build selection context

-> Resolve transition input snapshot

-> Persist started state

-> Append TransitionStarted journal record

-> Run SelectNextEpic prompt

-> Append TransitionCompleted journal record

-> Persist completed state

-> Write .agents/selection.md

-> Capture optional HITL requests

-> Write numbered selection evidence

-> Record active selection provenance

-> Upsert selection lifecycle Ready

-> Parse selection output

-> Append decision ledger entry

-> Return SelectionDecision
```

Recovered failure shape for runtime prompt failure:

```text
Prompt throws non-cancellation exception

-> Stop stopwatch

-> Append TransitionFailed journal record

-> Persist EvidenceBlocked state

-> Set ResolveTransitionFailure intent

-> Throw already-persisted RoadmapStepException
```

Recovered projection block shape:

```text
Projection invalid or stale

-> Upsert projection manifest

-> Write projection-blocked evidence

-> Throw RoadmapStepException before transition start persistence
```

Recovered parse failure shape:

```text
Selection output already written and lifecycle/provenance already updated

-> Parse throws

-> No SelectNextEpic decision ledger entry is appended

-> Outer RunAsync reports failure without rewriting the completed transition state
```

## 14. Readability Improvements

Extraction would make the transition readable in one linear pass. Today, the reader jumps from `SelectNextInitiativeAsync` to projection cache behavior, context building, input hashing, shared prompt transition persistence, prompt runner behavior, selection provenance, lifecycle persistence, parsing, and decision ledger persistence.

The externally important ordering would become explicit. In the current implementation, it is easy to miss that completed state is saved before `.agents/selection.md` is written, and that parsing happens after selection artifact, evidence, provenance, and lifecycle writes.

The failure model would be easier to reason about. Projection failures happen before started-state persistence. Runtime prompt failures are owned by `RunPromptTransitionWithCompletionAsync`. Parse failures happen after selection materialization and do not become `TransitionFailed` records.

The transition would be easier to test because the handler boundary has one returned value, one prompt name, one output artifact, and a finite list of durable side effects.

The downstream router would become visually separate. An engineer could inspect `SelectNextEpic` selection production without reading existing epic audit, new epic creation, split epic generation, or milestone generation code.

The minimum navigation path would shrink. The handler would still use the same stores and helpers, but the transition flow itself would no longer be split across generic orchestration and post-output code in `RoadmapStateMachine`.
