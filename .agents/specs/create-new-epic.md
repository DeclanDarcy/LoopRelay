# CreateNewEpic Transition Extraction Audit

Exactly one transition is audited here: `CreateNewEpic`.

Scope: the `"Select New Intermediary Epic"` branch in `ContinueAfterSelectionAsync`, implemented by `CreateNewEpicAsync` in `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`.

Entry: `ContinueAfterSelectionAsync` receives a `SelectionDecision` whose `RecommendedOutcome` is `"Select New Intermediary Epic"` and calls `CreateNewEpicAsync(projectContext, cancellationToken)`.

Exit: `CreateNewEpicAsync` returns an `ArtifactPromotionResult`.

Included:

- active selection read and freshness validation required before authoring
- `CreateNewEpic` prompt contract lookup
- `CreateNewEpic` projection cache resolution
- runtime context construction
- prompt execution through `RunPromptForPromotionAsync`
- active epic promotion through `PromoteActiveEpicAsync`
- prompt transition state writes
- transition journal writes
- artifact lifecycle writes
- blocker persistence for rejected promotion output

Excluded:

- the already-audited `SelectNextEpic` transition that chose `"Select New Intermediary Epic"`
- downstream `GenerateMilestoneDeepDivesForEpic`
- startup planning, resume planning, and Project Context preflight except where their persisted state is consumed by this transition
- any redesign of prompt contracts, projection caching, artifact promotion, or lifecycle storage

## Deliverable 1: Transition Narrative

Current State

The roadmap has just selected a new intermediary epic. `.agents/selection.md` contains the active selection, selection provenance should prove that this selection belongs to the current `SelectNextEpic` cycle, and no active epic should be promoted from this branch until the authoring output is validated.

Goal

Turn the selected new-epic proposal into the authoritative active epic artifact at `.agents/epic.md`, or pause with durable blocker evidence if the prompt output is blocked, ambiguous, malformed, structurally invalid, or if runtime execution fails.

Major Steps

1. Route the `"Select New Intermediary Epic"` decision into `CreateNewEpicAsync`.
2. Announce the phase.
3. Read the active selection and prove it is fresh.
4. Load the `CreateNewEpic` contract.
5. Ensure the `CreateNewEpic` projection exists, is valid, and is fresh enough to use.
6. Build the runtime prompt context from the projection and selection.
7. Capture a transition input snapshot.
8. Persist started state and append a started journal record.
9. Run the `CreateNewEpic` prompt.
10. Persist prompt-completed state and append a prompt-completed journal record.
11. Classify and validate the prompt output as an epic candidate.
12. Either promote `.agents/epic.md` and complete the transition, or preserve the output as blocker evidence and pause.
13. Return the promotion result to the caller.

Completion

The transition is complete when `CreateNewEpicAsync` returns:

- promoted: `.agents/epic.md` is written, lifecycle marks it `Ready`, journal records `ArtifactPromoted`, state records `ActiveEpicReady` / `Completed`, and the caller continues to milestone generation.
- not promoted: the prompt output is written under `.agents/evidence/blockers/active-epic-promotion.NNNN.md`, lifecycle marks that evidence `Blocked`, journal records `ArtifactPromotionBlocked`, state records `EvidenceBlocked` / `Paused`, and the caller returns `RoadmapOutcome.Paused` without milestone generation.
- prompt failure: transition state records `EvidenceBlocked` / `Failed`, journal records `TransitionFailed`, transition intent is `ResolveTransitionFailure`, and outer `RunAsync` returns `RoadmapOutcome.Failed`.

## Deliverable 2: Current Execution Trace

T1. `ContinueAfterSelectionAsync` enters with a parsed `SelectionDecision`.

T2. The switch evaluates `selection.RecommendedOutcome`.

T3. The `"Select New Intermediary Epic"` branch calls `CreateNewEpicAsync(projectContext, cancellationToken)`.

T4. `CreateNewEpicAsync` sets `runtimePrompt = "CreateNewEpic"`.

T5. It prints the phase text `"Create new epic"` through `console.Phase`.

T6. It calls `ReadCurrentSelectionAsync(cancellationToken)`.

T7. `ReadCurrentSelectionAsync` checks cancellation.

T8. It reads `.agents/selection.md` with `artifacts.ReadRequiredAsync`.

T9. `ReadRequiredAsync` reads the artifact and throws `RoadmapStepException` if the content is missing or whitespace.

T10. `ReadCurrentSelectionAsync` resolves the `SelectNextEpic` projection path from `RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"]`.

T11. It reads `.agents/projections/select-next-epic.md`.

T12. If the selection projection is missing or blank, it throws `RoadmapStepException("Active selection cannot be used because its SelectNextEpic projection is missing.")`.

T13. It loads persisted roadmap state through `stateStore.LoadAsync`.

T14. `stateStore.LoadAsync` prefers `.agents/state.json`, falls back to legacy `.agents/state.md`, and can migrate legacy state.

T15. It calls `selectionProvenance.CaptureCurrentCycleAsync(selectionProjection, state?.RetiredEpics ?? [], cancellationToken)`.

T16. `CaptureCurrentCycleAsync` checks cancellation.

T17. It builds the current `SelectNextEpic` selection context by calling `contextBuilder.BuildSelectionContextAsync`.

T18. `BuildSelectionContextAsync` reads `.agents/core/roadmap-completion-context.md`.

T19. It lists roadmap source files from `.agents/roadmap/*.md`.

T20. It verifies every roadmap source file is readable and non-empty.

T21. It builds a context containing projection content, completion context, roadmap source references, and retired epics.

T22. It validates that the context does not contain raw Project Context file markers.

T23. `CaptureCurrentCycleAsync` resolves a `SelectNextEpic` transition input snapshot.

T24. `TransitionInputResolver.ResolveAsync` adds the `SelectNextEpic` projection as a required projection input.

T25. It adds `.agents/core/roadmap-completion-context.md` as a required roadmap completion context input.

T26. It adds each `.agents/roadmap/*.md` source as a required roadmap source input.

T27. It reads every required input and hashes present content.

T28. It hashes the rendered selection context and empty secondary input.

T29. It computes a selection-cycle snapshot hash and returns the `TransitionInputSnapshot`.

T30. `ReadCurrentSelectionAsync` calls `selectionProvenance.EvaluateActiveSelectionFreshnessAsync(currentCycle, state?.RetiredEpics ?? [], cancellationToken)`.

T31. `EvaluateActiveSelectionFreshnessAsync` checks cancellation.

T32. It loads `.agents/selection-provenance-manifest.json`.

T33. It identifies trusted active selection entries.

T34. If there are no trusted active selections, it returns stale or unknown freshness rather than accepting the selection.

T35. If there are multiple active selections, it returns unknown freshness.

T36. It reads `.agents/selection.md` again and hashes it when present.

T37. It evaluates whether the active selection provenance matches the current cycle and current selection hash.

T38. If freshness is not fresh, `ReadCurrentSelectionAsync` throws a `RoadmapStepException` explaining the stale reasons.

T39. `ReadCurrentSelectionAsync` returns the selection content.

T40. `CreateNewEpicAsync` gets the `CreateNewEpic` prompt contract from `contractRegistry.Get("CreateNewEpic")`.

T41. The contract declares required input `.agents/selection.md`, required output `.agents/epic.md`, allowed decision `"Create Epic"`, writer `ArtifactPromotionService`, stale projection policy `Block`, parser `EpicAuthoringOutputClassifier+EpicArtifactValidator`, and blocking output heading `# Create New Epic Blocked`.

T42. `CreateNewEpicAsync` calls `projectionCache.EnsureAsync("CreateNewEpic", projectContext, contract, cancellationToken)`.

T43. `ProjectionCache` gets the `CreateNewEpic` projection definition from `ProjectionRegistry`.

T44. The definition maps runtime prompt `CreateNewEpic` to projection prompt `ProjectionForCreateNewEpic` and path `.agents/projections/create-new-epic.md`.

T45. It creates current projection provenance from the projection definition and Project Context.

T46. It reads `.agents/projections/create-new-epic.md`.

T47. If projection content is missing or blank, it runs `ProjectionForCreateNewEpic` through `promptRunner.RunProjectionPromptAsync`.

T48. `RunProjectionPromptAsync` renders the projection prompt from the Project Context.

T49. `RoadmapPromptRunner.RunOneShotAsync` runs the agent with `AgentSpecs.ReadOnlyPlanning(repository)`.

T50. The console turn renderer streams output.

T51. If the projection turn is not completed, `RunOneShotAsync` throws `RoadmapStepException` with diagnostics.

T52. If the projection output is blank, `ProjectionCache` throws `RoadmapStepException`.

T53. `ProjectionCache` validates the projection with `ProjectionValidator.Validate("CreateNewEpic", content)`.

T54. It hashes the projection content.

T55. It loads the projection manifest.

T56. It finds any previous manifest entry for `CreateNewEpic`.

T57. It computes validation status.

T58. It computes freshness from current provenance and the previous manifest entry, unless the projection was generated in this run.

T59. It creates or updates a manifest entry with hash, validation status, freshness status, provenance status, and stale reasons.

T60. It writes the manifest entry through `manifestStore.UpsertAsync`.

T61. If validation failed, it writes `.agents/evidence/blockers/projection-blocked.NNNN.md` and throws `RoadmapStepException`.

T62. If the projection was generated, it writes `.agents/projections/create-new-epic.md`.

T63. If the projection is stale and the contract policy is `Block`, it writes `.agents/evidence/blockers/projection-blocked.NNNN.md` and throws `RoadmapStepException`.

T64. It returns `ProjectionCacheResult`.

T65. `CreateNewEpicAsync` builds runtime context with `contextBuilder.BuildCreateOrSplitContext(projection.Content, selection)`.

T66. `BuildCreateOrSplitContext` creates sections for projection content, selection proposal, and repository inspection instructions.

T67. It validates that the runtime context does not contain raw Project Context file markers.

T68. `CreateNewEpicAsync` calls `RunPromptForPromotionAsync(NewEpicProposed, ActiveEpicReady, "CreateNewEpic", projectionPath, context, selection, [.agents/epic.md], cancellationToken)`.

T69. `RunPromptForPromotionAsync` resolves a transition input snapshot for `CreateNewEpic`.

T70. `TransitionInputResolver` adds `.agents/projections/create-new-epic.md` as a required projection input.

T71. It adds `.agents/selection.md` as a required selection input.

T72. It reads and hashes the required artifact inputs.

T73. It hashes the rendered runtime context.

T74. It hashes the secondary input, which is the same selection content passed to the prompt.

T75. It computes the `CreateNewEpic` snapshot hash.

T76. `RunPromptForPromotionAsync` creates a correlation id.

T77. It records `started = DateTimeOffset.UtcNow`.

T78. It starts a stopwatch.

T79. It formats the output list as `.agents/epic.md`.

T80. It calls `SaveStateAsync` with current `NewEpicProposed`, status `Started`, from `NewEpicProposed`, to `ActiveEpicReady`, prompt `CreateNewEpic`, projection `.agents/projections/create-new-epic.md`, output `.agents/epic.md`, decision `Prompt Started`, started timestamp, no completed timestamp, no new retired epics, and no new blockers.

T81. `SaveStateAsync` loads existing state.

T82. It loads the projection manifest.

T83. It reads active artifact statuses for roadmap completion context, selection, and active epic.

T84. It reads the last decision id from the decision ledger.

T85. It carries forward retired epics and blockers from existing state.

T86. It counts split-family JSON files.

T87. It saves `.agents/state.json` with the new last-transition summary, manifest counts, carried transition intent unless one was provided, and default next transitions for the current state unless provided.

T88. `RunPromptForPromotionAsync` appends `TransitionStarted` to `.agents/journal/transitions.jsonl`.

T89. It calls `promptRunner.RunRuntimePromptAsync("CreateNewEpic", context, selection, cancellationToken)`.

T90. `RunRuntimePromptAsync` renders the `CreateNewEpic` runtime prompt with the runtime context and secondary selection input.

T91. It appends the implementation-first prompt policy.

T92. It runs the agent read-only through `RunOneShotAsync`.

T93. The console renderer streams the agent turn.

T94. If the agent state is not `Completed`, `RunOneShotAsync` throws `RoadmapStepException`.

T95. If the renderer has not already displayed output, it echoes the result output.

T96. `RunRuntimePromptAsync` returns the raw prompt output.

T97. `RunPromptForPromotionAsync` stops the stopwatch.

T98. It records `completed = DateTimeOffset.UtcNow`.

T99. It appends `PromptCompleted` to the transition journal.

T100. It calls `SaveStateAsync` with current `NewEpicProposed`, status `PromptCompleted`, output `.agents/epic.md`, decision `Prompt Completed`, started and completed timestamps.

T101. It returns `PromptTransitionCompletion` containing correlation id, started, completed, elapsed milliseconds, raw output, and input snapshot.

T102. If prompt execution throws a non-cancellation exception, `RunPromptForPromotionAsync` stops the stopwatch.

T103. It appends `TransitionFailed` to the transition journal.

T104. It saves state as `EvidenceBlocked` / `Failed` with from `NewEpicProposed`, to `ActiveEpicReady`, prompt `CreateNewEpic`, output `.agents/epic.md`, decision `Runtime Failure`, a blocker row using the exception message, transition intent `ResolveTransitionFailure`, evidence paths `[.agents/epic.md]`, and next transition `Resolve blocker and rerun`.

T105. It throws `RoadmapStepException.AlreadyPersisted(exception)`.

T106. `CreateNewEpicAsync` calls `PromoteActiveEpicAsync(NewEpicProposed, "CreateNewEpic", projectionPath, completion)`.

T107. `PromoteActiveEpicAsync` calls `promotionService.PromoteAsync` with target `.agents/epic.md`, candidate `completion.Output`, evidence directory `.agents/evidence/blockers`, evidence stem `active-epic-promotion`, artifact name `active epic`, `EpicAuthoringOutputClassifier`, `EpicArtifactValidator`, lifecycle state `Ready`, and notes `Promoted by CreateNewEpic.`

T108. `ArtifactPromotionService` classifies the candidate output.

T109. `EpicAuthoringOutputClassifier` finds the first top-level Markdown heading.

T110. If no top-level heading exists, classification is `Ambiguous`.

T111. If the heading contains `Blocked`, classification is `Blocked`.

T112. If the heading matches `# Epic: ...`, classification is `Promotable`.

T113. If the heading resembles `# Epic` without the required colon form, or the content contains `## Epic Metadata`, classification is `Malformed`.

T114. Otherwise classification is `Ambiguous`.

T115. If classification is not promotable, `ArtifactPromotionService` preserves evidence.

T116. Evidence preservation writes the raw candidate output to `.agents/evidence/blockers/active-epic-promotion.NNNN.md`.

T117. It upserts lifecycle metadata for that evidence path with state `Blocked` and the classification reason.

T118. It returns `ArtifactPromotionResult.NotPromoted`.

T119. If classification is promotable, `ArtifactPromotionService` validates the candidate with `EpicArtifactValidator`.

T120. `EpicArtifactValidator` rejects empty content.

T121. It reclassifies and rejects anything not promotable.

T122. It verifies required sections: `## Epic Metadata`, `## Desired Capability`, `## Acceptance Criteria`, and `## Milestone Roadmap`.

T123. It verifies either `## Strategic Purpose` or `## Strategic Continuity`.

T124. It parses the `## Epic Metadata` field table.

T125. It verifies `Epic ID` and `Status`.

T126. It extracts `## Milestone Roadmap`.

T127. It parses milestone tables strictly.

T128. It requires at least one row containing required milestone columns.

T129. It verifies required non-empty milestone columns.

T130. If validation fails, `ArtifactPromotionService` preserves evidence as structurally invalid.

T131. If validation succeeds, it writes `.agents/epic.md`.

T132. It upserts lifecycle metadata for `.agents/epic.md` as `Ready` with notes `Promoted by CreateNewEpic.`

T133. It returns `ArtifactPromotionResult.PromotedResult(.agents/epic.md)`.

T134. `PromoteActiveEpicAsync` records a new completed timestamp.

T135. If `result.Promoted` is true, it calls `CaptureHitlRequestsAsync(.agents/epic.md, completion.Output)`.

T136. `CaptureHitlRequestsAsync` returns immediately if no HITL capture service exists or the content is blank.

T137. Otherwise it captures explicit HITL non-implementation requests from the active epic content.

T138. `PromoteActiveEpicAsync` appends `ArtifactPromoted` to the transition journal with prompt contract key `ArtifactPromotionService`, output path `.agents/epic.md`, result `Promoted`, parser decision `Active epic promoted`, elapsed milliseconds from the prompt run, and the original input snapshot.

T139. It saves state as current `ActiveEpicReady`, status `Completed`, from `NewEpicProposed`, to `ActiveEpicReady`, prompt `CreateNewEpic`, projection `.agents/projections/create-new-epic.md`, output `.agents/epic.md`, decision `Artifact Promoted`, started timestamp from the prompt transition, and the new completed timestamp.

T140. It returns the promoted result.

T141. If `result.Promoted` is false, `PromoteActiveEpicAsync` picks the evidence path from `result.EvidencePath`, falling back to `.agents/evidence/blockers`.

T142. It maps the promotion status to a decision string: `Artifact Promotion Blocked`, `Artifact Promotion Ambiguous`, `Artifact Promotion Invalid`, or `Artifact Promotion Rejected`.

T143. It appends `ArtifactPromotionBlocked` to the transition journal with prompt contract key `ArtifactPromotionService`, output path equal to the evidence path, result equal to the promotion status, parser decision equal to the mapped decision, error message equal to `result.Reason`, and the original input snapshot.

T144. It saves state as current `EvidenceBlocked`, status `Paused`, from `NewEpicProposed`, to `ActiveEpicReady`, prompt `CreateNewEpic`, projection `.agents/projections/create-new-epic.md`, output equal to the evidence path, decision equal to the mapped promotion decision, blocker row instructing the user to review the evidence path and rerun, transition intent `ResolveArtifactPromotionBlocker`, dispatch state `EvidenceBlocked`, evidence paths `[evidencePath]`, and next transition `Resolve blocker and rerun`.

T145. It returns the not-promoted result.

T146. `CreateNewEpicAsync` returns the `ArtifactPromotionResult` to `ContinueAfterSelectionAsync`.

T147. If `createPromotion.Promoted` is false, the caller returns `RoadmapOutcome.Paused`.

T148. If `createPromotion.Promoted` is true, the caller breaks out of the switch.

T149. The caller then invokes `GenerateMilestoneSpecsAsync(projectContext, cancellationToken)`.

T150. After downstream milestone generation, the caller returns `RoadmapOutcome.Paused`.

## Deliverable 3: Concern Inventory

| Trace | Concern(s) | Mixed? |
|---|---|---|
| T1-T3 | routing, decision dispatch | Yes. The caller both routes `SelectNextEpic` results and decides whether downstream milestone generation should run. |
| T4-T5 | transition setup, reporting | Low. Phase reporting is embedded in transition execution. |
| T6-T14 | artifact read, validation, persistence read, migration | Yes. A helper named as a read can throw, migrate persisted state, and prepare provenance inputs. |
| T15-T29 | hidden context build, artifact reads, input snapshot, hashing | Yes. Selection freshness validation reconstructs `SelectNextEpic` inputs inside `CreateNewEpic`. |
| T30-T39 | provenance validation, artifact read, decision, recovery exception | Yes. A selection read also enforces causal freshness and controls whether the transition may proceed. |
| T40-T41 | prompt contract lookup, validation authority | No significant mix. |
| T42-T64 | projection read/write, prompt execution, validation, manifest persistence, blocker evidence, freshness decision | Yes. Projection cache can execute an agent, write artifacts, update manifests, create blocker evidence, and throw. |
| T65-T67 | context construction, validation | Low. Context build also enforces a raw Project Context guard. |
| T68-T75 | input resolution, artifact reads, hashing | Yes. Runtime input capture is hidden inside prompt execution. |
| T76-T88 | state mutation, journaling, timing, transition identity | Yes. Prompt runner setup persists durable state before any prompt call. |
| T89-T96 | prompt rendering, policy append, agent execution, console streaming, exception production | Yes. Runtime prompt execution also owns console behavior and agent diagnostic conversion. |
| T97-T101 | timing, journaling, state mutation, return shaping | Yes. Prompt completion is persisted before artifact output is interpreted. |
| T102-T105 | recovery, journaling, state mutation, blocker creation, exception wrapping | Yes. Failure handling is durable state-machine behavior inside the prompt helper. |
| T106-T118 | artifact classification, evidence write, lifecycle write, result shaping | Yes. Promotion service both decides whether output is usable and preserves rejected output. |
| T119-T130 | parsing, structural validation, evidence write, lifecycle write | Yes. Validation failure is converted to durable blocker evidence by the same service. |
| T131-T133 | artifact write, lifecycle update, return shaping | Yes. Promotion success writes both active artifact and lifecycle. |
| T134-T140 | HITL capture, journaling, state mutation, return | Yes. Successful promotion combines artifact-derived request capture with transition completion persistence. |
| T141-T145 | decision mapping, journaling, state mutation, blocker persistence, return | Yes. Not-promoted handling converts validator/classifier outcomes into roadmap pause state. |
| T146-T150 | return routing, downstream transition launch, reporting outcome | Yes. The caller decides whether to pause or immediately run milestone generation. |

Concerns become most mixed in four places:

1. `ReadCurrentSelectionAsync`: read, provenance reconstruction, freshness validation, state load, and exception routing are hidden under a read-shaped name.
2. `ProjectionCache.EnsureAsync`: projection availability, agent execution, validation, manifest writes, blocker evidence, and freshness policy are combined.
3. `RunPromptForPromotionAsync`: input snapshotting, durable state writes, journal writes, prompt execution, timing, and failure persistence are combined.
4. `PromoteActiveEpicAsync`: artifact promotion, HITL capture, journal events, state transitions, blocker intent, and result return are combined.

## Deliverable 4: Hidden Steps

These steps are not obvious from the small `CreateNewEpicAsync` body, but they are part of the real transition:

Build Current Selection Cycle

- Rebuilds the current `SelectNextEpic` input snapshot from projection, roadmap completion context, roadmap sources, retired epics, context hash, and secondary input hash.

Validate Active Selection Freshness

- Confirms `.agents/selection.md` is the trusted active selection for the current cycle before using it as the authoring proposal.

Resolve Projection

- May generate `.agents/projections/create-new-epic.md`.
- Validates the projection.
- Updates the projection manifest.
- Blocks on invalid or stale projection.

Build Runtime Context

- Combines `CreateNewEpic` projection content, selection proposal, and repository inspection instructions.
- Rejects raw Project Context markers.

Capture Transition Snapshot

- Hashes projection, selection, rendered context, and secondary selection input.
- Stores this snapshot on transition journal records.

Start Durable Transition

- Saves `.agents/state.json` before the runtime prompt executes.
- Appends `TransitionStarted`.

Run Prompt

- Renders `CreateNewEpic`, appends prompt policy, runs an agent in read-only planning mode, streams console output, and converts non-completed agent turns into exceptions.

Record Prompt Completion

- Appends `PromptCompleted`.
- Saves `PromptCompleted` state even before the output is promoted.

Normalize Output Into Promotion Result

- Classifies the first top-level heading.
- Distinguishes blocked, ambiguous, malformed, and promotable output.

Validate Active Epic Shape

- Requires epic metadata, capability, acceptance criteria, milestone roadmap, strategic purpose or continuity, valid metadata table, and at least one valid milestone row.

Apply Promotion Effects

- On success, writes `.agents/epic.md` and marks it `Ready`.
- On rejection, writes numbered blocker evidence and marks that evidence `Blocked`.

Finalize Transition

- Appends either `ArtifactPromoted` or `ArtifactPromotionBlocked`.
- Saves final roadmap state.
- Returns `ArtifactPromotionResult`.

## Deliverable 5: Natural Step Boundaries

Step 1: Route selected-new-epic outcome

Purpose: Enter this transition only for `"Select New Intermediary Epic"`.

Inputs: `SelectionDecision`, `ProjectContext`, cancellation token.

Outputs: Call to `CreateNewEpicAsync`, later `ArtifactPromotionResult`.

---

Step 2: Load and validate active selection

Purpose: Get the proposal text and prove it belongs to the current selection cycle.

Inputs: `.agents/selection.md`, `.agents/projections/select-next-epic.md`, persisted state retired epics, selection provenance manifest, roadmap completion context, roadmap source files.

Outputs: Selection content or `RoadmapStepException`.

---

Step 3: Resolve CreateNewEpic projection

Purpose: Obtain valid, fresh projection content for the authoring prompt.

Inputs: `CreateNewEpic` prompt contract, Project Context, `.agents/projections/create-new-epic.md`, projection manifest.

Outputs: `ProjectionCacheResult`, optional generated projection file, updated projection manifest, or projection-blocked evidence plus exception.

---

Step 4: Build authoring context

Purpose: Create the exact runtime context passed to the prompt.

Inputs: Projection content and selection content.

Outputs: Rendered runtime context string.

---

Step 5: Capture transition inputs

Purpose: Record causal inputs before executing the prompt.

Inputs: Runtime prompt name, projection path, rendered context, secondary selection input, `.agents/selection.md`, `.agents/projections/create-new-epic.md`.

Outputs: `TransitionInputSnapshot`.

---

Step 6: Persist prompt start

Purpose: Make the in-progress authoring prompt externally visible.

Inputs: From state `NewEpicProposed`, target `ActiveEpicReady`, prompt, projection, output `.agents/epic.md`, timestamp, input snapshot.

Outputs: `.agents/state.json` with `Started`, `.agents/journal/transitions.jsonl` `TransitionStarted`.

---

Step 7: Run CreateNewEpic prompt

Purpose: Produce candidate active epic content.

Inputs: Runtime context, secondary selection input, repository, prompt policy, cancellation token.

Outputs: Raw prompt output or exception.

---

Step 8: Persist prompt completion or failure

Purpose: Record the prompt result before artifact interpretation.

Inputs: Raw output or exception, timing, input snapshot.

Outputs: `PromptTransitionCompletion` plus `PromptCompleted` journal/state, or `TransitionFailed` journal/state and `RoadmapStepException.AlreadyPersisted`.

---

Step 9: Promote candidate active epic

Purpose: Convert raw prompt output into `.agents/epic.md` only if it is a valid active epic.

Inputs: Candidate output, classifier, validator, target path, blocker evidence directory, lifecycle store.

Outputs: `.agents/epic.md` and lifecycle `Ready`, or blocker evidence and lifecycle `Blocked`.

---

Step 10: Persist promotion result

Purpose: Record final transition outcome.

Inputs: `ArtifactPromotionResult`, prompt completion metadata, input snapshot.

Outputs: `ArtifactPromoted` or `ArtifactPromotionBlocked` journal record, final `.agents/state.json`, blocker rows and transition intent when blocked.

---

Step 11: Return to caller

Purpose: Let caller decide whether to continue to milestone generation.

Inputs: `ArtifactPromotionResult`.

Outputs: Paused outcome if not promoted, or downstream `GenerateMilestoneDeepDivesForEpic` if promoted.

## Deliverable 6: Mixed-Concern Analysis

Step 1 mixes routing and downstream orchestration.

`ContinueAfterSelectionAsync` both selects the `CreateNewEpic` transition and decides whether the successful transition should immediately flow into milestone generation. This makes the boundary harder to read because a promoted result is not the end of the command turn, while a blocked result is.

Step 2 mixes artifact reading, provenance reconstruction, validation, and state loading.

The method name `ReadCurrentSelectionAsync` suggests a read, but the method also loads state, rebuilds the current `SelectNextEpic` input cycle, reads multiple artifacts, consults selection provenance, and throws transition-stopping exceptions. A reader cannot trust the selection read without following provenance logic.

Step 3 mixes projection retrieval, prompt execution, validation, manifest persistence, and blocker evidence.

`projectionCache.EnsureAsync` can perform an agent call, write a generated projection, update `.agents/projections/manifest.json`, write `.agents/evidence/blockers/projection-blocked.NNNN.md`, and throw. That makes projection resolution a durable transition step, not a passive cache lookup.

Step 4 is mostly cohesive.

It builds a runtime prompt context from already-known inputs. The extra guard against raw Project Context markers is validation mixed into context construction, but it is local and easy to understand.

Step 5 mixes input selection and hashing.

The handler does not locally show that `CreateNewEpic` causal inputs are the projection, selection artifact, rendered context, and secondary input. This matters for journal records and resume safety, but it is hidden inside the shared resolver.

Step 6 mixes transition identity, state mutation, artifact status snapshots, manifest counts, decision-ledger reads, split-family counts, and journaling.

Starting the prompt writes much more than "started": it snapshots active artifact statuses, carries blockers and retired epics, records manifest counts, carries or creates transition intent, computes next transitions, and appends journal history. This is hard to reason about from the call site.

Step 7 mixes prompt rendering, prompt policy, read-only agent execution, console streaming, and diagnostics.

The transition code only says "run runtime prompt", but externally visible console behavior and diagnostic exception text are produced here.

Step 8 mixes prompt completion with durable transition state.

Prompt output is not yet accepted as an active epic, but state is updated to `PromptCompleted`. This intermediate state is important for crash and resume behavior and should be explicit in any extraction.

Step 9 mixes output interpretation, artifact validation, artifact writes, evidence writes, lifecycle updates, and result shaping.

Promotion service is the behavioral gate. It can write the active epic or write blocker evidence. It also updates lifecycle in both cases. A reader looking only at `CreateNewEpicAsync` cannot see these externally observable effects.

Step 10 mixes promotion result mapping, journal persistence, state persistence, blocker rows, and transition intent.

The decision string in state is not the raw classifier outcome. It is mapped from `ArtifactPromotionStatus`. The same step writes recovery instructions and next transitions, which are critical to behavior.

Step 11 mixes transition return with caller continuation.

The transition returns only `ArtifactPromotionResult`, but the caller maps promoted results to milestone generation and unpromoted results to immediate pause. Extraction should keep that caller decision outside the handler while preserving the same return value.

## Deliverable 7: Data Flow

Selection decision

- Origin: Parsed by `SelectNextEpic` before this transition.
- Consumed by: `ContinueAfterSelectionAsync` switch.
- Effect: Selects the `CreateNewEpic` branch.

Active selection content

- Origin: `.agents/selection.md`.
- Read by: `ReadCurrentSelectionAsync`.
- Validated by: selection provenance freshness check.
- Consumed by: `BuildCreateOrSplitContext`, `RunRuntimePromptAsync` as secondary input, `TransitionInputResolver` for artifact hash.

SelectNextEpic projection content

- Origin: `.agents/projections/select-next-epic.md`.
- Read by: `ReadCurrentSelectionAsync`.
- Consumed by: `selectionProvenance.CaptureCurrentCycleAsync`.
- Effect: Reconstructs current selection-cycle inputs.

Persisted roadmap state

- Origin: `.agents/state.json`, with legacy `.agents/state.md` fallback.
- Read by: `ReadCurrentSelectionAsync` and every `SaveStateAsync`.
- Consumed by: retired epics, carried blockers, carried transition intent, and existing state migration.

Selection provenance manifest

- Origin: `.agents/selection-provenance-manifest.json`.
- Read by: `EvaluateActiveSelectionFreshnessAsync`.
- Consumed by: freshness decision.
- Effect: May block the transition if the selection is stale, unknown, superseded, or ambiguous.

CreateNewEpic contract

- Origin: `PromptContractRegistry`.
- Consumed by: `ProjectionCache.EnsureAsync`.
- Effect: Requires `.agents/selection.md`, output `.agents/epic.md`, stale projection policy `Block`, blocker heading `# Create New Epic Blocked`, and promotion parser/validator.

CreateNewEpic projection definition

- Origin: `ProjectionRegistry`.
- Values: runtime prompt `CreateNewEpic`, projection prompt `ProjectionForCreateNewEpic`, path `.agents/projections/create-new-epic.md`.
- Consumed by: `ProjectionCache`.

CreateNewEpic projection content

- Origin: existing `.agents/projections/create-new-epic.md` or generated by `ProjectionForCreateNewEpic`.
- Validated by: `ProjectionValidator`.
- Hashed by: `ProjectionCache` and `TransitionInputResolver`.
- Consumed by: runtime context.

Projection manifest entry

- Origin: current provenance plus previous manifest.
- Written by: `ProjectionCache`.
- Consumed by: state manifest counts via `SaveStateAsync`.

Runtime context

- Origin: `BuildCreateOrSplitContext`.
- Contains: projection content, selection proposal, repository inspection instructions.
- Hashed by: `TransitionInputResolver`.
- Consumed by: `RoadmapPromptCatalog.RenderRuntime("CreateNewEpic", context, selection)`.

Transition input snapshot

- Origin: `TransitionInputResolver.ResolveAsync`.
- Contains: projection identity, artifact input hashes, context hash, secondary input hash, snapshot hash.
- Consumed by: transition journal records and `PromptTransitionCompletion`.

Correlation id

- Origin: `Guid.NewGuid().ToString("N")` in `RunPromptForPromotionAsync`.
- Consumed by: `TransitionStarted`, `PromptCompleted`, `TransitionFailed`, `ArtifactPromoted`, and `ArtifactPromotionBlocked`.

Started and completed timestamps

- Origin: `DateTimeOffset.UtcNow` in prompt and promotion helpers.
- Consumed by: state transition summaries and journal records.

Prompt output

- Origin: `promptRunner.RunRuntimePromptAsync("CreateNewEpic", context, selection)`.
- Consumed by: `PromptTransitionCompletion`, `ArtifactPromotionService`, HITL capture on promotion, blocker evidence on rejection.

Artifact classification

- Origin: `EpicAuthoringOutputClassifier`.
- Consumed by: `ArtifactPromotionService`.
- Effect: Chooses promotable, blocked, structurally invalid, or ambiguous path.

Artifact validation result

- Origin: `EpicArtifactValidator`.
- Consumed by: `ArtifactPromotionService`.
- Effect: Allows `.agents/epic.md` write or forces blocker evidence.

Promoted active epic

- Origin: valid prompt output.
- Written to: `.agents/epic.md`.
- Lifecycle: `.agents/artifacts/lifecycle.json` entry `Ready`.
- Consumed by: downstream milestone generation outside this transition.

Promotion blocker evidence

- Origin: rejected prompt output.
- Written to: `.agents/evidence/blockers/active-epic-promotion.NNNN.md`.
- Lifecycle: evidence path `Blocked`.
- Consumed by: persisted blocker row and transition intent.

Transition journal records

- Origin: prompt runner and promotion completion.
- Written to: `.agents/journal/transitions.jsonl`.
- Events: `TransitionStarted`, `PromptCompleted`, `TransitionFailed`, `ArtifactPromoted`, `ArtifactPromotionBlocked`.

Roadmap state

- Origin: `SaveStateAsync`.
- Written to: `.agents/state.json`.
- Key successful states: `NewEpicProposed` / `Started`, `NewEpicProposed` / `PromptCompleted`, `ActiveEpicReady` / `Completed`.
- Key blocked state: `EvidenceBlocked` / `Paused`.
- Key prompt-failure state: `EvidenceBlocked` / `Failed`.

Returned result

- Origin: `ArtifactPromotionService` plus `PromoteActiveEpicAsync`.
- Type: `ArtifactPromotionResult`.
- Consumed by: `ContinueAfterSelectionAsync`.
- Effect: Promoted continues to milestone generation; not promoted pauses.

## Deliverable 8: Human Navigation Audit

Minimum files an engineer must read to understand this transition:

- `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`
- `src/LoopRelay.Roadmap.Cli/TransitionInputs.cs`
- `src/LoopRelay.Roadmap.Cli/ProjectionCache.cs`
- `src/LoopRelay.Roadmap.Cli/ProjectionRegistry.cs`
- `src/LoopRelay.Roadmap.Cli/PromptContractRegistry.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapPromptContextBuilder.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapPromptRunner.cs`
- `src/LoopRelay.Roadmap.Cli/ArtifactPromotion.cs`
- `src/LoopRelay.Roadmap.Cli/EpicArtifactPromotion.cs`
- `src/LoopRelay.Roadmap.Cli/SelectionProvenance.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapArtifacts.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapArtifactPaths.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapStateStore.cs`
- `src/LoopRelay.Roadmap.Cli/TransitionJournal.cs`
- `src/LoopRelay.Roadmap.Cli/TransitionJournalStore.cs`
- `src/LoopRelay.Roadmap.Cli/DecisionLedger.cs`
- `src/LoopRelay.Roadmap.Cli/DecisionLedgerStore.cs`
- `src/LoopRelay.Roadmap.Cli/ArtifactLifecycleStore.cs`
- `src/LoopRelay.Core/Prompts/Planning/CreateNewEpic.prompt`
- `src/LoopRelay.Core/Prompts/Projections/ProjectionForCreateNewEpic.prompt`

Helper methods in `RoadmapStateMachine.cs`:

- `ContinueAfterSelectionAsync`
- `CreateNewEpicAsync`
- `ReadCurrentSelectionAsync`
- `RunPromptForPromotionAsync`
- `PromoteActiveEpicAsync`
- `CaptureHitlRequestsAsync`
- `SaveStateAsync`
- `ActiveArtifactRowsAsync`
- `NextTransitions`
- `ReportEphemeralBlockerAsync`
- `WriteCancelledStateAsync`

Services:

- `ProjectionCache`
- `ProjectionRegistry`
- `PromptContractRegistry`
- `RoadmapPromptContextBuilder`
- `TransitionInputResolver`
- `SelectionProvenanceService`
- `RoadmapPromptRunner`
- `ArtifactPromotionService`
- `ArtifactLifecycleStore`
- `RoadmapStateStore`
- `TransitionJournalStore`
- `DecisionLedgerStore`
- optional `ExplicitHitlNonImplementationRequestCaptureService`

Models:

- `SelectionDecision`
- `PromptContract`
- `ProjectionDefinition`
- `ProjectionCacheResult`
- `TransitionInputRequest`
- `TransitionInputSnapshot`
- `TransitionArtifactInput`
- `PromptTransitionCompletion`
- `ArtifactPromotionRequest`
- `ArtifactPromotionResult`
- `ArtifactPromotionStatus`
- `ArtifactOutputClassification`
- `ArtifactValidationResult`
- `RoadmapStateDocument`
- `RoadmapTransitionSummary`
- `RoadmapTransitionIntent`
- `TransitionJournalRecord`
- `ArtifactLifecycleEntry`

Persistence objects and paths:

- `.agents/selection.md`
- `.agents/projections/select-next-epic.md`
- `.agents/projections/create-new-epic.md`
- `.agents/projections/manifest.json`
- `.agents/selection-provenance-manifest.json`
- `.agents/state.json`
- `.agents/journal/transitions.jsonl`
- `.agents/decision-ledger.json`
- `.agents/artifacts/lifecycle.json`
- `.agents/epic.md`
- `.agents/evidence/blockers/active-epic-promotion.NNNN.md`
- `.agents/evidence/blockers/projection-blocked.NNNN.md`

Tests that protect current behavior:

- `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapStateMachinePromotionTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapFailurePersistenceTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapStateMachineResumeTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/TransitionInputResolverTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/ArtifactPromotionServiceTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/ProjectionCacheTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/PromptContractRegistryTests.cs`

## Deliverable 9: Extraction Boundary

Smallest behavior-preserving boundary:

Extract the body and directly required helper flow of `CreateNewEpicAsync` into one named transition handler whose single public execution method returns the same `ArtifactPromotionResult`.

The handler should own:

- phase reporting for `"Create new epic"`
- reading and validating active selection freshness
- loading the `CreateNewEpic` contract
- ensuring the `CreateNewEpic` projection
- building the `CreateNewEpic` runtime context
- calling the existing prompt-for-promotion runner behavior in the same order
- calling the existing active-epic promotion behavior in the same order
- returning the promotion result

The handler should not own:

- the `ContinueAfterSelectionAsync` switch
- the selection transition that produced `.agents/selection.md`
- downstream milestone generation after a promoted active epic
- Project Context preflight
- startup or resume planning
- `RealignEpic`, `ReimagineEpic`, or `SplitEpic`
- new state names, new commands, new prompt contracts, or new artifact semantics

Natural entry:

`ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)`.

Natural exit:

`ArtifactPromotionResult`.

The extraction should preserve the same shared services and helper behavior. It should only make the linear transition flow readable in one place.

## Deliverable 10: Required Inputs

Required values:

- `ProjectContext`
- `CancellationToken`
- active `.agents/selection.md`
- active `SelectNextEpic` projection at `.agents/projections/select-next-epic.md`
- persisted state retired epics for selection freshness
- selection provenance manifest
- `CreateNewEpic` prompt contract
- `CreateNewEpic` projection content or ability to generate it
- rendered `CreateNewEpic` runtime context
- `CreateNewEpic` prompt output

Required collaborators:

- `RoadmapArtifacts`
- `PromptContractRegistry`
- `ProjectionCache`
- `RoadmapPromptContextBuilder`
- `SelectionProvenanceService`
- `TransitionInputResolver`
- `RoadmapPromptRunner`
- `RoadmapStateStore`
- `ProjectionManifestStore`
- `DecisionLedgerStore`
- `TransitionJournalStore`
- `ArtifactLifecycleStore`
- `ArtifactPromotionService`
- `ILoopConsole`

Required validators and classifiers:

- `ProjectionValidator`, through `ProjectionCache`
- `EpicAuthoringOutputClassifier`
- `EpicArtifactValidator`

Optional:

- `ExplicitHitlNonImplementationRequestCaptureService`

Incidental to this transition and should remain outside:

- `RoadmapStartupPlanner`
- `RoadmapResumePlanner`
- `RoadmapUnblockPlanner`
- `CompletionCertificationPolicy`
- `CompletionCertificationRouter`
- `ICompletedEpicArchiveService`
- `BundleFileExtractor`
- `SplitEpicBundleInterpreter`
- `BundleManifestWriter`
- `SplitFamilyStore`
- `ExecutionPreparationProvenanceService`, except where indirectly needed by already-existing context builder construction
- `InvariantValidator`
- non-implementation completion review services

## Deliverable 11: Required Outputs

Returned value:

- `ArtifactPromotionResult` with status, target path `.agents/epic.md`, optional evidence path, and reason.

Console behavior:

- phase output `Create new epic`
- streamed or echoed agent output from projection generation if needed
- streamed or echoed agent output from runtime prompt execution
- outer warning/error behavior for non-persisted exceptions remains handled by `RunAsync`

Projection artifacts:

- `.agents/projections/create-new-epic.md` is written if generated.
- `.agents/projections/manifest.json` is updated for `CreateNewEpic`.
- `.agents/evidence/blockers/projection-blocked.NNNN.md` is written if projection validation or freshness blocks.

Prompt transition state:

- `.agents/state.json` records `NewEpicProposed` / `Started` before runtime prompt execution.
- `.agents/state.json` records `NewEpicProposed` / `PromptCompleted` after prompt output is produced.
- On prompt failure, `.agents/state.json` records `EvidenceBlocked` / `Failed` with `ResolveTransitionFailure`.

Transition journal:

- `TransitionStarted`
- `PromptCompleted`
- `TransitionFailed` on runtime failure
- `ArtifactPromoted` on successful promotion
- `ArtifactPromotionBlocked` on blocked, ambiguous, structurally invalid, or rejected promotion

Artifact writes:

- Success writes `.agents/epic.md`.
- Promotion rejection writes `.agents/evidence/blockers/active-epic-promotion.NNNN.md`.

Lifecycle writes:

- Success marks `.agents/epic.md` as `Ready`.
- Promotion rejection marks the evidence path as `Blocked`.

State completion:

- Success saves current state `ActiveEpicReady`, status `Completed`, from `NewEpicProposed`, to `ActiveEpicReady`, output `.agents/epic.md`, decision `Artifact Promoted`.
- Promotion rejection saves current state `EvidenceBlocked`, status `Paused`, from `NewEpicProposed`, to `ActiveEpicReady`, output evidence path, decision based on promotion status.

Blockers and transition intent:

- Prompt failure writes blocker row from exception message and transition intent `ResolveTransitionFailure`.
- Promotion rejection writes blocker row instructing review of evidence path and transition intent `ResolveArtifactPromotionBlocker`.

Decision ledger:

- No new decision ledger entry is appended by `CreateNewEpic`.
- `SaveStateAsync` preserves the current last decision id by reading the decision ledger.

Downstream caller behavior:

- If promoted, `ContinueAfterSelectionAsync` proceeds to `GenerateMilestoneSpecsAsync`.
- If not promoted, `ContinueAfterSelectionAsync` returns `RoadmapOutcome.Paused` immediately.

## Deliverable 12: Behavioral Equivalence Contract

Inputs that must remain identical:

- Same `ProjectContext`.
- Same cancellation behavior.
- Same active selection content from `.agents/selection.md`.
- Same selection freshness validation against current `SelectNextEpic` cycle.
- Same `CreateNewEpic` contract.
- Same projection path `.agents/projections/create-new-epic.md`.
- Same runtime context shape from `BuildCreateOrSplitContext`.
- Same secondary input passed to the runtime prompt: active selection content.
- Same transition input snapshot inputs and hashing behavior.

Outputs that must remain identical:

- Same `ArtifactPromotionResult` statuses and reasons for the same prompt output.
- Same `.agents/epic.md` content on successful promotion.
- Same blocker evidence content on rejected promotion.
- Same projection file and projection manifest behavior.
- Same `.agents/state.json` transition summaries, statuses, output paths, decisions, blockers, transition intents, active artifact rows, split-family counts, projection manifest counts, retired epic carry-forward, and last decision id behavior.
- Same `.agents/journal/transitions.jsonl` event names, previous and attempted states, prompt, projection, prompt contract key, output paths, result, parser decision, error message, correlation id reuse, input artifact hashes, and embedded input snapshot.
- Same lifecycle entries for `.agents/epic.md` and blocker evidence.
- Same absence of a new decision-ledger entry in this transition.
- Same optional HITL capture behavior.
- Same console phase and agent-output streaming behavior.

Persisted state equivalence:

- Before prompt execution: `CurrentState = NewEpicProposed`, `Status = Started`, `From = NewEpicProposed`, `To = ActiveEpicReady`, `Prompt = CreateNewEpic`, `Output = .agents/epic.md`, `Decision = Prompt Started`.
- After prompt completion before promotion: `CurrentState = NewEpicProposed`, `Status = PromptCompleted`, `Decision = Prompt Completed`.
- After successful promotion: `CurrentState = ActiveEpicReady`, `Status = Completed`, `Output = .agents/epic.md`, `Decision = Artifact Promoted`.
- After promotion rejection: `CurrentState = EvidenceBlocked`, `Status = Paused`, `Output = evidencePath`, `Decision = Artifact Promotion Blocked`, `Artifact Promotion Ambiguous`, `Artifact Promotion Invalid`, or `Artifact Promotion Rejected`.
- After prompt failure: `CurrentState = EvidenceBlocked`, `Status = Failed`, `Output = .agents/epic.md`, `Decision = Runtime Failure`.

Recovery behavior equivalence:

- Prompt failure must remain `AlreadyPersisted` so outer `RunAsync` returns `RoadmapOutcome.Failed` without generic overwrite.
- Promotion rejection must remain a paused durable blocker, not a failed command.
- Projection validation or freshness failure must keep current behavior: projection blocker evidence is written by `ProjectionCache`, the exception is not transition-persisted by `RunPromptForPromotionAsync`, and outer `RunAsync` reports failure through generic state-machine error handling.
- Operation cancellation must continue to be caught by outer `RunAsync`, which writes cancellation state based on the last persisted transition.

Anything not externally observable should not be part of the contract:

- Local variable names.
- Whether the extracted handler has private helper methods.
- Internal line breaks in implementation.
- Class placement, as long as calls and effects happen in the same order.

## Deliverable 13: Transition Handler Shape

Ideal linear structure recovered from current behavior:

```text
ExecuteAsync(projectContext, cancellationToken)

-> Announce Phase

-> Read And Validate Active Selection

-> Load CreateNewEpic Contract

-> Ensure CreateNewEpic Projection

-> Build CreateNewEpic Runtime Context

-> Resolve Transition Inputs

-> Persist Prompt Started

-> Append TransitionStarted

-> Run CreateNewEpic Prompt

-> Append PromptCompleted

-> Persist PromptCompleted

-> Promote Active Epic

   -> Classify Output

   -> If Not Promotable:
      -> Write Blocker Evidence
      -> Mark Evidence Blocked
      -> Append ArtifactPromotionBlocked
      -> Persist EvidenceBlocked Paused
      -> Return NotPromoted

   -> Validate Epic

   -> If Invalid:
      -> Write Blocker Evidence
      -> Mark Evidence Blocked
      -> Append ArtifactPromotionBlocked
      -> Persist EvidenceBlocked Paused
      -> Return NotPromoted

   -> Write .agents/epic.md
   -> Mark Active Epic Ready
   -> Capture HITL Requests
   -> Append ArtifactPromoted
   -> Persist ActiveEpicReady Completed
   -> Return Promoted

-> On Prompt Failure:
   -> Append TransitionFailed
   -> Persist EvidenceBlocked Failed
   -> Throw AlreadyPersisted
```

The caller keeps:

```text
if (!result.Promoted)
    return Paused

GenerateMilestoneSpecsAsync(...)
return Paused
```

## Deliverable 14: Readability Improvements

One obvious entry point

Today, the reader starts in `ContinueAfterSelectionAsync`, jumps to `CreateNewEpicAsync`, then follows `ReadCurrentSelectionAsync`, `ProjectionCache`, `RunPromptForPromotionAsync`, `PromoteActiveEpicAsync`, `ArtifactPromotionService`, classifier, validator, lifecycle store, journal store, and state saving. A named transition handler would give the selected-new-epic workflow one local execution owner.

Linear reading order

The current method body is six lines, but the real transition has more than one hundred meaningful execution steps. Extraction can present the actual order: validate selection, ensure projection, build context, snapshot inputs, persist start, run prompt, persist prompt completion, promote or block.

Explicit intermediate state

The important `PromptCompleted` state is hidden inside `RunPromptForPromotionAsync`. A handler can make it visible that prompt output exists before the output has been accepted as `.agents/epic.md`.

Reduced working memory

The reader currently must remember that a successful prompt is not sufficient; promotion classification and validation still decide whether the transition completes. A linear handler can keep prompt output and promotion result adjacent.

Clearer blocked-output behavior

Blocked, ambiguous, malformed, and structurally invalid prompt outputs all preserve evidence and pause with `ResolveArtifactPromotionBlocker`. Today this is spread across `EpicAuthoringOutputClassifier`, `EpicArtifactValidator`, `ArtifactPromotionService`, and `PromoteActiveEpicAsync`.

Clearer failure behavior

Runtime prompt failure is persisted as `EvidenceBlocked` / `Failed` with `ResolveTransitionFailure`, while promotion rejection is `EvidenceBlocked` / `Paused` with `ResolveArtifactPromotionBlocker`. Keeping those branches side by side would make recovery behavior easier to compare.

Fewer files for routine debugging

An engineer debugging why `.agents/epic.md` was not written currently has to inspect promotion tests, classifier rules, validator rules, lifecycle writes, state persistence, and journal records separately. A handler can identify the same collaborators in order without changing them.

Safer modification surface

Changes to `CreateNewEpic` would be less likely to accidentally affect `SplitEpic`, `RealignEpic`, or `ReimagineEpic` because the new-epic transition flow would no longer be mentally reconstructed from shared helper call sites alone.

Preserved behavior with better names

The extracted handler does not need new concepts. It only names the existing steps already present in the implementation and keeps their observable effects identical.
