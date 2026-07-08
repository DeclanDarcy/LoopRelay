# GenerateMilestoneDeepDivesForEpic Transition Extraction Audit

## Audited Transition

Exactly one transition is audited here: `GenerateMilestoneDeepDivesForEpic`.

Scope: `RoadmapState.ActiveEpicReady` -> `RoadmapState.MilestoneSpecsReady`, implemented by `GenerateMilestoneSpecsAsync` in `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`.

The audit begins when `GenerateMilestoneSpecsAsync` is called and ends when milestone specs are either materialized and the state is persisted as `MilestoneSpecsReady`, or the transition persists a durable blocker/failure and throws an already-persisted `RoadmapStepException`.

Out of scope: selecting the epic, creating or rewriting the active epic, execution prompt generation, operational context generation, completion certification, and unblock handling.

## 1. Transition Narrative

Current State

An active epic has already been selected or authored and is present at `.agents/epic.md`. The roadmap state is ready to turn that epic's milestone roadmap into execution-planning specs.

Goal

Run `GenerateMilestoneDeepDivesForEpic` against the active epic and milestone projection, extract the returned multi-file bundle into `.agents/specs/*.md`, record lifecycle and provenance for those specs, validate that the specs belong to the active epic and current project context, and persist the roadmap state as `MilestoneSpecsReady`.

Major Steps

1. Announce milestone deep-dive generation.
2. Resolve the prompt contract and ensure the milestone projection is present, valid, and fresh.
3. Build runtime prompt context from projection content and the active epic.
4. Capture transition inputs and persist prompt-start state.
5. Run the runtime prompt through the read-only planning agent.
6. Persist prompt-completed state and journal evidence.
7. Extract the returned bundle into individual spec files.
8. Write the bundle manifest.
9. Mark each extracted spec as lifecycle `Ready`.
10. Capture optional HITL non-implementation request markers from each spec.
11. Record execution-preparation provenance for the active spec set.
12. Run post-materialization invariant validation.
13. Append the materialization journal record.
14. Persist final `MilestoneSpecsReady` state and pause.

Completion

The transition is complete when all extracted milestone specs have been written, `.agents/specs/bundle-manifest.md` records their hashes, lifecycle metadata marks each spec `Ready`, execution-preparation provenance identifies the active epic and active spec set, invariant validation has passed, `.agents/journal/transitions.jsonl` includes `MilestoneSpecsMaterialized`, and `.agents/state.json` has `CurrentState = MilestoneSpecsReady`.

## 2. Current Execution Trace

This is the current successful execution order.

1. `ExecuteResumePlanAsync` reaches `RoadmapResumeAction.GenerateMilestoneSpecs`, or `ContinueAfterSelectionAsync` finishes active epic preparation and calls `GenerateMilestoneSpecsAsync`.
2. `GenerateMilestoneSpecsAsync` sets `runtimePrompt = "GenerateMilestoneDeepDivesForEpic"`.
3. `console.Phase("Generate milestone deep dives")` writes the phase message.
4. `contractRegistry.Get(runtimePrompt)` loads the prompt contract.
5. `projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken)` starts.
6. `ProjectionCache` resolves `GenerateMilestoneDeepDivesForEpic` from `ProjectionRegistry`.
7. `ProjectionCache` creates current projection provenance from the projection definition and the preflight `ProjectContext`.
8. `ProjectionCache` reads `.agents/projections/milestone-deep-dive.md`.
9. If projection content is missing or empty, `ProjectionCache` renders `ProjectionForGenerateMilestoneDeepDivesForEpic`.
10. If projection generation is required, `RoadmapPromptRunner.RunProjectionPromptAsync` runs the projection prompt through `RunOneShotAsync`.
11. `RunOneShotAsync` creates a `ConsoleTurnRenderer`.
12. `RunOneShotAsync` calls `runtime.RunOneShotAsync(AgentSpecs.ReadOnlyPlanning(repository), prompt, renderer.Stream, cancellationToken)`.
13. `RunOneShotAsync` requires `AgentTurnState.Completed`; otherwise it throws `RoadmapStepException`.
14. `RunOneShotAsync` echoes silent output if needed and returns the projection output.
15. `ProjectionCache` rejects empty generated projection content.
16. `ProjectionCache` validates the projection with `ProjectionValidator.Validate("GenerateMilestoneDeepDivesForEpic", content)`.
17. `ProjectionCache` hashes the projection content.
18. `ProjectionCache` loads the projection manifest.
19. `ProjectionCache` finds any prior manifest entry for `GenerateMilestoneDeepDivesForEpic`.
20. `ProjectionCache` computes validation status.
21. `ProjectionCache` computes freshness as fresh for generated content or via `ProjectionFreshnessEvaluator` for existing content.
22. `ProjectionCache` creates or updates the manifest entry.
23. `ProjectionCache` upserts the manifest entry before later blocking or writing generated content.
24. If validation failed, `ProjectionCache` writes `.agents/evidence/blockers/projection-blocked.NNNN.md` and throws `RoadmapStepException`.
25. If projection content was generated, `ProjectionCache` writes `.agents/projections/milestone-deep-dive.md`.
26. If the projection is stale and the contract policy is `Block`, `ProjectionCache` writes `.agents/evidence/blockers/projection-blocked.NNNN.md` and throws `RoadmapStepException`.
27. `ProjectionCache` returns `ProjectionCacheResult`.
28. `contextBuilder.BuildMilestoneContextAsync(projection.Content)` starts.
29. The context builder reads required `.agents/epic.md`.
30. The context builder builds `# Roadmap Runtime Prompt Context` with sections in this order: projection content, active epic.
31. The context builder rejects the context if it contains raw project-context markers.
32. `GenerateMilestoneSpecsAsync` calls `RunPromptForPromotionAsync` with from `ActiveEpicReady`, target `MilestoneSpecsReady`, prompt `GenerateMilestoneDeepDivesForEpic`, projection `.agents/projections/milestone-deep-dive.md`, the rendered context, empty secondary input, and output path `.agents/specs`.
33. `RunPromptForPromotionAsync` calls `inputResolver.ResolveAsync`.
34. `TransitionInputResolver` creates an input accumulator.
35. The resolver adds the projection path as a required input.
36. The resolver applies prompt-specific input rules for `GenerateMilestoneDeepDivesForEpic`.
37. The resolver adds `.agents/epic.md` as a required `ActiveEpic` input.
38. The resolver snapshots inputs in path order.
39. The snapshot reads each required input.
40. The snapshot throws `RoadmapStepException` if a required input is missing.
41. The snapshot hashes every present input.
42. The resolver extracts the projection hash.
43. The resolver hashes the rendered prompt context.
44. The resolver hashes the empty secondary input.
45. The resolver computes the overall snapshot hash.
46. The resolver returns `TransitionInputSnapshot`.
47. `RunPromptForPromotionAsync` creates a correlation id.
48. It captures `started = DateTimeOffset.UtcNow`.
49. It starts a stopwatch.
50. It sets `outputList` to `.agents/specs`.
51. It calls `SaveStateAsync` with current state `ActiveEpicReady`, status `Started`, from `ActiveEpicReady`, to `MilestoneSpecsReady`, prompt `GenerateMilestoneDeepDivesForEpic`, projection `.agents/projections/milestone-deep-dive.md`, output `.agents/specs`, and decision `Prompt Started`.
52. `SaveStateAsync` loads existing roadmap state.
53. `SaveStateAsync` loads the projection manifest.
54. `SaveStateAsync` reads active artifact statuses for roadmap completion context, selection, and active epic.
55. `SaveStateAsync` reads the last decision id from the decision ledger.
56. `SaveStateAsync` preserves existing retired epics.
57. `SaveStateAsync` preserves existing blockers because no replacement list is passed.
58. `SaveStateAsync` lists `.agents/splits/split-family-*.json`.
59. `SaveStateAsync` computes projection manifest counts.
60. `SaveStateAsync` saves `.agents/state.json` with `NextValidTransitions(ActiveEpicReady)`, which is `["GenerateMilestoneDeepDives"]`, unless an existing transition intent is preserved.
61. `RunPromptForPromotionAsync` appends `TransitionStarted` to `.agents/journal/transitions.jsonl`.
62. The start journal record includes the correlation id, previous state `ActiveEpicReady`, attempted state `MilestoneSpecsReady`, prompt, projection, prompt contract key, input artifact hashes, output path `.agents/specs`, duration `0`, result `Started`, parser decision `None`, no error, and the full input snapshot.
63. `promptRunner.RunRuntimePromptAsync("GenerateMilestoneDeepDivesForEpic", context, string.Empty, cancellationToken)` starts.
64. `RoadmapPromptRunner` renders the runtime prompt through `RoadmapPromptCatalog.RenderRuntime`.
65. `RoadmapPromptRunner` appends the implementation-first prompt policy.
66. `RoadmapPromptRunner` calls `RunOneShotAsync`.
67. `RunOneShotAsync` creates a `ConsoleTurnRenderer`.
68. `RunOneShotAsync` calls the agent runtime with `AgentSpecs.ReadOnlyPlanning(repository)`.
69. `RunOneShotAsync` requires `AgentTurnState.Completed`.
70. `RunOneShotAsync` echoes silent output if needed.
71. The raw prompt output is returned to `RunPromptForPromotionAsync`.
72. `RunPromptForPromotionAsync` stops the stopwatch.
73. It captures the prompt completion timestamp.
74. It appends `PromptCompleted` to `.agents/journal/transitions.jsonl`.
75. The `PromptCompleted` journal record uses output path `.agents/specs`, result `PromptCompleted`, parser decision `Output produced`, and the same input snapshot.
76. `RunPromptForPromotionAsync` calls `SaveStateAsync` with current state `ActiveEpicReady`, status `PromptCompleted`, from `ActiveEpicReady`, to `MilestoneSpecsReady`, output `.agents/specs`, and decision `Prompt Completed`.
77. `SaveStateAsync` repeats its state-load, manifest-load, active-artifact, last-decision, split-family, manifest-count, and save work.
78. `RunPromptForPromotionAsync` returns `PromptTransitionCompletion` containing correlation id, started timestamp, completed timestamp, elapsed milliseconds, raw output, and input snapshot.
79. `GenerateMilestoneSpecsAsync` enters the post-prompt `try` block.
80. `bundleExtractor.Extract(completion.Output)` starts.
81. `BundleFileExtractor` finds `# FILE: <path>` markers.
82. If no file markers are found, extraction returns a blocked result with reason `No FILE markers were found in the bundle output.`
83. For each file marker, the extractor normalizes backslashes to slashes.
84. The extractor rejects rooted paths and paths containing `..`.
85. The extractor applies the roadmap bundle policy, allowing `.agents/specs/*.md`, `.agents/epic-*.md`, and `.agents/epic.md`.
86. The extractor rejects duplicate file markers for the same path.
87. The extractor slices content between the current marker and next marker or end of output.
88. The extractor trims only leading and trailing newline separator noise.
89. The extractor hashes each extracted file content.
90. The extractor returns `BundleExtractionResult`.
91. `GenerateMilestoneSpecsAsync` throws `RoadmapStepException` if the bundle is blocked or contains zero files.
92. `bundleExtractor.WriteExtractedFilesAsync(artifacts, bundle)` writes each extracted file to its target path.
93. `bundleManifestWriter.WriteAsync(".agents/specs/bundle-manifest.md", runtimePrompt, projectionPath, bundle, "Valid")` writes the bundle manifest.
94. The manifest records source prompt, projection, expected file count, validation result `Valid`, and extracted file hashes sorted by path.
95. For each extracted file, `lifecycleStore.UpsertAsync(file.Path, ArtifactLifecycleState.Ready)` starts.
96. `ArtifactLifecycleStore` loads `.agents/artifacts/lifecycle.json`, or migrates legacy markdown if needed.
97. `ArtifactLifecycleStore` removes any existing lifecycle row for the same path case-insensitively.
98. `ArtifactLifecycleStore` appends a new `Ready` entry with current timestamp and empty notes.
99. `ArtifactLifecycleStore` sorts entries by path and saves `.agents/artifacts/lifecycle.json`.
100. For each extracted file, `CaptureHitlRequestsAsync(file.Path, file.Content)` starts.
101. If no HITL request capture service is configured, capture returns immediately.
102. If configured and content is non-empty, the capture service scans for `## HITL-Requested Non-Implementation Deliverables`.
103. If structured HITL rows are found and not duplicates, the non-implementation review ledger is updated.
104. `executionPreparation.RecordMilestoneSpecsAsync(bundle.Files.Select(file => file.Path).ToArray(), cancellationToken)` starts.
105. The service checks cancellation.
106. It captures an input set from the extracted spec paths.
107. It rejects an empty spec path list.
108. It sorts spec paths.
109. For each spec path, it reads and hashes the written spec content.
110. It reads and hashes `.agents/epic.md`.
111. It reads and hashes `.agents/decision-ledger.json`, or uses `missing` if absent.
112. It loads `.agents/execution-preparation-manifest.json`, or starts from an empty manifest.
113. It records authoritative inputs: active epic path, active epic hash, and sorted milestone specs.
114. It supersedes previously active milestone spec artifacts not in the current spec identity set.
115. For each current spec, it creates trusted provenance with artifact kind `MilestoneSpec`, generator `GenerateMilestoneDeepDivesForEpic:v1`, and causal input `.agents/epic.md` with active epic hash.
116. It upserts each current spec as an active derived artifact.
117. It saves `.agents/execution-preparation-manifest.json`.
118. `invariantValidator.ValidateAsync(RoadmapState.MilestoneSpecsReady, projectContext.Hash, cancellationToken)` starts.
119. The validator reloads project context.
120. It verifies the project context hash still matches the preflight hash.
121. It verifies every projection registry entry has a prompt contract.
122. It loads the projection manifest.
123. For each existing projection file, it verifies a manifest entry exists.
124. For each existing projection file, it verifies the manifest entry is not invalid.
125. For each existing projection file, it verifies projection provenance is fresh against the current project context.
126. It loads artifact lifecycle entries.
127. It counts active epics with lifecycle `Ready` or `Executing` across `.agents/epic.md` and `.agents/epic-*` paths.
128. It fails if more than one active epic is marked active.
129. It validates that `.agents/epic.md` exists.
130. It validates active epic content with `EpicArtifactValidator`.
131. It evaluates execution-preparation readiness with `requireSpecs = true`, `requireOperationalContext = false`, `requireExecutionPrompt = false`, and `requireCompatibilityArtifacts = false`.
132. Execution-preparation readiness verifies milestone spec provenance is fresh against the active epic and current spec hashes.
133. Execution-preparation readiness rejects missing manifest, missing active epic, stale spec provenance, or unexpected active milestone spec artifacts.
134. The validator asks execution preparation for fresh milestone spec paths.
135. For each fresh spec, it reads spec content.
136. For each spec, it looks for an `Epic Path` field in a markdown table or `Epic Path:` line.
137. If a declared epic path exists and is not `.agents/epic.md`, validation fails with `SpecEpicMismatch`.
138. If all checks pass, the validator returns valid.
139. `GenerateMilestoneSpecsAsync` captures `completed = DateTimeOffset.UtcNow`.
140. It appends `MilestoneSpecsMaterialized` to `.agents/journal/transitions.jsonl`.
141. The materialization journal record uses the original correlation id, previous state `ActiveEpicReady`, attempted state `MilestoneSpecsReady`, prompt `GenerateMilestoneDeepDivesForEpic`, projection `.agents/projections/milestone-deep-dive.md`, prompt contract key `MilestoneSpecPostProcessing`, input artifact hashes from the prompt snapshot, output path `.agents/specs`, original elapsed prompt milliseconds, result `Completed`, parser decision `Milestone Specs Ready`, and the same input snapshot.
142. `SaveStateAsync` is called with current state `MilestoneSpecsReady`, status `Completed`, from `ActiveEpicReady`, to `MilestoneSpecsReady`, prompt `GenerateMilestoneDeepDivesForEpic`, projection `.agents/projections/milestone-deep-dive.md`, output `.agents/specs`, decision `Milestone Specs Ready`, original started timestamp, final completed timestamp, replacement blockers `[]`, transition intent `RoadmapTransitionIntent.Empty(MilestoneSpecsReady)`.
143. `SaveStateAsync` loads existing state.
144. `SaveStateAsync` loads projection manifest.
145. `SaveStateAsync` reads active artifact statuses for roadmap completion context, selection, and active epic.
146. `SaveStateAsync` reads the last decision id.
147. `SaveStateAsync` preserves retired epic records.
148. `SaveStateAsync` uses the explicitly passed empty blocker list.
149. `SaveStateAsync` lists split families.
150. `SaveStateAsync` computes projection manifest counts.
151. `SaveStateAsync` saves `.agents/state.json` with empty transition intent and `NextValidTransitions(MilestoneSpecsReady)`, which is `[]`.
152. `GenerateMilestoneSpecsAsync` returns.
153. `ExecuteResumePlanAsync` returns `RoadmapOutcome.Paused` for a resume-triggered generation path.
154. In the selection-following path, `RunSelectionAndFollowingAsync` returns `RoadmapOutcome.Paused` after `GenerateMilestoneSpecsAsync` returns.

Recovered runtime prompt failure shape:

```text
Prompt throws non-cancellation exception

-> Stop stopwatch

-> Append TransitionFailed journal record

-> Persist EvidenceBlocked state with status Failed

-> Set ResolveTransitionFailure intent with output path .agents/specs

-> Throw already-persisted RoadmapStepException
```

Recovered bundle or post-processing failure shape:

```text
Prompt already completed

-> Bundle extraction/materialization/provenance/lifecycle/HITL step throws

-> Write .agents/evidence/blockers/milestone-spec-generation-failed.NNNN.md

-> Append MilestoneSpecGenerationFailed journal record

-> Persist EvidenceBlocked state with status Paused

-> Set ResolveMilestoneSpecGenerationFailure intent

-> Throw already-persisted RoadmapStepException
```

Recovered invariant failure shape:

```text
Specs and execution-preparation provenance may already be written

-> InvariantValidator writes orchestration evidence

-> PersistInvariantFailureAndThrowAsync appends InvariantFailed

-> Persist EvidenceBlocked or Failed state using transition PostMilestoneInvariantValidation

-> Set ResolveInvariantViolation intent

-> Throw already-persisted RoadmapStepException
```

Recovered projection block shape:

```text
Projection invalid or stale

-> Upsert projection manifest

-> Write projection-blocked evidence

-> Throw RoadmapStepException before prompt-start persistence
```

Recovered cancellation shape:

```text
OperationCanceledException escapes local transition catches

-> Outer RunAsync writes cancelled state

-> Return RoadmapOutcome.Cancelled
```

## 3. Concern Inventory

| Step | Execution Step | Primary Concern | Secondary Concerns | Mixed? |
|---|---|---|---|---|
| 1 | Enter `GenerateMilestoneSpecsAsync` | routing | transition selection | no |
| 2 | Set runtime prompt name | routing | prompt identity | no |
| 3 | `console.Phase` | reporting | user-visible progress | no |
| 4 | Load prompt contract | validation | prompt metadata | no |
| 5 | Ensure projection | artifact read/write | validation, prompt execution, provenance, blocker evidence | yes |
| 6 | Build milestone context | context building | artifact read, validation | slight |
| 7 | Resolve transition input snapshot | evidence capture | artifact read, hashing, validation | yes |
| 8 | Persist started state | persistence | lifecycle summary, active artifact status, projection counts | yes |
| 9 | Append `TransitionStarted` | journaling | evidence snapshot | no |
| 10 | Run prompt | prompt execution | console streaming, agent status validation | yes |
| 11 | Append `PromptCompleted` | journaling | timing, input hashes | no |
| 12 | Persist prompt-completed state | persistence | progress reporting through state | yes |
| 13 | Extract bundle | parsing | path validation, hashing | yes |
| 14 | Reject blocked or empty bundle | validation | failure routing | slight |
| 15 | Write extracted files | artifact write | materialization | no |
| 16 | Write bundle manifest | artifact write | provenance-like reporting | slight |
| 17 | Upsert spec lifecycle | lifecycle | persistence | no |
| 18 | Capture HITL requests | side-channel evidence | optional ledger write, parsing | yes |
| 19 | Record milestone spec provenance | provenance | artifact hashing, manifest mutation, supersedence | yes |
| 20 | Validate invariants | validation | artifact reads, lifecycle reads, projection checks, evidence write on failure | yes |
| 21 | Persist invariant failure | recovery | journaling, state mutation, blocker creation | yes |
| 22 | Append `MilestoneSpecsMaterialized` | journaling | final evidence report | no |
| 23 | Persist final state | persistence | transition intent, blockers, active artifact statuses, counts | yes |
| 24 | Return to caller | routing | outcome selection outside handler | no |

Concerns become materially mixed at these points:

- `projectionCache.EnsureAsync` combines projection lookup, optional prompt execution, validation, manifest mutation, projection artifact writes, and blocker evidence.
- `RunPromptForPromotionAsync` combines input snapshotting, state persistence, journal writes, prompt execution, runtime failure recovery, and elapsed-time capture.
- The post-prompt block in `GenerateMilestoneSpecsAsync` combines parsing, materialization, lifecycle, optional HITL capture, provenance, invariant validation, final journaling, and state persistence.
- `SaveStateAsync` is not just a state write. It also reads active artifact status, decision ledger, projection manifest, split families, retired epics, and blockers.
- `InvariantValidator.ValidateAsync` is a broad guard that reads project context, projections, lifecycle, active epic, execution-preparation provenance, and spec content; on failure it also writes evidence.

## 4. Hidden Steps

These steps are present only as helper calls or side effects, but they are real transition work.

Build Projection

`projectionCache.EnsureAsync` may run the projection prompt, write `.agents/projections/milestone-deep-dive.md`, and update the projection manifest before the runtime prompt begins.

Build Context

`BuildMilestoneContextAsync` reads `.agents/epic.md` and constructs the runtime context from projection content and active epic content.

Resolve Inputs

`TransitionInputResolver.ResolveAsync` captures a durable input snapshot by reading and hashing the projection and active epic, plus hashing rendered context and secondary input.

Capture Prompt Snapshot

The transition stores the same `TransitionInputSnapshot` in `TransitionStarted`, `PromptCompleted`, and `MilestoneSpecsMaterialized`.

Persist Prompt Progress

`RunPromptForPromotionAsync` writes state twice before post-processing: `Started` and `PromptCompleted`.

Normalize Bundle Output

`BundleFileExtractor` interprets `# FILE:` markers, normalizes path separators, trims separator newlines, rejects unsafe paths, and hashes extracted content.

Materialize Specs

The raw prompt output is not saved as the success artifact. It becomes individual files under `.agents/specs/*.md`.

Record Bundle Evidence

`BundleManifestWriter` writes `.agents/specs/bundle-manifest.md` with source prompt, projection, file count, validation result, and per-file hashes.

Update Lifecycle

Each extracted spec receives a lifecycle `Ready` row.

Capture Optional HITL Requests

Each spec may update the non-implementation review ledger when a structured HITL request table is present and capture is configured.

Record Provenance

`RecordMilestoneSpecsAsync` updates `.agents/execution-preparation-manifest.json` so later execution-preparation steps know the active spec set and its active epic dependency.

Validate Postconditions

`InvariantValidator` verifies project-context stability, projection manifest health, active epic validity, milestone spec freshness, and spec-to-active-epic ownership.

Record Materialization

`MilestoneSpecsMaterialized` is the post-processing completion journal event. There is no `TransitionCompleted` event for this transition's success path because `RunPromptForPromotionAsync` records `PromptCompleted` and final materialization is recorded separately.

Finalize Workflow State

The final `SaveStateAsync` clears blockers, sets empty transition intent for `MilestoneSpecsReady`, and leaves no next valid roadmap transitions.

## 5. Natural Step Boundaries

Step 1: Resolve Milestone Projection

Purpose

Ensure the stable projection frame for milestone spec generation exists and is valid.

Inputs

- `ProjectContext`
- prompt contract for `GenerateMilestoneDeepDivesForEpic`
- `.agents/projections/milestone-deep-dive.md`, if already present

Outputs

- `ProjectionCacheResult`
- updated projection manifest
- optional generated projection file
- optional projection-blocked evidence on failure

---

Step 2: Build Runtime Context

Purpose

Create the runtime prompt context that combines projection content and active epic content.

Inputs

- projection content
- `.agents/epic.md`

Outputs

- rendered `# Roadmap Runtime Prompt Context`

---

Step 3: Start Prompt Transition

Purpose

Capture transition inputs, persist started state, and write the start journal record.

Inputs

- prompt name
- projection path
- rendered context
- empty secondary input
- output path `.agents/specs`
- current state and persistence documents

Outputs

- `TransitionInputSnapshot`
- `.agents/state.json` status `Started`
- `TransitionStarted` journal record
- correlation id and started timestamp

---

Step 4: Execute Prompt

Purpose

Run the runtime prompt through the read-only planning agent.

Inputs

- prompt name
- rendered context
- empty secondary input
- repository
- cancellation token

Outputs

- raw agent output
- `PromptCompleted` journal record
- `.agents/state.json` status `PromptCompleted`
- elapsed milliseconds
- or durable runtime failure state

---

Step 5: Extract and Materialize Bundle

Purpose

Turn raw multi-file bundle output into actual repository artifacts.

Inputs

- raw prompt output
- roadmap bundle extraction policy

Outputs

- extracted spec files under `.agents/specs/*.md`
- `.agents/specs/bundle-manifest.md`
- or milestone-spec-generation blocker evidence

---

Step 6: Record Spec Side Effects

Purpose

Make the newly written specs visible to lifecycle, HITL review, and execution-preparation provenance.

Inputs

- extracted file paths and content
- active epic content
- current decision ledger, if present

Outputs

- `.agents/artifacts/lifecycle.json`
- optional non-implementation review ledger updates
- `.agents/execution-preparation-manifest.json`

---

Step 7: Validate Postconditions

Purpose

Prove that the materialized spec set is coherent with active epic, projection state, project context, and execution-preparation provenance.

Inputs

- expected project context hash
- projection manifest
- lifecycle metadata
- active epic
- execution-preparation manifest
- spec contents

Outputs

- valid result
- or invariant evidence plus durable blocked/failed state

---

Step 8: Finalize Transition

Purpose

Record final materialization and persist `MilestoneSpecsReady`.

Inputs

- prompt completion data
- input snapshot
- projection path
- final timestamp

Outputs

- `MilestoneSpecsMaterialized` journal record
- `.agents/state.json` with `CurrentState = MilestoneSpecsReady`
- empty blockers
- empty transition intent
- empty next valid transitions

## 6. Mixed-Concern Analysis

Resolve Milestone Projection

Runs projection prompt if needed

AND

validates projection structure

AND

writes projection manifest

AND

writes blocker evidence for invalid or stale projection

This makes projection readiness harder to scan because the caller sees one `EnsureAsync` call, but the call can mutate artifacts, execute an agent turn, and fail before the transition start is ever persisted.

Build Runtime Context

Reads active epic

AND

assembles prompt context

AND

enforces a raw-project-context marker invariant

This is a small mix, but it hides the active epic read inside a context-building helper.

Start Prompt Transition

Captures input hashes

AND

persists roadmap state

AND

records journal evidence

AND

computes status summaries unrelated to the prompt itself

This makes the "start" step heavier than its name suggests. It is both an evidence snapshot and a state mutation.

Execute Prompt

Runs the agent

AND

streams console output

AND

validates agent turn state

AND

on failure writes state and journal records

The prompt execution path is readable only after inspecting both `RoadmapPromptRunner` and `RunPromptForPromotionAsync`.

Extract and Materialize Bundle

Parses markers

AND

validates target paths

AND

hashes content

AND

writes files

AND

creates manifest metadata

The runtime prompt returns one markdown string, but successful behavior is a set of artifact writes and a manifest. That transformation is the main reason this transition benefits from extraction.

Record Spec Side Effects

Writes lifecycle metadata

AND

parses optional HITL tables

AND

writes optional HITL ledger entries

AND

captures execution-preparation provenance

This step mixes three audiences: artifact lifecycle, non-implementation review, and execution-preparation freshness.

Validate Postconditions

Checks project-context drift

AND

checks projection manifest health

AND

checks active epic uniqueness and validity

AND

checks execution-preparation freshness

AND

checks spec `Epic Path` ownership

AND

writes evidence on failure

The broad invariant scope is correct behavior, but it forces a reader of this transition to understand a large amount of global workflow health checking.

Finalize Transition

Appends final journal event

AND

persists state

AND

clears blockers

AND

clears transition intent

AND

computes projection and split-family summary counts

The final line of the transition is not only "mark complete"; it also recomputes dashboard-like state summary fields.

## 7. Data Flow

`ProjectContext`

Originates from preflight in `RunAsync`.

Consumed by `ProjectionCache` for projection provenance and by `InvariantValidator` to verify the project context hash did not drift.

`PromptContract`

Originates from `PromptContractRegistry.Get("GenerateMilestoneDeepDivesForEpic")`.

Consumed by `ProjectionCache` for stale projection policy and by `InvariantValidator` indirectly when checking registry coverage.

`ProjectionDefinition`

Originates from `ProjectionRegistry.Get("GenerateMilestoneDeepDivesForEpic")`.

Consumed by `ProjectionCache`, `ProjectionManifestStore`, journal records, state summaries, and final materialization records.

`Projection Content`

Originates from `.agents/projections/milestone-deep-dive.md` or a generated projection prompt turn.

Consumed by `ProjectionValidator`, `ProjectionCache`, `BuildMilestoneContextAsync`, `TransitionInputResolver`, and the runtime prompt context.

`Active Epic`

Originates at `.agents/epic.md`.

Consumed by `BuildMilestoneContextAsync`, `TransitionInputResolver`, `ExecutionPreparationProvenanceService`, and `InvariantValidator`.

`Rendered Context`

Originates from `BuildMilestoneContextAsync`.

Consumed by `TransitionInputResolver` for hashing and by `RoadmapPromptRunner.RunRuntimePromptAsync`.

`TransitionInputSnapshot`

Originates from `TransitionInputResolver.ResolveAsync`.

Consumed by `TransitionStarted`, `PromptCompleted`, `MilestoneSpecsMaterialized`, runtime failure journal records, and milestone generation failure journal records.

`Correlation Id`

Originates in `RunPromptForPromotionAsync`.

Consumed by all journal records for the prompt and post-processing path.

`Raw Prompt Output`

Originates from the agent runtime.

Consumed by `BundleFileExtractor`.

On success it is not persisted as a raw success artifact.

On milestone generation failure it is embedded in `.agents/evidence/blockers/milestone-spec-generation-failed.NNNN.md`.

`BundleExtractionResult`

Originates from `BundleFileExtractor.Extract`.

Consumed by `WriteExtractedFilesAsync`, `BundleManifestWriter`, lifecycle updates, HITL capture, and execution-preparation provenance.

`Extracted Spec Files`

Originate from file markers in raw prompt output.

Consumed by artifact writes, bundle manifest hashes, lifecycle entries, HITL capture, execution-preparation manifest, and invariant validation.

`Bundle Manifest`

Originates from `BundleManifestWriter.WriteAsync`.

Consumed externally as artifact evidence under `.agents/specs/bundle-manifest.md`; it is not listed as an active milestone spec.

`ExecutionPreparationManifest`

Originates from `ExecutionPreparationProvenanceService.RecordMilestoneSpecsAsync`.

Consumed by invariant validation and later execution-preparation stages.

`InvariantValidationResult`

Originates from `InvariantValidator.ValidateAsync`.

Consumed by `GenerateMilestoneSpecsAsync`; invalid results are passed to `PersistInvariantFailureAndThrowAsync`.

`Final State Document`

Originates from `SaveStateAsync`.

Consumes current state inputs, projection manifest counts, artifact statuses, last decision id, split-family count, blockers, transition intent, and retired epics.

Persists to `.agents/state.json`.

## 8. Human Navigation Audit

Minimum navigation path for this transition:

1. `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`
   - `GenerateMilestoneSpecsAsync`
   - `RunPromptForPromotionAsync`
   - `PersistMilestoneSpecGenerationFailureAndThrowAsync`
   - `RenderMilestoneSpecGenerationFailure`
   - `PersistInvariantFailureAndThrowAsync`
   - `PersistWorkflowFailureAsync`
   - `CaptureHitlRequestsAsync`
   - `SaveStateAsync`
2. `src/LoopRelay.Roadmap.Cli/PromptContractRegistry.cs`
   - contract for `GenerateMilestoneDeepDivesForEpic`
3. `src/LoopRelay.Roadmap.Cli/RoadmapArtifactPaths.cs`
   - `.agents/specs`, projection path, state, lifecycle, journal, blocker, and execution-preparation paths
4. `src/LoopRelay.Roadmap.Cli/ProjectionCache.cs`
   - projection generation, validation, freshness, manifest update, and projection blocker behavior
5. `src/LoopRelay.Roadmap.Cli/ProjectionRegistry.cs`
   - runtime prompt to projection prompt and projection path mapping
6. `src/LoopRelay.Roadmap.Cli/ProjectionValidator.cs`
   - required projection title and sections
7. `src/LoopRelay.Roadmap.Cli/RoadmapPromptContextBuilder.cs`
   - `BuildMilestoneContextAsync`
8. `src/LoopRelay.Roadmap.Cli/TransitionInputs.cs`
   - active epic input snapshot rules
9. `src/LoopRelay.Roadmap.Cli/RoadmapPromptRunner.cs`
   - runtime prompt rendering, policy append, read-only agent invocation, and turn-state enforcement
10. `src/LoopRelay.Roadmap.Cli/BundleFileExtractor.cs`
    - `# FILE:` parsing, path validation, hashing, file writes
11. `src/LoopRelay.Roadmap.Cli/BundleManifestWriter.cs`
    - `.agents/specs/bundle-manifest.md` content
12. `src/LoopRelay.Roadmap.Cli/ArtifactLifecycleStore.cs` and `ArtifactLifecycle.cs`
    - lifecycle load, migration, upsert, and JSON persistence
13. `src/LoopRelay.Roadmap.Cli/ExecutionPreparationProvenanceService.cs`
    - active spec set provenance, supersedence, freshness
14. `src/LoopRelay.Roadmap.Cli/ExecutionPreparationManifest.cs`
    - manifest persistence shape
15. `src/LoopRelay.Roadmap.Cli/InvariantValidator.cs`
    - post-materialization validation and evidence creation
16. `src/LoopRelay.Roadmap.Cli/EpicArtifactPromotion.cs`
    - `EpicArtifactValidator` used by invariant validation
17. `src/LoopRelay.Roadmap.Cli/RoadmapWorkflowFailure.cs`
    - invariant failure state and journal shape
18. `src/LoopRelay.Roadmap.Cli/RoadmapBlockedArtifact.cs`
    - rendered invariant and projection blocker evidence format
19. `src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs` and `RoadmapStateStore.cs`
    - final state shape and persistence
20. `src/LoopRelay.Roadmap.Cli/TransitionJournal.cs` and `TransitionJournalStore.cs`
    - journal record shape and JSONL append behavior
21. `src/LoopRelay.Core/Prompts/Planning/GenerateMilestoneDeepDivesForEpic.prompt`
    - runtime prompt output contract and `# FILE:` bundle format
22. `src/LoopRelay.Core/Prompts/Projections/ProjectionForGenerateMilestoneDeepDivesForEpic.prompt`
    - projection prompt expectations

Minimum test navigation path:

1. `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapStateMachinePromotionTests.cs`
   - success path generates specs and pauses
2. `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapFailurePersistenceTests.cs`
   - runtime failure, invalid bundle, and invariant failure shapes
3. `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapConsoleOutputTests.cs`
   - confirms the roadmap CLI stops at milestone specs and does not generate operational context or execution prompt
4. `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapResumePlannerTests.cs`
   - confirms `ActiveEpicReady` resumes at milestone generation and `MilestoneSpecsReady` pauses

## 9. Extraction Boundary

Smallest extraction boundary:

Extract the body of `GenerateMilestoneSpecsAsync`, plus its direct failure renderer and milestone-generation failure persistence, into a named transition handler such as:

```text
GenerateMilestoneDeepDivesTransition.ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

The handler should own:

- prompt identity `GenerateMilestoneDeepDivesForEpic`
- phase reporting for milestone deep dives
- projection and context preparation
- prompt start and prompt-completed persistence through existing helpers or equivalent injected collaborators
- bundle extraction and spec materialization
- bundle manifest write
- spec lifecycle updates
- optional HITL capture calls
- execution-preparation provenance recording
- post-materialization invariant validation
- final materialization journal and state persistence
- milestone generation failure persistence

The handler should not own:

- resume planning
- selection routing
- active epic creation or promotion
- generic state document schema
- projection cache internals
- artifact store implementation
- execution prompt generation
- operational context generation
- completion certification
- unblock planning

One entry:

`ExecuteAsync(projectContext, cancellationToken)`

One normal exit:

successful return after final `MilestoneSpecsReady` state is persisted.

One failure exit style:

throw the same already-persisted `RoadmapStepException` after durable failure state is written, or allow pre-start projection/context failures and cancellation to propagate exactly as today.

## 10. Required Inputs

Required

- `ProjectContext` with content and hash
- cancellation token
- `ILoopConsole`
- `PromptContractRegistry`
- `ProjectionCache`
- `RoadmapPromptContextBuilder`
- `TransitionInputResolver`
- `RoadmapPromptRunner`
- `RoadmapArtifacts`
- `BundleFileExtractor`
- `BundleManifestWriter`
- `ArtifactLifecycleStore`
- `ExecutionPreparationProvenanceService`
- `InvariantValidator`
- `TransitionJournalStore`
- `RoadmapStateStore`
- `ProjectionManifestStore`
- `DecisionLedgerStore`

Required because of exact current state persistence, not because of milestone generation itself

- active artifact status reads for roadmap completion context, selection, and active epic
- split-family count
- projection manifest counts
- last decision id
- existing retired epics and blockers preservation

Optional

- `ExplicitHitlNonImplementationRequestCaptureService`

Incidental

- existing transition intent before the final state write
- decision ledger content except for `LastDecisionId` and execution-preparation provenance's optional decision-ledger hash
- retired epics, because they are preserved in state but not used to generate milestone specs
- split families, because only the count is reported in persisted state

Not needed by the extracted handler

- `SelectionParser`
- `SelectionProvenanceService`
- `CompletedEpicArchiveService`
- `CompletionCertificationPolicy`
- `CompletionCertificationRouter`
- `SplitEpicBundleInterpreter`
- `SplitFamilyStore`, except indirectly through invariant validator construction outside the handler
- `ArtifactPromotionService`
- `RoadmapStartupPlanner`
- `RoadmapResumePlanner`
- `RoadmapUnblockPlanner`

## 11. Required Outputs

Successful externally observable effects

- console phase message `Generate milestone deep dives`
- optional generated projection `.agents/projections/milestone-deep-dive.md`
- updated projection manifest
- `.agents/state.json` started-state write
- `TransitionStarted` journal record
- agent runtime prompt turn
- `PromptCompleted` journal record
- `.agents/state.json` prompt-completed write
- extracted milestone spec files under `.agents/specs/*.md`
- `.agents/specs/bundle-manifest.md`
- lifecycle `Ready` entries for each extracted spec
- optional HITL request ledger updates
- `.agents/execution-preparation-manifest.json`
- invariant validation reads and possible evidence writes only on failure
- `MilestoneSpecsMaterialized` journal record
- `.agents/state.json` final `MilestoneSpecsReady` write
- `RoadmapOutcome.Paused` from the caller

Failure effects before prompt start

- projection manifest may be updated
- projection-blocked evidence may be written
- no transition-start state is written by this transition if projection or context preparation fails before `RunPromptForPromotionAsync`

Runtime prompt failure effects

- `TransitionStarted` journal record
- started state write
- `TransitionFailed` journal record
- `.agents/state.json` with `CurrentState = EvidenceBlocked`
- `LastTransition.Status = Failed`
- `TransitionIntent.Intent = ResolveTransitionFailure`
- blocker row with the runtime exception message
- no generic `roadmap-transition-blocked` evidence

Post-prompt milestone generation failure effects

- `PromptCompleted` journal record and prompt-completed state already exist
- `.agents/evidence/blockers/milestone-spec-generation-failed.NNNN.md`
- `MilestoneSpecGenerationFailed` journal record
- `.agents/state.json` with `CurrentState = EvidenceBlocked`
- `LastTransition.Status = Paused`
- `TransitionIntent.Intent = ResolveMilestoneSpecGenerationFailure`
- blocker row requiring review of the numbered evidence path
- no final `MilestoneSpecsMaterialized` record

Invariant failure effects

- spec files, bundle manifest, lifecycle, and execution-preparation manifest may already exist
- `.agents/evidence/orchestration/invariant-failure.NNNN.md`, or fallback blocker evidence if validator evidence is missing
- `InvariantFailed` journal record
- `.agents/state.json` with `CurrentState = EvidenceBlocked` or `Failed`, depending on invariant failure state
- `LastTransition.Prompt = PostMilestoneInvariantValidation`
- `TransitionIntent.Intent = ResolveInvariantViolation`
- no final `MilestoneSpecsMaterialized` record

Not produced

- no decision ledger entry on success
- no `.agents/context` operational context
- no `.agents/execution-prompt.md`
- no execution plan
- no direct raw prompt output artifact on success

## 12. Behavioral Equivalence Contract

Inputs that must remain equivalent

- same prompt name: `GenerateMilestoneDeepDivesForEpic`
- same transition states: `ActiveEpicReady` -> `MilestoneSpecsReady`
- same projection path: `.agents/projections/milestone-deep-dive.md`
- same required runtime input: `.agents/epic.md`
- same rendered context section order: projection content, active epic
- same empty secondary input
- same output path in state and journal before materialization: `.agents/specs`
- same read-only planning agent spec

Success contract

- Preserve the exact order: projection ensure, context build, input snapshot, started state, `TransitionStarted`, prompt run, `PromptCompleted`, prompt-completed state, bundle extraction, spec writes, bundle manifest, lifecycle/HITL per file, execution-preparation provenance, invariant validation, `MilestoneSpecsMaterialized`, final state.
- Preserve `TransitionStarted` and `PromptCompleted` journal event names and fields.
- Preserve absence of `TransitionCompleted` on the success path.
- Preserve `MilestoneSpecsMaterialized` as the post-processing completion event.
- Preserve `.agents/specs/bundle-manifest.md` path, validation result `Valid`, sorted file hash rows, source prompt, and projection fields.
- Preserve lifecycle state `Ready` for each extracted spec.
- Preserve execution-preparation generator id `GenerateMilestoneDeepDivesForEpic:v1`.
- Preserve final state values: `CurrentState = MilestoneSpecsReady`, `LastTransition.Status = Completed`, output `.agents/specs`, decision `Milestone Specs Ready`, empty blockers, empty transition intent, and no next valid transitions.
- Preserve the caller-visible outcome `Paused`.
- Preserve that operational context and execution prompt are not generated.

Runtime failure contract

- Preserve that non-cancellation runtime prompt exceptions are persisted by `RunPromptForPromotionAsync`.
- Preserve `TransitionFailed`.
- Preserve `CurrentState = EvidenceBlocked`, status `Failed`, output `.agents/specs`, decision `Runtime Failure`, intent `ResolveTransitionFailure`, and next transition text `Resolve blocker and rerun`.
- Preserve already-persisted exception behavior.

Post-prompt failure contract

- Preserve that bundle extraction or later post-processing failures occur after `PromptCompleted`.
- Preserve numbered blocker evidence under `.agents/evidence/blockers/milestone-spec-generation-failed.NNNN.md`.
- Preserve raw prompt output embedded in the failure evidence.
- Preserve `MilestoneSpecGenerationFailed`.
- Preserve `CurrentState = EvidenceBlocked`, status `Paused`, decision `Milestone Spec Generation Failed`, intent `ResolveMilestoneSpecGenerationFailure`, and required next step wording shape.
- Preserve no automatic rollback of spec files or manifest files already written before the failure.

Invariant failure contract

- Preserve validator execution after spec materialization and execution-preparation provenance recording.
- Preserve validator-owned evidence path when provided.
- Preserve fallback evidence behavior if no validator evidence path exists.
- Preserve `InvariantFailed`.
- Preserve prompt `PostMilestoneInvariantValidation` in final state.
- Preserve intent `ResolveInvariantViolation`.
- Preserve already-persisted exception behavior.

Projection and context failure contract

- Preserve that projection validation or stale-projection blocks occur before transition start persistence.
- Preserve projection-blocked evidence behavior.
- Preserve missing active epic failures from context building or input snapshotting as ordinary `RoadmapStepException` unless already persisted by a lower layer.

Console contract

- Preserve `Generate milestone deep dives` phase output.
- Preserve prompt runner streaming behavior and silent output echo.

Exception and cancellation contract

- Preserve that `OperationCanceledException` is not converted into milestone failure state by this transition.
- Preserve outer cancellation handling through `WriteCancelledStateAsync`.
- Preserve `RoadmapStepException` with `AlreadyPersisted` passing through without re-persisting a generic failure.

## 13. Transition Handler Shape

Recovered linear structure:

```text
Execute(projectContext, cancellationToken)

-> Announce phase

-> Load prompt contract

-> Ensure projection

-> Build runtime context

-> Start prompt transition
   -> Resolve input snapshot
   -> Save Started state
   -> Append TransitionStarted

-> Run prompt
   -> Render runtime prompt
   -> Invoke read-only planning agent
   -> Validate completed turn

-> Complete prompt phase
   -> Append PromptCompleted
   -> Save PromptCompleted state

-> Extract bundle
   -> Parse FILE markers
   -> Validate paths
   -> Hash extracted content

-> Materialize specs
   -> Write extracted files
   -> Write bundle manifest

-> Apply spec side effects
   -> Upsert lifecycle Ready for each spec
   -> Capture optional HITL requests for each spec
   -> Record execution-preparation provenance

-> Validate postconditions
   -> Run invariant validator
   -> Persist invariant failure if needed

-> Finalize
   -> Append MilestoneSpecsMaterialized
   -> Save MilestoneSpecsReady state

-> Return
```

Recovered failure branches inside the handler:

```text
Prompt runtime failure

-> Append TransitionFailed
-> Save EvidenceBlocked Failed state
-> Throw already-persisted exception
```

```text
Bundle/materialization/provenance/lifecycle/HITL failure

-> Write milestone-spec-generation-failed evidence
-> Append MilestoneSpecGenerationFailed
-> Save EvidenceBlocked Paused state
-> Throw already-persisted exception
```

```text
Invariant failure

-> Use validator evidence or fallback evidence
-> Append InvariantFailed
-> Save EvidenceBlocked/Failed state
-> Throw already-persisted exception
```

## 14. Readability Improvements

Extraction would make the successful transition readable as one materialization pipeline: projection, context, prompt, bundle extraction, artifact writes, provenance, validation, final state.

The current implementation hides the most important behavior behind generic helper names. A reader has to know that `RunPromptForPromotionAsync` does not promote an artifact here; it starts and completes only the prompt phase, leaving milestone materialization to the caller.

The event model would become easier to understand. The successful transition writes `TransitionStarted`, `PromptCompleted`, and `MilestoneSpecsMaterialized`, not `TransitionCompleted`. That distinction is easy to miss while reading the shared prompt helper.

The artifact behavior would become explicit. On success the raw model output is not stored directly; it is parsed into individual spec files and a bundle manifest. On post-processing failure the raw output is stored inside numbered blocker evidence.

The failure boundaries would be easier to test. Runtime failures, bundle failures, and invariant failures have different persisted prompts, event names, output paths, transition intents, and blocker evidence locations.

The state-machine method would lose a large mixed-concern block. `RoadmapStateMachine` could continue to route resume plans and selection outcomes while the extracted handler owns the linear milestone spec transition.

The minimum human navigation path would shrink. Engineers would still inspect helper implementations for details, but the transition's ordering and externally observable effects would be visible in one named handler instead of spread across `GenerateMilestoneSpecsAsync`, `RunPromptForPromotionAsync`, persistence helpers, and invariant failure helpers.

The exact refactor target is narrow: move the existing transition flow into a named handler while preserving the helper calls, event names, artifact paths, persistence order, and failure behavior described above.
