# EpicPreparationAudit Transition Extraction Audit

## Audited Transition

Exactly one transition is audited here: `EpicPreparationAudit`.

Scope: selected-existing-epic preparation, implemented by `AuditAndPrepareExistingEpicAsync` in `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`.

Entry: `ContinueAfterSelectionAsync` reaches the `"Select Existing Epic"` branch and calls `AuditAndPrepareExistingEpicAsync(selection, projectContext, cancellationToken)`.

Exit: `AuditAndPrepareExistingEpicAsync` returns one of:

- `EpicPreparationResult.Retired`
- `EpicPreparationResult.Blocked`
- `EpicPreparationResult.ActiveEpicReady`

The audit includes the helper behavior currently required before that return value is produced:

- active selection reread and freshness validation
- `EpicPreparationAudit` projection resolution
- audit prompt execution
- audit evidence materialization
- audit parsing
- decision ledger writes
- retire routing
- `RealignEpic` / `ReimagineEpic` rewrite-and-promotion routing when selected by the audit

Out of scope:

- the already-audited `SelectNextEpic` transition
- the already-audited `GenerateMilestoneDeepDivesForEpic` transition that the caller runs after `ActiveEpicReady`
- `CreateNewEpic`
- `SplitEpic`
- completion certification
- unblock handling

The extraction target is the existing selected-existing-epic workflow, not a new command surface or architecture.

## 1. Transition Narrative

Current State

`SelectNextEpic` has selected an existing roadmap epic. The selection artifact exists at `.agents/selection.md`, and selection provenance identifies the selection cycle that produced it. The state machine has a preflight `ProjectContext`.

Goal

Verify whether the selected existing epic is still the right implementation target before the roadmap CLI proceeds into milestone deep dives.

Major Steps

1. Re-read the active selection and prove it still belongs to the current selection cycle.
2. Load or generate the `EpicPreparationAudit` projection.
3. Build an audit prompt context from the projection and the selected epic.
4. Run the audit prompt through the read-only planning agent.
5. Persist audit transition state and journal entries around prompt execution.
6. Write the audit output as numbered evidence.
7. Parse the audit disposition.
8. Record the audit decision.
9. Route the disposition:
   - `Retire`: record a retired epic, supersede the active selection, and stop.
   - `Insufficient Evidence`: throw a non-persisted step exception after the audit evidence and decision have been written.
   - `Realign`: run `RealignEpic`, promote the resulting active epic, and return prepared or blocked.
   - `Reimagine`: run `ReimagineEpic`, promote the resulting active epic, and return prepared or blocked.

Completion

The transition is complete when `AuditAndPrepareExistingEpicAsync` returns:

- `Retired`, with roadmap state persisted at `RetireEpic` and the active selection superseded.
- `Blocked`, with roadmap state persisted at `EvidenceBlocked` by active-epic promotion handling.
- `ActiveEpicReady`, with `.agents/epic.md` promoted and roadmap state persisted at `ActiveEpicReady`.

The caller may continue into milestone generation after `ActiveEpicReady`; that later transition is not part of this audit.

## 2. Current Execution Trace

This is the current execution order for the selected-existing-epic path.

### Entry

E1. `ContinueAfterSelectionAsync` receives a parsed `SelectionDecision`.

E2. It switches on `selection.RecommendedOutcome`.

E3. For `"Select Existing Epic"`, it calls `AuditAndPrepareExistingEpicAsync(selection, projectContext, cancellationToken)`.

E4. `AuditAndPrepareExistingEpicAsync` sets `runtimePrompt = "EpicPreparationAudit"`.

E5. `console.Phase("Audit selected epic")` writes the audit phase message.

### Active Selection Recovery

S1. `ReadCurrentSelectionAsync(cancellationToken)` starts.

S2. `cancellationToken.ThrowIfCancellationRequested()` runs.

S3. `.agents/selection.md` is read through `artifacts.ReadRequiredAsync(RoadmapArtifactPaths.Selection)`.

S4. The `SelectNextEpic` projection path is resolved from `RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"]`.

S5. The `SelectNextEpic` projection file is read.

S6. If that projection is missing or whitespace, `RoadmapStepException` is thrown with the message that active selection cannot be used because its projection is missing.

S7. Persisted roadmap state is loaded with `stateStore.LoadAsync()`.

S8. `selectionProvenance.CaptureCurrentCycleAsync(selectionProjection, state?.RetiredEpics ?? [], cancellationToken)` starts.

S9. `BuildSelectionContextAsync` rebuilds the `SelectNextEpic` context from the selection projection, roadmap completion context, roadmap source references, and retired epics.

S10. `TransitionInputResolver.ResolveAsync` resolves the current `SelectNextEpic` input snapshot.

S11. The resolver adds the `SelectNextEpic` projection as a required input.

S12. The resolver adds `.agents/core/roadmap-completion-context.md` as required roadmap-completion context.

S13. The resolver enumerates `.agents/roadmap/*.md` and adds each roadmap source as required input.

S14. The resolver reads and hashes all required inputs.

S15. The resolver hashes the rebuilt prompt context and empty secondary input.

S16. The resolver computes the selection-cycle snapshot hash.

S17. `selectionProvenance.EvaluateActiveSelectionFreshnessAsync(currentCycle, state?.RetiredEpics ?? [], cancellationToken)` starts.

S18. Selection provenance manifest `.agents/selection-provenance-manifest.json` is loaded.

S19. Active trusted selection entries are selected from the manifest.

S20. `.agents/selection.md` is read and hashed when present.

S21. `DerivedArtifactFreshnessEvaluator.Evaluate` compares current selection-cycle provenance, the active manifest entry, and the selection artifact hash.

S22. If freshness is not fresh, `RoadmapStepException` is thrown with the stale reasons.

S23. If fresh, `ReadCurrentSelectionAsync` returns the selection markdown.

### Audit Projection And Context

A1. `contractRegistry.Get("EpicPreparationAudit")` loads the prompt contract.

A2. `projectionCache.EnsureAsync("EpicPreparationAudit", projectContext, contract, cancellationToken)` starts.

A3. `ProjectionCache` resolves `EpicPreparationAudit` from `ProjectionRegistry`.

A4. `ProjectionProvenanceFactory` creates current projection provenance from the projection definition and `ProjectContext`.

A5. `.agents/projections/epic-preparation-audit.md` is read.

A6. If projection content is missing or whitespace, `RoadmapPromptRunner.RunProjectionPromptAsync` renders and runs `ProjectionForEpicPreparationAudit`.

A7. Projection prompt execution uses `RunOneShotAsync`.

A8. `RunOneShotAsync` creates `ConsoleTurnRenderer`.

A9. `runtime.RunOneShotAsync(AgentSpecs.ReadOnlyPlanning(repository), prompt, renderer.Stream, cancellationToken)` runs.

A10. If the agent turn does not complete, `RoadmapStepException` is thrown.

A11. Silent output is echoed if needed and projection output is returned.

A12. Empty generated projection content is rejected.

A13. `ProjectionValidator.Validate("EpicPreparationAudit", content)` validates title, required sections, intended consumer, and absence of forbidden runtime-state headings.

A14. The projection content is hashed.

A15. Projection manifest is loaded.

A16. Existing manifest entry for `EpicPreparationAudit` is found if present.

A17. Validation status is computed.

A18. Freshness is computed as fresh for generated content, or by `ProjectionFreshnessEvaluator` for existing content.

A19. A new or updated manifest entry is created.

A20. The manifest entry is upserted before later blocking or generated-file writes.

A21. If validation failed, `.agents/evidence/blockers/projection-blocked.NNNN.md` is written and `RoadmapStepException` is thrown.

A22. If projection content was generated, `.agents/projections/epic-preparation-audit.md` is written.

A23. If projection freshness is not fresh and the prompt contract stale policy is `Block`, projection-blocker evidence is written and `RoadmapStepException` is thrown.

A24. `ProjectionCacheResult` is returned.

A25. `contextBuilder.BuildAuditContext(projection.Content, selection)` builds the audit runtime context.

A26. The audit context contains sections in this order:

- `Projection Content`
- `Selected Epic`
- `Repository Inspection Instructions`

A27. The context is rejected if it contains raw Project Context markers.

### Audit Prompt Execution

P1. `RunPromptTransitionAsync` is called with:

- from: `RoadmapState.ExistingEpicSelected`
- to: `RoadmapState.EpicPreparationAudit`
- prompt: `EpicPreparationAudit`
- projection: `.agents/projections/epic-preparation-audit.md`
- project context: the rendered audit context
- secondary input: the selection markdown
- outputs: `.agents/evidence/audits`

P2. `RunPromptTransitionAsync` delegates to `RunPromptTransitionWithCompletionAsync`.

P3. `inputResolver.ResolveAsync` starts for `EpicPreparationAudit`.

P4. The projection path is added as a required input with role `Projection`.

P5. `.agents/selection.md` is added as a required input with role `Selection`.

P6. The resolver reads and hashes the projection and selection inputs.

P7. The resolver hashes the rendered audit context.

P8. The resolver hashes the secondary selection input.

P9. The resolver computes a transition input snapshot hash.

P10. A correlation id is generated.

P11. `started = DateTimeOffset.UtcNow` is captured.

P12. A `Stopwatch` is started.

P13. `SaveStateAsync` persists the transition as started:

- current state: `EpicPreparationAudit`
- status: `Started`
- from: `ExistingEpicSelected`
- to: `EpicPreparationAudit`
- prompt: `EpicPreparationAudit`
- projection: `.agents/projections/epic-preparation-audit.md`
- output: `.agents/evidence/audits`
- decision: `Pending`

P14. Inside `SaveStateAsync`, existing roadmap state is loaded.

P15. Projection manifest is loaded.

P16. active artifact rows are rebuilt for roadmap completion context, selection, and active epic.

P17. last decision id is loaded from the decision ledger.

P18. existing retired epics and blockers are retained.

P19. split family count is computed from `.agents/splits/split-family-*.json`.

P20. projection manifest counts are computed.

P21. `.agents/state.json` is written through `stateStore.SaveAsync`.

P22. `journalStore.AppendAsync` appends `TransitionStarted` to `.agents/journal/transitions.jsonl`.

P23. `promptRunner.RunRuntimePromptAsync("EpicPreparationAudit", context, selection, cancellationToken)` starts.

P24. The runtime prompt is rendered from `RoadmapPromptCatalog`.

P25. The implementation-first prompt policy is appended.

P26. `RunOneShotAsync` creates a `ConsoleTurnRenderer`.

P27. `runtime.RunOneShotAsync(AgentSpecs.ReadOnlyPlanning(repository), prompt, renderer.Stream, cancellationToken)` runs the audit prompt.

P28. If the agent result state is not `Completed`, `RoadmapStepException` is thrown.

P29. Silent output is echoed if needed.

P30. The raw audit output is returned.

P31. The stopwatch is stopped.

P32. `completed = DateTimeOffset.UtcNow` is captured.

P33. `journalStore.AppendAsync` appends `TransitionCompleted` with elapsed milliseconds, output paths, input hashes, and input snapshot.

P34. `SaveStateAsync` persists the transition as completed:

- current state: `EpicPreparationAudit`
- status: `Completed`
- from: `ExistingEpicSelected`
- to: `EpicPreparationAudit`
- prompt: `EpicPreparationAudit`
- projection: `.agents/projections/epic-preparation-audit.md`
- output: `.agents/evidence/audits`
- decision: `Completed`

P35. `RunPromptTransitionWithCompletionAsync` returns `PromptTransitionCompletion`.

P36. `RunPromptTransitionAsync` returns the raw audit output.

### Audit Evidence And Disposition

D1. `artifacts.WriteNumberedEvidenceAsync(.agents/evidence/audits, "epic-preparation-audit", output)` chooses the next `epic-preparation-audit.NNNN.md` path.

D2. The raw audit output is written to that numbered audit evidence path.

D3. `CaptureHitlRequestsAsync(auditPath, output)` runs.

D4. If `hitlRequestCapture` is null or output is whitespace, capture returns without effect.

D5. Otherwise, HITL non-implementation request markers are captured from the audit evidence artifact.

D6. `EpicPreparationAuditParser.Parse(output)` parses the audit markdown.

D7. The parser reads the `## Audit Disposition` field table.

D8. The parser reads the `## Selected Epic` field table.

D9. Required selected epic fields are read: `Epic ID`, `Epic Name`.

D10. Required disposition fields are read: `Disposition`, `Confidence`, `Primary Reason`, `Evidence Strength`, `Recommended Next Step`.

D11. `Disposition` must be one of `Realign`, `Reimagine`, `Retire`, or `Insufficient Evidence`.

D12. `Confidence` must be one of the common confidence values.

D13. `Recommended Next Step` must be one of `Realign Epic`, `Reimagine Epic`, `Retire Epic`, or `Gather More Evidence`.

D14. `EpicPreparationAuditDecision` is returned.

D15. `AppendDecisionAsync` appends an audit decision:

- state: `EpicPreparationAudit`
- transition: `EpicPreparationAudit`
- prompt: `EpicPreparationAudit`
- projection path: `.agents/projections/epic-preparation-audit.md`
- output path: the numbered audit evidence path
- decision: parsed disposition
- confidence: parsed confidence
- rationale: parsed recommended next step

D16. `AppendDecisionAsync` gets the next decision id from the decision ledger.

D17. The decision ledger document is loaded or migrated.

D18. A `DecisionLedgerEntry` is appended and saved.

### Retire Branch

R1. If `decision.Disposition == "Retire"`, persisted state is loaded.

R2. `retiredAt = DateTimeOffset.UtcNow` is captured.

R3. `RetiredEpic.FromSelectionAndAudit(selectionDecision, decision, auditPath, retiredAt)` builds a retired epic record.

R4. The retired epic id is the first known value from audit `EpicId` and selection `ExistingEpicId`.

R5. The retired epic name is the first known value from audit `EpicName`, selection `ExistingEpicName`, and selection `RecommendedInitiative`.

R6. The retired reason is the first known value from audit `PrimaryReason` and selection `PrimaryReason`.

R7. The retired record must have a stable identity, or `RoadmapStepException` is thrown.

R8. `RetiredEpic.Upsert(existing?.RetiredEpics ?? [], retired)` inserts or deduplicates by stable identity.

R9. A second decision ledger entry is appended:

- state: `RetireEpic`
- decision: `Retired Epic`
- output path: the audit evidence path
- rationale: identity kind, stable identity, and primary reason

R10. `SaveStateAsync` persists the retire result:

- current state: `RetireEpic`
- status: `Completed`
- from: `EpicPreparationAudit`
- to: `RetireEpic`
- prompt: `EpicPreparationAudit`
- projection: `.agents/projections/epic-preparation-audit.md`
- output: audit evidence path
- decision: `Retired Epic`
- retired epics: the upserted list
- blockers: existing blockers retained because null is passed

R11. `SupersedeActiveSelectionAsync([RetiredEpicStateDrift], "Retired epic state changed after EpicPreparationAudit.")` starts.

R12. `selectionProvenance.SupersedeActiveSelectionAsync` loads the selection provenance manifest.

R13. If there are active trusted selections, they are saved as superseded with reason `RetiredEpicStateDrift`.

R14. `lifecycleStore.UpsertAsync(.agents/selection.md, Superseded, "Retired epic state changed after EpicPreparationAudit.")` writes selection lifecycle metadata.

R15. `AuditAndPrepareExistingEpicAsync` returns `EpicPreparationResult.Retired`.

R16. The caller returns `RoadmapOutcome.Paused`.

### Insufficient Evidence Branch

I1. If `decision.Disposition == "Insufficient Evidence"`, `AuditAndPrepareExistingEpicAsync` throws `RoadmapStepException("Epic preparation audit requires more evidence.")`.

I2. The audit transition has already been persisted as completed.

I3. The audit evidence has already been written.

I4. The audit decision has already been appended to the decision ledger.

I5. The exception is not marked `AlreadyPersisted`.

I6. `RunAsync` catches it in the general `RoadmapStepException` catch block.

I7. `ReportEphemeralBlockerAsync("Roadmap state machine", exception.Message)` emits a warning.

I8. `console.Error(exception.Message)` emits the error.

I9. `RunAsync` returns `RoadmapOutcome.Failed`.

I10. No additional durable blocker state is written by this branch.

### Realign Branch

G1. If `decision.Disposition == "Realign"`, `RewriteActiveEpicAsync("RealignEpic", RoadmapState.RealignEpic, projectContext, auditPath, cancellationToken)` starts.

G2. `console.Phase("RealignEpic")` writes the phase message.

G3. The helper reads `.agents/epic.md` if it exists.

G4. If `.agents/epic.md` does not exist, it calls `ReadCurrentSelectionAsync` again and repeats the active selection freshness validation described in S1-S23.

G5. The numbered audit evidence artifact is read with `artifacts.ReadRequiredAsync(auditPath)`.

G6. `contractRegistry.Get("RealignEpic")` loads the prompt contract.

G7. `projectionCache.EnsureAsync("RealignEpic", projectContext, contract, cancellationToken)` resolves, validates, possibly generates, manifests, writes, or blocks the realign projection using the same projection-cache sequence described in A2-A24.

G8. `contextBuilder.BuildRealignOrReimagineContext(projection.Content, selectionOrEpic, audit)` builds the rewrite context.

G9. The rewrite context contains sections in this order:

- `Projection Content`
- `Current Epic`
- `Audit Output`
- `Repository Inspection Instructions`

G10. The rewrite context is rejected if it contains raw Project Context markers.

G11. `RunPromptForPromotionAsync` starts with:

- from: `RealignEpic`
- promotion target: `ActiveEpicReady`
- prompt: `RealignEpic`
- projection: `.agents/projections/realign-epic.md`
- project context: rendered rewrite context
- secondary input: audit content
- outputs: `.agents/epic.md`
- input context: audit evidence path

G12. `inputResolver.ResolveAsync` starts for `RealignEpic`.

G13. The realign projection is added as required input.

G14. `AddCurrentEpicOrSelectionInputAsync` adds `.agents/epic.md` as required input if present, otherwise `.agents/selection.md`.

G15. The audit evidence path is added as required input with role `AuditEvidence`.

G16. The resolver reads and hashes the required inputs, rendered context, and secondary audit input.

G17. A correlation id, start timestamp, and stopwatch are created.

G18. `SaveStateAsync` persists the prompt as started:

- current state: `RealignEpic`
- status: `Started`
- from: `RealignEpic`
- to: `ActiveEpicReady`
- output: `.agents/epic.md`
- decision: `Prompt Started`

G19. `TransitionStarted` is appended to the transition journal.

G20. `promptRunner.RunRuntimePromptAsync("RealignEpic", context, audit, cancellationToken)` runs the rewrite prompt.

G21. On success, `PromptCompleted` is appended to the transition journal.

G22. `SaveStateAsync` persists:

- current state: `RealignEpic`
- status: `PromptCompleted`
- from: `RealignEpic`
- to: `ActiveEpicReady`
- output: `.agents/epic.md`
- decision: `Prompt Completed`

G23. `PromptTransitionCompletion` is returned.

G24. `PromoteActiveEpicAsync(RealignEpic, "RealignEpic", projectionPath, completion)` starts.

G25. `promotionService.PromoteAsync` classifies the candidate active epic output.

G26. `EpicAuthoringOutputClassifier` requires a top-level Markdown heading.

G27. A heading containing `Blocked` produces `ArtifactOutputKind.Blocked`.

G28. A `# Epic: ...` heading produces `ArtifactOutputKind.Promotable`.

G29. A malformed epic heading or epic metadata without the required heading produces `Malformed`.

G30. Otherwise the output is `Ambiguous`.

G31. If not promotable, promotion evidence is preserved under `.agents/evidence/blockers/active-epic-promotion.NNNN.md`.

G32. Preserved blocker evidence is marked lifecycle `Blocked`.

G33. If promotable, `EpicArtifactValidator` validates active epic structure.

G34. The validator requires non-empty content, required epic sections, metadata fields, and at least one valid milestone roadmap row.

G35. If validation fails, promotion evidence is preserved under `.agents/evidence/blockers/active-epic-promotion.NNNN.md` and marked lifecycle `Blocked`.

G36. If validation passes, `.agents/epic.md` is written.

G37. `.agents/epic.md` lifecycle is upserted to `Ready` with `Promoted by RealignEpic.`

G38. On promotion success, HITL request markers are captured from `.agents/epic.md` if configured.

G39. `ArtifactPromoted` is appended to the transition journal.

G40. `SaveStateAsync` persists:

- current state: `ActiveEpicReady`
- status: `Completed`
- from: `RealignEpic`
- to: `ActiveEpicReady`
- output: `.agents/epic.md`
- decision: `Artifact Promoted`

G41. Promotion result is returned as promoted.

G42. `AuditAndPrepareExistingEpicAsync` returns `EpicPreparationResult.ActiveEpicReady`.

G43. The caller breaks out of the selection switch and then runs `GenerateMilestoneSpecsAsync`; that later transition is outside this audit.

G44. On promotion block, ambiguity, or structural invalidity, `ArtifactPromotionBlocked` is appended to the transition journal.

G45. `SaveStateAsync` persists:

- current state: `EvidenceBlocked`
- status: `Paused`
- from: `RealignEpic`
- to: `ActiveEpicReady`
- output: the preserved blocker evidence path
- decision: `Artifact Promotion Blocked`, `Artifact Promotion Ambiguous`, or `Artifact Promotion Invalid`
- blocker: promotion reason and instruction to review the evidence
- transition intent: `ResolveArtifactPromotionBlocker`
- next transitions: `Resolve blocker and rerun`

G46. Promotion result is returned as not promoted.

G47. `AuditAndPrepareExistingEpicAsync` returns `EpicPreparationResult.Blocked`.

G48. The caller returns `RoadmapOutcome.Paused`.

### Reimagine Branch

M1. If the disposition is neither `Retire`, `Insufficient Evidence`, nor `Realign`, the current implementation treats it as `Reimagine`.

M2. `RewriteActiveEpicAsync("ReimagineEpic", RoadmapState.ReimagineEpic, projectContext, auditPath, cancellationToken)` starts.

M3. The reimagine branch follows the same sequence as G2-G48 with these substitutions:

- prompt: `ReimagineEpic`
- from/current state during rewrite: `ReimagineEpic`
- projection path: `.agents/projections/reimagine-epic.md`
- lifecycle note on successful promotion: `Promoted by ReimagineEpic.`

### Prompt Failure And Cancellation Behavior

F1. If `RunPromptTransitionWithCompletionAsync` catches a non-cancellation exception during the audit prompt, it stops the stopwatch.

F2. It appends `TransitionFailed`.

F3. It persists state:

- current state: `EvidenceBlocked`
- status: `Failed`
- from: `ExistingEpicSelected`
- to: `EpicPreparationAudit`
- output: `.agents/evidence/audits`
- decision: `Failed`
- blocker: exception message
- transition intent: `ResolveTransitionFailure`
- next transitions: `Resolve blocker and rerun`

F4. It throws `RoadmapStepException.AlreadyPersisted(exception)`.

F5. `RunAsync` catches already-persisted failures, writes `console.Error`, and returns `RoadmapOutcome.Failed`.

F6. If `RunPromptForPromotionAsync` catches a non-cancellation exception during `RealignEpic` or `ReimagineEpic`, it appends `TransitionFailed`, persists `EvidenceBlocked` with status `Failed`, and throws `RoadmapStepException.AlreadyPersisted(exception)`.

F7. `OperationCanceledException` is not caught by either prompt helper.

F8. `RunAsync` catches cancellation, calls `WriteCancelledStateAsync`, and returns `RoadmapOutcome.Cancelled`.

## 3. Concern Inventory

| Step | Concern Classification | Where Concerns Mix |
|---|---|---|
| E1-E3 | routing | Caller selection routing chooses this transition and later decides whether to pause or continue to milestone generation. |
| E4-E5 | transition setup, reporting | Constant selection and console output happen in the orchestration method. |
| S1-S6 | artifact read, validation, recovery guard | Reading `.agents/selection.md`, reading the prior projection, and producing failure text are interleaved. |
| S7-S16 | persistence read, context build, provenance snapshot, hashing | Current state, retired epics, roadmap completion context, roadmap sources, and prompt hashes are assembled to validate one selection. |
| S17-S23 | provenance validation, artifact read, decision | Manifest loading, selection hashing, freshness evaluation, and exception construction are mixed. |
| A1-A5 | contract lookup, projection routing, artifact read | Prompt contract, projection registry, provenance, and file reads are coupled. |
| A6-A12 | projection prompt execution, console streaming, validation | Projection generation runs an agent and immediately validates non-empty output. |
| A13-A24 | validation, hashing, manifest persistence, artifact write, blocker creation | Projection validation, manifest mutation, generated projection writes, and blocker evidence are handled in one helper. |
| A25-A27 | context construction, validation | Prompt sections and raw-project-context rejection are bound together. |
| P1-P12 | transition input resolution, correlation, timing | Snapshot creation, ids, and timings are prepared immediately before state mutation. |
| P13-P21 | state mutation, persistence summary, manifest summarization | `SaveStateAsync` writes roadmap state while also recomputing active artifacts, manifest counts, split family count, blockers, and retired epic counts. |
| P22 | journaling | Journal append is adjacent to state persistence, creating two required records for one transition moment. |
| P23-P30 | prompt execution, console streaming, agent validation | Runtime prompt rendering, policy injection, agent invocation, and result validation are mixed. |
| P31-P35 | timing, journaling, state mutation | Completion timing, journal append, and completed-state persistence happen in sequence. |
| D1-D5 | artifact write, HITL capture | Audit evidence materialization is directly followed by optional non-implementation capture. |
| D6-D14 | parsing, validation | Markdown table recovery and allowed-value validation are embedded in parser behavior. |
| D15-D18 | decision ledger, persistence | Parsed disposition is written as a durable decision immediately before branch routing. |
| R1-R8 | state read, identity normalization, decision | The retire branch reads state, merges identity from selection and audit, validates stable identity, and deduplicates records. |
| R9-R10 | decision ledger, state mutation | A second retire-specific decision is written, then state is saved as `RetireEpic`. |
| R11-R14 | provenance mutation, lifecycle mutation | Retiring an epic mutates selection provenance and selection lifecycle. |
| R15-R16 | return routing | Return enum controls caller outcome. |
| I1-I10 | exception, reporting, partial durability | The branch throws after durable audit artifacts and decision ledger writes but before durable blocker state. |
| G1-G11 | branch routing, artifact read, projection, context construction | Disposition routing, active epic fallback, audit read, projection cache, and rewrite context construction are in one path. |
| G12-G23 | input snapshot, state mutation, journaling, prompt execution | Rewrite prompt execution owns started/prompt-completed state and journal writes. |
| G24-G37 | classification, validation, artifact write, lifecycle | Active epic promotion decides whether to write `.agents/epic.md` or preserve blocker evidence. |
| G38-G40 | HITL capture, journaling, state mutation | Successful promotion captures optional HITL markers, appends journal, and saves `ActiveEpicReady`. |
| G44-G48 | blocker persistence, journaling, return routing | Promotion failure writes evidence, lifecycle, journal, blocked state, transition intent, and return enum. |
| M1-M3 | default branch routing | The final branch assumes `Reimagine` because parser validation has already limited allowed values. |
| F1-F8 | recovery, persistence, reporting | Runtime exceptions produce durable failed state in prompt helpers; cancellation is handled by the outer `RunAsync`. |

The strongest mixing occurs at:

- `AuditAndPrepareExistingEpicAsync`, which reads selection, ensures projection, runs prompt, writes evidence, parses, records decisions, and routes branches.
- `RunPromptTransitionWithCompletionAsync`, which resolves inputs, persists state, journals, invokes the agent, and handles failures.
- `PromoteActiveEpicAsync`, which promotes artifacts, captures HITL requests, writes journal rows, persists state, and creates blockers.
- `SaveStateAsync`, which is a generic persistence writer but also recomputes summary state from several stores.

## 4. Hidden Steps

The transition has several implicit steps that are easy to miss because they are hidden inside helpers.

Build Selection Freshness Context

`ReadCurrentSelectionAsync` does not simply read `.agents/selection.md`. It reconstructs the current `SelectNextEpic` input cycle by rebuilding selection prompt context and hashing the projection, roadmap completion context, roadmap sources, prompt context, secondary input, and retired epic state.

Resolve Audit Inputs

`RunPromptTransitionWithCompletionAsync` calls `inputResolver.ResolveAsync`. For `EpicPreparationAudit`, this captures the projection and selection artifact as causal inputs and hashes the rendered audit context and secondary selection input.

Capture Transition Snapshot

The resolved `TransitionInputSnapshot` is stored in transition journal records and used to derive input artifact hashes. This is an evidence snapshot, not just a helper return value.

Persist Started State

Before the audit prompt runs, state is saved as `EpicPreparationAudit` / `Started`. This is externally observable in `.agents/state.json` if execution stops between start and completion.

Record Started Journal

The same started moment is also written to `.agents/journal/transitions.jsonl` as `TransitionStarted`.

Inject Prompt Policy

Runtime prompt execution appends the implementation-first prompt policy before invoking the agent.

Normalize Prompt Completion

The prompt helper turns an agent `Completed` result into raw output, echoes silent output, stops timing, and writes both journal and state completion records.

Materialize Audit Evidence

The audit prompt output is not used only in memory. It is copied into `.agents/evidence/audits/epic-preparation-audit.NNNN.md`.

Capture HITL Markers

Audit output and promoted active epic output may be scanned for explicit HITL non-implementation request markers when `hitlRequestCapture` is configured.

Normalize Audit Decision

`EpicPreparationAuditParser` translates markdown tables into a strongly-shaped decision and rejects unknown dispositions, confidence values, and next steps.

Record Audit Decision

The parsed disposition is always appended to the decision ledger before disposition routing.

Retired Epic Identity Recovery

The retire branch does not trust only the audit. It merges identity from audit fields and the original selection decision, then requires a known stable identity.

Selection Supersession

Retiring an epic invalidates the active selection cycle by marking the active selection provenance as superseded and the selection artifact lifecycle as `Superseded`.

Rewrite Input Selection

`RewriteActiveEpicAsync` uses `.agents/epic.md` when present; otherwise it falls back to the active selection and revalidates selection freshness.

Promotion Classification

Realign/Reimagine output is classified before validation. Blocked, malformed, and ambiguous outputs do not overwrite `.agents/epic.md`.

Promotion Evidence Preservation

Rejected promotion candidates are preserved as numbered blocker evidence and marked lifecycle `Blocked`.

Promotion Completion State

Successful promotion writes `.agents/epic.md`, marks it `Ready`, appends `ArtifactPromoted`, and persists state as `ActiveEpicReady`.

Blocked Promotion State

Failed promotion appends `ArtifactPromotionBlocked`, persists `EvidenceBlocked`, creates a blocker row, and sets `ResolveArtifactPromotionBlocker` transition intent.

## 5. Natural Step Boundaries

Step 1: Enter Selected Existing Epic Preparation

Purpose

Start from a parsed `SelectionDecision` whose recommended outcome is `"Select Existing Epic"`.

Inputs

- `SelectionDecision`
- `ProjectContext`
- `CancellationToken`

Outputs

- Transition method invoked
- Audit phase printed

---

Step 2: Recover And Validate Active Selection

Purpose

Ensure the selection artifact being audited is the same current-cycle selection produced by `SelectNextEpic`.

Inputs

- `.agents/selection.md`
- `.agents/projections/select-next-epic.md`
- `.agents/state.json`
- retired epics
- roadmap completion context
- roadmap source files
- selection provenance manifest

Outputs

- Selection markdown
- Exception if missing, stale, or untrusted

---

Step 3: Ensure Audit Projection

Purpose

Obtain usable `EpicPreparationAudit` projection content.

Inputs

- `ProjectContext`
- `EpicPreparationAudit` contract
- projection registry entry
- existing or generated projection file
- projection manifest

Outputs

- `ProjectionCacheResult`
- possibly generated `.agents/projections/epic-preparation-audit.md`
- possibly updated projection manifest
- possible projection blocker evidence and exception

---

Step 4: Build Audit Context

Purpose

Create the exact runtime context the audit prompt consumes.

Inputs

- audit projection content
- selection markdown

Outputs

- rendered audit context
- exception if raw Project Context markers are present

---

Step 5: Execute Audit Prompt Transition

Purpose

Run `EpicPreparationAudit` and persist the prompt transition around it.

Inputs

- from/to states
- prompt name
- projection path
- audit context
- selection markdown as secondary input
- output directory
- transition input resolver
- prompt runner
- state, journal, manifest, decision, artifact status stores

Outputs

- started state
- `TransitionStarted` journal record
- raw audit output
- completed state
- `TransitionCompleted` journal record
- failed state and `TransitionFailed` journal record on non-cancellation runtime failure

---

Step 6: Materialize And Parse Audit Evidence

Purpose

Turn raw prompt output into durable evidence and a parsed disposition.

Inputs

- raw audit output
- audit evidence directory
- optional HITL request capture service
- `EpicPreparationAuditParser`

Outputs

- `.agents/evidence/audits/epic-preparation-audit.NNNN.md`
- optional HITL request ledger entries
- `EpicPreparationAuditDecision`
- parser exception on malformed output

---

Step 7: Record Audit Decision

Purpose

Persist the parsed disposition to the decision ledger before routing.

Inputs

- parsed audit decision
- audit evidence path
- projection path

Outputs

- new decision ledger entry

---

Step 8: Route Retire

Purpose

Persist retirement and invalidate the active selection.

Inputs

- original `SelectionDecision`
- parsed audit decision
- audit evidence path
- existing state and retired epics
- selection provenance manifest
- lifecycle store

Outputs

- retired epic record
- retire decision ledger entry
- roadmap state `RetireEpic`
- selection provenance superseded
- selection lifecycle `Superseded`
- return `EpicPreparationResult.Retired`

---

Step 9: Route Insufficient Evidence

Purpose

Stop execution after preserving the completed audit and parsed decision.

Inputs

- parsed audit decision

Outputs

- `RoadmapStepException`
- outer warning and error
- `RoadmapOutcome.Failed`
- no additional durable blocker state

---

Step 10: Route Realign

Purpose

Use audit output to rewrite the selected epic into a valid active epic.

Inputs

- `RealignEpic` prompt
- project context
- active epic if present, otherwise current selection
- audit evidence
- projection/cache services
- promotion service

Outputs

- rewritten active epic candidate
- `.agents/epic.md` plus lifecycle `Ready` on success
- or blocker evidence plus `EvidenceBlocked` state on promotion failure
- return `ActiveEpicReady` or `Blocked`

---

Step 11: Route Reimagine

Purpose

Use audit output to re-author the selected epic into a valid active epic.

Inputs

- `ReimagineEpic` prompt
- project context
- active epic if present, otherwise current selection
- audit evidence
- projection/cache services
- promotion service

Outputs

- reimagined active epic candidate
- `.agents/epic.md` plus lifecycle `Ready` on success
- or blocker evidence plus `EvidenceBlocked` state on promotion failure
- return `ActiveEpicReady` or `Blocked`

---

Step 12: Return To Caller

Purpose

Let the caller decide whether to pause or continue into the next transition.

Inputs

- `EpicPreparationResult`

Outputs

- caller returns `RoadmapOutcome.Paused` for `Retired` or `Blocked`
- caller invokes `GenerateMilestoneSpecsAsync` for `ActiveEpicReady`

## 6. Mixed-Concern Analysis

Step 1 mixes routing and reporting.

The same entry path that chooses selected-existing-epic handling also prints phase output. An engineer reading transition selection has to keep console behavior in mind.

Step 2 mixes artifact reads, provenance reconstruction, hashing, retired epic state, and validation.

`ReadCurrentSelectionAsync` sounds like a read helper, but it also rebuilds selection-cycle provenance and can block the transition. The method name hides its validation authority.

Step 3 mixes projection generation, validation, manifest mutation, stale checking, artifact writes, and blocker evidence.

Projection cache behavior is essential to the transition, but the audit method reads as one `EnsureAsync` call. The call can run an agent, write files, update manifests, or throw blocker exceptions.

Step 4 mixes context formatting and safety validation.

`BuildAuditContext` appears to format strings, but it can reject raw project-context markers and stop the transition.

Step 5 mixes input capture, state mutation, journaling, prompt execution, timing, and failure persistence.

The audit prompt run is not just a prompt call. It writes started state, appends a journal row, invokes the agent, appends completion or failure journal rows, saves completed or failed state, and returns raw output. This is the largest readability burden.

Step 6 mixes evidence materialization, optional HITL capture, and structural parsing.

The transition moves directly from writing audit evidence to scanning it for HITL markers and parsing a disposition. These are separate reasons to inspect the same output.

Step 7 mixes parsed decision interpretation with durable ledger mutation.

The decision ledger entry is appended before branch effects. If later branch handling fails, the audit decision still exists.

Step 8 mixes retired epic identity normalization, deduplication, decision recording, state persistence, provenance mutation, lifecycle mutation, and return routing.

The retire branch is conceptually simple, but behavior spans multiple persistence surfaces.

Step 9 mixes semantic audit result with failure/reporting behavior.

`Insufficient Evidence` is an allowed parser disposition, but it throws a general `RoadmapStepException`. This creates partial durable behavior: audit completion and decision ledger remain, but no durable blocker row is created by the branch itself.

Step 10 and Step 11 mix disposition routing with a second prompt transition and artifact promotion.

Realign and Reimagine are chosen by the audit disposition but then use a separate prompt contract, projection, context, prompt execution, candidate validation, artifact write, journal write, and state write. Understanding this branch requires following several helper methods.

Step 12 mixes return enum interpretation with caller-controlled next transition.

`ActiveEpicReady` does not itself pause. The caller immediately starts milestone generation. The selected-existing-epic transition boundary is therefore not obvious from the top-level execution flow.

## 7. Data Flow

SelectionDecision

Originates from the already-completed `SelectNextEpic` transition and is passed into `ContinueAfterSelectionAsync`.

Consumed by:

- selected-existing-epic routing
- `RetiredEpic.FromSelectionAndAudit` in the retire branch

Selection Markdown

Originates from `.agents/selection.md`.

Recovered by:

- `ReadCurrentSelectionAsync`

Consumed by:

- selection freshness reconstruction
- audit context builder
- `EpicPreparationAudit` secondary input
- rewrite fallback input when `.agents/epic.md` is absent

Selection Projection

Originates from `.agents/projections/select-next-epic.md`.

Consumed by:

- `ReadCurrentSelectionAsync`
- selection provenance cycle capture

Retired Epics

Originate from `.agents/state.json` loaded by `stateStore.LoadAsync`.

Consumed by:

- selection-cycle reconstruction
- active selection freshness validation
- retire branch upsert
- `SaveStateAsync`

EpicPreparationAudit Contract

Originates from `PromptContractRegistry`.

Consumed by:

- projection cache stale policy
- audit projection ensure operation

Audit Projection

Originates from `.agents/projections/epic-preparation-audit.md` or from `ProjectionForEpicPreparationAudit`.

Consumed by:

- `BuildAuditContext`
- transition input snapshot
- journal records
- state transition summary
- decision ledger projection field

Audit Runtime Context

Originates from `BuildAuditContext`.

Includes:

- audit projection content
- selected epic markdown
- repository inspection instructions

Consumed by:

- transition input resolver for prompt context hash
- runtime prompt renderer

TransitionInputSnapshot

Originates from `TransitionInputResolver.ResolveAsync`.

For `EpicPreparationAudit`, it includes:

- projection artifact hash
- selection artifact hash
- rendered context hash
- secondary input hash
- snapshot hash

Consumed by:

- `TransitionStarted` journal row
- `TransitionCompleted` or `TransitionFailed` journal row
- input artifact hash map

Raw Audit Output

Originates from `promptRunner.RunRuntimePromptAsync("EpicPreparationAudit", ...)`.

Consumed by:

- numbered audit evidence write
- optional HITL request capture
- `EpicPreparationAuditParser`

Audit Evidence Path

Originates from `artifacts.WriteNumberedEvidenceAsync(.agents/evidence/audits, "epic-preparation-audit", output)`.

Consumed by:

- decision ledger entry
- retire branch retired epic record
- retire branch state output
- Realign/Reimagine audit evidence input

EpicPreparationAuditDecision

Originates from `EpicPreparationAuditParser.Parse(output)`.

Consumed by:

- audit decision ledger entry
- branch routing
- retired epic identity and rationale
- Realign/Reimagine selection

RetiredEpic

Originates from `RetiredEpic.FromSelectionAndAudit`.

Consumed by:

- `RetiredEpic.Upsert`
- `SaveStateAsync` persisted retired epics
- future selection context

Selection Supersession

Originates from retire branch.

Consumed by:

- selection provenance manifest
- artifact lifecycle store
- future resume planning and selection freshness checks

Rewrite Prompt Output

Originates from `RealignEpic` or `ReimagineEpic` prompt execution.

Consumed by:

- `ArtifactPromotionService.PromoteAsync`
- optional blocker evidence preservation
- `.agents/epic.md` write on success

Promotion Result

Originates from `ArtifactPromotionService`.

Consumed by:

- `PromoteActiveEpicAsync`
- transition journal
- final state persistence
- return enum from `AuditAndPrepareExistingEpicAsync`

Roadmap State Document

Originates from `SaveStateAsync`.

Written to:

- `.agents/state.json`

Consumes:

- current state and transition summary passed by the caller
- active artifact statuses
- projection manifest counts
- last decision id
- retired epics
- blockers
- split family count
- transition intent
- next valid transitions

Return Value

Originates from branch handling in `AuditAndPrepareExistingEpicAsync`.

Consumed by:

- `ContinueAfterSelectionAsync`

Effects:

- `Retired` -> caller returns `Paused`
- `Blocked` -> caller returns `Paused`
- `ActiveEpicReady` -> caller proceeds to `GenerateMilestoneSpecsAsync`

## 8. Human Navigation Audit

If an engineer wants to understand only this transition, the minimum navigation path is:

Primary orchestration

- `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`
  - `ContinueAfterSelectionAsync`
  - `AuditAndPrepareExistingEpicAsync`
  - `RewriteActiveEpicAsync`
  - `RunPromptTransitionAsync`
  - `RunPromptTransitionWithCompletionAsync`
  - `RunPromptForPromotionAsync`
  - `PromoteActiveEpicAsync`
  - `CaptureHitlRequestsAsync`
  - `AppendDecisionAsync`
  - `ReadCurrentSelectionAsync`
  - `SupersedeActiveSelectionAsync`
  - `SaveStateAsync`

Transition input and provenance

- `src/LoopRelay.Roadmap.Cli/TransitionInputs.cs`
  - `TransitionInputResolver.ResolveAsync`
  - `AddPromptInputsAsync`
  - `AddCurrentEpicOrSelectionInputAsync`
- `src/LoopRelay.Roadmap.Cli/SelectionProvenance.cs`
  - `CaptureCurrentCycleAsync`
  - `EvaluateActiveSelectionFreshnessAsync`
  - `SupersedeActiveSelectionAsync`

Prompt context and projection

- `src/LoopRelay.Roadmap.Cli/RoadmapPromptContextBuilder.cs`
  - `BuildAuditContext`
  - `BuildRealignOrReimagineContext`
  - `BuildSelectionContextAsync`
- `src/LoopRelay.Roadmap.Cli/ProjectionCache.cs`
  - `EnsureAsync`
- `src/LoopRelay.Roadmap.Cli/ProjectionRegistry.cs`
- `src/LoopRelay.Roadmap.Cli/ProjectionValidator.cs`
- `src/LoopRelay.Roadmap.Cli/PromptContractRegistry.cs`

Prompt execution

- `src/LoopRelay.Roadmap.Cli/RoadmapPromptRunner.cs`
  - `RunRuntimePromptAsync`
  - `RunProjectionPromptAsync`
  - `RunOneShotAsync`
- Runtime prompt sources:
  - `src/LoopRelay.Core/Prompts/Planning/EpicPreparationAudit.prompt`
  - `src/LoopRelay.Core/Prompts/Planning/RealignEpic.prompt`
  - `src/LoopRelay.Core/Prompts/Planning/ReimagineEpic.prompt`
- Projection prompt sources:
  - `src/LoopRelay.Core/Prompts/Projections/ProjectionForEpicPreparationAudit.prompt`
  - `src/LoopRelay.Core/Prompts/Projections/ProjectionForRealignEpic.prompt`
  - `src/LoopRelay.Core/Prompts/Projections/ProjectionForReimagineEpic.prompt`

Parsing and models

- `src/LoopRelay.Roadmap.Cli/EpicPreparationAuditParser.cs`
- `src/LoopRelay.Roadmap.Cli/SelectionParser.cs`
- `src/LoopRelay.Roadmap.Cli/RetiredEpic.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapState.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapStateDocument.cs`

Artifact promotion

- `src/LoopRelay.Roadmap.Cli/ArtifactPromotion.cs`
- `src/LoopRelay.Roadmap.Cli/EpicArtifactPromotion.cs`
- `src/LoopRelay.Roadmap.Cli/ArtifactLifecycleStore.cs`

Persistence and reporting surfaces

- `src/LoopRelay.Roadmap.Cli/RoadmapStateStore.cs`
- `src/LoopRelay.Roadmap.Cli/DecisionLedgerStore.cs`
- `src/LoopRelay.Roadmap.Cli/TransitionJournalStore.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapArtifacts.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapArtifactPaths.cs`

Behavioral tests

- `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapStateMachineEpicPreparationTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/EpicArtifactPromotionTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/MarkdownParserTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/SelectionProvenanceTests.cs`

The shortest accurate reading order is:

1. `ContinueAfterSelectionAsync`
2. `AuditAndPrepareExistingEpicAsync`
3. `ReadCurrentSelectionAsync`
4. `ProjectionCache.EnsureAsync`
5. `RoadmapPromptContextBuilder.BuildAuditContext`
6. `RunPromptTransitionWithCompletionAsync`
7. `EpicPreparationAuditParser`
8. `AppendDecisionAsync`
9. One branch:
   - retire: `RetiredEpic`, `SaveStateAsync`, `SupersedeActiveSelectionAsync`
   - realign/reimagine: `RewriteActiveEpicAsync`, `RunPromptForPromotionAsync`, `PromoteActiveEpicAsync`

## 9. Extraction Boundary

Smallest behavior-preserving boundary:

Extract the body and directly related branch flow of `AuditAndPrepareExistingEpicAsync` into a named transition handler with one entry and one return value equivalent to `EpicPreparationResult`.

The extracted handler should own:

- selected-existing-epic selection validation
- `EpicPreparationAudit` projection/context/prompt/evidence/parse/decision flow
- branch routing for `Retire`, `Insufficient Evidence`, `Realign`, and `Reimagine`
- the existing Realign/Reimagine call sequence needed before returning `ActiveEpicReady` or `Blocked`
- all state, journal, lifecycle, decision, artifact, HITL, and exception behavior currently produced before the method returns

The extracted handler should not own:

- top-level command execution
- startup planning
- resume planning
- `SelectNextEpic`
- caller behavior after `EpicPreparationResult.ActiveEpicReady`
- `GenerateMilestoneDeepDivesForEpic`
- `CreateNewEpic`
- `SplitEpic`
- completion certification
- unblock recovery

Natural entry:

```text
ExecuteAsync(selectionDecision, projectContext, cancellationToken)
```

Natural exit:

```text
EpicPreparationResult
```

The existing state machine can continue to interpret that result exactly as it does today.

## 10. Required Inputs

Required for every path

- Parsed `SelectionDecision`.
- `ProjectContext`.
- `CancellationToken`.
- Console phase output capability.
- Artifact access to:
  - `.agents/selection.md`
  - `.agents/projections/select-next-epic.md`
  - `.agents/projections/epic-preparation-audit.md`
  - `.agents/evidence/audits`
  - `.agents/state.json`
  - `.agents/decision-ledger.json`
  - `.agents/journal/transitions.jsonl`
  - projection manifest files
  - selection provenance manifest
  - lifecycle metadata
- `PromptContractRegistry`.
- `ProjectionCache`.
- `RoadmapPromptContextBuilder`.
- `TransitionInputResolver`.
- `RoadmapPromptRunner`.
- `RoadmapStateStore`.
- `ProjectionManifestStore` indirectly through `SaveStateAsync`.
- `DecisionLedgerStore`.
- `TransitionJournalStore`.
- `SelectionProvenanceService`.
- `ArtifactLifecycleStore`.

Required only for retire path

- Existing retired epics from persisted state.
- `RetiredEpic.FromSelectionAndAudit`.
- `RetiredEpic.Upsert`.

Required only for Realign/Reimagine paths

- Access to `.agents/epic.md` when present.
- Access to the audit evidence path.
- `RealignEpic` or `ReimagineEpic` prompt contract.
- `RealignEpic` or `ReimagineEpic` projection.
- `ArtifactPromotionService`.
- `EpicAuthoringOutputClassifier`.
- `EpicArtifactValidator`.
- Blocker evidence directory `.agents/evidence/blockers`.

Optional

- `hitlRequestCapture`; when null, HITL capture is skipped.
- Existing `.agents/epic.md`; if missing, the rewrite path falls back to the current selection.
- Existing projection files; if missing, projection cache generates them.
- Existing decision ledger/state/lifecycle markdown legacy files; stores may migrate them.

Incidental to this extraction

- Startup planner.
- Resume planner, except that later resume behavior observes the state written here.
- Unblock planner.
- Completion certification policy/router/archive services.
- Bundle file extractor and split-family services.
- Execution preparation provenance, except where it is a constructor dependency of `TransitionInputResolver` and context builder.
- Invariant validator.

## 11. Required Outputs

Returned value

- `EpicPreparationResult.Retired`
- `EpicPreparationResult.Blocked`
- `EpicPreparationResult.ActiveEpicReady`
- Exceptions for malformed, stale, missing, failed, or insufficient-evidence cases.

Console output

- `Audit selected epic`
- `RealignEpic` or `ReimagineEpic` when those branches run
- agent turn rendering through `ConsoleTurnRenderer`
- outer error/warning behavior through `RunAsync` on uncaught non-already-persisted exceptions

Projection artifacts

- possible `.agents/projections/epic-preparation-audit.md`
- possible `.agents/projections/realign-epic.md`
- possible `.agents/projections/reimagine-epic.md`
- projection manifest updates
- possible `.agents/evidence/blockers/projection-blocked.NNNN.md`

Transition journal

- `TransitionStarted` for `EpicPreparationAudit`
- `TransitionCompleted` for `EpicPreparationAudit`
- `TransitionFailed` for audit runtime failures
- `TransitionStarted` for `RealignEpic` or `ReimagineEpic` when applicable
- `PromptCompleted` for `RealignEpic` or `ReimagineEpic` when applicable
- `TransitionFailed` for rewrite runtime failures
- `ArtifactPromoted` on successful active epic promotion
- `ArtifactPromotionBlocked` on blocked/ambiguous/invalid promotion

State writes

- `EpicPreparationAudit` / `Started`
- `EpicPreparationAudit` / `Completed`
- `RetireEpic` / `Completed`
- `RealignEpic` or `ReimagineEpic` / `Started`
- `RealignEpic` or `ReimagineEpic` / `PromptCompleted`
- `ActiveEpicReady` / `Completed`
- `EvidenceBlocked` / `Paused` for promotion block
- `EvidenceBlocked` / `Failed` for runtime failures caught by prompt helpers
- `Cancelled` through outer cancellation handling

Audit artifacts

- `.agents/evidence/audits/epic-preparation-audit.NNNN.md`

Decision ledger

- audit disposition decision at `EpicPreparationAudit`
- additional `RetireEpic` decision for retire branch

Lifecycle writes

- `.agents/selection.md` -> `Superseded` on retire
- `.agents/epic.md` -> `Ready` on successful Realign/Reimagine promotion
- preserved active-epic promotion evidence -> `Blocked` on promotion rejection

Selection provenance

- active selection manifest entries superseded with `RetiredEpicStateDrift` on retire

Active epic artifact

- `.agents/epic.md` overwritten only on valid promoted Realign/Reimagine output

Blocker artifacts

- `.agents/evidence/blockers/active-epic-promotion.NNNN.md` on rejected promotion output
- `.agents/evidence/blockers/projection-blocked.NNNN.md` on projection validation/staleness failure

Blocker state

- `BlockerRow` and transition intent `ResolveArtifactPromotionBlocker` on promotion failure
- `BlockerRow` and transition intent `ResolveTransitionFailure` on prompt runtime failure

HITL capture

- optional capture from audit evidence
- optional capture from promoted active epic

## 12. Behavioral Equivalence Contract

Inputs

- The same `SelectionDecision`, `ProjectContext`, artifacts, stores, prompt contracts, projections, prompt outputs, and cancellation behavior must produce the same externally observable results.
- Active selection freshness must be validated before auditing.
- Realign/Reimagine fallback to `.agents/selection.md` must occur only when `.agents/epic.md` is absent.

Outputs

- The same `EpicPreparationResult` values must be returned for the same successful branch conditions.
- The caller-visible `RoadmapOutcome` must remain unchanged through existing caller logic.

Persisted state

- Every `SaveStateAsync` call must preserve the same current state, transition status, from/to states, prompt names, projection paths, output paths, decisions, blockers, transition intents, next valid transitions, retired epic records, active artifact rows, manifest counts, split family counts, and last decision ids.
- Started, prompt-completed, completed, paused, failed, and cancelled state behavior must remain identical.

Artifacts

- Audit output must be written to the same numbered evidence directory and stem.
- Realign/Reimagine successful output must overwrite `.agents/epic.md`.
- Rejected Realign/Reimagine output must be preserved as numbered blocker evidence and must not overwrite `.agents/epic.md`.
- Generated projections and projection blocker evidence must remain unchanged.

Lifecycle

- Retire must mark `.agents/selection.md` as `Superseded` with the same notes.
- Successful promotion must mark `.agents/epic.md` as `Ready` with the same notes.
- Rejected promotion evidence must be marked `Blocked` with the same reason.

Journals

- Journal event names, correlation behavior, from/to states, prompt names, projection paths, contract keys, input artifact hashes, output paths, elapsed milliseconds semantics, statuses, decisions, reasons, and snapshots must remain equivalent.

Decision records

- The audit disposition decision must be appended before branch routing.
- The retire-specific decision must be appended only on the retire branch.
- Decision ids must continue to come from `DecisionLedgerStore.NextDecisionIdAsync`.

Selection provenance

- Freshness validation must use the same current-cycle reconstruction.
- Retire must supersede active trusted selection provenance with `RetiredEpicStateDrift`.

Reports and console behavior

- Phase messages must be identical.
- Runtime prompt streaming/echo behavior must remain controlled by `ConsoleTurnRenderer`.
- Non-already-persisted exceptions must still be reported by the outer `RunAsync` catch path.

Exceptions

- Missing selection, missing selection projection, stale selection provenance, malformed audit markdown, invalid projections, stale blocked projections, prompt runtime failures, promotion validation failures, and insufficient evidence must preserve current exception or blocked-state behavior.
- `Insufficient Evidence` must continue to throw after audit evidence and audit decision persistence, without writing an additional durable blocker state inside the branch.
- `OperationCanceledException` must continue to bypass prompt-helper failure persistence and be handled by `RunAsync`.

Recovery behavior

- Prompt runtime failures must continue to persist `EvidenceBlocked` with `ResolveTransitionFailure`.
- Promotion rejections must continue to persist `EvidenceBlocked` with `ResolveArtifactPromotionBlocker`.
- Projection failures must continue to write projection blocker evidence and throw.
- Resume behavior must see the same state and artifact/lifecycle/provenance surfaces after extraction.

Non-contract internals

- Local variable names, private method names, and internal formatting of extracted code are not externally observable unless they change artifacts, state, journals, ledger entries, console output, exceptions, or prompt inputs.

## 13. Transition Handler Shape

The ideal linear structure already present in the current execution order is:

```text
Execute()

-> Announce Audit

-> Load And Validate Current Selection

-> Ensure EpicPreparationAudit Projection

-> Build Audit Context

-> Run Audit Prompt Transition

-> Write Audit Evidence

-> Capture HITL Requests From Audit

-> Parse Audit Decision

-> Record Audit Decision

-> Route Disposition

   -> Retire
      -> Build Retired Epic
      -> Upsert Retired Epic
      -> Record Retire Decision
      -> Persist Retire State
      -> Supersede Selection
      -> Return Retired

   -> Insufficient Evidence
      -> Throw Existing Failure

   -> Realign
      -> Run Existing Rewrite/Promotion Flow
      -> Return ActiveEpicReady Or Blocked

   -> Reimagine
      -> Run Existing Rewrite/Promotion Flow
      -> Return ActiveEpicReady Or Blocked
```

The Realign/Reimagine sub-flow is:

```text
RewriteActiveEpic()

-> Announce Rewrite Prompt

-> Resolve Current Epic Or Selection

-> Load Audit Evidence

-> Ensure Rewrite Projection

-> Build Rewrite Context

-> Run Prompt For Promotion

-> Promote Active Epic

   -> Classify
   -> Validate
   -> Write Active Epic Or Preserve Blocker Evidence
   -> Update Lifecycle
   -> Journal
   -> Persist State

-> Return Promotion Result
```

This shape does not require new semantics. It names the execution order already present.

## 14. Readability Improvements

Single-purpose reading path

Today, understanding selected-existing-epic preparation starts in `ContinueAfterSelectionAsync`, moves through `AuditAndPrepareExistingEpicAsync`, then branches through multiple generic helpers. A named handler would give engineers one obvious entry point for the selected-existing-epic workflow.

Explicit selection validation

`ReadCurrentSelectionAsync` currently sounds like a read. In the extracted transition, the step can be named as current selection validation, making the provenance and freshness check visible.

Clear audit lifecycle

The audit lifecycle is currently hidden inside `RunPromptTransitionAsync`: resolve inputs, persist started state, journal start, run prompt, journal completion, persist completed state. Naming these steps reduces the working memory required to understand why state and journal files change before audit evidence is written.

Branch-local reasoning

Retire, Insufficient Evidence, Realign, and Reimagine can be read as explicit routes after the parsed audit decision. Today those branches sit directly below prompt execution and evidence writes.

Retire effects become visible

The retire branch mutates more than state. It also writes a second decision, upserts retired epics, supersedes selection provenance, and changes selection lifecycle. Extraction can keep these effects in one visible retire route.

Promotion effects become visible

Realign/Reimagine currently jump through `RewriteActiveEpicAsync`, `RunPromptForPromotionAsync`, and `PromoteActiveEpicAsync`. The extracted handler can make it clear that these branches run another prompt and then either promote `.agents/epic.md` or persist a durable blocker.

Partial durability is easier to see

`Insufficient Evidence` is allowed by the parser but throws after audit evidence and the audit decision are already durable. A linear handler can make that partial durability explicit and easier to test.

Testing targets become smaller

Focused tests can exercise:

- stale selection rejection before audit prompt execution
- audit prompt output materialization and parse
- retire state/provenance/lifecycle effects
- insufficient-evidence partial durability
- realign promotion success
- reimagine promotion success
- promotion blocked state and evidence preservation

Navigation burden drops

The current minimum path spans orchestration, provenance, projection cache, context builder, prompt runner, parser, retired epic model, promotion service, lifecycle store, journal store, decision ledger, and state store. Extraction does not remove those collaborators, but it can order them in one file according to the actual human workflow.

