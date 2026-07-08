# RealignEpic Transition Extraction Audit

Exactly one transition is audited here: `RealignEpic`.

Scope: active-epic rewrite and promotion after `EpicPreparationAudit` returns disposition `Realign`, implemented by `RewriteActiveEpicAsync("RealignEpic", RoadmapState.RealignEpic, ...)` in `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`.

Entry: `AuditAndPrepareExistingEpicAsync` has already produced audit evidence, parsed it, recorded the audit decision, and selected the `Realign` branch.

Exit: `RewriteActiveEpicAsync` returns an `ArtifactPromotionResult` to `AuditAndPrepareExistingEpicAsync`, which converts it to either `EpicPreparationResult.ActiveEpicReady` or `EpicPreparationResult.Blocked`.

Included:

- dispatch from the `Realign` audit disposition into `RewriteActiveEpicAsync`
- active epic read, with current-selection fallback
- audit evidence read
- `RealignEpic` prompt contract lookup
- `RealignEpic` projection cache resolution
- runtime context construction
- transition input snapshot capture
- prompt execution through `RunPromptForPromotionAsync`
- prompt-started, prompt-completed, and failure state writes
- transition journal writes
- active epic promotion through `PromoteActiveEpicAsync`
- artifact classification and validation
- lifecycle writes for promoted active epic or blocked evidence
- HITL request capture from promoted active epic content
- paused blocker persistence for rejected realignment output

Excluded:

- the already-audited `SelectNextEpic` transition
- the already-audited `EpicPreparationAudit` transition before it chooses `Realign`
- the sibling `ReimagineEpic` transition
- downstream `GenerateMilestoneDeepDivesForEpic`
- resume/startup planning except where persisted state produced by `RealignEpic` is observable
- any redesign of prompt contracts, projections, promotion, lifecycle, state, or journal storage

## Deliverable 1: Transition Narrative

Current State

An existing roadmap epic has been selected and audited. The audit output was written as evidence, parsed, and recorded in the decision ledger. Its disposition is `Realign`, meaning the selected epic should remain the same strategic initiative but needs a minimal audit-grounded rewrite before milestone generation can proceed.

Goal

Produce a validated replacement active epic at `.agents/epic.md`, preserving the existing active epic when the rewrite is blocked, ambiguous, structurally invalid, or when the runtime prompt fails.

Major Steps

1. Route the `Realign` audit disposition into the `RealignEpic` rewrite path.
2. Announce the `RealignEpic` phase.
3. Resolve the epic content to realign, preferring `.agents/epic.md` and falling back to the current selection.
4. Load the audit evidence that justified realignment.
5. Resolve the `RealignEpic` contract and projection.
6. Build the runtime prompt context from projection, current epic content, audit output, and repository inspection instructions.
7. Capture transition inputs and hashes.
8. Persist prompt-started state and journal the transition start.
9. Run the `RealignEpic` runtime prompt.
10. Persist prompt-completed state and journal the prompt output event.
11. Classify and validate the output as an active epic candidate.
12. Promote the output to `.agents/epic.md`, or preserve it as blocker evidence.
13. Persist final state and journal the promotion result.
14. Return the promotion result to the existing-epic preparation caller.

Completion

The transition completes in one of three externally visible ways:

- Promoted: `.agents/epic.md` is replaced with the realigned epic, lifecycle marks it `Ready`, the journal records `ArtifactPromoted`, state records `ActiveEpicReady` / `Completed`, and the caller proceeds to milestone generation.
- Promotion blocked: `.agents/epic.md` remains unchanged, the rejected output is written under `.agents/evidence/blockers/active-epic-promotion.NNNN.md`, lifecycle marks that evidence `Blocked`, the journal records `ArtifactPromotionBlocked`, state records `EvidenceBlocked` / `Paused`, and the caller returns paused.
- Runtime failure: no realigned epic is promoted, the journal records `TransitionFailed`, state records `EvidenceBlocked` / `Failed` with intent `ResolveTransitionFailure`, and outer `RunAsync` returns failed.

## Deliverable 2: Current Execution Trace

This trace starts at the realignment dispatch point. The audit prompt, audit evidence write, audit parsing, and audit decision ledger entry already happened before this transition begins.

### Entry And Input Recovery

R1. `AuditAndPrepareExistingEpicAsync` evaluates `decision.Disposition`.

R2. The `Realign` branch calls `RewriteActiveEpicAsync("RealignEpic", RoadmapState.RealignEpic, projectContext, auditPath, cancellationToken)`.

R3. `RewriteActiveEpicAsync` enters with:

- `runtimePrompt = "RealignEpic"`
- `state = RoadmapState.RealignEpic`
- `projectContext`
- `auditPath`
- `cancellationToken`

R4. `console.Phase(runtimePrompt)` writes the phase text `RealignEpic`.

R5. The transition attempts to read `.agents/epic.md` through `artifacts.ReadAsync(RoadmapArtifactPaths.ActiveEpic)`.

R6. If `.agents/epic.md` exists, its content becomes `selectionOrEpic`.

R7. If `.agents/epic.md` is missing, the transition calls `ReadCurrentSelectionAsync(cancellationToken)`.

R8. `ReadCurrentSelectionAsync` checks cancellation.

R9. It reads `.agents/selection.md` through `artifacts.ReadRequiredAsync`.

R10. It resolves the `SelectNextEpic` projection path from `RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"]`.

R11. It reads `.agents/projections/select-next-epic.md`.

R12. If that projection is missing or blank, it throws `RoadmapStepException("Active selection cannot be used because its SelectNextEpic projection is missing.")`.

R13. It loads persisted roadmap state through `stateStore.LoadAsync`.

R14. It calls `selectionProvenance.CaptureCurrentCycleAsync(selectionProjection, state?.RetiredEpics ?? [], cancellationToken)`.

R15. Selection provenance rebuilds the current `SelectNextEpic` input cycle from the selection projection, roadmap completion context, roadmap sources, and retired epics.

R16. Selection provenance resolves and hashes that current selection-cycle snapshot.

R17. `ReadCurrentSelectionAsync` calls `selectionProvenance.EvaluateActiveSelectionFreshnessAsync(currentCycle, state?.RetiredEpics ?? [], cancellationToken)`.

R18. Selection provenance loads `.agents/selection-provenance-manifest.json`, identifies the trusted active selection entry, reads `.agents/selection.md`, hashes it, and compares artifact hash plus cycle hash.

R19. If freshness is not fresh, `ReadCurrentSelectionAsync` throws a `RoadmapStepException` with the stale reasons.

R20. If fresh, `ReadCurrentSelectionAsync` returns the selection markdown as `selectionOrEpic`.

R21. `RewriteActiveEpicAsync` reads `auditPath` through `artifacts.ReadRequiredAsync(auditPath)`.

R22. If `auditPath` is missing or empty, `ReadRequiredAsync` throws `RoadmapStepException("Required artifact is missing or empty: {auditPath}")`.

### Contract And Projection

R23. `contractRegistry.Get("RealignEpic")` loads the prompt contract.

R24. The contract declares required input `.agents/epic.md`, required output `.agents/epic.md`, allowed decision `Realign`, writer `ArtifactPromotionService`, stale projection policy `Block`, parser `EpicAuthoringOutputClassifier+EpicArtifactValidator`, and blocking heading `# Epic Realignment Blocked`.

R25. `projectionCache.EnsureAsync("RealignEpic", projectContext, contract, cancellationToken)` starts.

R26. `ProjectionCache` resolves the `RealignEpic` projection definition from `ProjectionRegistry`.

R27. The definition maps runtime prompt `RealignEpic` to projection prompt `ProjectionForRealignEpic` and path `.agents/projections/realign-epic.md`.

R28. `ProjectionProvenanceFactory` creates current projection provenance from the projection definition and Project Context.

R29. The cache reads `.agents/projections/realign-epic.md`.

R30. If projection content is missing or whitespace, it renders and runs `ProjectionForRealignEpic` through `RoadmapPromptRunner.RunProjectionPromptAsync`.

R31. The projection prompt runs through `RunOneShotAsync` using `AgentSpecs.ReadOnlyPlanning(repository)`.

R32. If the agent turn does not complete, `RunOneShotAsync` throws a `RoadmapStepException` containing the terminal state and diagnostics.

R33. If the generated projection output is empty, `ProjectionCache` throws `RoadmapStepException("{ProjectionForRealignEpic} returned empty projection content.")`.

R34. `ProjectionValidator.Validate("RealignEpic", content)` validates the projection.

R35. Validation requires `# Epic Realignment Projection`, required projection sections, intended consumer `RealignEpic`, and absence of forbidden runtime-state headings.

R36. The projection content is hashed.

R37. The projection manifest is loaded.

R38. Any previous manifest entry for `RealignEpic` is found.

R39. Projection freshness is computed from generated status or current Project Context provenance.

R40. A manifest entry is created or updated.

R41. The projection manifest entry is upserted before validation or freshness blocking returns to the caller.

R42. If validation failed, `ProjectionCache` writes numbered blocker evidence under `.agents/evidence/blockers/projection-blocked.NNNN.md` and throws a `RoadmapStepException` naming the blocked artifact.

R43. If projection content was generated, it writes `.agents/projections/realign-epic.md`.

R44. If the projection is stale and the contract policy is `Block`, `ProjectionCache` writes numbered projection-blocked evidence and throws a `RoadmapStepException`.

R45. If usable, `ProjectionCache` returns `ProjectionCacheResult`.

### Runtime Context And Input Snapshot

R46. `RewriteActiveEpicAsync` calls `contextBuilder.BuildRealignOrReimagineContext(projection.Content, selectionOrEpic, audit)`.

R47. The context builder creates `# Roadmap Runtime Prompt Context`.

R48. It adds `## Projection Content` with the realignment projection.

R49. It adds `## Current Epic` with either `.agents/epic.md` content or the fallback current selection.

R50. It adds `## Audit Output` with the audit evidence content.

R51. It adds `## Repository Inspection Instructions` with read-only audit-grounded instructions.

R52. The context builder validates that raw Project Context file markers are absent.

R53. `RewriteActiveEpicAsync` calls `RunPromptForPromotionAsync`.

R54. Arguments are:

- `from = RoadmapState.RealignEpic`
- `promotionTarget = RoadmapState.ActiveEpicReady`
- `prompt = "RealignEpic"`
- `projectionPath = ".agents/projections/realign-epic.md"`
- `projectContext = rendered runtime context`
- `secondaryInput = audit`
- `outputs = [".agents/epic.md"]`
- `inputContext = TransitionInputContext.AuditEvidence(auditPath)`

R55. `RunPromptForPromotionAsync` calls `inputResolver.ResolveAsync`.

R56. `TransitionInputResolver` starts a new input accumulator.

R57. Because projection path is not `None`, it adds `.agents/projections/realign-epic.md` as required `Projection`.

R58. For runtime prompt `RealignEpic`, it calls `AddCurrentEpicOrSelectionInputAsync`.

R59. `AddCurrentEpicOrSelectionInputAsync` checks whether `.agents/epic.md` exists.

R60. If `.agents/epic.md` exists, it adds it as required `ActiveEpic`.

R61. If `.agents/epic.md` does not exist, it adds `.agents/selection.md` as required `Selection`.

R62. The resolver requires `inputContext.AuditEvidencePath`; if absent, it throws `RoadmapStepException("Transition input context for RealignEpic did not provide a required evidence path.")`.

R63. It adds `auditPath` as required `AuditEvidence`.

R64. The accumulator snapshots inputs in path order.

R65. For every required input path, it reads content through `artifacts.ReadAsync`.

R66. If a required input is missing, it throws `RoadmapStepException("Required transition input is missing: {path}")`.

R67. For every present input, it records path, joined roles, required flag, presence, and SHA-256 hash.

R68. The resolver extracts the projection hash.

R69. It hashes the rendered runtime context.

R70. It hashes the secondary input, which is the audit evidence content.

R71. It computes the transition snapshot hash from runtime prompt name, projection identity, artifact inputs, prompt-context hash, and secondary-input hash.

R72. It returns `TransitionInputSnapshot`.

### Prompt Start And Runtime Execution

R73. `RunPromptForPromotionAsync` creates a new GUID correlation id.

R74. It records `started = DateTimeOffset.UtcNow`.

R75. It starts a stopwatch.

R76. It formats output list `.agents/epic.md`.

R77. It calls `SaveStateAsync` with:

- current `RealignEpic`
- status `Started`
- from `RealignEpic`
- to `ActiveEpicReady`
- prompt `RealignEpic`
- projection `.agents/projections/realign-epic.md`
- output `.agents/epic.md`
- decision `Prompt Started`
- started timestamp
- no completed timestamp
- no new retired epics
- no new blockers

R78. `SaveStateAsync` loads existing state.

R79. It loads the projection manifest.

R80. It builds active artifact rows from known artifacts and lifecycle state.

R81. It reads the last decision id from the decision ledger.

R82. It preserves existing retired epics unless replacements were supplied.

R83. It preserves existing blockers unless replacements were supplied.

R84. It counts split-family JSON artifacts.

R85. It saves `.agents/state.json` with current state `RealignEpic`, transition summary `Started`, projection manifest counts, transition intent from existing state if present, and default next transitions for `RealignEpic`.

R86. `RunPromptForPromotionAsync` appends a `TransitionStarted` journal record to `.agents/journal/transitions.jsonl`.

R87. The journal record includes correlation id, previous state `RealignEpic`, attempted state `ActiveEpicReady`, prompt `RealignEpic`, projection path, prompt contract key `RealignEpic`, input artifact hashes, output path `.agents/epic.md`, duration `0`, result `Started`, parser decision `None`, no error, and the input snapshot.

R88. `RunPromptForPromotionAsync` enters its try block.

R89. It calls `promptRunner.RunRuntimePromptAsync("RealignEpic", renderedContext, audit, cancellationToken)`.

R90. `RunRuntimePromptAsync` renders the runtime prompt with `RoadmapPromptCatalog.RenderRuntime("RealignEpic", renderedContext, audit)`.

R91. `RoadmapPromptCatalog` calls `Core.Prompts.Planning.RealignEpic.Render(projectContext, secondaryInput)`.

R92. `RunRuntimePromptAsync` appends the implementation-first prompt policy.

R93. `RunRuntimePromptAsync` calls `RunOneShotAsync("RealignEpic", prompt, cancellationToken)`.

R94. `RunOneShotAsync` creates a `ConsoleTurnRenderer`.

R95. It calls `runtime.RunOneShotAsync(AgentSpecs.ReadOnlyPlanning(repository), prompt, renderer.Stream, cancellationToken)`.

R96. If the agent turn state is not `Completed`, it throws `RoadmapStepException`.

R97. If the agent turn completed, the renderer echoes output if the agent was silent on stream.

R98. `RunRuntimePromptAsync` returns the raw `RealignEpic` output.

R99. `RunPromptForPromotionAsync` stops the stopwatch.

R100. It records `completed = DateTimeOffset.UtcNow`.

R101. It appends a `PromptCompleted` journal record with result `PromptCompleted`, parser decision `Output produced`, output path `.agents/epic.md`, elapsed milliseconds, and the same input snapshot.

R102. It calls `SaveStateAsync` with current state `RealignEpic`, status `PromptCompleted`, from `RealignEpic`, to `ActiveEpicReady`, output `.agents/epic.md`, decision `Prompt Completed`, started and completed timestamps, and no new blockers.

R103. `RunPromptForPromotionAsync` returns `PromptTransitionCompletion` with correlation id, timestamps, elapsed milliseconds, raw output, and input snapshot.

### Runtime Failure Branch

F1. If any non-cancellation exception is thrown inside the prompt try block, `RunPromptForPromotionAsync` stops the stopwatch.

F2. It records `failed = DateTimeOffset.UtcNow`.

F3. It appends a `TransitionFailed` journal record with previous state `RealignEpic`, attempted state `ActiveEpicReady`, prompt `RealignEpic`, projection path, output path `.agents/epic.md`, result `Failed`, parser decision `None`, and the exception message.

F4. It calls `SaveStateAsync` with:

- current `EvidenceBlocked`
- status `Failed`
- from `RealignEpic`
- to `ActiveEpicReady`
- prompt `RealignEpic`
- output `.agents/epic.md`
- decision `Runtime Failure`
- blocker row `{exception.Message, "Review the transition failure and rerun."}`
- transition intent `ResolveTransitionFailure`, dispatch state `EvidenceBlocked`, evidence paths `[.agents/epic.md]`
- next transitions `["Resolve blocker and rerun"]`

F5. It throws `RoadmapStepException.AlreadyPersisted(exception)`.

F6. Outer `RunAsync` catches already-persisted roadmap step exceptions, writes the error to console, and returns `RoadmapOutcome.Failed`.

### Promotion Success Branch

P1. `RewriteActiveEpicAsync` calls `PromoteActiveEpicAsync(RoadmapState.RealignEpic, "RealignEpic", projectionPath, completion)`.

P2. `PromoteActiveEpicAsync` constructs an `ArtifactPromotionRequest`:

- target `.agents/epic.md`
- candidate content `completion.Output`
- evidence directory `.agents/evidence/blockers`
- evidence stem `active-epic-promotion`
- artifact name `active epic`
- classifier `EpicAuthoringOutputClassifier`
- validator `EpicArtifactValidator`
- promoted lifecycle state `Ready`
- lifecycle notes `Promoted by RealignEpic.`

P3. `promotionService.PromoteAsync` classifies the candidate output.

P4. `EpicAuthoringOutputClassifier` finds the first top-level Markdown heading.

P5. If there is no top-level heading, classification is `Ambiguous`.

P6. If the first top-level heading contains `Blocked`, classification is `Blocked`.

P7. If the first top-level heading matches `# Epic: ...`, classification is `Promotable`.

P8. If the heading resembles an epic heading without the required `# Epic:` shape, or content contains `## Epic Metadata`, classification is `Malformed`.

P9. If none of those match, classification is `Ambiguous`.

P10. For a promotable candidate, `EpicArtifactValidator.Validate` runs.

P11. The validator rejects blank content.

P12. It reclassifies the output and rejects any non-promotable classification.

P13. It requires headings `## Epic Metadata`, `## Desired Capability`, `## Acceptance Criteria`, and `## Milestone Roadmap`.

P14. It requires either `## Strategic Purpose` or `## Strategic Continuity`.

P15. It parses the `## Epic Metadata` table and requires non-empty `Epic ID` and `Status`.

P16. It extracts the `## Milestone Roadmap` section.

P17. It parses milestone roadmap tables strictly.

P18. It requires at least one milestone row with columns `Milestone ID`, `Milestone Name`, `Purpose`, `Outcome`, `Depends On`, and `Completion Signal`.

P19. It requires non-empty values for `Milestone ID`, `Milestone Name`, `Purpose`, `Outcome`, and `Completion Signal`.

P20. If validation succeeds, `ArtifactPromotionService` writes `.agents/epic.md` with the realigned output.

P21. It upserts lifecycle for `.agents/epic.md` to `Ready` with notes `Promoted by RealignEpic.`

P22. It returns `ArtifactPromotionResult.PromotedResult(".agents/epic.md")`.

P23. `PromoteActiveEpicAsync` records a final `completed = DateTimeOffset.UtcNow`.

P24. Because `result.Promoted` is true, it calls `CaptureHitlRequestsAsync(".agents/epic.md", completion.Output)`.

P25. If no HITL capture service exists or output is blank, capture returns immediately.

P26. If the service exists, it parses `## HITL-Requested Non-Implementation Deliverables` from the active epic and appends new HITL request entries to the non-implementation review ledger.

P27. It appends an `ArtifactPromoted` journal record with prompt contract key `ArtifactPromotionService`, output path `.agents/epic.md`, result `Promoted`, parser decision `Active epic promoted`, and the original input snapshot.

P28. It calls `SaveStateAsync` with:

- current `ActiveEpicReady`
- status `Completed`
- from `RealignEpic`
- to `ActiveEpicReady`
- prompt `RealignEpic`
- projection `.agents/projections/realign-epic.md`
- output `.agents/epic.md`
- decision `Artifact Promoted`
- original prompt started timestamp
- final promotion timestamp
- no new retired epics
- no new blockers

P29. `PromoteActiveEpicAsync` returns the promoted result.

P30. `RewriteActiveEpicAsync` returns the promoted result.

P31. `AuditAndPrepareExistingEpicAsync` converts `promotion.Promoted` to `EpicPreparationResult.ActiveEpicReady`.

P32. `ContinueAfterSelectionAsync` sees that preparation is not retired and not blocked, leaves the selection branch, and runs downstream milestone generation. That downstream transition is outside this audit.

### Promotion Blocked Branch

B1. If classification is `Blocked`, `Malformed`, or `Ambiguous`, `ArtifactPromotionService` calls `PreserveEvidenceAsync`.

B2. If validation fails, `ArtifactPromotionService` calls `PreserveEvidenceAsync` with status `StructurallyInvalid`.

B3. `PreserveEvidenceAsync` writes the candidate output as numbered evidence under `.agents/evidence/blockers/active-epic-promotion.NNNN.md`.

B4. It upserts lifecycle for that evidence path to `Blocked` with the classifier or validator reason.

B5. It returns `ArtifactPromotionResult.NotPromoted(status, ".agents/epic.md", evidencePath, reason)`.

B6. `PromoteActiveEpicAsync` records a final `completed = DateTimeOffset.UtcNow`.

B7. It maps status to state decision text:

- `Blocked` -> `Artifact Promotion Blocked`
- `Ambiguous` -> `Artifact Promotion Ambiguous`
- `StructurallyInvalid` -> `Artifact Promotion Invalid`
- any other status -> `Artifact Promotion Rejected`

B8. It appends an `ArtifactPromotionBlocked` journal record with prompt contract key `ArtifactPromotionService`, output path `[evidencePath]`, result equal to promotion status, parser decision equal to mapped decision text, error message equal to promotion reason, and the original input snapshot.

B9. It calls `SaveStateAsync` with:

- current `EvidenceBlocked`
- status `Paused`
- from `RealignEpic`
- to `ActiveEpicReady`
- prompt `RealignEpic`
- projection `.agents/projections/realign-epic.md`
- output `evidencePath`
- decision mapped from status
- original prompt started timestamp
- final promotion-blocked timestamp
- blocker row `{result.Reason, "Review {evidencePath} and rerun the roadmap CLI after resolving the blocker."}`
- transition intent `ResolveArtifactPromotionBlocker`, dispatch state `EvidenceBlocked`, evidence paths `[evidencePath]`
- next transitions `["Resolve blocker and rerun"]`

B10. It returns the non-promoted result.

B11. `RewriteActiveEpicAsync` returns the non-promoted result.

B12. `AuditAndPrepareExistingEpicAsync` converts it to `EpicPreparationResult.Blocked`.

B13. `ContinueAfterSelectionAsync` returns `RoadmapOutcome.Paused` and does not run milestone generation.

## Deliverable 3: Concern Inventory

| Step | Concern | Mixed? |
|---|---|---|
| R1-R3 dispatch to `RewriteActiveEpicAsync` | routing | No |
| R4 console phase | reporting | Yes: reporting happens inside transition execution |
| R5-R7 active epic read with selection fallback | artifact read, decision, recovery | Yes |
| R8-R20 `ReadCurrentSelectionAsync` fallback | validation, artifact read, provenance, state read, recovery | Yes |
| R21-R22 audit evidence read | artifact read, validation | Slight |
| R23-R24 contract lookup | contract metadata | No |
| R25-R45 projection cache | projection read/write, prompt execution, validation, provenance, manifest persistence, blocker evidence | Yes |
| R46-R52 runtime context build | prompt preparation, input normalization, validation | Yes |
| R53-R72 transition input snapshot | provenance, artifact read, validation, hashing | Yes |
| R73-R87 prompt-start state and journal | state mutation, persistence, journaling, timing | Yes |
| R89-R98 runtime prompt execution | prompt execution, console streaming, runtime diagnostics | Yes |
| R99-R103 prompt-completed state and journal | state mutation, persistence, journaling, timing | Yes |
| F1-F6 runtime failure branch | recovery, journaling, state mutation, reporting via outer catch | Yes |
| P2-P22 promotion service success path | parsing, validation, artifact write, lifecycle | Yes |
| P24-P26 HITL capture | ledger write, optional post-processing | Yes |
| P27-P28 promotion success journal and state | journaling, persistence, state mutation | Yes |
| B1-B5 blocked evidence preservation | parsing, validation, artifact write, lifecycle | Yes |
| B7-B9 blocked journal and state | decision mapping, journaling, persistence, blocker creation | Yes |
| P30-P32 / B10-B13 return conversion | routing, outcome translation | Slight |

Concerns become most mixed in four places:

1. `RewriteActiveEpicAsync` resolves inputs, loads contracts/projections, builds context, runs a prompt, and promotes output through one short method.
2. `ProjectionCache.EnsureAsync` owns read-through cache behavior, projection prompt execution, validation, manifest mutation, stale detection, and blocker evidence.
3. `RunPromptForPromotionAsync` owns input snapshots, state persistence, journal persistence, runtime execution, timing, and failure recovery.
4. `PromoteActiveEpicAsync` owns artifact interpretation, lifecycle effects, HITL capture, transition journaling, state persistence, and blocker construction.

## Deliverable 4: Hidden Steps

The code reads linearly only at the top level, but several meaningful transition steps are hidden behind helpers.

Build Realign Context

- Hidden in `BuildRealignOrReimagineContext`.
- Explicit work: combine projection, current epic or selection, audit output, and repository inspection instructions; then reject raw Project Context markers.

Resolve Current Epic Input

- Hidden in `artifacts.ReadAsync(ActiveEpic) ?? ReadCurrentSelectionAsync(...)`.
- Explicit work: prefer active epic; if absent, prove the active selection is fresh before using it as the epic-like input.

Resolve Audit Evidence

- Hidden in `artifacts.ReadRequiredAsync(auditPath)`.
- Explicit work: make the audit evidence durable input mandatory.

Resolve Projection

- Hidden in `projectionCache.EnsureAsync`.
- Explicit work: locate path, read or generate projection, validate shape, update manifest, write generated projection, block stale or invalid projection.

Capture Snapshot

- Hidden in `inputResolver.ResolveAsync`.
- Explicit work: identify projection, active epic or selection, and audit evidence inputs; read and hash each; hash context and secondary input; compute snapshot hash.

Persist Started State

- Hidden in `RunPromptForPromotionAsync`.
- Explicit work: save state with `CurrentState = RealignEpic`, `LastTransition.Status = Started`, output `.agents/epic.md`, decision `Prompt Started`.

Record Transition Started

- Hidden in `RunPromptForPromotionAsync`.
- Explicit work: append `TransitionStarted` JSONL with correlation id and input snapshot.

Render Runtime Prompt

- Hidden in `RoadmapPromptRunner.RunRuntimePromptAsync`.
- Explicit work: render `RealignEpic`, append implementation-first prompt policy, run read-only planning agent.

Normalize Prompt Completion

- Hidden in `RunPromptForPromotionAsync`.
- Explicit work: stop timer, append `PromptCompleted`, persist `PromptCompleted`, return raw output plus timing and snapshot.

Classify Output

- Hidden in `ArtifactPromotionService.PromoteAsync`.
- Explicit work: decide whether the output is promotable, intentionally blocked, malformed, or ambiguous from its first top-level heading and epic-like content.

Validate Active Epic

- Hidden in `EpicArtifactValidator`.
- Explicit work: require epic metadata, strategic purpose or continuity, acceptance criteria, milestone roadmap table, and required milestone cells.

Apply Promotion Effects

- Hidden across `ArtifactPromotionService` and `PromoteActiveEpicAsync`.
- Explicit work: write `.agents/epic.md`, update lifecycle, optionally capture HITL requests, append promotion journal, save completed state.

Apply Blocked Effects

- Hidden across `ArtifactPromotionService` and `PromoteActiveEpicAsync`.
- Explicit work: write numbered blocker evidence, update evidence lifecycle, append blocked journal, save paused state, create transition intent and blocker row.

Persist Runtime Failure

- Hidden in the catch block inside `RunPromptForPromotionAsync`.
- Explicit work: append failure journal, save failed state, create `ResolveTransitionFailure` intent, and rethrow as already persisted.

## Deliverable 5: Natural Step Boundaries

Step 1: Enter Realignment

Purpose

Route the audited `Realign` disposition into a single realignment rewrite.

Inputs

- parsed audit decision
- `auditPath`
- `projectContext`
- cancellation token

Outputs

- call to `RewriteActiveEpicAsync("RealignEpic", RoadmapState.RealignEpic, ...)`

---

Step 2: Resolve Realignment Inputs

Purpose

Recover the current epic content and audit evidence required by the rewrite prompt.

Inputs

- `.agents/epic.md`
- `.agents/selection.md` and selection provenance, only if active epic is missing
- audit evidence path

Outputs

- `selectionOrEpic`
- audit markdown

---

Step 3: Resolve Contract And Projection

Purpose

Ensure the `RealignEpic` prompt has a valid, fresh projection and known artifact contract.

Inputs

- prompt name `RealignEpic`
- Project Context
- prompt contract registry
- projection cache and manifest

Outputs

- `PromptContract`
- `ProjectionCacheResult`
- possible generated `.agents/projections/realign-epic.md`
- possible projection-blocked evidence and exception

---

Step 4: Build Runtime Context

Purpose

Render the exact prompt context consumed by the runtime prompt.

Inputs

- projection content
- current epic or selection content
- audit evidence content

Outputs

- rendered `# Roadmap Runtime Prompt Context`

---

Step 5: Capture Transition Snapshot

Purpose

Record the artifact and prompt-input identity for journaling, provenance, and later diagnosis.

Inputs

- runtime prompt name
- projection path
- rendered context
- secondary input audit content
- audit evidence path
- active epic or selection

Outputs

- `TransitionInputSnapshot`
- input artifact hashes

---

Step 6: Start Prompt Transition

Purpose

Make the in-flight realignment durable before agent execution.

Inputs

- transition metadata
- started timestamp
- input snapshot

Outputs

- `.agents/state.json` with `RealignEpic` / `Started`
- `TransitionStarted` journal record

---

Step 7: Run Realignment Prompt

Purpose

Ask the read-only planning runtime to produce either a realigned epic or a blocked realignment document.

Inputs

- rendered runtime prompt
- implementation-first prompt policy
- read-only planning agent spec
- cancellation token

Outputs

- raw prompt output
- streamed console output
- runtime diagnostics on failure

---

Step 8: Persist Prompt Completion

Purpose

Record that the model turn produced output, without yet treating it as authoritative.

Inputs

- raw output
- timestamps
- elapsed milliseconds
- input snapshot

Outputs

- `PromptCompleted` journal record
- `.agents/state.json` with `RealignEpic` / `PromptCompleted`
- `PromptTransitionCompletion`

---

Step 9: Promote Or Preserve Candidate

Purpose

Interpret the prompt output through the existing active-epic promotion boundary.

Inputs

- raw output
- classifier
- validator
- target `.agents/epic.md`
- blocker evidence directory

Outputs

- promoted active epic and lifecycle entry, or blocker evidence and lifecycle entry
- `ArtifactPromotionResult`

---

Step 10: Persist Final Transition Outcome

Purpose

Record the promotion result as the externally visible transition outcome.

Inputs

- promotion result
- completion metadata
- input snapshot

Outputs

- `ArtifactPromoted` or `ArtifactPromotionBlocked` journal record
- final `.agents/state.json`
- optional HITL request ledger entries
- return result

## Deliverable 6: Mixed-Concern Analysis

Step 1 mixes routing with preparation workflow state.

The `Realign` branch lives inside `AuditAndPrepareExistingEpicAsync`, which has already audited, parsed, recorded, and routed audit outcomes. The realignment call is easy to miss because it is embedded in audit completion logic rather than in a named realignment transition handler.

Step 2 mixes artifact recovery with provenance validation.

The line that chooses active epic or selection hides a large fallback path. If active epic is absent, the transition re-enters selection freshness validation, state loading, selection-cycle hashing, and provenance checks. That makes the input source harder to understand from the realignment method alone.

Step 3 mixes projection caching with generation and blocking.

Projection resolution is not just a read. It can run an agent, validate projection shape, update manifest state, write projection files, write blocker evidence, and throw. Engineers reading `RewriteActiveEpicAsync` cannot see which effects happen before the runtime prompt starts.

Step 4 mixes context assembly with safety validation.

`BuildRealignOrReimagineContext` both formats prompt sections and enforces that raw Project Context markers are not present. The method is small, but the validation affects failure behavior.

Step 5 mixes provenance with required input validation.

`TransitionInputResolver` is a provenance helper, but it also decides whether `.agents/epic.md` or `.agents/selection.md` is the required input and throws if audit evidence is missing. That makes input requirements depend on current artifact existence rather than only on the prompt contract.

Step 6 mixes state persistence with transition start semantics.

`RunPromptForPromotionAsync` saves state and appends the start journal record before running the prompt. The helper name sounds like prompt execution, but it also defines resume-observable state.

Step 7 mixes prompt rendering, policy composition, console streaming, and runtime diagnostics.

The runtime prompt call is not just `Run`. It renders the strongly typed prompt, appends policy text, streams output, checks terminal state, echoes silent output, and throws diagnostic exceptions.

Step 8 mixes "prompt produced output" with persisted transition status.

The output is not authoritative yet, but state becomes `PromptCompleted`. This is an important intermediate state because resume safety treats started and prompt-completed transitions specially.

Step 9 mixes parsing, validation, artifact writes, and lifecycle.

Promotion service classifies and validates content before writing, but it also writes blocker evidence and lifecycle records on rejection. Understanding why `.agents/epic.md` did or did not change requires reading classifier, validator, promotion service, and promotion wrapper.

Step 10 mixes promotion reporting with state-machine outcome.

`PromoteActiveEpicAsync` appends final journal records, saves final state, optionally captures HITL requests, creates blocker rows, creates transition intents, and returns the result. These are unrelated responsibilities, but together they define the behavioral contract.

## Deliverable 7: Data Flow

Audit Disposition

- Origin: `EpicPreparationAuditParser.Parse(output)` in the previous transition.
- Consumed by: `AuditAndPrepareExistingEpicAsync` branch on `decision.Disposition == "Realign"`.
- Effect: selects `RewriteActiveEpicAsync("RealignEpic", RoadmapState.RealignEpic, ...)`.

Audit Evidence Path

- Origin: numbered evidence write in the previous audit transition.
- Consumed by: `RewriteActiveEpicAsync` for `artifacts.ReadRequiredAsync(auditPath)`.
- Consumed again by: `TransitionInputContext.AuditEvidence(auditPath)`.
- Persisted into: transition input snapshot artifact inputs and journal records.

Active Epic Or Selection Content

- Origin: `.agents/epic.md` if present.
- Fallback origin: `.agents/selection.md`, only after selection freshness is proven.
- Consumed by: `BuildRealignOrReimagineContext` as `Current Epic`.
- Persisted into: prompt-context hash, not directly written by this step unless prompt output is promoted.

Realign Projection

- Origin: `.agents/projections/realign-epic.md`, or generated by `ProjectionForRealignEpic`.
- Consumed by: context builder and transition input resolver.
- Persisted into: projection file when generated, projection manifest entry, input snapshot projection hash.

Rendered Runtime Context

- Origin: `BuildRealignOrReimagineContext`.
- Contains: projection content, current epic, audit output, repository inspection instructions.
- Consumed by: `RunPromptForPromotionAsync`, then `RoadmapPromptCatalog.RenderRuntime`.
- Persisted into: transition input snapshot as `PromptContextHash`.

Secondary Input

- Origin: audit evidence content.
- Consumed by: `RoadmapPromptCatalog.RenderRuntime("RealignEpic", context, audit)`.
- Persisted into: transition input snapshot as `SecondaryInputHash`.

Transition Input Snapshot

- Origin: `TransitionInputResolver.ResolveAsync`.
- Contains: runtime prompt name, projection identity, artifact inputs, context hash, secondary input hash, snapshot hash.
- Consumed by: `TransitionStarted`, `PromptCompleted`, `TransitionFailed`, `ArtifactPromoted`, and `ArtifactPromotionBlocked` journal records.

Started State

- Origin: `RunPromptForPromotionAsync` before prompt execution.
- Persisted to: `.agents/state.json`.
- Consumed by: status/resume readers if execution is interrupted.

Raw Prompt Output

- Origin: `RoadmapPromptRunner.RunRuntimePromptAsync`.
- Consumed by: `PromoteActiveEpicAsync`.
- If promoted: written to `.agents/epic.md`.
- If rejected: written to numbered blocker evidence.

Promotion Classification

- Origin: `EpicAuthoringOutputClassifier`.
- Consumed by: `ArtifactPromotionService`.
- Effect: decides promote, blocked, structurally invalid, or ambiguous path.

Validation Result

- Origin: `EpicArtifactValidator`.
- Consumed by: `ArtifactPromotionService`.
- Effect: allows `.agents/epic.md` write or forces blocked evidence.

ArtifactPromotionResult

- Origin: `ArtifactPromotionService.PromoteAsync`.
- Consumed by: `PromoteActiveEpicAsync` for final journal/state.
- Returned to: `RewriteActiveEpicAsync`, then `AuditAndPrepareExistingEpicAsync`.

Final State

- Success origin: `PromoteActiveEpicAsync` writes `ActiveEpicReady` / `Completed`.
- Blocked origin: `PromoteActiveEpicAsync` writes `EvidenceBlocked` / `Paused`.
- Failure origin: `RunPromptForPromotionAsync` writes `EvidenceBlocked` / `Failed`.
- Consumed by: resume planner, status command, and subsequent CLI runs.

## Deliverable 8: Human Navigation Audit

Minimum files to understand only `RealignEpic`:

- `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`
- `src/LoopRelay.Roadmap.Cli/PromptContractRegistry.cs`
- `src/LoopRelay.Roadmap.Cli/ProjectionRegistry.cs`
- `src/LoopRelay.Roadmap.Cli/ProjectionCache.cs`
- `src/LoopRelay.Roadmap.Cli/ProjectionValidator.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapPromptContextBuilder.cs`
- `src/LoopRelay.Roadmap.Cli/TransitionInputs.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapPromptRunner.cs`
- `src/LoopRelay.Roadmap.Cli/ArtifactPromotion.cs`
- `src/LoopRelay.Roadmap.Cli/EpicArtifactPromotion.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapArtifacts.cs`
- `src/LoopRelay.Roadmap.Cli/ArtifactLifecycleStore.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapStateStore.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs`
- `src/LoopRelay.Roadmap.Cli/TransitionJournal.cs`
- `src/LoopRelay.Roadmap.Cli/TransitionJournalStore.cs`
- `src/LoopRelay.Roadmap.Cli/DecisionLedgerStore.cs`
- `src/LoopRelay.Orchestration.Primitives/Services/NonImplementationReview/ExplicitHitlNonImplementationRequestCaptureService.cs`

Helper methods that must be read:

- `AuditAndPrepareExistingEpicAsync`
- `RewriteActiveEpicAsync`
- `ReadCurrentSelectionAsync`
- `RunPromptForPromotionAsync`
- `PromoteActiveEpicAsync`
- `CaptureHitlRequestsAsync`
- `SaveStateAsync`
- `ProjectionCache.EnsureAsync`
- `RoadmapPromptContextBuilder.BuildRealignOrReimagineContext`
- `TransitionInputResolver.ResolveAsync`
- `TransitionInputResolver.AddCurrentEpicOrSelectionInputAsync`
- `RoadmapPromptRunner.RunRuntimePromptAsync`
- `RoadmapPromptRunner.RunOneShotAsync`
- `ArtifactPromotionService.PromoteAsync`
- `ArtifactPromotionService.PreserveEvidenceAsync`
- `EpicAuthoringOutputClassifier.Classify`
- `EpicArtifactValidator.Validate`

Services:

- `RoadmapArtifacts`
- `PromptContractRegistry`
- `ProjectionCache`
- `RoadmapPromptContextBuilder`
- `TransitionInputResolver`
- `RoadmapPromptRunner`
- `ArtifactPromotionService`
- `ArtifactLifecycleStore`
- `RoadmapStateStore`
- `TransitionJournalStore`
- `SelectionProvenanceService`
- optional `ExplicitHitlNonImplementationRequestCaptureService`

Models and persistence objects:

- `RoadmapState`
- `RoadmapStateDocument`
- `RoadmapTransitionSummary`
- `RoadmapTransitionIntent`
- `TransitionInputRequest`
- `TransitionInputContext`
- `TransitionInputSnapshot`
- `TransitionJournalRecord`
- `PromptContract`
- `ProjectionCacheResult`
- `ProjectionDefinition`
- `ArtifactPromotionRequest`
- `ArtifactPromotionResult`
- `ArtifactLifecycleEntry`
- `BlockerRow`
- `NonImplementationHitlRequestEntry`

Parsers and validators:

- `EpicAuthoringOutputClassifier`
- `EpicArtifactValidator`
- `MarkdownTableParser`
- `ProjectionValidator`

Lifecycle and reporting code:

- `ArtifactLifecycleStore.UpsertAsync`
- `ConsoleTurnRenderer`
- `ILoopConsole.Phase`
- `ILoopConsole.Error` through outer `RunAsync` failure handling

Tests that pin behavior:

- `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapStateMachinePromotionTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/TransitionInputResolverTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapFailurePersistenceTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/PromptContractRegistryTests.cs`

## Deliverable 9: Extraction Boundary

Smallest natural extraction boundary:

`RealignEpicTransitionHandler.ExecuteAsync(ProjectContext projectContext, string auditPath, CancellationToken cancellationToken) -> Task<ArtifactPromotionResult>`

The handler should begin where `RewriteActiveEpicAsync("RealignEpic", RoadmapState.RealignEpic, ...)` begins and end where it returns the `ArtifactPromotionResult`.

It should own:

- `RealignEpic` phase reporting
- active epic or current selection input recovery
- audit evidence read
- contract/projection resolution for `RealignEpic`
- realignment context construction
- prompt execution through the existing prompt-for-promotion behavior
- active epic promotion through existing promotion behavior
- final result return

It should not own:

- deciding whether audit disposition is `Realign`
- parsing `EpicPreparationAudit`
- appending the audit decision ledger entry
- converting promoted/not-promoted into `EpicPreparationResult`
- downstream milestone spec generation
- startup/resume planning
- new command routing

The current code already has the boundary in method form, but the real behavior is split across shared helpers. The extraction should make the transition linear by naming the hidden steps while preserving the same helper effects.

## Deliverable 10: Required Inputs

Required

- `ProjectContext projectContext`
- `string auditPath`
- `CancellationToken cancellationToken`
- `RoadmapArtifacts artifacts`
- `PromptContractRegistry contractRegistry`
- `ProjectionCache projectionCache`
- `RoadmapPromptContextBuilder contextBuilder`
- `TransitionInputResolver inputResolver`
- `RoadmapPromptRunner promptRunner`
- `RoadmapStateStore stateStore`
- `ProjectionManifestStore manifestStore`
- `DecisionLedgerStore decisionLedger`
- `TransitionJournalStore journalStore`
- `ArtifactLifecycleStore lifecycleStore`
- `ArtifactPromotionService promotionService`
- `SelectionProvenanceService selectionProvenance`
- `ILoopConsole console`

Required artifact inputs

- `.agents/projections/realign-epic.md`, or ability to generate it
- audit evidence at `auditPath`
- `.agents/epic.md`, or `.agents/selection.md` with current selection provenance if active epic is absent
- `.agents/projections/select-next-epic.md`, roadmap completion context, roadmap sources, and selection provenance manifest only for the selection fallback path

Optional

- `ExplicitHitlNonImplementationRequestCaptureService hitlRequestCapture`
- existing `.agents/state.json`, for preserving retired epics, blockers, transition intent, and state summary continuity
- existing `.agents/artifacts/lifecycle.json`, for active artifact rows and lifecycle updates
- existing `.agents/projections/manifest.json`, for projection freshness and state summary counts
- existing `.agents/decision-ledger.json`, for last decision id in state summary
- existing split-family JSON files, for state summary count

Incidental

- `CompletionCertificationPolicy`
- `CompletionCertificationRouter`
- `ICompletedEpicArchiveService`
- `RoadmapStartupPlanner`
- `RoadmapResumePlanner`
- `RoadmapUnblockPlanner`
- `BundleFileExtractor`
- `SplitEpicBundleInterpreter`
- `BundleManifestWriter`
- `SplitFamilyStore`
- `ExecutionPreparationProvenanceService`, except indirectly if retained by shared context/snapshot services
- `InvariantValidator`
- `INonImplementationCompletionReviewService`

These incidental dependencies are constructor-level dependencies of `RoadmapStateMachine`, not RealignEpic-specific requirements.

## Deliverable 11: Required Outputs

Returned value

- `ArtifactPromotionResult`
- `Promoted == true` for promoted active epic
- `Promoted == false` for blocked, ambiguous, or structurally invalid output

Console output

- phase text `RealignEpic`
- agent runtime streaming through `ConsoleTurnRenderer`
- outer `RunAsync` error output on already-persisted runtime failure

Projection artifacts

- `.agents/projections/realign-epic.md` may be written if missing
- `.agents/projections/manifest.json` is upserted during projection resolution
- `.agents/evidence/blockers/projection-blocked.NNNN.md` may be written for invalid or stale projection

Prompt transition state

- `.agents/state.json` is written with `RealignEpic` / `Started` before runtime prompt execution
- `.agents/state.json` is written with `RealignEpic` / `PromptCompleted` after prompt output is produced

Prompt transition journal

- `.agents/journal/transitions.jsonl` appends `TransitionStarted`
- `.agents/journal/transitions.jsonl` appends `PromptCompleted` on prompt success
- `.agents/journal/transitions.jsonl` appends `TransitionFailed` on runtime failure

Promoted active epic

- `.agents/epic.md` is overwritten with realigned epic content only after classifier and validator pass
- `.agents/artifacts/lifecycle.json` marks `.agents/epic.md` `Ready` with notes `Promoted by RealignEpic.`
- `.agents/journal/transitions.jsonl` appends `ArtifactPromoted`
- `.agents/state.json` records current state `ActiveEpicReady`, status `Completed`, output `.agents/epic.md`, decision `Artifact Promoted`

Blocked promotion

- `.agents/epic.md` remains unchanged
- `.agents/evidence/blockers/active-epic-promotion.NNNN.md` is written with the rejected output
- `.agents/artifacts/lifecycle.json` marks that evidence path `Blocked` with the rejection reason
- `.agents/journal/transitions.jsonl` appends `ArtifactPromotionBlocked`
- `.agents/state.json` records current state `EvidenceBlocked`, status `Paused`, output evidence path, mapped decision text, blocker row, transition intent `ResolveArtifactPromotionBlocker`, and next transition `Resolve blocker and rerun`

Runtime failure

- `.agents/epic.md` is not overwritten by failed runtime output
- `.agents/journal/transitions.jsonl` appends `TransitionFailed`
- `.agents/state.json` records current state `EvidenceBlocked`, status `Failed`, output `.agents/epic.md`, decision `Runtime Failure`, blocker row, transition intent `ResolveTransitionFailure`, and next transition `Resolve blocker and rerun`
- `RoadmapStepException.AlreadyPersisted` is thrown to outer `RunAsync`

HITL ledger

- If the optional capture service exists and the promoted active epic contains `## HITL-Requested Non-Implementation Deliverables`, new request entries may be added to the non-implementation review ledger.

## Deliverable 12: Behavioral Equivalence Contract

Inputs that must remain identical

- `RealignEpic` must run only after the audit path has been supplied by the existing audit route.
- The active epic must be read from `.agents/epic.md` when present.
- The current selection fallback must remain available when `.agents/epic.md` is absent.
- The selection fallback must keep existing freshness validation behavior.
- The audit evidence at `auditPath` must be required and must be passed both in context and as secondary input.
- Transition input snapshot roles must remain `Projection`, `ActiveEpic` or `Selection`, and `AuditEvidence` with current path ordering and hash behavior.

Outputs that must remain identical

- Promoted output writes `.agents/epic.md`.
- Rejected output writes `.agents/evidence/blockers/active-epic-promotion.NNNN.md`.
- Runtime failure uses output path `.agents/epic.md` in failed state and journal records.
- Projection blocking writes `projection-blocked.NNNN.md`.

Persisted state that must remain identical

- Prompt start: current `RealignEpic`, status `Started`, from `RealignEpic`, to `ActiveEpicReady`, output `.agents/epic.md`, decision `Prompt Started`.
- Prompt completion: current `RealignEpic`, status `PromptCompleted`, output `.agents/epic.md`, decision `Prompt Completed`.
- Promotion success: current `ActiveEpicReady`, status `Completed`, output `.agents/epic.md`, decision `Artifact Promoted`.
- Promotion blocked: current `EvidenceBlocked`, status `Paused`, output evidence path, mapped artifact-promotion decision, transition intent `ResolveArtifactPromotionBlocker`.
- Runtime failure: current `EvidenceBlocked`, status `Failed`, output `.agents/epic.md`, decision `Runtime Failure`, transition intent `ResolveTransitionFailure`.

Artifacts that must remain identical

- `.agents/epic.md` is overwritten only after promotable classification and valid epic structure.
- Existing `.agents/epic.md` remains unchanged on blocked, ambiguous, malformed, structurally invalid, or prompt-failure outcomes.
- Blocked candidate content is preserved exactly as produced by the prompt.
- Generated realignment projection is written only after successful validation.

Lifecycle that must remain identical

- Promoted active epic lifecycle is `Ready`, notes `Promoted by RealignEpic.`
- Blocked promotion evidence lifecycle is `Blocked`, notes equal to classifier or validator reason.
- Existing active epic lifecycle is not replaced on blocked promotion.

Journals that must remain identical

- `TransitionStarted` before runtime execution.
- `PromptCompleted` after prompt output and before promotion.
- `TransitionFailed` for runtime exceptions.
- `ArtifactPromoted` for successful promotion.
- `ArtifactPromotionBlocked` for rejected promotion.
- Correlation id must connect the prompt and promotion records for a single run.
- Input artifact hashes and full input snapshot must be retained in journal records.

Decision records

- `RealignEpic` itself does not append a decision-ledger entry.
- The state summary's `LastDecisionId` must continue to reflect the latest ledger entry, usually the prior `EpicPreparationAudit` decision.

Reports and console behavior

- `console.Phase("RealignEpic")` must still occur.
- Runtime stream rendering and silent-output echo behavior must remain owned by `RoadmapPromptRunner`.
- Outer failure reporting for already persisted runtime failures must remain through `RunAsync`.

Exceptions and recovery behavior

- Operation cancellation must not be caught by the prompt failure catch block.
- Runtime prompt failures must be persisted before throwing `RoadmapStepException.AlreadyPersisted`.
- Missing required artifacts must still throw `RoadmapStepException`.
- Projection validation/staleness failures must still write projection-blocked evidence before throwing.
- Blocked promotion must pause, not fail.
- Runtime failure must fail, not pause.

Not part of the contract

- Private method names.
- Whether the extracted handler is implemented as a class, nested component, or local method.
- Internal local variable names.
- Any dependency that does not affect observable state, artifacts, lifecycle, journal, console, or exceptions.

## Deliverable 13: Transition Handler Shape

Ideal linear structure recovered from the current implementation:

```text
Execute(projectContext, auditPath, cancellationToken)

-> Announce RealignEpic

-> Resolve current epic content
   -> read .agents/epic.md
   -> fallback to current selection with freshness validation

-> Load audit evidence

-> Load RealignEpic contract

-> Ensure RealignEpic projection
   -> read or generate projection
   -> validate projection
   -> update manifest
   -> block invalid or stale projection

-> Build realignment runtime context

-> Capture transition input snapshot

-> Persist prompt started state

-> Append TransitionStarted journal

-> Run RealignEpic prompt

-> On runtime failure
   -> append TransitionFailed journal
   -> persist EvidenceBlocked / Failed
   -> throw already-persisted step exception

-> Persist prompt completed state

-> Append PromptCompleted journal

-> Promote active epic
   -> classify output
   -> validate epic structure
   -> write .agents/epic.md and lifecycle when valid
   -> otherwise write blocker evidence and lifecycle

-> If promoted
   -> capture HITL requests from active epic
   -> append ArtifactPromoted journal
   -> persist ActiveEpicReady / Completed
   -> return promoted result

-> If not promoted
   -> append ArtifactPromotionBlocked journal
   -> persist EvidenceBlocked / Paused
   -> return blocked result
```

This shape introduces no new behavior. It simply names the current execution order.

## Deliverable 14: Readability Improvements

Fewer files required for the first pass

Today an engineer starts in `AuditAndPrepareExistingEpicAsync`, jumps to `RewriteActiveEpicAsync`, then jumps into projection cache, context builder, input resolver, prompt runner, promotion service, validator, lifecycle store, journal store, and state persistence. A named handler would provide the realignment reading order before those helper details are needed.

The transition's purpose becomes explicit

`RewriteActiveEpicAsync` is shared between `RealignEpic` and `ReimagineEpic`. A `RealignEpic` handler would make the identity-preserving rewrite path visible as its own transition while still calling the same shared services.

Input fallback becomes visible

The current active-epic-or-selection fallback is compressed into one expression. Extraction would make the fallback and its provenance validation an explicit step, reducing surprise when `.agents/selection.md` appears in `RealignEpic` input snapshots.

Projection effects become easier to reason about

Projection resolution can generate files, update the manifest, and write blocker evidence before runtime prompt execution. A linear handler step named "Ensure RealignEpic projection" would make that pre-prompt effect boundary obvious.

Prompt completion no longer looks like artifact completion

The current helper persists `PromptCompleted` before promotion. A named step can state that prompt output exists but is not authoritative until promotion succeeds.

Promotion branching is isolated

Successful promotion, blocked output, ambiguous output, malformed output, structural invalidity, and runtime failure currently interleave across helper calls. Extraction would put those branches under one transition-specific "Promote Or Preserve" section.

State and journal effects become navigable

The transition writes at least two state snapshots and two journal records on success, and different state/journal combinations on blocked or failed outcomes. A handler with explicit persistence steps would let engineers verify behavior without reconstructing it from nested helpers.

Downstream milestone generation remains outside the mental frame

`ContinueAfterSelectionAsync` immediately continues to milestone generation after active epic readiness. Keeping `RealignEpic` extraction bounded to `ArtifactPromotionResult` makes the handoff to downstream generation clear without mixing the two transitions.

