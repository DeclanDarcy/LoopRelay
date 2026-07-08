# SplitEpic Transition Extraction Audit

## Audited Transition

Exactly one transition is audited here: `SplitEpic`.

Scope: the `"Select Split Epic"` branch in `ContinueAfterSelectionAsync`, implemented by `SplitEpicAsync` in `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`.

Entry: `ContinueAfterSelectionAsync` receives a `SelectionDecision` whose `RecommendedOutcome` is `"Select Split Epic"` and calls `SplitEpicAsync(projectContext, cancellationToken)`.

Exit: `SplitEpicAsync` returns an `ArtifactPromotionResult`:

- `Promoted`, after a validated split child epic is promoted to `.agents/epic.md`.
- `NotPromoted`, after split-output interpretation blocks or rejects the bundle and persists `EvidenceBlocked`.
- `NotPromoted`, if selected-child active-epic promotion is rejected and persists `EvidenceBlocked`.

Prompt runtime failures are persisted by `RunPromptTransitionWithCompletionAsync` and thrown as already-persisted roadmap failures.

Out of scope:

- the already-audited `SelectNextEpic` transition that chooses `"Select Split Epic"`
- the downstream `GenerateMilestoneDeepDivesForEpic` transition that the caller runs after a promoted split child
- `CreateNewEpic`
- `EpicPreparationAudit`, `RealignEpic`, and `ReimagineEpic`
- completion certification
- unblock handling after a split blocker has been recorded

The extraction target is the existing split-epic workflow, not a new command or architecture.

## 1. Transition Narrative

Current State

`SelectNextEpic` has selected a roadmap initiative that should be split before execution. The active selection exists at `.agents/selection.md`, selection provenance identifies the current selection cycle, and project context preflight has already completed.

Goal

Turn the selected initiative into one or more validated child epic drafts, record the split family, choose the first child epic by numeric order, and promote that child as the active epic at `.agents/epic.md`.

Major Steps

1. Announce the split transition.
2. Re-read the active selection and verify it still belongs to the current selection cycle.
3. Resolve the `SplitEpic` contract and projection.
4. Build runtime prompt context from the projection and selection.
5. Persist prompt-start state and journal evidence.
6. Run the `SplitEpic` prompt.
7. Persist prompt-completed state and journal evidence.
8. Extract `# FILE:` bundle entries from the prompt output.
9. Interpret the bundle as split child epics.
10. If the bundle is blocked or invalid, preserve blocker evidence and pause.
11. If valid, write all child epic files and a bundle manifest.
12. Mark child epic artifacts as draft and capture HITL requests from them.
13. Persist a split-family JSON document.
14. Promote the selected child epic to `.agents/epic.md`.
15. Return the promotion result to the caller.

Completion

The transition is complete when one of these externally visible outcomes exists:

- Successful split: child epic files such as `.agents/epic-1.md` are written, `.agents/bundle-manifest.md` records their hashes, lifecycle metadata marks children `Draft`, `.agents/splits/split-family-{id}.json` records the family, `.agents/epic.md` contains the selected child, lifecycle metadata marks `.agents/epic.md` `Ready`, transition journal includes `ArtifactPromoted`, and state is persisted as `ActiveEpicReady`.
- Blocked or rejected split bundle: no child files are written, no split family is written, the prior active epic is preserved, `.agents/evidence/blockers/split-epic-output.NNNN.md` records the rejection and raw output, lifecycle marks that evidence `Blocked`, journal includes `SplitBundleRejected`, and state is persisted as `EvidenceBlocked`.
- Active-epic promotion rejection: selected child files and split family may already exist, promotion evidence is written under `.agents/evidence/blockers/active-epic-promotion.NNNN.md`, journal includes `ArtifactPromotionBlocked`, and state is persisted as `EvidenceBlocked`.

## 2. Current Execution Trace

This is the current successful execution order.

1. `ContinueAfterSelectionAsync` receives a `SelectionDecision`.
2. The switch on `selection.RecommendedOutcome` enters `case "Select Split Epic"`.
3. The caller invokes `SplitEpicAsync(projectContext, cancellationToken)`.
4. `SplitEpicAsync` sets `runtimePrompt = "SplitEpic"`.
5. `console.Phase("Split epic")` writes the phase message.
6. `ReadCurrentSelectionAsync(cancellationToken)` starts.
7. `ReadCurrentSelectionAsync` checks cancellation.
8. It reads required `.agents/selection.md`.
9. It resolves `.agents/projections/select-next-epic.md`.
10. It reads the `SelectNextEpic` projection.
11. If that projection is missing or empty, it throws `RoadmapStepException`.
12. It loads current roadmap state from `stateStore`.
13. It captures the current selection cycle through `selectionProvenance.CaptureCurrentCycleAsync`.
14. It evaluates active-selection freshness through `selectionProvenance.EvaluateActiveSelectionFreshnessAsync`.
15. If the selection is stale, it throws `RoadmapStepException`.
16. It returns the selection content.
17. `contractRegistry.Get("SplitEpic")` loads the split prompt contract.
18. `projectionCache.EnsureAsync("SplitEpic", projectContext, contract, cancellationToken)` starts.
19. `ProjectionCache` resolves `SplitEpic` from `ProjectionRegistry`.
20. It creates current projection provenance from the projection definition and project context.
21. It reads `.agents/projections/split-epic.md`.
22. If projection content is missing or empty, it renders and runs `ProjectionForSplitEpic`.
23. If generated projection content is empty, it throws `RoadmapStepException`.
24. It validates the projection with `ProjectionValidator`.
25. It hashes the projection content.
26. It loads the projection manifest.
27. It finds any previous manifest entry for `SplitEpic`.
28. It computes validation status.
29. It computes projection freshness.
30. It builds the new or updated manifest entry.
31. It upserts the manifest entry before later projection blocking or writing.
32. If validation failed, it writes numbered projection blocker evidence and throws `RoadmapStepException`.
33. If projection content was generated, it writes `.agents/projections/split-epic.md`.
34. If the projection is stale and the contract policy blocks stale projections, it writes numbered projection blocker evidence and throws `RoadmapStepException`.
35. It returns the projection cache result.
36. `contextBuilder.BuildCreateOrSplitContext(projection.Content, selection)` builds runtime context.
37. The context contains `Projection Content`.
38. The context contains `Selection Proposal`.
39. The context contains repository inspection instructions.
40. The context is rejected if it contains raw project-context markers.
41. `RunPromptTransitionWithCompletionAsync` starts with from `SplitEpicProposed`, to `SplitChildSelection`, prompt `SplitEpic`, projection `.agents/projections/split-epic.md`, secondary input equal to the selection, and output path `.agents/splits`.
42. `inputResolver.ResolveAsync` starts.
43. It adds the projection path as a required projection input.
44. It recognizes `SplitEpic` and adds `.agents/selection.md` as a required selection input.
45. It snapshots the required artifacts by reading their contents and hashes.
46. It creates projection identity with the projection hash.
47. It hashes the rendered prompt context.
48. It hashes the secondary selection input.
49. It computes the transition snapshot hash.
50. It returns the input snapshot.
51. `RunPromptTransitionWithCompletionAsync` creates a correlation ID.
52. It captures `started = DateTimeOffset.UtcNow`.
53. It starts a stopwatch.
54. It calls `SaveStateAsync` with current state `SplitChildSelection`, status `Started`, from `SplitEpicProposed`, to `SplitChildSelection`, output `.agents/splits`, and decision `Pending`.
55. `SaveStateAsync` loads existing state.
56. It loads the projection manifest.
57. It reads active artifact statuses for roadmap completion context, selection, and active epic.
58. It reads the last decision ID from the decision ledger.
59. It carries forward retired epics and blockers from existing state.
60. It counts existing split-family JSON documents.
61. It saves `.agents/state.json`.
62. `journalStore.AppendAsync` appends `TransitionStarted` to `.agents/journal/transitions.jsonl`.
63. `promptRunner.RunRuntimePromptAsync("SplitEpic", context, selection, cancellationToken)` starts.
64. `RoadmapPromptCatalog.RenderRuntime` renders the runtime prompt.
65. The implementation-first prompt policy is appended.
66. `RunOneShotAsync` creates a `ConsoleTurnRenderer`.
67. The agent runtime is called with `AgentSpecs.ReadOnlyPlanning(repository)`.
68. Agent output streams through the console renderer.
69. If the agent turn does not complete, `RunOneShotAsync` throws `RoadmapStepException`.
70. If the renderer was silent, it echoes the output.
71. The prompt output returns.
72. `RunPromptTransitionWithCompletionAsync` stops the stopwatch.
73. It captures `completed = DateTimeOffset.UtcNow`.
74. It appends `TransitionCompleted` to the transition journal.
75. It calls `SaveStateAsync` with current state `SplitChildSelection`, status `Completed`, and decision `Completed`.
76. It returns `PromptTransitionCompletion`.
77. `SplitEpicAsync` calls `bundleExtractor.Extract(completion.Output, BundleExtractionPolicy.RepositorySafe)`.
78. The extractor searches for `# FILE:` markers.
79. If no markers exist, it returns a blocked bundle result.
80. For each marker, it normalizes path separators.
81. It rejects rooted paths and paths containing `..`.
82. It allows any repository-safe path at this layer.
83. It rejects duplicate `# FILE:` targets by throwing `RoadmapStepException`.
84. It trims separator noise around each extracted file body.
85. It hashes each extracted file body.
86. It returns the bundle extraction result.
87. If extraction threw `RoadmapStepException`, `SplitEpicAsync` creates an invalid split interpretation for that exception.
88. On extraction exception, `SplitEpicAsync` calls `BlockSplitEpicAsync`.
89. If extraction returned normally, `splitBundleInterpreter.Interpret(bundle, completion.Output)` starts.
90. If the bundle is blocked and the raw output has a top-level blocked heading, interpretation status is `Blocked`.
91. If the bundle is blocked without a blocked heading, interpretation status is `Invalid`.
92. If extracted files are empty, interpretation status is `Invalid`.
93. For each extracted file, the interpreter requires a path matching `.agents/epic-N.md`.
94. It classifies file content with `EpicAuthoringOutputClassifier`.
95. It rejects non-promotable child content.
96. It validates promotable child content with `EpicArtifactValidator`.
97. It rejects invalid child epic content.
98. It orders valid child epics by numeric child order, then by path.
99. If any rejected files exist, interpretation status is `Invalid`, even if some child epics were valid.
100. If no valid child epics remain, interpretation status is `Invalid`.
101. If interpretation is valid, the first ordered child is selected.
102. If interpretation is not valid, `SplitEpicAsync` calls `BlockSplitEpicAsync`.
103. For a valid interpretation, `BundleExtractionResult.Extracted(interpretation.ValidatedChildEpics)` creates the validated bundle.
104. `bundleExtractor.WriteExtractedFilesAsync` writes every validated child epic to its extracted path.
105. `bundleManifestWriter.WriteAsync` writes the default bundle manifest for the validated children.
106. For `.agents/epic-N.md` children, the default manifest path is `.agents/bundle-manifest.md`.
107. The manifest records source prompt `SplitEpic`, the projection path, expected file count, validation result `Valid`, and extracted file hashes sorted by path.
108. For each validated child epic, `lifecycleStore.UpsertAsync(child.Path, Draft, "Validated split child epic.")` runs.
109. For each validated child epic, `CaptureHitlRequestsAsync(child.Path, child.Content)` runs.
110. If HITL capture is unavailable or content is blank, capture is skipped.
111. `SplitEpicAsync` reads `interpretation.SelectedChild`.
112. If no selected child exists despite valid interpretation, it throws `RoadmapStepException`.
113. A new `SplitFamily` is built.
114. The family ID is the first eight characters of a new GUID formatted without separators.
115. The family proposal is the selection content.
116. Child epic paths are all validated child paths.
117. Dependency order is the same ordered child path list.
118. Selected child path and rationale come from the interpretation.
119. Created-at timestamp is `DateTimeOffset.UtcNow`.
120. `splitFamilyStore.WriteAsync(family)` writes `.agents/splits/split-family-{id}.json`.
121. `childPromotionCompletion = completion with { Output = selectedChild.Content }` replaces raw split output with selected child content while preserving prompt timing, correlation, and input snapshot.
122. `PromoteActiveEpicAsync` starts with from `SplitChildSelection`, prompt `SplitEpic`, projection `.agents/projections/split-epic.md`, the child promotion completion, and lifecycle note `Promoted split child {selectedChild.Path} by SplitEpic.`
123. `promotionService.PromoteAsync` receives target `.agents/epic.md`, selected child content, blocker evidence directory, evidence stem `active-epic-promotion`, artifact name `active epic`, `EpicAuthoringOutputClassifier`, `EpicArtifactValidator`, lifecycle state `Ready`, and the lifecycle note.
124. The promotion service classifies the selected child content.
125. It validates the selected child content.
126. If promotable and valid, it writes `.agents/epic.md`.
127. It marks `.agents/epic.md` lifecycle `Ready` with the split-child promotion note.
128. It returns a promoted result.
129. `PromoteActiveEpicAsync` captures a fresh `completed = DateTimeOffset.UtcNow`.
130. It captures HITL requests from `.agents/epic.md`.
131. It appends `ArtifactPromoted` to the transition journal with prompt contract key `ArtifactPromotionService` and output `.agents/epic.md`.
132. It calls `SaveStateAsync` with current state `ActiveEpicReady`, status `Completed`, from `SplitChildSelection`, to `ActiveEpicReady`, output `.agents/epic.md`, and decision `Artifact Promoted`.
133. It returns the promoted result.
134. `SplitEpicAsync` returns the promoted result.
135. `ContinueAfterSelectionAsync` observes `splitPromotion.Promoted == true`.
136. The caller breaks out of the selection switch.
137. The caller invokes `GenerateMilestoneSpecsAsync(projectContext, cancellationToken)`.
138. Milestone generation is downstream and outside this transition audit.

Blocked or invalid split-output trace:

1. Steps 1 through 102 above occur.
2. `BlockSplitEpicAsync` computes `reason = DescribeSplitInterpretation(interpretation)`.
3. It renders blocker evidence with the reason, rejected file list, and raw prompt output.
4. It writes numbered evidence at `.agents/evidence/blockers/split-epic-output.NNNN.md`.
5. It marks the blocker evidence lifecycle `Blocked`.
6. It captures `completed = DateTimeOffset.UtcNow`.
7. It sets decision to `Split Epic Blocked` for interpretation status `Blocked`, otherwise `Split Bundle Rejected`.
8. It maps blocked interpretation to `ArtifactPromotionStatus.Blocked`, otherwise `StructurallyInvalid`.
9. It appends `SplitBundleRejected` to the transition journal from `SplitChildSelection` to `EvidenceBlocked`.
10. It calls `SaveStateAsync` with current state `EvidenceBlocked`, status `Paused`, output equal to the evidence path, blocker row pointing to the evidence path, transition intent `ResolveSplitEpicBlocker`, and next transition `Resolve blocker and rerun`.
11. It returns `ArtifactPromotionResult.NotPromoted(status, .agents/epic.md, evidencePath, reason)`.
12. `ContinueAfterSelectionAsync` sees `!splitPromotion.Promoted` and returns `RoadmapOutcome.Paused`.

Prompt runtime failure trace:

1. Steps 1 through 68 of the successful trace occur.
2. The prompt runner or runtime throws an exception that is not `OperationCanceledException`.
3. `RunPromptTransitionWithCompletionAsync` stops the stopwatch.
4. It appends `TransitionFailed` to the journal.
5. It saves state as `EvidenceBlocked`, status `Failed`, from `SplitEpicProposed`, to `SplitChildSelection`, output `.agents/splits`, decision `Failed`, transition intent `ResolveTransitionFailure`, and next transition `Resolve blocker and rerun`.
6. It throws an already-persisted `RoadmapStepException`.
7. `RunAsync` logs the error and returns `RoadmapOutcome.Failed`.

Selected-child promotion rejection trace:

1. The successful trace reaches selected-child promotion.
2. `promotionService.PromoteAsync` classifies or validates the selected child as not promotable.
3. It writes numbered evidence under `.agents/evidence/blockers/active-epic-promotion.NNNN.md`.
4. It marks that evidence lifecycle `Blocked`.
5. `PromoteActiveEpicAsync` appends `ArtifactPromotionBlocked`.
6. It saves state as `EvidenceBlocked`, status `Paused`, from `SplitChildSelection`, to `ActiveEpicReady`, output equal to the promotion evidence path, transition intent `ResolveArtifactPromotionBlocker`, and next transition `Resolve blocker and rerun`.
7. It returns the not-promoted result.
8. The caller pauses.

## 3. Concern Inventory

| Step | Concern | Where It Occurs | Mixed With |
|---|---|---|---|
| Select branch routing | routing | `ContinueAfterSelectionAsync` | downstream milestone continuation |
| Announce phase | reporting | `console.Phase("Split epic")` | transition setup |
| Read active selection | artifact read | `ReadCurrentSelectionAsync` | freshness validation |
| Check cancellation | recovery/control | `ReadCurrentSelectionAsync` | artifact read |
| Read `SelectNextEpic` projection | artifact read | `ReadCurrentSelectionAsync` | selection provenance |
| Load persisted state | persistence read | `ReadCurrentSelectionAsync` | selection validation |
| Capture current selection cycle | provenance | `ReadCurrentSelectionAsync` | validation |
| Evaluate selection freshness | validation | `ReadCurrentSelectionAsync` | artifact read and state read |
| Load prompt contract | routing/contract lookup | `contractRegistry.Get` | none |
| Ensure projection | projection cache | `projectionCache.EnsureAsync` | artifact read/write, validation, manifest persistence, prompt execution |
| Build prompt context | prompt preparation | `BuildCreateOrSplitContext` | validation against raw project context |
| Resolve transition inputs | input snapshot | `TransitionInputResolver` | artifact reads and hashing |
| Persist started state | persistence | `RunPromptTransitionWithCompletionAsync` | journaling setup |
| Append started journal | journaling | `RunPromptTransitionWithCompletionAsync` | prompt orchestration |
| Run runtime prompt | prompt execution | `RoadmapPromptRunner` | console streaming and prompt policy |
| Persist completed state | persistence | `RunPromptTransitionWithCompletionAsync` | journaling and timing |
| Append completed journal | journaling | `RunPromptTransitionWithCompletionAsync` | prompt orchestration |
| Extract bundle files | parsing | `BundleFileExtractor` | repository-safety validation and hashing |
| Interpret split bundle | parsing/validation/decision | `SplitEpicBundleInterpreter` | child ordering and selection |
| Preserve split blocker | recovery/artifact write | `BlockSplitEpicAsync` | lifecycle, journaling, state mutation |
| Write child epics | artifact write | `WriteExtractedFilesAsync` | after bundle validation |
| Write bundle manifest | artifact write/provenance | `BundleManifestWriter` | validation reporting |
| Mark children draft | lifecycle | `SplitEpicAsync` loop | HITL capture |
| Capture HITL requests | side effect/reporting | `CaptureHitlRequestsAsync` | lifecycle loop |
| Build split family | state modeling | `SplitEpicAsync` | child selection and timestamp generation |
| Persist split family | artifact write/persistence | `SplitFamilyStore` | no journal entry |
| Replace prompt output with selected child | data transformation | `completion with { Output = selectedChild.Content }` | promotion preparation |
| Promote selected child | artifact promotion | `PromoteActiveEpicAsync` | classifier, validator, lifecycle, journal, state |
| Return result | routing contract | `SplitEpicAsync` and caller | downstream pause/continue decision |

Concerns become most mixed in four places:

1. `RunPromptTransitionWithCompletionAsync` combines input snapshotting, timestamps, state persistence, journaling, prompt execution, console output, and failure recovery.
2. `SplitEpicAsync` combines transition orchestration, bundle parsing, child artifact materialization, lifecycle updates, HITL capture, split-family persistence, selected-child choice, and promotion routing.
3. `BlockSplitEpicAsync` combines rendering evidence, writing evidence, lifecycle updates, journal append, state persistence, blocker creation, transition intent, and return-value construction.
4. `PromoteActiveEpicAsync` combines domain validation, active artifact write, lifecycle updates, HITL capture, journal append, persisted state update, blocker handling, and promotion result construction.

## 4. Hidden Steps

These steps are present in helpers and are easy to miss when reading only `SplitEpicAsync`.

Build Current Selection

- Reads `.agents/selection.md`.
- Reads `.agents/projections/select-next-epic.md`.
- Loads persisted state.
- Captures current selection-cycle input snapshot.
- Verifies the active selection is fresh.

Resolve Split Projection

- Reads or generates `.agents/projections/split-epic.md`.
- Validates projection content.
- Updates `.agents/projections/manifest.json`.
- Writes projection content if generated.
- Writes blocker evidence on invalid or stale projections.

Build Runtime Context

- Concatenates projection content, selection proposal, and repository inspection instructions.
- Rejects raw project-context markers.

Capture Transition Snapshot

- Reads and hashes the `SplitEpic` projection.
- Reads and hashes `.agents/selection.md`.
- Hashes rendered prompt context.
- Hashes secondary input.
- Computes a transition snapshot hash.

Persist Prompt Start

- Saves `.agents/state.json` with `CurrentState = SplitChildSelection`, status `Started`, output `.agents/splits`, and decision `Pending`.
- Appends `TransitionStarted`.

Normalize Prompt Execution

- Renders runtime prompt.
- Appends implementation-first policy.
- Runs the read-only planning agent.
- Streams or echoes console output.
- Requires completed agent state.

Persist Prompt Completion

- Appends `TransitionCompleted`.
- Saves `.agents/state.json` with status `Completed`.

Normalize Bundle Output

- Finds `# FILE:` markers.
- Normalizes path separators.
- Rejects rooted or parent-traversal paths.
- Rejects duplicate targets.
- Trims extracted bodies.
- Hashes each extracted body.

Validate Split Contract

- Requires `.agents/epic-N.md` targets.
- Classifies child content as epic authoring output.
- Validates required active-epic sections and metadata.
- Orders children by numeric suffix.
- Selects the first ordered child.

Record Blocked Evidence

- Converts interpreter status into user-visible decision text.
- Renders reason, rejected files, and raw output into durable evidence.
- Marks evidence lifecycle blocked.
- Appends a recovery journal record.
- Saves `EvidenceBlocked` state and transition intent.

Materialize Child Drafts

- Writes all validated child epic files.
- Writes `.agents/bundle-manifest.md`.
- Marks children `Draft`.
- Captures HITL requests.

Capture Split Family

- Generates a short family ID.
- Stores selection proposal, child paths, dependency order, selected child, rationale, and timestamp.
- Saves `.agents/splits/split-family-{id}.json`.

Promote Selected Child

- Reuses the original prompt completion metadata.
- Substitutes selected child content for raw split output.
- Runs active-epic classification and validation.
- Writes `.agents/epic.md`.
- Marks active epic `Ready`.
- Appends promotion journal.
- Saves `ActiveEpicReady` state.

## 5. Natural Step Boundaries

Step 1: Enter Split Branch

Purpose

Route the `"Select Split Epic"` decision into the split transition.

Inputs

- `SelectionDecision`
- `ProjectContext`
- `CancellationToken`

Outputs

- Call to `SplitEpicAsync`
- Caller-visible `RoadmapOutcome.Paused` when split is not promoted
- Downstream milestone generation when split is promoted

---

Step 2: Validate Active Selection

Purpose

Ensure the split prompt uses the current, non-stale selection artifact.

Inputs

- `.agents/selection.md`
- `.agents/projections/select-next-epic.md`
- persisted roadmap state
- selection provenance manifest

Outputs

- selection content
- exception when selection is unusable

---

Step 3: Resolve Split Contract And Projection

Purpose

Prepare the exact `SplitEpic` projection and prompt contract.

Inputs

- `PromptContractRegistry`
- `ProjectionRegistry`
- project context
- projection manifest
- optional existing `.agents/projections/split-epic.md`

Outputs

- `PromptContract`
- `ProjectionCacheResult`
- possible projection artifact write
- possible projection manifest write
- possible projection blocker evidence

---

Step 4: Build Prompt Context

Purpose

Create the runtime context consumed by the `SplitEpic` prompt.

Inputs

- projection content
- selection content

Outputs

- rendered context with projection, selection proposal, and repository inspection instructions

---

Step 5: Run Persisted Split Prompt

Purpose

Run the agent prompt with durable transition state and journal records.

Inputs

- from state `SplitEpicProposed`
- to state `SplitChildSelection`
- prompt name `SplitEpic`
- projection path
- rendered context
- selection as secondary input
- output path `.agents/splits`

Outputs

- `PromptTransitionCompletion`
- `.agents/state.json` started and completed records
- `TransitionStarted` and `TransitionCompleted` journal records
- persisted failure state on runtime failure

---

Step 6: Extract Bundle

Purpose

Turn raw prompt output into repository-safe file candidates.

Inputs

- raw prompt output
- `BundleExtractionPolicy.RepositorySafe`

Outputs

- `BundleExtractionResult`
- extraction exception converted into invalid split interpretation

---

Step 7: Interpret Split Bundle

Purpose

Decide whether extracted files are valid child epic drafts and choose the first child.

Inputs

- extracted bundle
- raw prompt output
- epic output classifier
- epic artifact validator

Outputs

- `SplitEpicBundleInterpretation`
- ordered validated child epic list
- selected child
- rejection list when invalid

---

Step 8: Persist Split Blocker

Purpose

Preserve blocked or invalid split output without mutating child epics or active epic.

Inputs

- runtime prompt
- projection path
- prompt completion metadata
- split interpretation

Outputs

- `.agents/evidence/blockers/split-epic-output.NNNN.md`
- lifecycle `Blocked` row for evidence
- `SplitBundleRejected` journal record
- `EvidenceBlocked` state
- `ResolveSplitEpicBlocker` transition intent
- not-promoted result

---

Step 9: Materialize Child Epics

Purpose

Write all validated children as draft artifacts after the entire bundle is proven acceptable.

Inputs

- ordered validated child epics

Outputs

- `.agents/epic-N.md` child files
- `.agents/bundle-manifest.md`
- lifecycle `Draft` rows
- optional HITL captures

---

Step 10: Persist Split Family

Purpose

Record how the child epic set relates to the original proposal and selected child.

Inputs

- selection content
- child epic paths
- selected child path
- selected child rationale
- current timestamp
- generated family ID

Outputs

- `.agents/splits/split-family-{id}.json`

---

Step 11: Promote Selected Child

Purpose

Make the selected child the active epic while preserving promotion behavior.

Inputs

- selected child content
- original prompt completion metadata
- active-epic promotion service

Outputs

- `.agents/epic.md` on success
- lifecycle `Ready` row for `.agents/epic.md`
- `ArtifactPromoted` journal record
- `ActiveEpicReady` state
- promotion blocker evidence and `EvidenceBlocked` state on rejection

---

Step 12: Return To Caller

Purpose

Tell `ContinueAfterSelectionAsync` whether it may proceed into milestone generation.

Inputs

- `ArtifactPromotionResult`

Outputs

- promoted result continues to downstream milestone generation
- not-promoted result returns `RoadmapOutcome.Paused`

## 6. Mixed-Concern Analysis

Step 1 mixes routing and downstream orchestration.

The branch both invokes `SplitEpicAsync` and decides whether to pause or continue into milestone generation. A reader must keep the transition boundary and caller continuation in mind at the same time.

Step 2 mixes artifact reads, provenance, validation, and state reads.

`ReadCurrentSelectionAsync` sounds like a simple artifact read, but it also checks projection presence, loads roadmap state, captures provenance, and enforces freshness. This hides a transition precondition behind a read helper.

Step 3 mixes projection cache behavior with prompt execution.

`projectionCache.EnsureAsync` may only read a projection, but it may also run a projection prompt, update manifests, write projection files, or write blocker evidence. A reader cannot infer the side effects from `EnsureAsync` alone.

Step 4 is mostly clean.

It builds a context and validates that raw project-context markers are absent. The concern mix is limited and linear.

Step 5 mixes prompt execution with durable workflow persistence.

`RunPromptTransitionWithCompletionAsync` resolves inputs, hashes artifacts, saves state, appends journals, executes the prompt, times it, handles exceptions, saves failure state, and returns the prompt output. This is useful shared machinery, but for transition readability it hides the real order of state and journal writes.

Step 6 mixes parsing with repository safety.

`BundleFileExtractor` both extracts file bodies and enforces path safety. This is reasonable locally, but in the transition trace it matters because an exception here routes into split-specific blocker persistence.

Step 7 mixes validation and child-selection policy.

`SplitEpicBundleInterpreter` validates path shape, content classification, active-epic structure, child ordering, and selected-child choice. The selection policy is not visible at the call site.

Step 8 mixes recovery, evidence, lifecycle, journaling, state, and return construction.

`BlockSplitEpicAsync` performs every externally observable blocked-transition effect. The step is harder to scan because it builds user-facing evidence, appends journal history, mutates lifecycle, persists state, creates transition intent, and returns an artifact-promotion-shaped result.

Step 9 mixes artifact writing with lifecycle and HITL scanning.

Child files are written, then lifecycle and optional HITL capture happen in the same loop. A reader has to notice that child files are only written after the entire bundle interpretation is valid.

Step 10 mixes identity generation with persistence.

The split family uses a nondeterministic ID and timestamp, then writes structured JSON. There is no journal event for this write, so it is easy to miss as a side effect.

Step 11 mixes promotion validation with workflow transition completion.

`PromoteActiveEpicAsync` delegates artifact writes to `ArtifactPromotionService`, then adds HITL capture, journal records, and state persistence. The promotion result controls caller routing, so validation and state-machine flow are tightly coupled.

Step 12 mixes transition return with caller behavior.

The transition returns only `ArtifactPromotionResult`, but the caller immediately maps it to pause or downstream milestone generation. This makes the extraction boundary important: the handler should end at the returned promotion result, not absorb milestone generation.

## 7. Data Flow

Selection decision

Origin

- Parsed by `SelectNextEpic` before this transition.

Consumed By

- `ContinueAfterSelectionAsync` branch routing.

Active selection content

Origin

- `.agents/selection.md`.

Consumed By

- selection freshness validation
- `BuildCreateOrSplitContext`
- runtime prompt secondary input
- split family `Proposal`

SelectNextEpic projection

Origin

- `.agents/projections/select-next-epic.md`.

Consumed By

- `ReadCurrentSelectionAsync` to validate active selection freshness.

Persisted roadmap state

Origin

- `.agents/state.json`, with possible legacy migration from `.agents/state.md`.

Consumed By

- `ReadCurrentSelectionAsync` for retired epics in selection-cycle freshness.
- `SaveStateAsync` for carried-forward retired epics, blockers, transition intent, and next transitions.

Project context

Origin

- Loaded by preflight before the transition.

Consumed By

- `projectionCache.EnsureAsync` for projection provenance and optional projection generation.

Prompt contract

Origin

- `PromptContractRegistry.Get("SplitEpic")`.

Consumed By

- `projectionCache.EnsureAsync` for stale projection policy.

Split projection

Origin

- Existing `.agents/projections/split-epic.md` or generated `ProjectionForSplitEpic`.

Consumed By

- `BuildCreateOrSplitContext`
- `TransitionInputResolver`
- journal records
- state last-transition summary
- bundle manifest source metadata

Rendered context

Origin

- `RoadmapPromptContextBuilder.BuildCreateOrSplitContext`.

Consumed By

- `TransitionInputResolver` for prompt-context hash.
- `RoadmapPromptRunner` for runtime prompt rendering.

Secondary input

Origin

- active selection content.

Consumed By

- `TransitionInputResolver` for secondary-input hash.
- `RoadmapPromptCatalog.RenderRuntime("SplitEpic", context, selection)`.

Input snapshot

Origin

- `TransitionInputResolver.ResolveAsync`.

Consumed By

- `TransitionStarted`, `TransitionCompleted`, `SplitBundleRejected`, `ArtifactPromoted`, or `ArtifactPromotionBlocked` journal records.

Raw prompt output

Origin

- `promptRunner.RunRuntimePromptAsync("SplitEpic", context, selection)`.

Consumed By

- `BundleFileExtractor`
- `SplitEpicBundleInterpreter`
- blocker evidence rendering

Bundle extraction result

Origin

- `BundleFileExtractor.Extract(rawOutput, RepositorySafe)`.

Consumed By

- `SplitEpicBundleInterpreter`
- invalid extraction conversion on exception

Split interpretation

Origin

- `SplitEpicBundleInterpreter.Interpret(bundle, rawOutput)`.

Consumed By

- blocked/rejected evidence path
- child materialization
- selected child selection
- split family construction

Validated child epics

Origin

- interpretation `ValidatedChildEpics`.

Consumed By

- `WriteExtractedFilesAsync`
- `BundleManifestWriter`
- lifecycle upsert
- HITL capture
- split family `ChildEpicPaths` and `DependencyOrder`

Selected child

Origin

- first validated child by numeric `.agents/epic-N.md` order.

Consumed By

- split family `SelectedChildPath`
- child-promotion completion output
- active-epic promotion

Split family

Origin

- `new SplitFamily(...)` inside `SplitEpicAsync`.

Consumed By

- `SplitFamilyStore.WriteAsync`, which writes `.agents/splits/split-family-{id}.json`.

Promotion candidate

Origin

- selected child content substituted into the prompt completion.

Consumed By

- `ArtifactPromotionService.PromoteAsync`.

Active epic

Origin

- selected child content on successful promotion.

Consumed By

- `.agents/epic.md`
- active epic lifecycle row
- downstream milestone generation outside this audit

Blocker evidence

Origin

- `BlockSplitEpicAsync` for split-output problems.
- `ArtifactPromotionService.PreserveEvidenceAsync` for promotion problems.

Consumed By

- lifecycle store
- transition journal
- persisted state blocker rows
- transition intent evidence paths

Persisted state

Origin

- `SaveStateAsync`.

Consumed By

- later roadmap runs, status, resume, and unblock planning.

## 8. Human Navigation Audit

Minimum files an engineer must read to understand this transition:

- `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapPromptContextBuilder.cs`
- `src/LoopRelay.Roadmap.Cli/TransitionInputs.cs`
- `src/LoopRelay.Roadmap.Cli/PromptContractRegistry.cs`
- `src/LoopRelay.Roadmap.Cli/ProjectionRegistry.cs`
- `src/LoopRelay.Roadmap.Cli/ProjectionCache.cs`
- `src/LoopRelay.Roadmap.Cli/BundleFileExtractor.cs`
- `src/LoopRelay.Roadmap.Cli/SplitEpicBundleInterpreter.cs`
- `src/LoopRelay.Roadmap.Cli/SplitFamily.cs`
- `src/LoopRelay.Roadmap.Cli/SplitFamilyStore.cs`
- `src/LoopRelay.Roadmap.Cli/BundleManifestWriter.cs`
- `src/LoopRelay.Roadmap.Cli/ArtifactPromotion.cs`
- `src/LoopRelay.Roadmap.Cli/EpicArtifactPromotion.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapArtifactPaths.cs`

Helper methods in `RoadmapStateMachine.cs`:

- `ContinueAfterSelectionAsync`
- `SplitEpicAsync`
- `BlockSplitEpicAsync`
- `DescribeSplitInterpretation`
- `RenderSplitInterpretationEvidence`
- `RunPromptTransitionWithCompletionAsync`
- `PromoteActiveEpicAsync`
- `ReadCurrentSelectionAsync`
- `CaptureHitlRequestsAsync`
- `SaveStateAsync`
- `ActiveArtifactRowsAsync`
- `NextTransitions`

Services:

- `RoadmapPromptRunner`
- `ProjectionCache`
- `TransitionInputResolver`
- `BundleFileExtractor`
- `SplitEpicBundleInterpreter`
- `BundleManifestWriter`
- `SplitFamilyStore`
- `ArtifactPromotionService`
- `ArtifactLifecycleStore`
- `TransitionJournalStore`
- `RoadmapStateStore`
- selection provenance service used by `ReadCurrentSelectionAsync`

Models:

- `PromptTransitionCompletion`
- `PromptContract`
- `ProjectionCacheResult`
- `TransitionInputSnapshot`
- `BundleExtractionResult`
- `ExtractedBundleFile`
- `SplitEpicBundleInterpretation`
- `SplitEpicBundleRejection`
- `SplitFamily`
- `ArtifactPromotionResult`
- `RoadmapStateDocument`
- `TransitionJournalRecord`

Persistence objects:

- `.agents/selection.md`
- `.agents/projections/select-next-epic.md`
- `.agents/projections/split-epic.md`
- `.agents/projections/manifest.json`
- `.agents/state.json`
- `.agents/journal/transitions.jsonl`
- `.agents/epic-N.md`
- `.agents/bundle-manifest.md`
- `.agents/splits/split-family-{id}.json`
- `.agents/epic.md`
- `.agents/artifacts/lifecycle.json`
- `.agents/evidence/blockers/split-epic-output.NNNN.md`
- `.agents/evidence/blockers/active-epic-promotion.NNNN.md`

Parsers and validators:

- `BundleFileExtractor`
- `SplitEpicBundleInterpreter`
- `EpicAuthoringOutputClassifier`
- `EpicArtifactValidator`
- `ProjectionValidator`
- `MarkdownTableParser`, indirectly through epic validation

Lifecycle code:

- `ArtifactLifecycleStore.UpsertAsync`
- `ArtifactLifecycleState.Draft`
- `ArtifactLifecycleState.Ready`
- `ArtifactLifecycleState.Blocked`

Reporting code:

- `console.Phase`
- `ConsoleTurnRenderer`
- transition journal append calls
- blocker evidence rendering in `RenderSplitInterpretationEvidence`

Tests that capture behavior:

- `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapStateMachineSplitTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/SplitEpicBundleInterpreterTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/SplitFamilyStoreTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapFailurePersistenceTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/PromptContractRegistryTests.cs`

## 9. Extraction Boundary

Smallest extraction boundary:

`SplitEpicAsync` plus the split-specific blocked-output helper logic should become one named transition handler.

Natural handler ownership:

- one entry: execute `SplitEpic` for a fresh active selection and project context
- one normal exit: return `ArtifactPromotionResult`
- one linear flow: validate selection, resolve projection, run prompt, interpret bundle, materialize child drafts, persist family, promote selected child
- one blocked-output branch: persist split-output blocker and return not promoted
- one promotion-blocked branch: delegate to existing active-epic promotion behavior and return not promoted

Keep outside the handler:

- `SelectNextEpic` decision parsing
- caller decision to continue into `GenerateMilestoneDeepDivesForEpic`
- generic projection cache internals
- generic transition prompt runner
- generic artifact promotion service
- generic state store and journal store implementations
- unblock planning

The extraction should not change the state model or add new commands. The natural seam is an explicit transition handler that owns the current `SplitEpicAsync` workflow and calls existing shared services in the same order.

Candidate handler name:

`SplitEpicTransitionHandler`

Candidate method:

`ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)`

## 10. Required Inputs

Required

- `ProjectContext`
- `CancellationToken`
- active selection content from `.agents/selection.md`
- `SelectNextEpic` projection content for selection freshness validation
- persisted roadmap state for selection freshness and state carry-forward
- selection provenance service
- `PromptContractRegistry` or resolved `SplitEpic` contract
- `ProjectionCache` or resolved `SplitEpic` projection content and path
- `RoadmapPromptContextBuilder`
- transition input resolver
- prompt runner
- state store
- transition journal store
- roadmap artifact store
- bundle extractor
- split bundle interpreter
- bundle manifest writer
- lifecycle store
- split family store
- active-epic promotion service
- current time source, currently `DateTimeOffset.UtcNow`
- family ID source, currently `Guid.NewGuid().ToString("N")[..8]`

Optional

- HITL request capture service
- existing `.agents/epic.md`, only to preserve it when split output is invalid or blocked
- generated projection content, only when `.agents/projections/split-epic.md` is missing

Incidental

- decision ledger store is not written by `SplitEpic`, but `SaveStateAsync` reads last decision ID for state summary.
- projection manifest counts are not split-specific, but `SaveStateAsync` includes them in persisted state.
- active artifact rows for roadmap completion context, selection, and active epic are state-summary data rather than split-specific inputs.
- console is only needed to preserve current phase and agent streaming behavior.

## 11. Required Outputs

Returned value

- `ArtifactPromotionResult.PromotedResult(".agents/epic.md")` on successful selected-child promotion.
- `ArtifactPromotionResult.NotPromoted(...)` on split-output blocked/rejected path.
- `ArtifactPromotionResult.NotPromoted(...)` on active-epic promotion rejection.

Persisted state

- Started prompt state: current `SplitChildSelection`, status `Started`, from `SplitEpicProposed`, to `SplitChildSelection`, output `.agents/splits`, decision `Pending`.
- Completed prompt state: current `SplitChildSelection`, status `Completed`, from `SplitEpicProposed`, to `SplitChildSelection`, output `.agents/splits`, decision `Completed`.
- Successful final state: current `ActiveEpicReady`, status `Completed`, from `SplitChildSelection`, to `ActiveEpicReady`, output `.agents/epic.md`, decision `Artifact Promoted`.
- Split-output blocker state: current `EvidenceBlocked`, status `Paused`, from `SplitChildSelection`, to `EvidenceBlocked`, output blocker evidence path, decision `Split Epic Blocked` or `Split Bundle Rejected`.
- Promotion blocker state: current `EvidenceBlocked`, status `Paused`, from `SplitChildSelection`, to `ActiveEpicReady`, output promotion evidence path, promotion rejection decision.
- Runtime failure state: current `EvidenceBlocked`, status `Failed`, from `SplitEpicProposed`, to `SplitChildSelection`, output `.agents/splits`, decision `Failed`.

Artifacts

- possible `.agents/projections/split-epic.md`
- possible projection blocker evidence
- `.agents/epic-N.md` child epic files on valid split
- `.agents/bundle-manifest.md` on valid split
- `.agents/splits/split-family-{id}.json` on valid split
- `.agents/epic.md` on successful selected-child promotion
- `.agents/evidence/blockers/split-epic-output.NNNN.md` on invalid or blocked split output
- `.agents/evidence/blockers/active-epic-promotion.NNNN.md` on promotion rejection

Lifecycle

- child epic files marked `Draft` with note `Validated split child epic.`
- selected active epic marked `Ready` with note `Promoted split child {path} by SplitEpic.`
- split-output blocker evidence marked `Blocked`
- promotion blocker evidence marked `Blocked`

Journals

- `TransitionStarted`
- `TransitionCompleted`
- `SplitBundleRejected` on split-output blocked or invalid
- `ArtifactPromoted` on successful selected-child promotion
- `ArtifactPromotionBlocked` on promotion rejection
- `TransitionFailed` on runtime failure

Decision ledger

- no new decision ledger entry is appended by `SplitEpic`.
- state persistence still carries forward the existing last decision ID.

Console behavior

- phase line `Split epic`
- agent runtime streaming through `ConsoleTurnRenderer`
- output echo if the renderer observed no streamed output
- error logging by outer `RunAsync` on thrown failures

Recovery behavior

- split-output blocker intent: `ResolveSplitEpicBlocker`
- promotion blocker intent: `ResolveArtifactPromotionBlocker`
- runtime failure intent: `ResolveTransitionFailure`
- next transition text: `Resolve blocker and rerun`

Exceptions

- `OperationCanceledException` propagates to `RunAsync`, which writes cancelled state.
- prompt runtime failures are persisted and rethrown as already-persisted `RoadmapStepException`.
- missing or stale selection and projection failures throw `RoadmapStepException`; outer `RunAsync` reports an ephemeral blocker unless the helper already persisted failure state.
- unexpected valid interpretation without a selected child throws `RoadmapStepException`.

## 12. Behavioral Equivalence Contract

Inputs must remain identical:

- The transition must start only from the `"Select Split Epic"` branch.
- It must use the active selection from `.agents/selection.md`.
- It must validate selection freshness through the current selection provenance flow.
- It must use the `SplitEpic` prompt contract.
- It must use `.agents/projections/split-epic.md`.
- It must pass the same rendered context and the selection as secondary input to `SplitEpic`.
- It must resolve transition input snapshots with the same artifact roles and hash behavior.

Prompt and projection behavior must remain identical:

- Projection generation, validation, freshness checks, manifest updates, and projection blocker behavior must be unchanged.
- Runtime prompt rendering must still append the implementation-first prompt policy.
- Runtime execution must still use `AgentSpecs.ReadOnlyPlanning(repository)`.
- Agent turn completion requirements and silent-output echo behavior must be unchanged.

State and journal behavior must remain identical:

- `TransitionStarted` and `TransitionCompleted` must be appended around the prompt run with the same states, prompt, projection, output paths, input hashes, timing behavior, and input snapshot.
- Started and completed state saves must preserve current field values, status values, output values, decision text, transition summaries, manifest counts, split family counts, active artifact rows, blockers, and carried-forward retired epics.
- Runtime failures must persist `EvidenceBlocked` with status `Failed` and intent `ResolveTransitionFailure`.

Bundle behavior must remain identical:

- Bundle extraction must use `BundleExtractionPolicy.RepositorySafe`.
- `# FILE:` marker syntax, path normalization, rooted path rejection, parent traversal rejection, duplicate target rejection, content trimming, and file hashing must remain the same.
- Split interpretation must require `.agents/epic-N.md` paths.
- Child content classification and validation must use `EpicAuthoringOutputClassifier` and `EpicArtifactValidator`.
- Any rejected file must reject the whole split bundle before writing children.
- Selected child must remain the first validated child by numeric child order.

Blocked split-output behavior must remain identical:

- Invalid or blocked split output must not write child files.
- Invalid or blocked split output must not write a split family.
- Invalid or blocked split output must not overwrite `.agents/epic.md`.
- Evidence path stem must remain `split-epic-output`.
- Evidence content must include reason, rejected files, and raw output.
- Decision must remain `Split Epic Blocked` for blocked interpretation and `Split Bundle Rejected` otherwise.
- Journal event must remain `SplitBundleRejected`.
- State must persist `EvidenceBlocked`, status `Paused`, intent `ResolveSplitEpicBlocker`, and the same blocker next-step text.

Successful materialization behavior must remain identical:

- All validated children must be written before active-epic promotion.
- Bundle manifest path and content shape must remain unchanged.
- Child lifecycle rows must be `Draft` with the same note.
- HITL capture must run for each child when configured.
- Split family JSON schema, family ID shape, proposal, child paths, dependency order, selected child path, selected child rationale, and timestamp behavior must remain unchanged.

Promotion behavior must remain identical:

- Promotion must use selected child content, not raw split prompt output.
- The original prompt correlation ID, timing, and input snapshot must be preserved in promotion journal records.
- Target path must remain `.agents/epic.md`.
- Promotion classifier and validator must remain `EpicAuthoringOutputClassifier` and `EpicArtifactValidator`.
- Successful promotion must write `.agents/epic.md`, mark it `Ready`, capture HITL requests, append `ArtifactPromoted`, and save `ActiveEpicReady`.
- Promotion rejection must preserve evidence, mark it `Blocked`, append `ArtifactPromotionBlocked`, and save `EvidenceBlocked` with intent `ResolveArtifactPromotionBlocker`.

Caller behavior must remain identical:

- A promoted split result lets `ContinueAfterSelectionAsync` proceed to `GenerateMilestoneSpecsAsync`.
- A not-promoted split result returns `RoadmapOutcome.Paused`.
- Downstream milestone generation is not part of the extracted handler.

Not part of the behavioral contract:

- local variable names
- private helper boundaries, as long as observable order and effects remain identical
- internal handler class name
- comments or code organization

## 13. Transition Handler Shape

Recovered linear structure:

```text
Execute(projectContext, cancellationToken)

↓

Announce Phase

↓

Resolve Fresh Selection

↓

Resolve Contract And Projection

↓

Build Split Prompt Context

↓

Run Prompt With Transition Persistence

↓

Extract Bundle

↓

Interpret Split Bundle

↓

If Invalid Or Blocked:
    Persist Split Blocker
    Return Not Promoted

↓

Write Validated Child Epics

↓

Write Bundle Manifest

↓

Mark Children Draft

↓

Capture Child HITL Requests

↓

Select First Child

↓

Write Split Family

↓

Promote Selected Child As Active Epic

↓

Return Promotion Result
```

The handler should read in that order. Shared services can keep their current responsibilities, but the transition handler should make the split-specific steps explicit and linear.

## 14. Readability Improvements

Fewer files required for the main story

Today, an engineer must jump from `SplitEpicAsync` into selection freshness, projection cache, prompt transition persistence, bundle extraction, bundle interpretation, family persistence, promotion, lifecycle, state persistence, and journal persistence. Extracting a handler would give the engineer one named place to read the split workflow before drilling into generic services.

Clearer transition boundary

The current caller immediately proceeds into milestone generation after a promoted split. A named `SplitEpic` handler would make it obvious that the split transition ends at `ArtifactPromotionResult`, and milestone generation is downstream.

Explicit blocked-output path

The blocked path is currently split between extraction exception conversion, interpreter status, and `BlockSplitEpicAsync`. A handler can present this as one named branch: interpret output, persist split blocker, return not promoted.

Lower working memory for child materialization

The successful path writes child files, writes a manifest, updates lifecycle, captures HITL requests, writes family metadata, and then promotes the selected child. Extracting the sequence into named local steps would reduce the need to remember which side effects happen before promotion.

Safer understanding of no-write-before-validation behavior

Tests rely on invalid split bundles writing no child files and preserving the previous active epic. A linear handler can make the ordering explicit: interpret entire bundle first, write children only after interpretation is valid.

Easier debugging of persisted state

The transition currently saves state at prompt start, prompt completion, blocked split output, promotion blocked, and promotion success. A handler with named steps would make it easier to match state files and journal events to the exact phase that produced them.

Easier modification of split selection policy

The first-child-by-numeric-order rule lives inside `SplitEpicBundleInterpreter`. A handler can name the selected-child step without changing the interpreter, making later local edits easier to find.

Easier testing

The current tests already isolate invalid bundle, blocked bundle, direct active epic target, valid split, and runtime failure behavior. An extracted handler would give those tests a smaller subject with the same external contract, while keeping integration tests around the caller path.

No behavior redesign required

All improvements come from naming and ordering the existing work. The state names, prompt names, artifacts, contracts, parsers, lifecycle states, journals, and recovery intents remain unchanged.
