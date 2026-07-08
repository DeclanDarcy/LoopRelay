# Completion Certification Transition Extraction Audit

## Audited Transition

Exactly one transition is audited here: `Completion Certification`.

Scope: persisted `RoadmapState.EpicCompletionDetected` -> completion-certification route state, implemented by `RunCompletionCertificationAsync` in `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`.

Live entry: `RoadmapResumePlanner` returns `RoadmapResumeAction.EvaluateCompletionClaim` for persisted `RoadmapState.EpicCompletionDetected`; `ExecuteResumePlanAsync` reads the persisted execution evidence path and calls:

```text
RunCompletionCertificationAsync(
    projectContext,
    DateTimeOffset.UtcNow,
    executionEvidencePath,
    cancellationToken,
    persistCompletionClaim: false)
```

Exit: the method returns the route's `RoadmapOutcome`, or returns `RoadmapOutcome.Paused` after durable completion-review or certification-policy blocking, or throws through existing failure handling.

Included because the current transition owns these effects:

- optional non-implementation completion-review gate
- `EvaluateEpicCompletionAndDrift` projection resolution
- completion evaluation prompt execution
- evaluation evidence materialization
- completion parsing and policy validation
- completion route mapping
- close-route archive and completed-epic synthesis
- close-route `UpdateRoadmapCompletionContext` prompt execution
- active epic lifecycle update
- completion route journal and state persistence

Out of scope:

- execution-loop work that produced the completion claim
- execution disposition parsing before `EpicCompletionDetected`
- the existing `GenerateMilestoneDeepDivesForEpic` audit
- the existing `EpicPreparationAudit` audit
- the existing `SelectNextEpic` audit
- unblock recovery after `ResolveInvalidCompletionCertification`
- redesigning completion routing, policy, prompts, or artifact schemas

The conditional `UpdateRoadmapCompletionContextAsync` call is included only as an already-existing side effect of close routes. This audit does not select or redesign that prompt as a separate transition.

## 1. Transition Narrative

Current State

An execution turn has already reported `Epic Complete` with next step `EvaluateEpicCompletionAndDrift`. The roadmap state is persisted at `EpicCompletionDetected`, and persisted state or transition intent points at the execution evidence artifact under `.agents/evidence/execution`.

Goal

Certify whether the active epic is actually complete, decide the correct roadmap route, preserve the completion evidence, and update durable roadmap state so the next run knows whether to select another epic, continue execution, reopen preparation, or gather more evidence.

Major Steps

1. Resume the persisted completion claim and recover the execution evidence path.
2. Optionally block before certification if non-implementation HITL review decisions are pending.
3. Load or generate the completion-evaluation projection.
4. Build the `EvaluateEpicCompletionAndDrift` runtime context from the active epic, execution evidence, fresh milestone specs, and optional review evidence.
5. Run the completion-evaluation prompt while persisting transition start and completion journal/state records.
6. Write numbered evaluation evidence and capture HITL markers.
7. Parse the completion evaluation.
8. Validate the parsed recommendation against completion policy.
9. Persist a durable invalid-certification blocker if the parsed fields contradict policy.
10. Map a valid certification to a roadmap route.
11. For close routes, archive the execution workspace, synthesize a completed-epic summary, and update roadmap completion context.
12. Update active epic lifecycle state for the route.
13. Persist the completion route journal/state record.
14. Return the route's CLI outcome.

Completion

The transition is complete when one of these externally visible outcomes occurs:

- close route: completed-epic archive and synthesis are produced, roadmap completion context is rewritten, selection is superseded, active epic is marked `Completed`, route state is `SelectNextStrategicInitiative`, and the method returns `RoadmapOutcome.Completed`
- continue route: active epic is marked `Executing`, route state is `ExecutionLoop`, and the method returns `RoadmapOutcome.Paused`
- reopen route: active epic is marked `Ready`, route state is `EpicPreparationAudit`, and the method returns `RoadmapOutcome.Paused`
- gather-more-evidence route: active epic is marked `Ready`, route state is `EvidenceGathering`, and the method returns `RoadmapOutcome.Paused`
- invalid certification: blocker evidence is written, route state is `EvidenceBlocked`, and the method returns `RoadmapOutcome.Paused`
- non-implementation completion review block: blocker evidence is written, route state is `EvidenceBlocked`, and the method returns `RoadmapOutcome.Paused`

## 2. Current Execution Trace

This is the current live execution order for persisted `EpicCompletionDetected`.

### Entry

1. `RunAsync` loads persisted roadmap state.
2. `RunAsync` performs project-context preflight.
3. `RunAsync` emits the prompt-contract snapshot.
4. `resumePlanner.PlanAsync` receives the persisted state and `ProjectContext`.
5. `RoadmapResumePlanner.PlanForStateAsync` sees `RoadmapState.EpicCompletionDetected`.
6. It returns `RoadmapResumePlan.EvaluateCompletionClaim`.
7. `ExecuteResumePlanAsync` enters the `RoadmapResumeAction.EvaluateCompletionClaim` case.
8. It captures `DateTimeOffset.UtcNow` for the `executionStarted` argument.
9. It calls `ReadPersistedExecutionEvidencePathAsync`.
10. `ReadPersistedExecutionEvidencePathAsync` loads `.agents/state.json`.
11. It reads candidate paths from `state.TransitionIntent.EvidencePaths`.
12. It appends paths parsed from `state.LastTransition.Output`.
13. It filters candidates to paths beginning with `.agents/evidence/execution`.
14. It removes duplicates.
15. It checks each candidate with `artifacts.GetStatusAsync`.
16. It returns the first present execution evidence path.
17. If no candidate is present, it throws `RoadmapStepException`.
18. `ExecuteResumePlanAsync` calls `RunCompletionCertificationAsync` with `persistCompletionClaim: false`.

### Completion Certification

19. `RunCompletionCertificationAsync` starts.
20. It captures `executionCompleted = DateTimeOffset.UtcNow`.
21. The live path skips the `persistCompletionClaim` branch because `persistCompletionClaim` is `false`.
22. It resolves `runtimePrompt` from `ExecutionDispositionProtocol.CommandText(EvaluateEpicCompletionAndDrift)`, producing `EvaluateEpicCompletionAndDrift`.
23. If `nonImplementationCompletionReview` is configured, it calls `ReviewAsync`.
24. If the review is blocked, execution jumps to the non-implementation review block trace below.
25. If the review is not blocked, execution continues.
26. `console.Phase("Evaluate epic completion and drift")` writes the phase message.
27. `contractRegistry.Get("EvaluateEpicCompletionAndDrift")` loads the prompt contract.
28. `projectionCache.EnsureAsync("EvaluateEpicCompletionAndDrift", projectContext, contract, cancellationToken)` starts.
29. `ProjectionCache` resolves the projection definition from `ProjectionRegistry`.
30. It builds current projection provenance from the projection definition and `ProjectContext`.
31. It reads `.agents/projections/epic-completion-evaluation.md`.
32. If the projection is missing or blank, it renders/runs `ProjectionForEvaluateEpicCompletionAndDrift`.
33. If generated projection content is empty, it throws `RoadmapStepException`.
34. It validates projection content with `ProjectionValidator`.
35. It hashes the projection content.
36. It loads `.agents/projections/manifest.json`.
37. It finds any prior manifest entry for `EvaluateEpicCompletionAndDrift`.
38. It computes validation status.
39. It computes projection freshness.
40. It upserts the manifest entry before later blocking or writing generated content.
41. If validation failed, it writes `.agents/evidence/blockers/projection-blocked.NNNN.md` and throws `RoadmapStepException`.
42. If the projection was generated, it writes `.agents/projections/epic-completion-evaluation.md`.
43. If the projection is stale and the contract policy is `Block`, it writes `.agents/evidence/blockers/projection-blocked.NNNN.md` and throws `RoadmapStepException`.
44. It returns `ProjectionCacheResult`.
45. `contextBuilder.BuildCompletionEvaluationContextAsync(projection.Content, executionEvidencePath)` starts.
46. It reads `.agents/epic.md`.
47. It reads the execution evidence path returned at entry.
48. It asks `ExecutionPreparationProvenanceService` for fresh milestone spec paths.
49. It starts a context section list with projection content, active epic, execution evidence, and repository inspection instructions.
50. For each fresh milestone spec path, it reads the spec and appends a `Milestone Spec: {path}` section.
51. It adds optional non-implementation review evidence sections when those artifacts are present and non-blank.
52. It builds `# Roadmap Runtime Prompt Context`.
53. It rejects the context if it contains raw project-context markers.
54. `RunCompletionCertificationAsync` calls `RunPromptTransitionAsync` from `EpicCompletionDetected` to `CompletionEvaluationAndContextUpdate`.
55. `RunPromptTransitionAsync` delegates to `RunPromptTransitionWithCompletionAsync`.
56. `RunPromptTransitionWithCompletionAsync` calls `inputResolver.ResolveAsync`.
57. `TransitionInputResolver` adds the projection path as a required input.
58. It applies `EvaluateEpicCompletionAndDrift` input rules: required active epic, required execution evidence path, and required fresh milestone specs.
59. It snapshots and hashes all required inputs.
60. It hashes the rendered prompt context.
61. It hashes the secondary input, which is empty for this prompt.
62. It computes the transition snapshot hash.
63. `RunPromptTransitionWithCompletionAsync` creates a correlation id.
64. It captures `started = DateTimeOffset.UtcNow`.
65. It starts a stopwatch.
66. It calls `SaveStateAsync` with current state `CompletionEvaluationAndContextUpdate`, status `Started`, from `EpicCompletionDetected`, to `CompletionEvaluationAndContextUpdate`, prompt `EvaluateEpicCompletionAndDrift`, projection `.agents/projections/epic-completion-evaluation.md`, output `.agents/evidence/evaluations`, and decision `Pending`.
67. `SaveStateAsync` loads existing state.
68. It loads the projection manifest.
69. It reads active artifact rows for roadmap completion context, selection, and active epic.
70. It loads the last decision id.
71. It preserves existing retired epics unless replacements were passed.
72. It preserves existing blockers unless replacements were passed.
73. It counts split-family json files.
74. It saves `.agents/state.json`.
75. `RunPromptTransitionWithCompletionAsync` appends `TransitionStarted` to `.agents/journal/transitions.jsonl`.
76. `promptRunner.RunRuntimePromptAsync("EvaluateEpicCompletionAndDrift", context, string.Empty, cancellationToken)` starts.
77. `RoadmapPromptRunner` renders the runtime prompt.
78. It appends the implementation-first prompt policy.
79. It creates a `ConsoleTurnRenderer`.
80. It calls the agent runtime with `AgentSpecs.ReadOnlyPlanning(repository)`.
81. If the agent state is not completed, it throws `RoadmapStepException` with diagnostics.
82. It echoes silent output if needed.
83. It returns the raw evaluation output.
84. `RunPromptTransitionWithCompletionAsync` stops the stopwatch.
85. It captures `completed = DateTimeOffset.UtcNow`.
86. It appends `TransitionCompleted` to `.agents/journal/transitions.jsonl`.
87. It calls `SaveStateAsync` again with status `Completed` and decision `Completed`.
88. It returns `PromptTransitionCompletion`.
89. `RunPromptTransitionAsync` returns the raw output string.
90. `RunCompletionCertificationAsync` writes numbered evaluation evidence at `.agents/evidence/evaluations/epic-completion-and-drift.NNNN.md`.
91. It captures HITL requests from that evaluation evidence if HITL capture is configured.
92. `CompletionEvaluationParser.Parse(output)` reads `## Evaluation Summary`.
93. The parser requires `Overall Completion Status`.
94. The parser requires `Overall Drift Classification`.
95. The parser requires `Closure Recommendation`.
96. It validates those fields against completion vocabulary.
97. It returns `CompletionEvaluationDecision`.
98. `completionPolicy.Validate(decision)` checks allowed completion status, drift classification, recommendation, and recommendation-specific allowed combinations.
99. `AppendDecisionAsync` allocates the next decision id.
100. It appends a decision-ledger entry for state `CompletionEvaluationAndContextUpdate`, transition `EvaluateEpicCompletionAndDrift`, projection `.agents/projections/epic-completion-evaluation.md`, output evaluation evidence path, decision `decision.ClosureRecommendation`, confidence `Unclear`, and rationale `decision.OverallCompletionStatus`.
101. If certification is invalid, execution jumps to the invalid-certification block trace below.
102. `completionRouter.Route(certification.Decision)` maps the recommendation to a completion route.
103. `RoadmapCompletionRouteMapper.Map(route)` maps the shared route to roadmap state, transition status, CLI outcome, active-epic lifecycle state, and next transitions.
104. It initializes `archive = null`.
105. If the roadmap route requires roadmap completion context update, execution enters the close-route update trace below.
106. If the route has an active epic lifecycle state, it upserts `.agents/epic.md` lifecycle with that state and route notes.
107. It calls `PersistCompletionRouteAsync` with the roadmap route, parsed decision, projection path, evaluation path, and optional archive synthesis path.
108. `PersistCompletionRouteAsync` captures `completed = DateTimeOffset.UtcNow`.
109. It builds `outputs`: close routes include evaluation path, `.agents/core/roadmap-completion-context.md`, and the completed-epic synthesis path; non-close routes include only the evaluation path.
110. It builds routing context from closure recommendation, completion status, drift classification, target state, and transition status.
111. It resolves a `CompletionCertificationRouting` input snapshot.
112. That snapshot includes the evaluation evidence path as required completion-evaluation input.
113. It appends a `TransitionCompleted` journal record from `CompletionEvaluationAndContextUpdate` to the route target state.
114. The journal record uses prompt `CompletionCertificationRouting` and prompt contract key `CompletionCertificationRouter`.
115. It calls `SaveStateAsync` with the route target state, route transition status, route outputs, route decision, route transition intent, and route next transitions.
116. `RunCompletionCertificationAsync` returns `roadmapRoute.CliOutcome`.

### Close-Route Update Trace

This trace runs only for `Close Epic` and `Close With Follow-Up`.

117. `completionArchive.ArchiveAndSynthesizeAsync(new CompletedEpicArchiveRequest(artifacts.Repository), cancellationToken)` starts.
118. The archive service checks cancellation.
119. It creates `CompletionArtifacts` over the same repository artifact store.
120. It computes archive index as the count of existing archive directories plus one.
121. It computes archive directory `.agents/archive/epics/{index}`.
122. It computes synthesis path `.agents/archive/epics/{index}.md`.
123. It checks for archive directory collision.
124. It checks for synthesis file collision.
125. It reports phase `Archive completed execution workspace`.
126. It copies `.agents/epic.md` to `{archiveDirectory}/epic.md` if present.
127. It moves `.agents/decisions` contents to `{archiveDirectory}/decisions`.
128. It moves `.agents/deltas` contents to `{archiveDirectory}/deltas`.
129. It moves `.agents/handoffs` contents to `{archiveDirectory}/handoffs`.
130. It moves `.agents/milestones` contents to `{archiveDirectory}/milestones`.
131. It moves non-implementation review contents to `{archiveDirectory}/review`.
132. It moves details, operational context, and plan files into the archive when present.
133. It reports phase `Synthesize completed epic`.
134. It runs `SynthesizeCompletedEpic` with the archive index label through the completion prompt runner.
135. It reads `.agents/archive/epics/{index}.md`.
136. If synthesis content is missing or blank, it throws `CompletionCertificationException`.
137. It returns `CompletedEpicArchiveResult`.
138. `UpdateRoadmapCompletionContextAsync(projectContext, evaluationPath, archive.SynthesisPath, archive.SynthesisContent, cancellationToken)` starts.
139. It sets runtime prompt `UpdateRoadmapCompletionContext`.
140. It writes phase `Update roadmap completion context`.
141. It loads the `UpdateRoadmapCompletionContext` prompt contract.
142. It ensures `.agents/projections/roadmap-completion-update.md` through `ProjectionCache`.
143. It builds completion-update context.
144. The context builder reads `.agents/core/roadmap-completion-context.md`.
145. It reads the latest evaluation evidence path.
146. It adds projection content, current roadmap completion context, completed-epic synthesis content, latest completion evaluation, and repository inspection instructions.
147. It adds optional non-implementation review evidence sections when present.
148. It rejects raw project-context markers.
149. It calls `RunPromptTransitionAsync` from `CompletionEvaluationAndContextUpdate` to `SelectNextStrategicInitiative`.
150. The transition input resolver snapshots the update projection, roadmap completion context, active epic, and completion evaluation evidence.
151. The prompt runner runs `UpdateRoadmapCompletionContext` with completed-epic synthesis as secondary input.
152. Prompt start and completion are persisted to state and journal by the shared transition runner.
153. The update output is written to `.agents/core/roadmap-completion-context.md`.
154. HITL requests are captured from the rewritten completion context when configured.
155. The same update output is also written as numbered evidence `.agents/evidence/evaluations/roadmap-completion-update.NNNN.md`.
156. The numbered update evidence path is not carried into `PersistCompletionRouteAsync` outputs.
157. `SupersedeActiveSelectionAsync` supersedes active selection provenance for roadmap completion context drift.
158. It upserts `.agents/selection.md` lifecycle as `Superseded`.
159. It appends a decision-ledger entry for `UpdateRoadmapCompletionContext`.
160. Control returns to `RunCompletionCertificationAsync`.

### Non-Implementation Review Block Trace

This trace runs at step 24 when review is configured and blocked.

161. `PersistNonImplementationCompletionReviewBlockedAsync` captures `blockedAt`.
162. It builds the required next step pointing at the non-implementation decisions file.
163. It builds details from review evidence paths and blocker messages.
164. It writes `.agents/evidence/blockers/non-implementation-completion-review-blocked.NNNN.md`.
165. It builds outputs from review evidence paths plus blocker path.
166. It calls `SaveStateAsync` with current state `EvidenceBlocked`, status `Paused`, from `EpicCompletionDetected`, to `EvidenceBlocked`, prompt `NonImplementationCompletionReview`, projection `None`, output list, and decision `Pending non-implementation HITL review`.
167. It writes blockers and transition intent `ResolveNonImplementationCompletionReview`.
168. It returns `RoadmapOutcome.Paused`.

### Invalid-Certification Block Trace

This trace runs at step 101 when the parser succeeds but policy validation fails.

169. `PersistInvalidCompletionCertificationAsync` captures `blockedAt`.
170. It resolves rejection reason.
171. It builds required next step.
172. It builds blocked-transition label.
173. It renders details containing parsed decision, validation failure, blocked transition, and preserved evidence.
174. It writes `.agents/evidence/blockers/invalid-completion-certification.NNNN.md`.
175. It builds routing context from closure recommendation, completion status, drift classification, blocked transition, and validation failure.
176. It resolves a `CompletionCertificationRouting` input snapshot for the evaluation path.
177. It builds outputs `[evaluationPath, blockerPath]`.
178. It appends `CompletionCertificationRejected` to `.agents/journal/transitions.jsonl`.
179. It calls `SaveStateAsync` with current state `EvidenceBlocked`, status `Paused`, from `CompletionEvaluationAndContextUpdate`, to `EvidenceBlocked`, prompt `CompletionCertificationRouting`, projection `.agents/projections/epic-completion-evaluation.md`, output list, and decision `Invalid Completion Certification`.
180. It writes blocker row and transition intent `ResolveInvalidCompletionCertification`.
181. It returns `RoadmapOutcome.Paused`.

### Failure and Cancellation Trace

182. If `EvaluateEpicCompletionAndDrift` prompt execution throws a non-cancellation exception, the shared transition runner appends `TransitionFailed`, saves `EvidenceBlocked` with status `Failed`, records `ResolveTransitionFailure`, and throws `RoadmapStepException.AlreadyPersisted`.
183. If `UpdateRoadmapCompletionContext` prompt execution throws a non-cancellation exception, the same shared transition failure path runs with from `CompletionEvaluationAndContextUpdate`, to `SelectNextStrategicInitiative`, prompt `UpdateRoadmapCompletionContext`, and output `.agents/core/roadmap-completion-context.md`.
184. `OperationCanceledException` is not caught by the shared transition runner; outer `RunAsync` writes `Cancelled`.
185. If projection ensure throws before the prompt transition starts, no prompt start state is written by this transition.
186. If completion parsing throws after evaluation evidence is written, the generic outer exception handling reports an ephemeral blocker and returns failed; the completed prompt transition state is not retroactively marked failed.
187. If archive or synthesis throws on a close route, the evaluation evidence and decision ledger entry already exist; the route is not persisted unless later code completes.
188. If `PersistCompletionRouteAsync` throws, any prior close-route archive, completion-context update, selection superseding, and lifecycle update may already exist.

## 3. Concern Inventory

| Execution step | Primary concern | Additional concerns | Where concerns become mixed |
|---|---|---|---|
| Resume planner selects `EvaluateCompletionClaim` | routing | persisted state interpretation | Minimal. It only decides to enter certification. |
| Read persisted execution evidence path | artifact lookup | persisted state parsing, recovery validation | It combines transition-intent evidence and last-transition output parsing. |
| Optional persist completion claim branch | state mutation | route intent persistence, legacy/private method behavior | Mixed but not live for current resume entry. |
| Non-implementation completion review | validation/gate | external review service, blocker routing | A pre-certification gate can write durable blocker state and return. |
| Console phase | reporting | none | Clean. |
| Load prompt contract | configuration | allowed decisions, stale policy, writer/parser metadata | Light setup concern. |
| Ensure evaluation projection | artifact read/write | projection prompt execution, validation, manifest persistence, blocker evidence | Strongly mixed: input preparation can execute an agent and mutate manifest/blocker artifacts. |
| Build evaluation context | artifact read | execution-preparation freshness, optional review evidence, validation | Mixed because prompt context assembly also enforces freshness and raw-context safety. |
| Resolve transition input snapshot | provenance/evidence | artifact reads and hashing | Mixed with validation because missing inputs throw here. |
| Save started state | persistence | active artifact status, manifest counts, split counts, prior blockers | Strongly mixed global state refresh. |
| Append start journal | journaling | input snapshot persistence | Lightly mixed. |
| Run evaluation prompt | prompt execution | prompt rendering, prompt policy, console streaming, agent session selection | Mixed execution infrastructure surrounds the actual prompt call. |
| Save completed state | persistence | same global state refresh as start | Strongly mixed. |
| Write evaluation evidence | artifact write | sequence allocation | Lightly mixed. |
| Capture HITL requests | HITL extraction | optional side effect | Hidden optional side effect after evidence write. |
| Parse evaluation | parsing | vocabulary validation | Clean but positioned after durable evidence/state writes. |
| Validate certification policy | decision validation | route eligibility | Clean local logic, but its failure creates durable blockers elsewhere. |
| Append evaluation decision | decision ledger | id allocation, timestamp | Light persistence mixing. |
| Persist invalid certification | recovery/blocker | blocker artifact, journal, state, transition intent | Strongly mixed error branch. |
| Route valid certification | decision | shared completion route to roadmap route mapping | Clean but controls many downstream effects. |
| Archive completed execution workspace | artifact move/copy | archive index allocation, collision checks, prompt phase reporting | Strongly mixed artifact archival. |
| Synthesize completed epic | prompt execution | writable planning agent, archive artifact validation | Mixed because close routing depends on generated archive summary. |
| Update roadmap completion context | prompt transition | projection ensure, context build, state/journal, artifact write, evidence write, selection supersede, decision ledger | Strongly mixed and conditional inside completion certification. |
| Upsert active epic lifecycle | lifecycle | route reporting in notes | Lightly mixed. |
| Persist completion route | routing persistence | input snapshot, journal, state, transition intent, next transitions | Strongly mixed finalization. |
| Return outcome | result | no persistence | Clean. |

The highest-concern mixing occurs in projection ensure, shared prompt transition execution, close-route archive/update, invalid-certification persistence, and final route persistence.

## 4. Hidden Steps

These steps are present only through helper calls or compact branches:

1. Resolve persisted execution evidence path from transition intent and last output.
2. Validate that the execution evidence artifact is still present.
3. Optionally preserve the execution completion claim before certification.
4. Run non-implementation HITL completion-review gate.
5. Build evaluation projection provenance.
6. Validate and refresh projection manifest entry.
7. Convert projection validation or staleness failure into blocker evidence.
8. Resolve fresh milestone spec paths from execution-preparation provenance.
9. Add optional non-implementation review evidence into prompt context.
10. Reject raw project-context markers in runtime context.
11. Build transition input snapshot for evaluation prompt.
12. Capture transition correlation id and timing.
13. Refresh global state summary while saving one transition state.
14. Render runtime prompt and append implementation-first prompt policy.
15. Stream prompt output to console renderer and echo silent output.
16. Allocate numbered evaluation evidence path.
17. Capture optional HITL request markers from evaluation evidence.
18. Validate parsed completion fields against vocabulary.
19. Validate recommendation/status/drift consistency against completion policy.
20. Map completion route to roadmap state, lifecycle state, next transitions, and CLI outcome.
21. Allocate completed-epic archive index.
22. Check archive and synthesis collisions.
23. Move execution-workspace artifacts into archive.
24. Run `SynthesizeCompletedEpic` through writable planning.
25. Build update-context prompt from completion context, synthesis, and evaluation.
26. Use completed-epic synthesis as `UpdateRoadmapCompletionContext` secondary input.
27. Write completion-context update evidence without carrying that evidence path into final route outputs.
28. Supersede active selection provenance after completion context changes.
29. Build routing context and snapshot for `CompletionCertificationRouting`.
30. Persist final transition intent differently for routes with extra outputs.

## 5. Natural Step Boundaries

Step 1: Resume completion claim

Purpose: Enter completion certification from persisted `EpicCompletionDetected`.

Inputs: persisted `.agents/state.json`, `ProjectContext`.

Outputs: execution evidence path, call into completion certification.

---

Step 2: Gate on non-implementation review

Purpose: Stop certification until pending HITL review decisions are resolved.

Inputs: optional `INonImplementationCompletionReviewService`.

Outputs: either continue, or blocker evidence plus paused `EvidenceBlocked` state.

---

Step 3: Prepare completion evaluation

Purpose: Make `EvaluateEpicCompletionAndDrift` ready to run.

Inputs: prompt contract, project context, projection cache, active epic, execution evidence, fresh milestone specs, optional review evidence.

Outputs: evaluation projection content and rendered runtime context.

---

Step 4: Run completion evaluation prompt

Purpose: Execute the read-only evaluation prompt with transition start/completion persistence.

Inputs: runtime prompt name, projection path, rendered context, transition inputs, output directory.

Outputs: raw evaluation output, prompt transition journal records, started/completed state writes.

---

Step 5: Materialize and parse evaluation

Purpose: Preserve the raw certification and extract the decision fields.

Inputs: raw evaluation output.

Outputs: numbered evaluation evidence, optional HITL captures, parsed `CompletionEvaluationDecision`.

---

Step 6: Validate certification

Purpose: Decide whether the parsed recommendation is policy-coherent.

Inputs: parsed decision, completion policy.

Outputs: valid certification result or invalid-certification blocker path/state.

---

Step 7: Route certification

Purpose: Convert the valid certification recommendation into roadmap target state and effects.

Inputs: certification decision, completion router, roadmap route mapper.

Outputs: `RoadmapCompletionRoute`.

---

Step 8: Apply close-route archive/update effects

Purpose: For close routes, archive the completed execution workspace and update strategic completion context.

Inputs: repository artifacts, active epic, execution workspace artifacts, evaluation evidence, project context, completed-epic synthesis.

Outputs: archive directory, synthesis artifact, updated roadmap completion context, update evidence, superseded selection.

---

Step 9: Apply lifecycle effects

Purpose: Make the active epic lifecycle match the selected route.

Inputs: roadmap route.

Outputs: lifecycle entry for `.agents/epic.md`.

---

Step 10: Persist route and return

Purpose: Record the final completion-certification route as the durable state-machine result.

Inputs: roadmap route, parsed decision, projection path, evaluation path, optional synthesis path.

Outputs: route journal record, final `.agents/state.json`, transition intent, next transitions, returned `RoadmapOutcome`.

## 6. Mixed-Concern Analysis

Step 1 mixes resume routing with artifact recovery. A reader following completion certification must understand both `RoadmapResumePlanner` and `ReadPersistedExecutionEvidencePathAsync` before the certification method has even started.

Step 2 mixes validation with durable workflow blocking. A review service result can cause blocker evidence, state mutation, transition intent, and a returned outcome without running the completion prompt.

Step 3 mixes input preparation with projection generation, projection validation, manifest persistence, blocker creation, active epic reads, execution evidence reads, milestone provenance checks, optional review evidence, and prompt-context safety validation. The transition cannot be understood by reading only the certification method.

Step 4 mixes prompt execution with state persistence, journaling, input snapshot hashing, prompt rendering, prompt policy application, console streaming, and failure recovery. The actual prompt run is embedded inside the generic transition runner.

Step 5 mixes artifact evidence writing, HITL request capture, and parser preparation. The raw prompt output becomes durable before parsing can reject it.

Step 6 is logically a validation step, but invalid results are handled by a large persistence branch that writes blocker evidence, journal records, state, transition intent, and blocker rows.

Step 7 is mostly clean, but it bridges shared completion routing into roadmap-specific state, lifecycle, next transitions, and CLI outcome.

Step 8 mixes route execution with archive mutation, file moves, synthesis prompt execution, projection ensure for the update prompt, context building, another prompt transition, completion-context artifact write, numbered update evidence, selection provenance superseding, lifecycle mutation, and decision ledger append. This is the largest conditional branch in the transition.

Step 9 is small but order-sensitive. It happens after close-route update effects and before final route persistence.

Step 10 mixes final routing with input snapshot creation, journal append, state save, transition intent construction, output list construction, and next-transition assignment.

## 7. Data Flow

Persisted roadmap state

Origin: `.agents/state.json` loaded by `RunAsync`, `resumePlanner`, `ReadPersistedExecutionEvidencePathAsync`, and `SaveStateAsync`.

Consumed by: resume planning, execution evidence recovery, state-preserving saves, final state persistence.

Output influence: determines entry into certification and preserves existing retired epics/blockers unless overwritten.

---

Project context

Origin: `projectContextLoader.LoadAsync` during preflight.

Consumed by: projection provenance, projection generation, evaluation/update context building.

Output influence: projection freshness and prompt context.

---

Execution evidence path

Origin: `state.TransitionIntent.EvidencePaths` and `state.LastTransition.Output`.

Consumed by: evaluation context builder, transition input snapshot, evaluation parser recovery context, final route transition intent.

Output influence: raw evaluation evidence, final route state, blocker recovery.

---

Evaluation projection

Origin: `.agents/projections/epic-completion-evaluation.md`, or generated from `ProjectionForEvaluateEpicCompletionAndDrift`.

Consumed by: projection validator, manifest store, evaluation context, transition input snapshot, decision ledger, route persistence.

Output influence: prompt input and final route provenance.

---

Active epic

Origin: `.agents/epic.md`.

Consumed by: evaluation context, evaluation input snapshot, update-context input snapshot, archive service, lifecycle update.

Output influence: completion evaluation, completed epic archive, lifecycle state.

---

Milestone specs

Origin: fresh paths from `ExecutionPreparationProvenanceService`, read from `.agents/specs/*.md`.

Consumed by: evaluation context builder and transition input snapshot.

Output influence: completion evaluation input evidence.

---

Optional non-implementation review evidence

Origin: review artifacts known to `NonImplementationReviewPromptEvidence`.

Consumed by: evaluation and update context builders.

Output influence: prompt context only unless review blocks before certification.

---

Raw evaluation output

Origin: `RoadmapPromptRunner.RunRuntimePromptAsync("EvaluateEpicCompletionAndDrift")`.

Consumed by: evaluation evidence writer, HITL capture, completion parser.

Output influence: numbered evaluation evidence and parsed route decision.

---

Parsed completion decision

Origin: `CompletionEvaluationParser`.

Consumed by: completion policy, decision ledger, completion router, invalid-certification blocker, final route persistence.

Output influence: final target state, lifecycle state, next transitions, CLI outcome.

---

Completion policy result

Origin: `CompletionCertificationPolicy.Validate`.

Consumed by: invalid-certification persistence or route selection.

Output influence: either blocker state or route continuation.

---

Roadmap completion route

Origin: `CompletionCertificationRouter.Route` followed by `RoadmapCompletionRouteMapper.Map`.

Consumed by: archive/update branch, lifecycle upsert, final route persistence, return value.

Output influence: target state, transition status, lifecycle state, transition intent, next transitions, `RoadmapOutcome`.

---

Completed epic synthesis

Origin: `CompletedEpicArchiveService` after archive and `SynthesizeCompletedEpic`.

Consumed by: update context builder, update prompt secondary input, final close-route output list.

Output influence: roadmap completion context update and final transition intent for close routes.

---

Updated roadmap completion context

Origin: `UpdateRoadmapCompletionContext` prompt output.

Consumed by: artifact write, HITL capture, update evidence write, final close-route output list.

Output influence: `.agents/core/roadmap-completion-context.md`, active selection superseding, decision ledger.

---

Final state document

Origin: `PersistCompletionRouteAsync` via `SaveStateAsync`.

Consumed by: future resume/status/unblock runs.

Output influence: persisted current state, last transition summary, blockers, transition intent, next transitions, active artifact rows, projection counts, split counts.

## 8. Human Navigation Audit

Minimum files and members an engineer must read to understand only this transition:

1. `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`
   - `ExecuteResumePlanAsync`
   - `RunCompletionCertificationAsync`
   - `PersistNonImplementationCompletionReviewBlockedAsync`
   - `ReadPersistedExecutionEvidencePathAsync`
   - `UpdateRoadmapCompletionContextAsync`
   - `RunPromptTransitionAsync`
   - `RunPromptTransitionWithCompletionAsync`
   - `PersistCompletionRouteAsync`
   - `PersistInvalidCompletionCertificationAsync`
   - `AppendDecisionAsync`
   - `SupersedeActiveSelectionAsync`
   - `SaveStateAsync`
2. `src/LoopRelay.Roadmap.Cli/RoadmapResumePlanner.cs`
   - `PlanForStateAsync` for `EpicCompletionDetected`
   - prompt readiness and artifact snapshot context for resume behavior
3. `src/LoopRelay.Roadmap.Cli/RoadmapState.cs`
   - state names used by entry and route targets
4. `src/LoopRelay.Roadmap.Cli/RoadmapArtifactPaths.cs`
   - evaluation, blocker, projection, archive, active epic, selection, and completion context paths
5. `src/LoopRelay.Roadmap.Cli/PromptContractRegistry.cs`
   - `EvaluateEpicCompletionAndDrift` and `UpdateRoadmapCompletionContext` contracts
6. `src/LoopRelay.Roadmap.Cli/ProjectionRegistry.cs`
   - projection prompt names and paths
7. `src/LoopRelay.Roadmap.Cli/ProjectionCache.cs`
   - projection generation, manifest update, validation, staleness, blocker behavior
8. `src/LoopRelay.Roadmap.Cli/RoadmapPromptContextBuilder.cs`
   - completion evaluation and completion update contexts
9. `src/LoopRelay.Roadmap.Cli/TransitionInputs.cs`
   - input snapshots for evaluation, update, and routing
10. `src/LoopRelay.Roadmap.Cli/RoadmapPromptRunner.cs`
    - prompt rendering, prompt policy, read-only planning agent behavior
11. `src/LoopRelay.Completion/CompletionEvaluationParser.cs`
    - parsed fields and parse failures
12. `src/LoopRelay.Completion/CompletionCertificationPolicy.cs`
    - valid and invalid combinations
13. `src/LoopRelay.Completion/CompletionCertificationRouter.cs`
    - recommendation-to-route mapping
14. `src/LoopRelay.Roadmap.Cli/RoadmapCompletionRoute.cs`
    - route-to-roadmap state, lifecycle, next transitions, outcome
15. `src/LoopRelay.Completion/CompletedEpicArchiveService.cs`
    - close-route archive and synthesis effects
16. `src/LoopRelay.Roadmap.Cli/ArtifactLifecycleStore.cs`
    - active epic and selection lifecycle writes
17. `src/LoopRelay.Roadmap.Cli/DecisionLedgerStore.cs`
    - decision id allocation and ledger persistence
18. `src/LoopRelay.Roadmap.Cli/TransitionJournalStore.cs`
    - transition journal append behavior
19. `src/LoopRelay.Roadmap.Cli/RoadmapStateStore.cs`
    - state persistence shape
20. `src/LoopRelay.Roadmap.Cli/SelectionProvenance.cs`
    - close-route selection superseding

Relevant tests that anchor current behavior:

1. `tests/LoopRelay.Roadmap.Cli.Tests/CompletionCertificationPolicyTests.cs`
   - accepted and rejected certification combinations
2. `tests/LoopRelay.Roadmap.Cli.Tests/TransitionInputResolverTests.cs`
   - completion evaluation and update input snapshots
3. `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapFailurePersistenceTests.cs`
   - prompt failure persistence for `EvaluateEpicCompletionAndDrift` and `UpdateRoadmapCompletionContext`
4. `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapStateMachineUnblockTests.cs`
   - invalid completion certification recovery behavior
5. `tests/LoopRelay.Completion.Tests/CompletionCertificationServiceTests.cs`
   - shared completion archive/update behavior outside the roadmap state machine

## 9. Extraction Boundary

Smallest extraction boundary:

Extract the body and directly required helper flow of `RunCompletionCertificationAsync` into a named linear transition handler whose public operation returns `RoadmapOutcome`.

The handler should own:

1. completion-claim evidence path already resolved by the caller, or the existing evidence-path recovery if the extraction starts at `EvaluateCompletionClaim`
2. optional non-implementation completion-review gate
3. evaluation prompt preparation
4. evaluation prompt transition execution
5. evaluation evidence materialization
6. completion parsing and policy validation
7. invalid-certification blocking
8. valid route mapping
9. close-route archive and completion-context update
10. active epic lifecycle update
11. final route persistence
12. returned route outcome

Keep outside the handler:

1. project-context preflight
2. prompt-contract snapshot emission
3. startup and resume plan selection
4. execution-loop transport and execution-disposition parsing
5. unblock recovery after invalid certification
6. whole-state-machine status/report handling
7. unrelated selection, epic-preparation, split, create, and milestone transitions

The natural extraction boundary is the existing completion-certification execution flow, not a new command or architecture.

## 10. Required Inputs

Required:

1. `ProjectContext`
2. execution evidence path under `.agents/evidence/execution`
3. `CancellationToken`
4. artifact access through `RoadmapArtifacts`
5. `PromptContractRegistry`
6. `ProjectionCache`
7. `RoadmapPromptContextBuilder`
8. `RoadmapPromptRunner`
9. `TransitionInputResolver`
10. `TransitionJournalStore`
11. `RoadmapStateStore`
12. `ProjectionManifestStore`, because `SaveStateAsync` reads manifest counts
13. `DecisionLedgerStore`
14. `ArtifactLifecycleStore`
15. `CompletionCertificationPolicy`
16. `CompletionCertificationRouter`
17. `ICompletedEpicArchiveService`
18. selection provenance service, because close-route update supersedes active selection
19. console, for phase and warning behavior through called helpers

Required only if preserving the private method's unused `persistCompletionClaim: true` branch:

1. `executionStarted`
2. `ExecutionDispositionRoute completionRoute`

Optional:

1. `INonImplementationCompletionReviewService`
2. HITL request capture service
3. optional non-implementation review evidence artifacts

Incidental in the current state-machine class:

1. selection parser and selection provenance capture for selection transitions other than close-route superseding
2. epic promotion service
3. split bundle interpreter
4. invariant validator outside update/prompt failure paths
5. startup planner
6. resume planner beyond entry selection
7. unblock planner beyond invalid-certification recovery
8. execution bridge and execution transport
9. milestone bundle extraction
10. roadmap source selection inputs

## 11. Required Outputs

Externally observable outputs and effects:

1. returned `RoadmapOutcome`
2. console phase output for evaluation and close-route update
3. console phase output from archive service when close routes run
4. `.agents/projections/epic-completion-evaluation.md` when generated
5. `.agents/projections/roadmap-completion-update.md` when generated by close routes
6. `.agents/projections/manifest.json`
7. `.agents/evidence/evaluations/epic-completion-and-drift.NNNN.md`
8. optional `.agents/evidence/evaluations/roadmap-completion-update.NNNN.md`
9. optional `.agents/evidence/blockers/non-implementation-completion-review-blocked.NNNN.md`
10. optional `.agents/evidence/blockers/invalid-completion-certification.NNNN.md`
11. optional `.agents/evidence/blockers/projection-blocked.NNNN.md`
12. `.agents/journal/transitions.jsonl` records
13. `.agents/state.json`
14. `.agents/decision-ledger.json` and markdown ledger output if the store writes both
15. `.agents/artifacts/lifecycle.json`
16. optional HITL request capture outputs
17. close-route archive directory `.agents/archive/epics/{index}`
18. close-route synthesis `.agents/archive/epics/{index}.md`
19. moved execution workspace directories/files under the close-route archive
20. `.agents/core/roadmap-completion-context.md` rewritten on close routes
21. selection provenance manifest changes on close-route completion-context update
22. active selection lifecycle changed to `Superseded` on close-route update
23. active epic lifecycle changed to `Completed`, `Executing`, or `Ready` according to route
24. thrown `RoadmapStepException.AlreadyPersisted` for prompt failures
25. thrown/caught non-roadmap exceptions for parse/archive/finalization failures, with outer ephemeral blocker warning behavior
26. cancellation state when cancellation reaches outer `RunAsync`

## 12. Behavioral Equivalence Contract

Inputs that must remain equivalent:

1. persisted roadmap state at `EpicCompletionDetected`
2. recovered execution evidence path selection order
3. `ProjectContext`
4. active epic content
5. fresh milestone spec set from execution-preparation provenance
6. completion-evaluation projection content and manifest provenance
7. optional non-implementation review service result
8. optional non-implementation review evidence artifacts
9. completion policy and route table
10. close-route archive existing directory count

Outputs that must remain equivalent:

1. same `RoadmapOutcome` for each route and block condition
2. same prompt names, projection paths, from/to states, output paths, and decisions in state summaries
3. same journal event names and ordering
4. same transition input roles and hashed inputs
5. same decision-ledger entries and order
6. same numbered evidence prefixes and directories
7. same lifecycle states and lifecycle notes
8. same transition intents and next transitions
9. same close-route archive path and synthesis path allocation
10. same selection superseding behavior after roadmap completion context update
11. same console phase messages from the state machine and archive service
12. same prompt failure persistence behavior
13. same parse failure behavior after evaluation evidence is written
14. same cancellation behavior

Persisted state contract:

1. Evaluation prompt start saves current state `CompletionEvaluationAndContextUpdate`, status `Started`, from `EpicCompletionDetected`, to `CompletionEvaluationAndContextUpdate`.
2. Evaluation prompt success saves current state `CompletionEvaluationAndContextUpdate`, status `Completed`.
3. Invalid certification saves current state `EvidenceBlocked`, status `Paused`, from `CompletionEvaluationAndContextUpdate`, to `EvidenceBlocked`, prompt `CompletionCertificationRouting`, decision `Invalid Completion Certification`.
4. Final route save uses route target state and route transition status.
5. Close routes save target `SelectNextStrategicInitiative`, status `Completed`, next transition `SelectNextEpic`.
6. Continue route saves target `ExecutionLoop`, status `Paused`, next transition `ContinueExecution`.
7. Reopen route saves target `EpicPreparationAudit`, status `Paused`, next transition `EpicPreparationAudit`.
8. Gather-more-evidence route saves target `EvidenceGathering`, status `Paused`, next transitions `GatherAdditionalEvidence` and `EvaluateEpicCompletionAndDrift`.

Artifact contract:

1. Evaluation raw output is written before parsing.
2. HITL capture for evaluation evidence happens before parsing.
3. Close-route archive happens after valid route mapping and before completion-context update.
4. Completion-context update output rewrites `.agents/core/roadmap-completion-context.md`.
5. Completion-context update evidence is written but not included in the final route output list.
6. Active selection is superseded only after completion-context update output is written.

Exception and recovery contract:

1. Projection failures occur before prompt transition start persistence for the affected prompt.
2. Runtime prompt failures are persisted as `EvidenceBlocked` with `ResolveTransitionFailure` and rethrown as already persisted.
3. `OperationCanceledException` is not swallowed inside the transition runner.
4. Parse failures after evaluation evidence are not converted into durable invalid-certification blockers.
5. Policy failures after parsing are converted into durable invalid-certification blockers.
6. Archive and synthesis failures are not converted by this method into a completion-certification blocker.
7. Final route persistence must remain after lifecycle/update effects, matching current ordering.

Anything not externally observable, such as private local variable names or internal grouping inside the extracted handler, is outside this contract.

## 13. Transition Handler Shape

Recovered linear structure:

```text
Execute()

-> Resolve live completion claim inputs

-> Gate non-implementation completion review

-> Load EvaluateEpicCompletionAndDrift contract

-> Ensure EvaluateEpicCompletionAndDrift projection

-> Build completion evaluation context

-> Run evaluation prompt transition

-> Write evaluation evidence

-> Capture evaluation HITL requests

-> Parse evaluation

-> Validate certification policy

-> Append evaluation decision

-> If invalid:
     -> Write invalid-certification blocker
     -> Append rejection journal
     -> Save EvidenceBlocked state
     -> Return Paused

-> Route valid certification

-> If route requires roadmap completion context update:
     -> Archive completed execution workspace
     -> Synthesize completed epic
     -> Load UpdateRoadmapCompletionContext contract
     -> Ensure UpdateRoadmapCompletionContext projection
     -> Build completion update context
     -> Run update prompt transition
     -> Write roadmap completion context
     -> Capture completion-context HITL requests
     -> Write update evidence
     -> Supersede active selection
     -> Append update decision

-> Upsert active epic lifecycle

-> Persist completion route

-> Return route outcome
```

Failure shape:

```text
Projection failure
-> Write projection blocker if validation/staleness failure
-> Throw before prompt transition start

Prompt failure
-> Append TransitionFailed journal
-> Save EvidenceBlocked failed state
-> Throw already-persisted RoadmapStepException

Invalid parsed certification
-> Write invalid-certification blocker
-> Append CompletionCertificationRejected journal
-> Save EvidenceBlocked paused state
-> Return Paused

Cancellation
-> Propagate OperationCanceledException
-> Outer RunAsync writes Cancelled
```

## 14. Readability Improvements

Extracting this transition would make the current implementation easier to read in concrete ways:

1. Completion certification would have one named entry and one return surface instead of being embedded among selection, epic-preparation, split, milestone, unblock, and persistence helpers in `RoadmapStateMachine.cs`.
2. The engineer could read the transition in the same order it executes: resume input, review gate, evaluation prompt, parse/policy, route, conditional close update, lifecycle, route persistence.
3. The invalid-certification branch would be visible as a named block instead of interrupting the middle of a large state-machine method.
4. The close-route archive/update branch would be isolated as the only place that mutates archive, completion context, selection provenance, and selection lifecycle.
5. Prompt-transition infrastructure would still be shared, but the completion-specific calls and outputs would sit in one local flow.
6. The ordering hazard around parse-after-evidence-write would be explicit.
7. The ordering hazard around close-route update-before-final-route-persistence would be explicit.
8. Required dependencies would shrink from the entire state-machine constructor surface to the completion-specific stores, prompt services, route services, and artifact services.
9. Tests could target the completion handler by route: review blocked, prompt failure, parse failure, invalid certification, close route, continue route, reopen route, gather-more-evidence route, update prompt failure.
10. Debugging would require fewer cross-file jumps because route mapping, lifecycle effects, and final state outputs would be presented in one execution flow.

The extraction should not change route semantics, artifact schemas, prompt content, policy rules, or command surfaces. It should only make this existing transition linear and navigable.
