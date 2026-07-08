# Roadmap CLI Transition Extraction Details

This file fills the behavioral gaps left by `.agents/plan.md` and the milestone
checklists. It is distilled from the transition audits in `.agents/specs/` and is
intended as the implementation reference for extracting transition handlers
without changing observable Roadmap CLI behavior.

## Meaningful Gaps In The Roadmap

The roadmap is correct at the architectural level, but it does not fully specify:

- the exact prompt envelopes for normal transitions, promotion-candidate
  transitions, milestone materialization, and completion routing
- ordering constraints between state writes, journal records, artifact writes,
  lifecycle writes, provenance writes, parsing, and decision-ledger writes
- prompt-specific input snapshot roles, secondary inputs, and output paths
- parse-failure and post-materialization-failure behavior after partial durable
  writes
- promotion classification, validation, blocked-output decisions, and lifecycle
  notes
- selection freshness recovery and superseding behavior
- completion certification route outputs, invalid-certification blocking, and
  close-route completion-context update side effects
- exact default state-summary preservation performed by `SaveStateAsync`

Use this file to fill those gaps during each extraction phase. Do not treat it
as permission to redesign state names, prompt names, prompt contracts, artifact
paths, journal events, lifecycle states, decision text, or recovery intents.

## Global Equivalence Rules

All extracted handlers must preserve these cross-cutting behaviors.

- Project Context preflight, prompt-contract snapshot emission, startup planning,
  resume planning, unblock planning, status reporting, cancellation persistence,
  terminal selection route persistence, and generic error reporting remain owned
  by `RoadmapStateMachine` unless a later explicit plan changes that.
- Projection resolution must keep current `ProjectionCache` behavior:
  manifest entry upsert happens before validation or stale-projection blocking,
  generated projection content is written after validation and manifest upsert,
  invalid or stale blocked projections write
  `.agents/evidence/blockers/projection-blocked.NNNN.md`, and projection failures
  occur before transition-start state persistence for the affected runtime
  prompt.
- Runtime and projection prompts still run through the read-only planning agent
  and still append the implementation-first prompt policy. Non-completed agent
  turns still fail with diagnostics. Silent output echo behavior remains in the
  prompt runner.
- `OperationCanceledException` must not be converted into durable transition
  failure by prompt helpers. It must propagate to outer `RunAsync`, which writes
  the cancelled state.
- Optional HITL capture is always a no-op when the capture service is absent or
  content is blank.
- Parse failures after artifact materialization must not be retroactively
  converted into prompt transition failures unless the current implementation
  already does so. Several transitions deliberately have completed state and
  evidence before parsing.
- Do not roll back partially materialized artifacts unless the current
  implementation already does. Split output is the main positive no-partial-write
  exception: the whole split bundle is interpreted before child epic files are
  written.
- `SaveStateAsync` state summaries must continue to refresh active artifact rows
  for `.agents/core/roadmap-completion-context.md`, `.agents/selection.md`, and
  `.agents/epic.md`; projection manifest counts; split-family JSON count; last
  decision id; retained retired epics; retained blockers; carried transition
  intent unless explicitly replaced; and default next transitions unless
  explicitly supplied.
- Default next transitions remain:
  - `CoreReady`: `BootstrapRoadmapCompletionContext`,
    `SelectNextStrategicInitiative`
  - `RoadmapCompletionContextReady`: `SelectNextStrategicInitiative`
  - `SelectNextStrategicInitiative`: `SelectNextEpic`
  - `ActiveEpicReady`: `GenerateMilestoneDeepDives`
  - `MilestoneSpecsReady`: none
  - `EpicPreparationAudit`: `EpicPreparationAudit`
  - `RetireEpic`: `SelectNextStrategicInitiative`
  - `EvidenceGathering`: `GatherAdditionalEvidence`,
    `EvaluateEpicCompletionAndDrift`
  - `EvidenceBlocked`: `Resolve blocker and rerun`

## Shared Service Contracts

### RoadmapTransitionPersistence

This service should own the existing `SaveStateAsync` behavior, not just a thin
store write.

Required behavior:

- load existing roadmap state before each save
- load projection manifest and compute valid, stale, and invalid counts
- read active artifact status rows for roadmap completion context, selection,
  and active epic
- read the current last decision id
- preserve existing retired epics unless replacements are provided
- preserve existing blockers unless replacements are provided
- count `.agents/splits/split-family-*.json`
- preserve existing transition intent unless a replacement intent is provided
- compute default next transitions when an explicit list is not supplied
- output formatting must remain exactly as callers provide it, including joined
  comma-separated output lists
- workflow-failure helpers must persist the same current state, status, prompt,
  output path, blocker rows, recovery intent, evidence paths, and next-transition
  text as the current state machine

Do not move transition-specific decisions into this service. Callers should pass
the exact state, status, from/to state, prompt, projection, output, decision,
timestamps, blocker rows, transition intent, and next transitions.

### RoadmapPromptTransitionRunner

The plan names this service but omits the distinct envelopes. Preserve all of
them.

Normal prompt transition envelope:

- used by bootstrap, selection, epic preparation audit, completion evaluation,
  and completion-context update
- resolve transition input snapshot first
- save started state before `TransitionStarted`
- started state uses current state equal to the target state, status `Started`,
  decision `Pending`, and the runtime prompt output path
- append `TransitionStarted`
- run runtime prompt
- append `TransitionCompleted`
- save completed state after the completion journal record
- completed state uses status `Completed` and decision `Completed`
- on non-cancellation runtime failure, append `TransitionFailed`, save
  `EvidenceBlocked` with status `Failed`, intent `ResolveTransitionFailure`,
  next transition `Resolve blocker and rerun`, and throw
  `RoadmapStepException.AlreadyPersisted`

Promotion-candidate prompt envelope:

- used by `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`, and by milestone
  prompt generation before post-processing
- resolve transition input snapshot first
- save started state before `TransitionStarted`
- current state remains the source state while the prompt is running
- started state uses decision `Prompt Started`
- append `TransitionStarted`
- run runtime prompt
- append `PromptCompleted` with parser decision `Output produced`
- save prompt-completed state with status `PromptCompleted` and decision
  `Prompt Completed`
- return `PromptTransitionCompletion` with correlation id, started/completed
  timestamps, elapsed milliseconds, raw output, and the original input snapshot
- on non-cancellation runtime failure, append `TransitionFailed`, save
  `EvidenceBlocked` with status `Failed`, decision `Runtime Failure`, intent
  `ResolveTransitionFailure`, next transition `Resolve blocker and rerun`, and
  throw `RoadmapStepException.AlreadyPersisted`

Milestone materialization must still use the promotion-candidate prompt envelope
for prompt start and prompt completion, but success finalization is not
`ArtifactPromoted`. It appends `MilestoneSpecsMaterialized` and saves
`MilestoneSpecsReady` only after bundle extraction, spec writes, lifecycle/HITL,
execution-preparation provenance, and invariant validation.

### ActiveSelectionReader

This service must do more than read `.agents/selection.md`.

Required sequence:

1. check cancellation
2. read required `.agents/selection.md`
3. read `.agents/projections/select-next-epic.md`
4. if the selection projection is missing or blank, throw
   `RoadmapStepException("Active selection cannot be used because its SelectNextEpic projection is missing.")`
5. load persisted roadmap state
6. rebuild the current `SelectNextEpic` cycle from selection projection, roadmap
   completion context, roadmap source references, retired epics, empty secondary
   input, and transition input hashes
7. evaluate active selection freshness against
   `.agents/selection-provenance-manifest.json` and the current selection hash
8. throw a `RoadmapStepException` with stale reasons when freshness is not fresh
9. return selection content only after freshness is fresh

Consumers include `CreateNewEpic`, `SplitEpic`, `EpicPreparationAudit`, and the
`RealignEpic`/`ReimagineEpic` fallback path when `.agents/epic.md` is absent.

### DecisionRecorder

This service should preserve the current `AppendDecisionAsync` fields and id
allocation.

Required behavior:

- allocate ids through `DecisionLedgerStore.NextDecisionIdAsync`
- append through the existing decision ledger store
- do not add decision entries to `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`,
  `SplitEpic`, or successful milestone generation, because they currently do not
  append their own decision-ledger entries
- keep output path lists exactly as the current call site supplies them

### HitlArtifactCapture

This wrapper must:

- return immediately if the optional capture service is absent
- return immediately if content is blank
- otherwise scan the named artifact content for explicit non-implementation HITL
  request markers and let the existing capture service update its ledger

### ActiveEpicPromotionCoordinator

This coordinator must preserve `PromoteActiveEpicAsync` and
`ArtifactPromotionService` behavior.

Input rules:

- target is always `.agents/epic.md`
- evidence directory is `.agents/evidence/blockers`
- evidence stem is `active-epic-promotion`
- artifact name is `active epic`
- classifier is `EpicAuthoringOutputClassifier`
- validator is `EpicArtifactValidator`
- promoted lifecycle state is `Ready`

Classification rules:

- no top-level markdown heading: `Ambiguous`
- first top-level heading contains `Blocked`: `Blocked`
- first top-level heading matches `# Epic: ...`: `Promotable`
- heading resembles `# Epic` without the required colon form, or content
  contains `## Epic Metadata` while the first heading is wrong: `Malformed`
- otherwise: `Ambiguous`

Validation rules for promotable output:

- reject blank content
- reject anything that reclassifies as non-promotable
- require headings `## Epic Metadata`, `## Desired Capability`,
  `## Acceptance Criteria`, and `## Milestone Roadmap`
- require either `## Strategic Purpose` or `## Strategic Continuity`
- require non-empty `Epic ID` and `Status` in the metadata field table
- require at least one milestone row with columns `Milestone ID`,
  `Milestone Name`, `Purpose`, `Outcome`, `Depends On`, and
  `Completion Signal`
- require non-empty values for `Milestone ID`, `Milestone Name`, `Purpose`,
  `Outcome`, and `Completion Signal`

Promotion success:

- write `.agents/epic.md`
- mark `.agents/epic.md` lifecycle `Ready` with the caller-supplied note
- capture HITL requests from `.agents/epic.md`
- append `ArtifactPromoted` with prompt contract key
  `ArtifactPromotionService`, result `Promoted`, parser decision
  `Active epic promoted`, and the original prompt input snapshot
- save state `ActiveEpicReady` / `Completed`, decision `Artifact Promoted`,
  output `.agents/epic.md`

Promotion rejection:

- write exact rejected output to
  `.agents/evidence/blockers/active-epic-promotion.NNNN.md`
- mark evidence lifecycle `Blocked` with the rejection reason
- append `ArtifactPromotionBlocked`
- save `EvidenceBlocked` / `Paused`
- use transition intent `ResolveArtifactPromotionBlocker`
- use next transition `Resolve blocker and rerun`
- map promotion status to decision text:
  - blocked: `Artifact Promotion Blocked`
  - ambiguous: `Artifact Promotion Ambiguous`
  - structurally invalid: `Artifact Promotion Invalid`
  - other rejected status: `Artifact Promotion Rejected`

### SelectionSuperseder

This service must update both provenance and lifecycle.

Retire branch:

- supersede active trusted selection provenance with
  `RetiredEpicStateDrift`
- mark `.agents/selection.md` lifecycle `Superseded`
- lifecycle note:
  `Retired epic state changed after EpicPreparationAudit.`

Completion-context update:

- supersede active trusted selection provenance with
  `RoadmapCompletionContextDrift`
- mark `.agents/selection.md` lifecycle `Superseded`
- lifecycle note:
  `Roadmap completion context changed after completion certification.`

## Prompt And Projection Matrix

### CreateRoadmapCompletionContext

- phase: `Bootstrap roadmap completion context`
- projection prompt: `ProjectionForCreateRoadmapCompletionContext`
- projection path: `.agents/projections/roadmap-completion.md`
- from/to: `CoreReady -> RoadmapCompletionContextReady`
- output: `.agents/core/roadmap-completion-context.md`
- context: `# Roadmap Completion Bootstrap`, then projection content
- secondary input: rendered completed epic archive evidence
- optional inputs: `.agents/archive/epics/*.md`
- envelope: normal prompt transition

Completed epic archive rendering must remain identical: list
`.agents/archive/epics/*.md` in ordinal path order, skip files that disappear
before read, extract first `# ` title, extract `Epic ID` from field table,
extract known evidence sections, fall back to normalized full content, assign
evidence quality, apply per-epic and total truncation budgets, and render the
fixed no-archive message when no completed epic markdown files exist.

Success order:

1. ensure projection
2. render bootstrap context and completed-epic evidence
3. run normal prompt envelope
4. write prompt output verbatim to `.agents/core/roadmap-completion-context.md`
5. capture HITL requests from the context artifact
6. mark context lifecycle `Ready`
7. return to caller, which proceeds to selection

Runtime failure persists `EvidenceBlocked` / `Failed` with output
`.agents/core/roadmap-completion-context.md` and intent
`ResolveTransitionFailure`. Projection failures occur before transition-start
state.

### SelectNextEpic

- phase: `Select next strategic initiative`
- projection prompt: `ProjectionForSelectNextEpic`
- projection path: `.agents/projections/select-next-epic.md`
- from/to: `RoadmapCompletionContextReady -> SelectNextStrategicInitiative`
- output: `.agents/selection.md`
- secondary input: empty string
- envelope: normal prompt transition

Selection context section order:

1. `Projection Content`
2. `Current Roadmap Completion Context`
3. `Roadmap Source References`
4. `Retired Epics`

Required inputs:

- projection path
- `.agents/core/roadmap-completion-context.md`
- every non-empty `.agents/roadmap/*.md`

Success order:

1. ensure projection
2. load existing state for retired epics
3. build selection context
4. run normal prompt envelope
5. write `.agents/selection.md`
6. capture HITL requests
7. write `.agents/evidence/selection/selection.NNNN.md`
8. record active selection provenance
9. mark `.agents/selection.md` lifecycle `Ready` with evidence path as notes
10. parse selection
11. append decision-ledger entry
12. return `SelectionDecision`

Decision ledger entry:

- state: `SelectNextStrategicInitiative`
- transition: `SelectNextEpic`
- prompt: `SelectNextEpic`
- projection: `.agents/projections/select-next-epic.md`
- input artifacts: empty list
- output artifacts: `.agents/selection.md`
- decision: parsed recommended outcome
- confidence: parsed confidence
- rationale: parsed primary reason

Parse failure happens after selection artifact, numbered evidence, provenance,
and lifecycle are written, and before decision-ledger append. It must not become
`TransitionFailed`.

### CreateNewEpic

- phase: `Create new epic`
- projection prompt: `ProjectionForCreateNewEpic`
- projection path: `.agents/projections/create-new-epic.md`
- source/target: `NewEpicProposed -> ActiveEpicReady`
- output: `.agents/epic.md`
- secondary input: active selection content
- envelope: promotion-candidate prompt transition

Required precondition:

- read and validate fresh active selection through `ActiveSelectionReader`

Runtime context is built through `BuildCreateOrSplitContext` with projection
content, selection proposal, and repository inspection instructions. Reject raw
Project Context markers.

Success and blocked behavior:

- after `PromptCompleted`, promote through `ActiveEpicPromotionCoordinator`
- success saves `ActiveEpicReady` / `Completed`, decision `Artifact Promoted`,
  lifecycle note `Promoted by CreateNewEpic.`
- rejection writes `active-epic-promotion.NNNN.md`, saves
  `EvidenceBlocked` / `Paused`, and returns not promoted
- prompt runtime failure saves `EvidenceBlocked` / `Failed`, decision
  `Runtime Failure`, intent `ResolveTransitionFailure`
- no decision-ledger entry is appended by this transition
- caller continues to milestone generation only when `Promoted == true`

### EpicPreparationAudit

- phase: `Audit selected epic`
- projection prompt: `ProjectionForEpicPreparationAudit`
- projection path: `.agents/projections/epic-preparation-audit.md`
- from/to: `ExistingEpicSelected -> EpicPreparationAudit`
- output during prompt envelope: `.agents/evidence/audits`
- numbered evidence stem: `epic-preparation-audit`
- secondary input: active selection content
- envelope: normal prompt transition

Required precondition:

- read and validate fresh active selection through `ActiveSelectionReader`

Audit context section order:

1. `Projection Content`
2. `Selected Epic`
3. `Repository Inspection Instructions`

Success order before branching:

1. ensure projection
2. build audit context
3. run normal prompt envelope
4. write `.agents/evidence/audits/epic-preparation-audit.NNNN.md`
5. capture HITL requests from audit evidence
6. parse `EpicPreparationAuditDecision`
7. append audit decision-ledger entry
8. route parsed disposition

Parser requirements:

- `## Audit Disposition` field table
- `## Selected Epic` field table
- selected epic fields: `Epic ID`, `Epic Name`
- disposition fields: `Disposition`, `Confidence`, `Primary Reason`,
  `Evidence Strength`, `Recommended Next Step`
- disposition must be one of `Realign`, `Reimagine`, `Retire`,
  `Insufficient Evidence`
- recommended next step must be one of `Realign Epic`, `Reimagine Epic`,
  `Retire Epic`, `Gather More Evidence`

Audit decision ledger entry:

- state, transition, prompt: `EpicPreparationAudit`
- projection: `.agents/projections/epic-preparation-audit.md`
- output: numbered audit evidence path
- decision: parsed disposition
- confidence: parsed confidence
- rationale: parsed recommended next step

Retire branch:

- load persisted state
- build `RetiredEpic` from selection and audit
- identity is first known audit `Epic ID`, then selection existing epic id
- name is first known audit `Epic Name`, then selection existing epic name, then
  selection recommended initiative
- reason is first known audit primary reason, then selection primary reason
- throw if no stable identity can be built
- upsert retired epics by stable identity
- append second decision-ledger entry at state `RetireEpic`, decision
  `Retired Epic`, output audit evidence path
- save state `RetireEpic` / `Completed`, from `EpicPreparationAudit` to
  `RetireEpic`, prompt `EpicPreparationAudit`, output audit evidence path,
  decision `Retired Epic`, with replacement retired epics
- supersede active selection provenance and lifecycle using retire notes
- return `EpicPreparationResult.Retired`

Insufficient Evidence branch:

- throw `RoadmapStepException("Epic preparation audit requires more evidence.")`
- audit transition, evidence, HITL capture, and audit decision are already
  durable
- do not write a durable blocker inside this branch
- outer `RunAsync` reports an ephemeral blocker and returns failed

Realign/Reimagine branches:

- delegate to `ActiveEpicRewriteTransition`
- return `EpicPreparationResult.ActiveEpicReady` when promoted
- return `EpicPreparationResult.Blocked` when not promoted

### ActiveEpicRewriteTransition

This handler supports `RealignEpic` and `ReimagineEpic` by configuration.

Allowed pairs:

- prompt `RealignEpic`, source state `RealignEpic`, projection
  `.agents/projections/realign-epic.md`, projection prompt
  `ProjectionForRealignEpic`, lifecycle note `Promoted by RealignEpic.`
- prompt `ReimagineEpic`, source state `ReimagineEpic`, projection
  `.agents/projections/reimagine-epic.md`, projection prompt
  `ProjectionForReimagineEpic`, lifecycle note `Promoted by ReimagineEpic.`

Common behavior:

- phase text equals the prompt name exactly
- prefer `.agents/epic.md` as current epic input
- if `.agents/epic.md` is absent, fall back to fresh active selection through
  `ActiveSelectionReader`
- require audit evidence at the supplied audit path
- context section order is `Projection Content`, `Current Epic`,
  `Audit Output`, `Repository Inspection Instructions`
- audit evidence is both in the context and passed as secondary input
- transition input roles are projection, active epic or selection, and audit
  evidence
- prompt envelope is promotion-candidate
- promotion target is `ActiveEpicReady`

Runtime failure:

- save `EvidenceBlocked` / `Failed`
- output path `.agents/epic.md`
- decision `Runtime Failure`
- intent `ResolveTransitionFailure`
- throw already-persisted failure

Promotion success:

- overwrite `.agents/epic.md` only after classification and validation pass
- lifecycle `Ready` with note `Promoted by {prompt}.`
- append `ArtifactPromoted`
- save `ActiveEpicReady` / `Completed`, decision `Artifact Promoted`
- no decision-ledger entry from the rewrite transition itself

Promotion rejection:

- preserve existing `.agents/epic.md`
- write `.agents/evidence/blockers/active-epic-promotion.NNNN.md`
- append `ArtifactPromotionBlocked`
- save `EvidenceBlocked` / `Paused`
- intent `ResolveArtifactPromotionBlocker`
- return not promoted

### SplitEpic

- phase: `Split epic`
- projection prompt: `ProjectionForSplitEpic`
- projection path: `.agents/projections/split-epic.md`
- from/to for prompt: `SplitEpicProposed -> SplitChildSelection`
- prompt output path in state/journal: `.agents/splits`
- secondary input: active selection content
- envelope: normal prompt transition

Required precondition:

- read and validate fresh active selection through `ActiveSelectionReader`

Runtime context is `BuildCreateOrSplitContext` with projection content,
selection proposal, and repository inspection instructions.

Bundle extraction and interpretation:

- use `BundleExtractionPolicy.RepositorySafe`
- parse `# FILE:` markers
- normalize separators
- reject rooted paths and parent traversal
- reject duplicate targets by throwing `RoadmapStepException`, then convert that
  exception to invalid split interpretation
- trim only leading and trailing separator noise around file bodies
- hash each extracted file body
- interpreter requires paths matching `.agents/epic-N.md`
- child content classification and validation use the same epic classifier and
  validator as active epic promotion
- any rejected file rejects the whole bundle
- no child files are written unless the whole interpreted bundle is valid
- selected child remains the first valid child by numeric order, then path

Valid split success order:

1. write all validated child epic files
2. write `.agents/bundle-manifest.md` with source prompt `SplitEpic`,
   projection path, expected file count, validation result `Valid`, and sorted
   file hashes
3. mark each child lifecycle `Draft` with note `Validated split child epic.`
4. capture HITL requests for each child
5. build split family with id `Guid.NewGuid().ToString("N")[..8]`
6. write `.agents/splits/split-family-{id}.json`
7. replace the prompt completion output with selected child content while
   preserving prompt correlation id, timing, and input snapshot
8. promote selected child as `.agents/epic.md` with lifecycle note
   `Promoted split child {selectedChild.Path} by SplitEpic.`
9. return promotion result

Split-output blocker:

- invalid or blocked split output writes no child files and no split family
- previous `.agents/epic.md` remains unchanged
- evidence stem: `split-epic-output`
- evidence content includes reason, rejected files, and raw prompt output
- lifecycle for evidence path: `Blocked`
- journal event: `SplitBundleRejected`
- blocked interpretation decision: `Split Epic Blocked`
- other invalid interpretation decision: `Split Bundle Rejected`
- state: `EvidenceBlocked` / `Paused`
- intent: `ResolveSplitEpicBlocker`
- next transition: `Resolve blocker and rerun`
- returned result is `ArtifactPromotionResult.NotPromoted(...)`

Runtime prompt failure:

- state `EvidenceBlocked` / `Failed`
- output `.agents/splits`
- decision `Failed`
- intent `ResolveTransitionFailure`
- throw already-persisted failure

Selected-child promotion rejection:

- child files and split family may already exist
- promotion rejection uses active epic promotion blocker behavior
- intent `ResolveArtifactPromotionBlocker`
- caller pauses

### GenerateMilestoneDeepDivesForEpic

- phase: `Generate milestone deep dives`
- projection prompt: `ProjectionForGenerateMilestoneDeepDivesForEpic`
- projection path: `.agents/projections/milestone-deep-dive.md`
- from/to: `ActiveEpicReady -> MilestoneSpecsReady`
- prompt output path before materialization: `.agents/specs`
- required input: `.agents/epic.md`
- secondary input: empty string
- prompt envelope: promotion-candidate prompt envelope, followed by custom
  materialization finalization

Milestone context section order:

1. `Projection Content`
2. `Active Epic`

Success order:

1. ensure projection
2. build milestone context
3. run promotion-candidate prompt envelope
4. extract `# FILE:` bundle
5. reject blocked or zero-file bundle
6. write extracted files
7. write `.agents/specs/bundle-manifest.md`
8. mark each extracted spec lifecycle `Ready`
9. capture HITL requests for each spec
10. record execution-preparation provenance
11. run invariant validation for `MilestoneSpecsReady`
12. append `MilestoneSpecsMaterialized`
13. save final `MilestoneSpecsReady` / `Completed`, decision
    `Milestone Specs Ready`, output `.agents/specs`, replacement blockers `[]`,
    and empty transition intent
14. caller returns `RoadmapOutcome.Paused`

Important success details:

- successful prompt path uses `TransitionStarted` and `PromptCompleted`, not
  `TransitionCompleted`
- raw prompt output is not stored directly on success
- execution-preparation generator id is
  `GenerateMilestoneDeepDivesForEpic:v1`
- final state has no next valid transitions
- no decision-ledger entry is appended
- no operational context, execution prompt, or execution plan is generated here

Bundle or post-processing failure after prompt completion:

- prompt-completed journal and state already exist
- write `.agents/evidence/blockers/milestone-spec-generation-failed.NNNN.md`
  with raw prompt output embedded in the failure evidence
- append `MilestoneSpecGenerationFailed`
- save `EvidenceBlocked` / `Paused`
- decision `Milestone Spec Generation Failed`
- intent `ResolveMilestoneSpecGenerationFailure`
- throw already-persisted failure
- do not roll back files, manifest, lifecycle, or provenance already written
  before the failure

Invariant failure:

- specs, bundle manifest, lifecycle, and execution-preparation manifest may
  already exist
- use validator-owned evidence path when available, otherwise fallback evidence
- append `InvariantFailed`
- save using prompt `PostMilestoneInvariantValidation`
- intent `ResolveInvariantViolation`
- throw already-persisted failure
- do not append `MilestoneSpecsMaterialized`

Runtime prompt failure:

- append `TransitionFailed`
- save `EvidenceBlocked` / `Failed`
- decision `Runtime Failure`
- output `.agents/specs`
- intent `ResolveTransitionFailure`

### CompletionCertificationTransition

Live entry:

- resume planner sees persisted `EpicCompletionDetected`
- caller recovers execution evidence from transition intent evidence paths and
  output paths, filtering to `.agents/evidence/execution`
- first present candidate is used
- if none exists, throw
  `RoadmapStepException("Cannot resume completion certification because execution evidence is missing.")`
- live resume calls completion certification with `persistCompletionClaim: false`

Prompt:

- runtime prompt: `EvaluateEpicCompletionAndDrift`
- phase: `Evaluate epic completion and drift`
- projection prompt: `ProjectionForEvaluateEpicCompletionAndDrift`
- projection path: `.agents/projections/epic-completion-evaluation.md`
- from/to: `EpicCompletionDetected -> CompletionEvaluationAndContextUpdate`
- output during prompt envelope: `.agents/evidence/evaluations`
- numbered evidence stem: `epic-completion-and-drift`
- secondary input: empty string
- envelope: normal prompt transition

Optional non-implementation review gate:

- runs before evaluation phase and projection ensure
- if blocked, write
  `.agents/evidence/blockers/non-implementation-completion-review-blocked.NNNN.md`
- output list is review evidence paths plus blocker path
- save `EvidenceBlocked` / `Paused`
- from `EpicCompletionDetected` to `EvidenceBlocked`
- prompt `NonImplementationCompletionReview`
- projection `None`
- decision `Pending non-implementation HITL review`
- intent `ResolveNonImplementationCompletionReview`
- next transition text points at the non-implementation decisions file
- return `RoadmapOutcome.Paused`

Completion evaluation context inputs:

- projection content
- `.agents/epic.md`
- execution evidence path
- fresh milestone spec paths from execution-preparation provenance
- repository inspection instructions
- optional non-implementation review evidence sections when present

Required transition input roles:

- projection
- active epic
- execution evidence
- fresh milestone specs

Evaluation success order:

1. run optional review gate
2. ensure evaluation projection
3. build evaluation context
4. run normal prompt envelope
5. write `.agents/evidence/evaluations/epic-completion-and-drift.NNNN.md`
6. capture HITL requests from evaluation evidence
7. parse completion evaluation
8. validate with completion certification policy
9. append evaluation decision-ledger entry
10. if invalid, persist invalid-certification blocker and return paused
11. route valid certification
12. run close-route side effects when required
13. update active epic lifecycle according to route
14. persist final completion route
15. return mapped `RoadmapOutcome`

Parser and policy behavior:

- parser reads `## Evaluation Summary`
- parser requires `Overall Completion Status`,
  `Overall Drift Classification`, and `Closure Recommendation`
- parse failure happens after evaluation evidence and HITL capture and is not
  converted into invalid-certification blocker state
- policy failure after a successful parse is converted into durable invalid
  certification blocker state

Evaluation decision ledger entry:

- state: `CompletionEvaluationAndContextUpdate`
- transition: `EvaluateEpicCompletionAndDrift`
- projection: `.agents/projections/epic-completion-evaluation.md`
- output: evaluation evidence path
- decision: parsed closure recommendation
- confidence: `Unclear`
- rationale: parsed overall completion status

Invalid certification:

- evidence stem: `invalid-completion-certification`
- required next step:
  `Review {evaluationPath}, preserve the certification evidence, correct the certification decision, and rerun the roadmap CLI.`
- input snapshot prompt: `CompletionCertificationRouting`
- journal event: `CompletionCertificationRejected`
- prompt contract key: `CompletionCertificationPolicy`
- outputs: evaluation path and blocker path
- state: `EvidenceBlocked` / `Paused`
- from/to: `CompletionEvaluationAndContextUpdate -> EvidenceBlocked`
- prompt: `CompletionCertificationRouting`
- decision: `Invalid Completion Certification`
- intent: `ResolveInvalidCompletionCertification`
- next transition: `Resolve invalid completion certification and rerun`
- return `RoadmapOutcome.Paused`

Valid route mapping:

- `Close Epic` and `Close With Follow-Up`:
  - target `SelectNextStrategicInitiative`
  - status `Completed`
  - CLI outcome `Completed`
  - active epic lifecycle `Completed`
  - next transition `SelectNextEpic`
  - requires roadmap completion context update
- `Continue Epic`:
  - target `ExecutionLoop`
  - status `Paused`
  - CLI outcome `Paused`
  - active epic lifecycle `Executing`
  - next transition `ContinueExecution`
- `Reopen Epic`:
  - target `EpicPreparationAudit`
  - status `Paused`
  - CLI outcome `Paused`
  - active epic lifecycle `Ready`
  - next transition `EpicPreparationAudit`
- `Gather More Evidence`:
  - target `EvidenceGathering`
  - status `Paused`
  - CLI outcome `Paused`
  - active epic lifecycle `Ready`
  - next transitions `GatherAdditionalEvidence`,
    `EvaluateEpicCompletionAndDrift`

Final route persistence:

- routing input snapshot uses prompt `CompletionCertificationRouting` and the
  evaluation evidence path as required completion-evaluation input
- append `TransitionCompleted` from
  `CompletionEvaluationAndContextUpdate` to route target state
- prompt: `CompletionCertificationRouting`
- prompt contract key: `CompletionCertificationRouter`
- save route target state, route transition status, route outputs, route
  decision, route transition intent, and route next transitions
- final route persistence occurs after lifecycle and close-route update effects

### RoadmapCompletionContextUpdateTransition

This helper runs only from close routes.

Prerequisite close-route effects:

- archive completed execution workspace first
- synthesize completed epic
- archive directory is `.agents/archive/epics/{index}`
- synthesis path is `.agents/archive/epics/{index}.md`
- archive index is existing archive directory count plus one
- archive service reports phases `Archive completed execution workspace` and
  `Synthesize completed epic`

Update prompt:

- phase: `Update roadmap completion context`
- runtime prompt: `UpdateRoadmapCompletionContext`
- projection prompt: `ProjectionForUpdateRoadmapCompletionContext`
- projection path: `.agents/projections/roadmap-completion-update.md`
- from/to: `CompletionEvaluationAndContextUpdate -> SelectNextStrategicInitiative`
- output: `.agents/core/roadmap-completion-context.md`
- secondary input: completed-epic synthesis content
- input context: completion evaluation evidence path
- envelope: normal prompt transition

Update context inputs:

- projection content
- current `.agents/core/roadmap-completion-context.md`
- completed-epic synthesis content and path
- latest completion evaluation evidence
- repository inspection instructions
- optional non-implementation review evidence sections when present

Update success order:

1. ensure projection
2. build completion-update context
3. run normal prompt envelope
4. write `.agents/core/roadmap-completion-context.md`
5. capture HITL requests from rewritten completion context
6. write `.agents/evidence/evaluations/roadmap-completion-update.NNNN.md`
7. supersede active selection with `RoadmapCompletionContextDrift`
8. append decision `Roadmap Completion Context Updated`

Decision ledger entry:

- state: `CompletionEvaluationAndContextUpdate`
- transition/prompt: `UpdateRoadmapCompletionContext`
- projection: `.agents/projections/roadmap-completion-update.md`
- output: `.agents/core/roadmap-completion-context.md`
- decision: `Roadmap Completion Context Updated`
- confidence: `Unclear`
- rationale: `Completion context updated after certification.`

Final close-route output list includes evaluation path,
`.agents/core/roadmap-completion-context.md`, and completed-epic synthesis path.
It does not include numbered `roadmap-completion-update.NNNN.md` evidence.

Archive or synthesis failures are not converted into invalid-certification
blockers by completion certification.

## Transition Input Snapshot Summary

Preserve these prompt-specific input roles and secondary inputs.

- `CreateRoadmapCompletionContext`: required projection; optional completed epic
  archives; prompt context hash; secondary hash of rendered archive evidence
- `SelectNextEpic`: required projection, roadmap completion context, roadmap
  source files; secondary hash of empty string
- `EpicPreparationAudit`: required projection and selection; secondary hash of
  selection content
- `CreateNewEpic`: required projection and selection; secondary hash of
  selection content
- `RealignEpic` / `ReimagineEpic`: required projection; required active epic if
  `.agents/epic.md` exists, otherwise required selection; required audit
  evidence path; secondary hash of audit evidence content
- `SplitEpic`: required projection and selection; secondary hash of selection
  content
- `GenerateMilestoneDeepDivesForEpic`: required projection and active epic;
  secondary hash of empty string
- `EvaluateEpicCompletionAndDrift`: required projection, active epic, execution
  evidence path, and fresh milestone specs; secondary hash of empty string
- `UpdateRoadmapCompletionContext`: required projection, roadmap completion
  context, active epic, and completion evaluation evidence; secondary hash of
  completed-epic synthesis content
- `CompletionCertificationRouting`: completion evaluation evidence path is the
  required input used to anchor route and invalid-certification journal records

Input snapshots must keep path ordering, role joining, required/present flags,
SHA-256 hashes for present inputs, projection identity, prompt context hash,
secondary input hash, and overall snapshot hash identical.

## Handler Extraction Boundaries

Use these boundaries when wiring handlers.

- `BootstrapRoadmapCompletionContextTransition.ExecuteAsync(ProjectContext, CancellationToken)`
  starts after the caller observes missing or empty completion context and ends
  after context artifact write, HITL capture, and lifecycle `Ready`.
- `SelectNextEpicTransition.ExecuteAsync(ProjectContext, CancellationToken)`
  returns `SelectionDecision`; downstream selection routing remains outside.
- `CreateNewEpicTransition.ExecuteAsync(ProjectContext, CancellationToken)`
  returns `ArtifactPromotionResult`; caller controls milestone continuation.
- `EpicPreparationAuditTransition.ExecuteAsync(SelectionDecision, ProjectContext, CancellationToken)`
  returns `EpicPreparationResult`; selected-existing-epic routing after the
  result remains outside.
- `ActiveEpicRewriteTransition.ExecuteAsync(prompt, sourceState, ProjectContext, auditPath, CancellationToken)`
  returns `ArtifactPromotionResult`; it does not parse audit decisions or append
  audit decision records.
- `SplitEpicTransition.ExecuteAsync(ProjectContext, CancellationToken)` returns
  `ArtifactPromotionResult`; caller controls milestone continuation.
- `GenerateMilestoneDeepDivesTransition.ExecuteAsync(ProjectContext, CancellationToken)`
  returns after final `MilestoneSpecsReady` state is persisted; caller returns
  paused.
- `CompletionCertificationTransition.ExecuteAsync(ProjectContext, DateTimeOffset, string, CancellationToken, bool, ExecutionDispositionRoute?)`
  returns `RoadmapOutcome`; startup/resume selection of that call remains
  outside.
- `RoadmapCompletionContextUpdateTransition` is a helper called only by
  completion close routes and should not become an independent CLI route.

## Characterization Priorities From Gaps

Before or during extraction, coverage should pin these high-risk details:

- projection validation or stale-projection failure writes projection blocker
  evidence before any prompt-start state for that prompt
- bootstrap runtime failure output path remains
  `.agents/core/roadmap-completion-context.md`
- selection parse failure happens after `.agents/selection.md`, numbered
  evidence, provenance, and lifecycle are written, and before decision-ledger
  append
- stale active selection is rejected before create, split, or audit prompts run
- `Insufficient Evidence` audit output persists audit evidence and audit
  decision before throwing, with no durable blocker branch state
- `RealignEpic` and `ReimagineEpic` use current selection fallback only when
  `.agents/epic.md` is absent
- promotion prompt completion is not artifact completion
- invalid split bundle writes no child files and no split family
- split promotion uses selected child content and reuses original prompt
  correlation id, timing, and input snapshot
- milestone success uses `PromptCompleted` plus `MilestoneSpecsMaterialized`, not
  `TransitionCompleted`
- milestone post-prompt failure writes
  `milestone-spec-generation-failed.NNNN.md` and does not roll back already
  written artifacts
- completion evaluation parse failure occurs after evaluation evidence is
  written and is not an invalid-certification blocker
- invalid parsed certification writes `CompletionCertificationRejected` and
  intent `ResolveInvalidCompletionCertification`
- close-route completion-context update supersedes active selection after the
  context rewrite and excludes numbered update evidence from final route outputs
- archive and synthesis failures are not converted into invalid-certification
  blockers
