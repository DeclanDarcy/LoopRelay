# CommandCenter.Roadmap.CLI Implementation Plan

> For agentic workers: implement this plan task-by-task. Use checkbox (`- [ ]`) tracking and keep changes scoped to the files named in each task.

## Goal

Create a standalone .NET console app, `CommandCenter.Roadmap.CLI`, that executes the projection-based engineering roadmap state machine as a file-backed workflow.

The CLI must:

- Validate the fixed Core North-Star source files before doing any other work.
- Create task-specific projections only when their deterministic projection file is missing.
- Assemble every runtime prompt from a cached projection plus only the artifacts required for that transition.
- Never pass raw Core North-Star content directly to runtime prompts.
- Persist `.agents/state.md`, a transition journal, and a decision ledger around every major transition.
- Maintain projection manifests with Core hashes, prompt provenance, projection hashes, stale detection, and validation results.
- Validate projection structure, prompt contracts, global invariants, and artifact lifecycle metadata before trusting generated artifacts.
- Drive roadmap-completion bootstrap, next-initiative selection, epic preparation, epic creation/transformation/splitting, milestone deep-dive generation, execution bridging, completion certification, and roadmap-completion-context update.

## Architectural Position

`CommandCenter.Roadmap.CLI` is a new console app, not a replacement for `CommandCenter.Plan.CLI` or `CommandCenter.CLI`.

It reuses existing low-level building blocks:

- `CommandCenter.Agents`: `IAgentRuntime`, `AgentSessionSpec`, streaming turn rendering.
- `CommandCenter.Core`: source-generated prompt classes from `.prompt` files.
- `CommandCenter.Core.Artifacts`: `IArtifactStore`, `FileSystemArtifactStore`, `ArtifactPath`.
- `CommandCenter.Orchestration`: existing artifact constants where they match current paths.

It owns roadmap-state-machine concerns:

- North-star context loading and validation.
- Projection registry and projection cache.
- Projection manifest and projection integrity validation.
- Prompt contract registry.
- Runtime prompt context assembly.
- State transition parsing.
- State document persistence.
- Transition journal and decision ledger persistence.
- Global invariant validation.
- Artifact lifecycle metadata.
- Roadmap-specific artifact paths.
- Bundle extraction for multi-file prompt outputs.

## Global Constraints

- Target framework: `net10.0`.
- Enable nullable and implicit usings in every new project.
- Set `<UseExecutionContextAlias>false</UseExecutionContextAlias>` in the CLI and test csproj because the CLI does not reference `CommandCenter.Execution`.
- Do not use `Microsoft.Extensions.Hosting`; use an explicit `Program.cs` composition like the existing CLIs.
- Use `Microsoft.Extensions.DependencyInjection` for composition.
- Run Codex turns through `IAgentRuntime`.
- Use read-only planning turns for projection/runtime prompt generation. The CLI writes artifacts after successful turns; agents should not mutate the repo directly during planning transitions.
- Thread one `CancellationTokenSource` from `Console.CancelKeyPress` through every async operation.
- Write transition-started state before every long-running Codex or execution-bridge operation, then write transition-completed state after output validation succeeds.
- After any failed or cancelled transition, write `.agents/state.md`, transition journal failure details, and a canonical blocked artifact when possible.
- Resolve all repository-relative paths through `ArtifactPath.ResolveRepositoryPath(repository, relativePath)`.
- Treat existing user changes as authoritative. Do not revert unrelated files.

## Fixed North-Star Source Context

At the very start of the state machine, before any projection lookup or agent call, validate these eight files exist under `.agents/north-star/`:

```text
.agents/north-star/01-purpose.md
.agents/north-star/02-capability-model.md
.agents/north-star/03-invariants.md
.agents/north-star/04-strategic-structure.md
.agents/north-star/05-authority-model.md
.agents/north-star/06-evaluation-model.md
.agents/north-star/07-drift-and-false-success.md
.agents/north-star/08-vocabulary.md
```

The `northStarContext` injected into projection prompts is one string formed by concatenating exactly those files in the order above.

Use this format for each file in the concatenation:

```markdown
<!-- BEGIN NORTH-STAR FILE: 01-purpose.md -->

{file content}

<!-- END NORTH-STAR FILE: 01-purpose.md -->
```

Important boundary:

- These eight files are the only Core North-Star source context files.
- `.agents/north-star/roadmap-completion-context.md` is a runtime strategic-state artifact and must never be included in `northStarContext`.
- Projection files must be written under `.agents/projections/`, not under `.agents/north-star/projections/`.
- Runtime prompts receive cached projection content, never the raw eight-file Core concatenation.

## Artifact Paths

Add a roadmap artifact path catalog instead of scattering string literals.

```text
.agents/state.md
.agents/decision-ledger.md
.agents/artifacts/lifecycle.md
.agents/roadmap.md
.agents/roadmap/*.md
.agents/selection.md
.agents/epic.md
.agents/epic-*.md
.agents/specs/*.md
.agents/operational_context.md
.agents/execution-prompt.md
.agents/north-star/roadmap-completion-context.md
.agents/projections/manifest.md
.agents/projections/roadmap-completion.md
.agents/projections/roadmap-completion-update.md
.agents/projections/select-next-epic.md
.agents/projections/epic-preparation-audit.md
.agents/projections/realign-epic.md
.agents/projections/reimagine-epic.md
.agents/projections/create-new-epic.md
.agents/projections/split-epic.md
.agents/projections/milestone-deep-dive.md
.agents/projections/epic-completion-evaluation.md
.agents/contracts/prompt-contracts.md
.agents/journal/transitions.jsonl
.agents/splits/split-family-*.md
.agents/evidence/selection/*.md
.agents/evidence/audits/*.md
.agents/evidence/evaluations/*.md
.agents/evidence/blockers/*.md
.agents/evidence/orchestration/*.md
```

Roadmap input can be either `.agents/roadmap.md` or `.agents/roadmap/*.md`. If both exist, concatenate `.agents/roadmap.md` first and then `.agents/roadmap/*.md` sorted by repository-relative path.

Separate evidence categories:

- Domain evidence: selections, audits, split bundles, completion evaluations, and roadmap-completion updates.
- Orchestration evidence: transition timing, prompt/projection provenance, retries, failures, stale projection warnings, invariant failures, and blocked-transition records.

Canonical blocked artifacts must use this shape:

```markdown
# Roadmap Transition Blocked

| Field | Value |
|---|---|
| State | {{state}} |
| Transition | {{transition_name}} |
| Reason | {{reason}} |
| Required Next Step | {{next_step}} |
| Evidence Path | {{evidence_path_or_none}} |
| Created At | {{utc_timestamp}} |

## Details

{{details}}
```

## Projection Registry

Create a registry that maps each runtime prompt to one cached projection path and one projection creation prompt.

| Runtime prompt | Projection prompt file | Cached path |
|---|---|---|
| `CreateRoadmapCompletionContext` | `ProjectionForCreateRoadmapCompletionContext` | `.agents/projections/roadmap-completion.md` |
| `UpdateRoadmapCompletionContext` | `ProjectionForUpdateRoadmapCompletionContext` | `.agents/projections/roadmap-completion-update.md` |
| `SelectNextEpic` | `ProjectionForSelectNextEpic` | `.agents/projections/select-next-epic.md` |
| `EpicPreparationAudit` | `ProjectionForEpicPreparationAudit` | `.agents/projections/epic-preparation-audit.md` |
| `RealignEpic` | `ProjectionForRealignEpic` | `.agents/projections/realign-epic.md` |
| `ReimagineEpic` | `ProjectionForReimagineEpic` | `.agents/projections/reimagine-epic.md` |
| `CreateNewEpic` | `ProjectionForCreateNewEpic` | `.agents/projections/create-new-epic.md` |
| `SplitEpic` | `ProjectionForSplitEpic` | `.agents/projections/split-epic.md` |
| `GenerateMilestoneDeepDivesForEpic` | `ProjectionForGenerateMilestoneDeepDivesForEpic` | `.agents/projections/milestone-deep-dive.md` |
| `EvaluateEpicCompletionAndDrift` | `ProjectionForEvaluateEpicCompletionAndDrift` | `.agents/projections/epic-completion-evaluation.md` |

Projection cache rule:

- If the cached file exists and is non-whitespace, read it and do not call Codex.
- If it does not exist, render the projection prompt with `projectContext = northStarContext`, run a read-only one-shot, require `AgentTurnState.Completed`, and write the output to the cached path.
- Do not regenerate projections automatically when the Core files change. For MVP, deleting a projection file is the refresh mechanism.
- Even when reusing a cached projection, compare its manifest `northStarHash` to the current eight-file Core hash. If hashes differ, mark the projection `Stale` in `.agents/projections/manifest.md`, emit an orchestration warning, and continue only when the prompt contract allows stale projections for that transition. Default behavior is to pause in `EvidenceBlocked` with "Projection refresh recommended" rather than silently proceeding.

Projection manifest rule:

- Maintain `.agents/projections/manifest.md` as the authoritative cache index.
- Each projection entry records:
  - runtime prompt name
  - projection prompt name
  - projection path
  - projection prompt source hash, when available from generated prompt provenance
  - ordered north-star file list
  - `northStarHash`
  - projection content hash
  - generated timestamp
  - validation status
  - stale status
  - last validation error, if any
- The manifest is orchestration metadata; runtime prompts do not consume it unless a prompt contract explicitly requires provenance.

Projection validation rule:

- Before a new or cached projection can be used, run `ProjectionValidator`.
- Validation checks:
  - required top-level title and required sections for that projection type
  - intended consumer matches the runtime prompt
  - no forbidden runtime state sections are present, such as current roadmap completion state, selected epic content, completed epic history, or codebase facts
  - canonical vocabulary section exists when required by the projection prompt
  - downstream-use instructions exist
  - projection integrity checklist exists
- Validation failure writes a blocked artifact and enters `EvidenceBlocked`; do not cache invalid newly generated projections as valid.

## Runtime Prompt Context Assembly

Create `PromptContractRegistry`.

Each runtime prompt contract must define:

| Field | Meaning |
|---|---|
| Runtime Prompt | Generated prompt class or prompt adapter name |
| Required Projection | Projection registry key |
| Required Inputs | Artifact paths or derived context sections that must exist before execution |
| Optional Inputs | Artifact paths that may be included when present |
| Required Outputs | Artifact paths or parser result fields expected after execution |
| Allowed Decisions | Exact allowed selection, disposition, completion, or blocking values |
| Blocking Outputs | Output markers that intentionally pause the machine |
| Artifact Writer | The only component allowed to write the output artifact |
| Stale Projection Policy | `Block`, `WarnOnly`, or `Allow` |

Prompt contracts are code-level definitions, with `.agents/contracts/prompt-contracts.md` emitted as a human-readable snapshot during startup. The state machine must validate the contract before every prompt run:

- required projection exists, is validated, and is not stale under the contract policy
- required inputs exist and are non-whitespace
- expected output parser exists
- output writer is authorized for the target artifact

Create `RoadmapPromptContextBuilder`.

It assembles a structured `projectContext` string for each runtime prompt. The string must include only the required projection and artifact state for that transition.

Required context sections:

- Projection content.
- Current roadmap completion context when required.
- Roadmap source when selecting.
- Selected epic or active epic when required.
- Selection proposal when creating or splitting.
- Audit output when realigning or reimagining.
- Milestone specs and active epic when certifying completion.
- Repository inspection instructions for prompts that must audit codebase reality.

Do not include:

- Raw north-star source files.
- Unrelated projections.
- Unrelated old evidence files.
- Full operational history unless the specific prompt requires it.

## State Model

Implement a `RoadmapState` enum containing:

```text
CoreReady
BootstrapRoadmapCompletionContext
RoadmapCompletionContextReady
SelectNextStrategicInitiative
ExistingEpicSelected
NewEpicProposed
SplitEpicProposed
EpicPreparationAudit
RealignEpic
ReimagineEpic
RetireEpic
EvidenceBlocked
EvidenceGathering
CreateNewEpic
SplitEpic
SplitChildSelection
ActiveEpicReady
GenerateMilestoneDeepDives
MilestoneSpecsReady
GenerateOperationalContext
OperationalContextReady
GenerateExecutionPrompt
ExecutionPromptReady
ExecutionLoop
ExecutionBlocked
EpicCompletionDetected
CompletionEvaluationAndContextUpdate
StrategicInvestigationRequired
RoadmapRevisionRequired
NoSuitableInitiative
Completed
Failed
Cancelled
```

Persist `.agents/state.md` after every transition using the shape in the requirements:

```markdown
# Engineering Loop State

## Current State

{{state_name}}

## Active Artifacts

| Artifact | Path | Status |
|---|---|---|

## Last Transition

| Field | Value |
|---|---|
| From | {{previous_state}} |
| To | {{current_state}} |
| Prompt | {{prompt_name}} |
| Projection | {{projection_name}} |
| Output | {{artifact_path}} |
| Decision | {{decision_or_disposition}} |
| Status | Started / Completed / Failed / Cancelled |
| Started At | {{utc_timestamp}} |
| Completed At | {{utc_timestamp_or_blank}} |

## Blockers

| Blocker | Required Next Step |
|---|---|

## Decision Ledger Summary

| Field | Value |
|---|---|
| Ledger Path | .agents/decision-ledger.md |
| Last Decision ID | {{decision_id_or_none}} |
| Retired Exclusions | {{count}} |
| Split Families | {{count}} |

## Projection Manifest Summary

| Field | Value |
|---|---|
| Manifest Path | .agents/projections/manifest.md |
| Valid Projections | {{count}} |
| Stale Projections | {{count}} |
| Invalid Projections | {{count}} |

## Next Valid Transitions

- {{transition_1}}
```

Also track runtime-only state needed to avoid loops:

- Retired epic IDs/names excluded for the current selection cycle.
- Last selection artifact path.
- Last audit artifact path.
- Current active epic path.
- Current milestone spec set hash or timestamp summary.

Decision ledger rule:

- Maintain `.agents/decision-ledger.md` as durable orchestration history, not operational context.
- Append one compact entry for every selection, audit disposition, split child choice, completion certification routing decision, roadmap-completion update, and terminal pause.
- Each entry records decision ID, timestamp, state, transition, prompt, projection path, input artifact paths, output artifact paths, decision/disposition, confidence, and rationale excerpt.
- `.agents/state.md` contains only a summary and pointers; the ledger contains the durable "why" history.

Transition journal rule:

- Maintain `.agents/journal/transitions.jsonl`.
- Write a `TransitionStarted` record before any long-running prompt or execution-bridge operation.
- Write a `TransitionCompleted`, `TransitionFailed`, or `TransitionCancelled` record afterward.
- Each record includes previous state, next or attempted state, prompt, projection, prompt contract key, input artifact paths with content hashes, output paths, duration, result, parser decision, error message, and correlation ID.
- Do not put domain reasoning in the journal; put domain findings in evidence artifacts and decision rationale summaries in the ledger.

Artifact lifecycle rule:

- Maintain `.agents/artifacts/lifecycle.md`.
- Track lifecycle for projections, roadmap-completion context, active epic, split children, specs, operational context, execution prompt, and evaluation artifacts.
- Allowed lifecycle values:
  - `Missing`
  - `Draft`
  - `Ready`
  - `Executing`
  - `Completed`
  - `Archived`
  - `Superseded`
  - `Blocked`
- State transitions must update lifecycle metadata when they create, promote, supersede, archive, or block an artifact.

## Global Invariants

Create `InvariantValidator` and run it after every completed transition and before entering execution.

Required invariants:

- The eight Core North-Star source files exist and match the current north-star hash recorded for the run.
- The Core North-Star source files are never included in runtime prompt contexts.
- Exactly one roadmap-completion context path is active: `.agents/north-star/roadmap-completion-context.md`.
- Every runtime prompt has exactly one required projection in the projection registry.
- Every cached projection has a manifest entry and a validation result.
- Every prompt run has a prompt contract entry.
- Only authorized transition components write their owned artifacts.
- At most one active epic is `Ready` or `Executing`.
- Every `.agents/specs/*.md` file belongs to the active epic, based on metadata or current generation correlation ID.
- No execution bridge invocation happens without active epic, milestone specs, operational context, and execution prompt.
- Every completed epic certification that allows closure is followed by `UpdateRoadmapCompletionContext` before the next selection cycle.
- Split child epics must reference a split-family artifact before any child is promoted to active epic.
- Blocked transitions must write a canonical blocked artifact.

Invariant failures:

- Write an orchestration evidence artifact under `.agents/evidence/orchestration/`.
- Append a failed transition journal entry.
- Enter `Failed` for internal corruption or `EvidenceBlocked` for missing/ambiguous evidence that a human can resolve.

## Prompt Output Parsing

Implement small markdown parsers with deterministic failure behavior.

Parsers:

- `MarkdownTableParser`: parse simple pipe tables by header name.
- `SelectionParser`: read `## Recommendation Summary` and extract `Recommended Outcome`, `Recommended Initiative`, `Initiative Type`, `Confidence`.
- `EpicPreparationAuditParser`: read `## Audit Disposition` and extract `Disposition`, `Confidence`, `Recommended Next Step`.
- `CompletionEvaluationParser`: read `## Evaluation Summary` and extract `Overall Completion Status`, `Overall Drift Classification`, `Closure Recommendation`.
- `BundleFileExtractor`: parse `# FILE: .agents/specs/*.md` and `# FILE: .agents/epic-*.md` blocks.
- `BundleManifestWriter`: write a manifest next to extracted bundle files with source prompt, projection, expected file count, extracted file paths, file hashes, and validation result.

Failure rules:

- If a required field is missing or outside the allowed vocabulary, stop with `EvidenceBlocked` or `Failed` depending on whether more evidence could resolve it.
- Do not guess dispositions from vague prose unless the required table is absent and the final statement contains an exact allowed value.
- Bundle extraction must reject paths outside the allowed target directory.
- Bundle extraction must reject duplicate target paths.
- Bundle extraction must write a manifest before downstream stages consume extracted files.

## Execution Integration

The roadmap state machine defines an execution loop, but the existing execution CLI is built around `.agents/plan.md` and `.agents/milestones/m*.md`, while this roadmap machine produces `.agents/epic.md` and `.agents/specs/*.md`.

For MVP, implement explicit grounding and bridge stages:

1. `GenerateOperationalContext`: create `.agents/operational_context.md` from active epic, ordered specs, latest accepted decisions, and execution-relevant artifact lifecycle metadata.
2. `OperationalContextReady`: verify operational context exists, is non-whitespace, and references the active epic/spec set.
3. `GenerateExecutionPrompt`: create `.agents/execution-prompt.md` from operational context plus current milestone/spec readiness.
4. `ExecutionPromptReady`: verify execution prompt exists and does not include raw Core North-Star source content.
5. `ExecutionCompatibilityMaterializer`: materialize execution-compatible `.agents/plan.md` and `.agents/milestones/mNNN.md` from `.agents/epic.md`, `.agents/specs/*.md`, `.agents/operational_context.md`, and `.agents/execution-prompt.md`.
6. Write `.agents/plan.md` as a generated execution index containing the active epic, the ordered spec list, and the execution prompt pointer.
7. Write `.agents/milestones/mNNN.md` files derived from specs, preserving the full spec content and adding strict checkbox items for auditable execution progress.
8. Invoke the existing execution loop through an `IRoadmapExecutionBridge` abstraction.
9. When the execution bridge reports epic completion, transition to `EpicCompletionDetected`.
10. Always run `EvaluateEpicCompletionAndDrift` before updating roadmap completion context. Existing execution completion is only a detection signal, not certification.

Operational-context generation is not roadmap-completion-context update. It is execution memory and handoff state for the current active epic only.

Execution-prompt generation is not execution. It produces the bounded instruction artifact consumed by the bridge and recorded for auditability.

Keep the bridge behind an interface so tests do not run the real execution CLI.

Bridge output mapping:

| Bridge result | Roadmap transition |
|---|---|
| Epic completed | `EpicCompletionDetected` |
| Cancelled | `Cancelled` |
| Failed with strategic blocker marker | `ExecutionBlocked` |
| Failed otherwise | `Failed` |
| Stalled | `ExecutionBlocked` |

If the execution-compatible materializer cannot produce at least one checkbox-backed milestone file from the specs, transition to `EvidenceBlocked` with a blocker explaining that execution readiness could not be derived from milestone specs.

## Control Flow

Implement `RoadmapStateMachine.RunAsync`.

High-level flow:

```text
ValidateNorthStarSourceContext
CoreReady

if roadmap-completion-context missing:
  EnsureProjection(roadmap-completion)
  Run CreateRoadmapCompletionContext
  write roadmap-completion-context

RoadmapCompletionContextReady

while not terminal:
  Run SelectNextEpic
  branch by Recommended Outcome

  Select Existing Epic:
    load selected roadmap epic
    Run EpicPreparationAudit
    branch by Disposition

  Select New Intermediary Epic:
    Run CreateNewEpic
    write .agents/epic.md

  Select Split Epic:
    Run SplitEpic
    write .agents/epic-*.md
    select dependency-root child or pause for roadmap revision

  ActiveEpicReady:
    Run GenerateMilestoneDeepDivesForEpic
    write .agents/specs/*.md

  MilestoneSpecsReady:
    Generate .agents/operational_context.md
    Generate .agents/execution-prompt.md
    Run execution bridge

  EpicCompletionDetected:
    Run EvaluateEpicCompletionAndDrift
    if closure recommendation allows close:
      Run UpdateRoadmapCompletionContext
      loop back to selection
    else:
      route to ExecutionLoop, EpicPreparationAudit, or EvidenceBlocked
```

Every `Run ...` line above is implemented as:

```text
Validate prompt contract
Validate required inputs
Write TransitionStarted to state + journal
Run prompt or bridge
Validate output
Write domain/orchestration evidence
Append decision ledger entry when a decision was made
Update artifact lifecycle
Run invariant validator
Write TransitionCompleted to state + journal
```

Terminal or paused states:

- `StrategicInvestigationRequired`
- `RoadmapRevisionRequired`
- `NoSuitableInitiative`
- `EvidenceBlocked`
- `Cancelled`
- `Failed`
- `Completed`

## Transition Definition Model

Avoid baking all orchestration behavior into one large `switch` statement.

Represent each transition with a `TransitionDefinition`:

| Field | Meaning |
|---|---|
| Name | Stable transition name used by state, journal, and ledger |
| From State | Required current state |
| To State Candidates | Allowed next states |
| Guard | Predicate that checks state, artifact lifecycle, prompt contract, and required inputs |
| Executor | Prompt runner, deterministic generator, parser, bundle extractor, or execution bridge |
| Output Validator | Parser and artifact validation for the transition result |
| Lifecycle Updates | Artifact lifecycle changes applied after success |
| Decision Ledger Policy | Whether the transition appends a decision entry |
| Invariant Scope | Which invariants must be checked before and after |

The MVP may still dispatch definitions procedurally, but all transition metadata should live in data structures that can evolve toward a table-driven state machine. This keeps future projection types, audit types, and execution loops additive instead of requiring state-machine rewrites.

## Transition Details

### Bootstrap Roadmap Completion Context

Inputs:

- `.agents/projections/roadmap-completion.md`
- Completed epic history from `.agents/archive/`, `.agents/completed/`, and any configured completed-epic source files.

Output:

- `.agents/north-star/roadmap-completion-context.md`

If completed epic history is missing, write a blocker and enter `EvidenceBlocked`; do not fabricate current strategic state.

### Select Next Strategic Initiative

Inputs:

- `.agents/projections/select-next-epic.md`
- `.agents/north-star/roadmap-completion-context.md`
- roadmap source
- retired epic exclusions from current state

Output:

- `.agents/selection.md`
- numbered evidence copy under `.agents/evidence/selection/`

Allowed outcomes:

- `Select Existing Epic`
- `Select New Intermediary Epic`
- `Select Split Epic`
- `Strategic Investigation Required`
- `Roadmap Revision Required`
- `No Suitable Initiative`

### Existing Epic Preparation

Inputs:

- `.agents/projections/epic-preparation-audit.md`
- selected epic
- repository reality via read-only codebase inspection

Output:

- `.agents/evidence/audits/epic-preparation-audit.{N:0000}.md`

Allowed dispositions:

- `Realign`
- `Reimagine`
- `Retire`
- `Insufficient Evidence`

### Realign Epic

Inputs:

- `.agents/projections/realign-epic.md`
- selected epic
- latest preparation audit

Output:

- `.agents/epic.md`

The parser must require the audit disposition to be `Realign` before running this prompt.

### Reimagine Epic

Inputs:

- `.agents/projections/reimagine-epic.md`
- selected epic
- latest preparation audit

Output:

- `.agents/epic.md`

The parser must require the audit disposition to be `Reimagine` before running this prompt.

### Retire Epic

Output:

- retirement evidence under `.agents/evidence/audits/retire-epic.{N:0000}.md`
- updated `.agents/state.md` retired-exclusion list

If retirement appears local, return to selection with the epic excluded. If retirement implies roadmap structure is stale, transition to `RoadmapRevisionRequired`.

### Create New Epic

Inputs:

- `.agents/projections/create-new-epic.md`
- new epic proposal extracted from selection
- repository reality via read-only codebase inspection

Output:

- `.agents/epic.md`

If the prompt returns a blocking document rather than an epic, enter `EvidenceBlocked`.

### Split Epic

Inputs:

- `.agents/projections/split-epic.md`
- split proposal extracted from selection
- selected source epic when applicable
- repository reality via read-only codebase inspection

Output:

- `.agents/epic-*.md`
- `.agents/splits/split-family-{N:0000}.md`

Split family artifact:

- Preserve the original selection split proposal.
- List every generated child epic path and hash.
- Record source roadmap epic path when applicable.
- Record dependency relationships among children.
- Record selected child path and selection rationale when a child is promoted.
- Record remaining children as deferred roadmap candidates or roadmap-revision inputs.

After bundle extraction, select a dependency-root child only when exactly one child is clearly marked first executable. If not, enter `SplitChildSelection` and then either `RoadmapRevisionRequired` or `SelectNextStrategicInitiative`. A child epic cannot be promoted to `.agents/epic.md` unless its split-family artifact exists and is referenced in the decision ledger.

### Generate Milestone Deep Dives

Inputs:

- `.agents/projections/milestone-deep-dive.md`
- `.agents/epic.md`

Output:

- `.agents/specs/*.md`

Extract files only from `# FILE: .agents/specs/*.md` markers. Require at least one spec. Reject blocked output unless state transitions to `EvidenceBlocked`.

### Generate Operational Context

Inputs:

- `.agents/epic.md`
- `.agents/specs/*.md`
- `.agents/artifacts/lifecycle.md`
- latest relevant decision ledger entries

Output:

- `.agents/operational_context.md`

Purpose:

- Create execution memory for the active epic.
- Preserve current spec ordering, dependency assumptions, active constraints, blocked or deferred work, and handoff-relevant decisions.
- Keep execution memory separate from roadmap-completion context.

The generator may be deterministic for MVP. If a prompt-backed version is added later, it must receive an execution-grounding projection and follow the same projection/cache/contract rules.

### Generate Execution Prompt

Inputs:

- `.agents/operational_context.md`
- `.agents/epic.md`
- ordered `.agents/specs/*.md`

Output:

- `.agents/execution-prompt.md`

Purpose:

- Produce the bounded instruction artifact for the execution bridge.
- Identify the current first executable spec or milestone.
- Preserve constraints and validation expectations without redefining roadmap intent.

The execution prompt must not include raw Core North-Star source content. It may reference cached projections only if a future execution-grounding projection contract explicitly allows that input.

### Epic Completion Detection and Certification

Execution bridge completion is only a detection signal.

After detection:

1. Ensure `.agents/projections/epic-completion-evaluation.md`.
2. Run `EvaluateEpicCompletionAndDrift`.
3. Persist evaluation under `.agents/evidence/evaluations/epic-completion-and-drift.{N:0000}.md`.
4. Route based on `Closure Recommendation`.

Routing:

| Closure recommendation | Next state |
|---|---|
| `Close Epic` | `CompletionEvaluationAndContextUpdate` |
| `Close With Follow-Up` | `CompletionEvaluationAndContextUpdate` |
| `Continue Epic` | `ExecutionLoop` |
| `Reopen Epic` | `EpicPreparationAudit` |
| `Gather More Evidence` | `EvidenceBlocked` |

### Roadmap Completion Context Update

Inputs:

- `.agents/projections/roadmap-completion-update.md`
- current `.agents/north-star/roadmap-completion-context.md`
- `.agents/epic.md`
- latest completion evaluation
- repository reality via read-only inspection

Output:

- updated `.agents/north-star/roadmap-completion-context.md`
- evidence copy under `.agents/evidence/evaluations/roadmap-completion-update.{N:0000}.md`

After a successful update, clear active epic/spec execution state as appropriate and return to `SelectNextStrategicInitiative`.

## File Structure

```text
src/CommandCenter.Roadmap.CLI/
  CommandCenter.Roadmap.CLI.csproj
  Program.cs
  CliArguments.cs
  LoopConsole.cs
  AgentSpecs.cs
  RoadmapOutcome.cs
  RoadmapStepException.cs
  RoadmapArtifactPaths.cs
  RoadmapArtifacts.cs
  NorthStarContextLoader.cs
  ProjectionRegistry.cs
  ProjectionManifest.cs
  ProjectionManifestStore.cs
  ProjectionValidator.cs
  ProjectionCache.cs
  PromptContractRegistry.cs
  RoadmapPromptRunner.cs
  RoadmapPromptContextBuilder.cs
  RoadmapState.cs
  RoadmapStateDocument.cs
  RoadmapStateStore.cs
  DecisionLedger.cs
  DecisionLedgerStore.cs
  TransitionJournal.cs
  TransitionJournalStore.cs
  ArtifactLifecycle.cs
  ArtifactLifecycleStore.cs
  InvariantValidator.cs
  RoadmapStateMachine.cs
  MarkdownTableParser.cs
  SelectionParser.cs
  EpicPreparationAuditParser.cs
  CompletionEvaluationParser.cs
  BundleFileExtractor.cs
  BundleManifestWriter.cs
  SplitFamily.cs
  SplitFamilyStore.cs
  OperationalContextGenerator.cs
  ExecutionPromptGenerator.cs
  ExecutionCompatibilityMaterializer.cs
  RoadmapExecutionBridge.cs

tests/CommandCenter.Roadmap.CLI.Tests/
  CommandCenter.Roadmap.CLI.Tests.csproj
  TestDoubles.cs
  CliArgumentsTests.cs
  NorthStarContextLoaderTests.cs
  ProjectionManifestTests.cs
  ProjectionCacheTests.cs
  ProjectionValidatorTests.cs
  PromptContractRegistryTests.cs
  RoadmapPromptContextBuilderTests.cs
  MarkdownParserTests.cs
  BundleFileExtractorTests.cs
  BundleManifestWriterTests.cs
  RoadmapStateStoreTests.cs
  DecisionLedgerTests.cs
  TransitionJournalTests.cs
  ArtifactLifecycleTests.cs
  InvariantValidatorTests.cs
  RoadmapStateMachineSelectionTests.cs
  RoadmapStateMachineEpicPreparationTests.cs
  OperationalContextGeneratorTests.cs
  ExecutionPromptGeneratorTests.cs
  ExecutionCompatibilityMaterializerTests.cs
```

## Task 1: Project Scaffold

Files:

- Create `src/CommandCenter.Roadmap.CLI/CommandCenter.Roadmap.CLI.csproj`
- Create `src/CommandCenter.Roadmap.CLI/Program.cs`
- Create `tests/CommandCenter.Roadmap.CLI.Tests/CommandCenter.Roadmap.CLI.Tests.csproj`
- Modify `CommandCenter.slnx`

Requirements:

- CLI references `CommandCenter.Agents`, `CommandCenter.Core`, `CommandCenter.Orchestration`.
- Test project references the CLI and `CommandCenter.Core`.
- Add `InternalsVisibleTo` for the test project.
- Program parses one required `REPO_DIR`.

Acceptance:

- `dotnet build CommandCenter.slnx` discovers both projects.

## Task 2: Artifact Path and Repository IO Layer

Files:

- Create `RoadmapArtifactPaths.cs`
- Create `RoadmapArtifacts.cs`

Requirements:

- Centralize all roadmap-specific paths.
- Wrap `IArtifactStore`.
- Provide helpers for exists/read/write/list, numbered evidence writes, roadmap source concatenation, and active artifact status.
- Reject path traversal through `ArtifactPath`.

Tests:

- Roadmap source concatenates `.agents/roadmap.md` before sorted `.agents/roadmap/*.md`.
- Numbered evidence paths increment without overwriting.

## Task 3: North-Star Context Loader

Files:

- Create `NorthStarContextLoader.cs`
- Create `NorthStarContextLoaderTests.cs`

Requirements:

- Validate all eight required source files before any state-machine work.
- Return a single ordered concatenation string.
- Exclude `roadmap-completion-context.md` from the Core concatenation.
- Error message must list every missing required file.
- If extra numbered north-star source files are found, fail with a message explaining the exact eight-file contract.

Tests:

- Concatenates in fixed order.
- Fails on missing file.
- Ignores runtime completion context.
- Fails on `09-something.md`.

## Task 4: Projection Manifest, Projection Validator, and Prompt Contracts

Files:

- Create `ProjectionRegistry.cs`
- Create `ProjectionManifest.cs`
- Create `ProjectionManifestStore.cs`
- Create `ProjectionValidator.cs`
- Create `PromptContractRegistry.cs`
- Create `ProjectionManifestTests.cs`
- Create `ProjectionValidatorTests.cs`
- Create `PromptContractRegistryTests.cs`

Requirements:

- Registry maps runtime prompts to cached paths and generated projection prompt classes.
- Projection manifest records prompt provenance, north-star hash, projection hash, validation status, and stale status.
- Projection validator enforces required sections and forbidden content rules before any projection is trusted.
- Prompt contract registry defines required projection, required inputs, allowed outputs, stale projection policy, parser, and authorized writer for every runtime prompt.
- Emit `.agents/contracts/prompt-contracts.md` as a startup snapshot of prompt contracts.

Tests:

- Manifest entry records hashes and validation status.
- Stale detection compares manifest `northStarHash` to current Core hash.
- Projection validator accepts a structurally valid projection.
- Projection validator rejects missing required sections and forbidden runtime-state content.
- Prompt contract registry has one contract for every runtime prompt in the projection registry.

## Task 5: Projection Cache and Prompt Runner

Files:

- Create `ProjectionCache.cs`
- Create `RoadmapPromptRunner.cs`
- Create `ProjectionCacheTests.cs`

Requirements:

- Cache checks file existence before running Codex.
- Projection prompt receives `projectContext = northStarContext`.
- Existing non-whitespace projection is reused.
- New projection is written only after completed agent turn.
- Every cached projection is validated before use.
- Every cache read/write updates `.agents/projections/manifest.md`.
- Stale projections follow the prompt contract stale policy.

Tests:

- Existing projection does not call runtime.
- Missing projection calls runtime once and writes output.
- Failed turn does not write projection.
- Invalid generated projection writes a blocked artifact and is not marked valid.
- Stale projection blocks when contract policy is `Block`.

## Task 6: Runtime Prompt Context Builder

Files:

- Create `RoadmapPromptContextBuilder.cs`
- Create `RoadmapPromptContextBuilderTests.cs`

Requirements:

- Build per-transition `projectContext` strings from projection plus required artifacts.
- Never include raw `northStarContext` in runtime prompt contexts.
- Include repository-audit instructions for audit, create, split, completion, and update prompts.
- Include retired epic exclusions for selection.

Tests:

- Selection context contains projection, completion context, roadmap, and exclusions.
- Audit context contains projection and selected epic only.
- Realign/reimagine context contains projection, current epic, and audit.
- Raw Core file markers never appear in runtime contexts.

## Task 7: Markdown Parsers

Files:

- Create `MarkdownTableParser.cs`
- Create `SelectionParser.cs`
- Create `EpicPreparationAuditParser.cs`
- Create `CompletionEvaluationParser.cs`
- Create parser tests.

Requirements:

- Parse required tables and fields.
- Normalize allowed values with exact vocabulary.
- Return structured records with confidence/reason fields where available.
- Produce actionable parse errors.

Tests:

- Parses valid selection outcomes.
- Parses all audit dispositions.
- Parses completion closure recommendations.
- Rejects unknown values.

## Task 8: Bundle Extraction and Split Family Tracking

Files:

- Create `BundleFileExtractor.cs`
- Create `BundleManifestWriter.cs`
- Create `SplitFamily.cs`
- Create `SplitFamilyStore.cs`
- Create `BundleFileExtractorTests.cs`
- Create `BundleManifestWriterTests.cs`

Requirements:

- Extract multi-file bundles from `# FILE: ...` markers.
- Support `.agents/specs/*.md` and `.agents/epic-*.md`.
- Reject output outside allowed paths.
- Reject duplicate file markers.
- Preserve file content exactly between markers, trimmed only for leading/trailing blank separator noise.
- Write bundle manifests with extracted path hashes and source prompt/projection provenance.
- For split epic bundles, write a split-family artifact before any child is promoted.

Tests:

- Extracts multiple spec files.
- Rejects `../` paths.
- Rejects duplicate paths.
- Handles blocked bundle with no files as a typed result.
- Writes bundle manifest for extracted specs.
- Writes split-family artifact preserving proposal, children, dependency order, and selected child rationale.

## Task 9: State Store, Decision Ledger, Transition Journal, and Artifact Lifecycle

Files:

- Create `RoadmapState.cs`
- Create `RoadmapStateDocument.cs`
- Create `RoadmapStateStore.cs`
- Create `DecisionLedger.cs`
- Create `DecisionLedgerStore.cs`
- Create `TransitionJournal.cs`
- Create `TransitionJournalStore.cs`
- Create `ArtifactLifecycle.cs`
- Create `ArtifactLifecycleStore.cs`
- Create `RoadmapStateStoreTests.cs`
- Create `DecisionLedgerTests.cs`
- Create `TransitionJournalTests.cs`
- Create `ArtifactLifecycleTests.cs`

Requirements:

- Write `.agents/state.md` when transitions start and when transitions complete, fail, or cancel.
- Include active artifact status and next valid transitions.
- Preserve retired epic exclusions.
- Provide a load method for resuming from existing state.
- Append decision entries to `.agents/decision-ledger.md`.
- Append transition-started/completed/failed/cancelled records to `.agents/journal/transitions.jsonl`.
- Track lifecycle state in `.agents/artifacts/lifecycle.md`.

Tests:

- Writes required sections.
- Round-trips current state and retired exclusions.
- Marks missing active artifacts correctly.
- Journal records started and completed correlation IDs.
- Decision ledger records selections, dispositions, split choices, and completion routing.
- Lifecycle store records active epic and specs as `Ready`, `Executing`, `Completed`, or `Superseded`.

## Task 10: Invariant Validator

Files:

- Create `InvariantValidator.cs`
- Create `InvariantValidatorTests.cs`

Requirements:

- Implement the global invariants listed above.
- Run after every completed transition and before execution bridge invocation.
- Distinguish internal corruption (`Failed`) from missing evidence or human-resolvable ambiguity (`EvidenceBlocked`).
- Write orchestration evidence on invariant failure.

Tests:

- Rejects multiple active ready/executing epics.
- Rejects specs that do not belong to the active epic.
- Rejects execution without specs, operational context, or execution prompt.
- Rejects a prompt contract missing for a runtime prompt.
- Rejects a split child promotion without a split-family artifact.

## Task 11: Core State Machine Through Active Epic

Files:

- Create `RoadmapStateMachine.cs`
- Add selection and epic-preparation tests.

Requirements:

- Implement bootstrap, selection, existing-epic audit, realign, reimagine, retire, create-new, split, and active-epic transitions.
- Persist durable evidence for selection and audits.
- Validate prompt contracts before every prompt run.
- Write transition-started and transition-completed journal/state records around every Codex call.
- Append decision ledger entries for selections, dispositions, retirements, split choices, and terminal pauses.
- Update artifact lifecycle when active epic, split children, or evidence artifacts are created or superseded.
- Run invariant validation after each completed transition.
- Write `.agents/epic.md` only from CreateNewEpic, RealignEpic, ReimagineEpic, or split-child adoption.
- Never modify roadmap from selection.

Tests:

- Missing completion context triggers bootstrap.
- Existing completion context skips bootstrap.
- Existing epic selected routes to audit.
- Realign writes active epic.
- Reimagine writes active epic.
- Retire excludes epic and returns to selection.
- Transition-started state survives a simulated prompt failure.
- Decision ledger records why a retired epic is excluded.
- Split child promotion requires a split-family artifact.
- Strategic investigation and roadmap revision enter terminal paused states.

## Task 12: Milestone Deep Dive Generation

Files:

- Extend `RoadmapStateMachine.cs`
- Add bundle extraction integration tests.

Requirements:

- Ensure `.agents/projections/milestone-deep-dive.md`.
- Run `GenerateMilestoneDeepDivesForEpic`.
- Extract `.agents/specs/*.md`.
- Write a bundle manifest for extracted specs.
- Mark specs as `Ready` in lifecycle metadata.
- Run invariant validation to prove every spec belongs to the active epic.
- Require one spec per detected epic milestone when the epic has a parseable milestone roadmap; otherwise require at least one spec.
- Transition to `MilestoneSpecsReady`.

Tests:

- Valid bundle writes specs.
- Blocked output enters `EvidenceBlocked`.
- Bundle manifest includes every extracted spec path and hash.
- Missing specs fails before execution bridge.

## Task 13: Operational Context and Execution Prompt Generation

Files:

- Create `OperationalContextGenerator.cs`
- Create `ExecutionPromptGenerator.cs`
- Create `OperationalContextGeneratorTests.cs`
- Create `ExecutionPromptGeneratorTests.cs`

Requirements:

- Generate `.agents/operational_context.md` from active epic, ordered specs, relevant ledger entries, and lifecycle metadata.
- Generate `.agents/execution-prompt.md` from operational context, active epic, and ordered specs.
- Ensure neither artifact includes raw Core North-Star file markers.
- Mark operational context and execution prompt lifecycle as `Ready`.
- Transition through `GenerateOperationalContext`, `OperationalContextReady`, `GenerateExecutionPrompt`, and `ExecutionPromptReady`.

Tests:

- Operational context references active epic and all ordered specs.
- Execution prompt identifies first executable spec or clearly blocks when none is executable.
- Raw Core file markers are absent.
- Missing specs enters `EvidenceBlocked`.

## Task 14: Execution Bridge

Files:

- Create `ExecutionCompatibilityMaterializer.cs`
- Create `RoadmapExecutionBridge.cs`
- Create execution bridge tests.

Requirements:

- Materialize `.agents/plan.md` and `.agents/milestones/mNNN.md` from active epic and specs.
- Require `.agents/operational_context.md` and `.agents/execution-prompt.md` before materialization.
- Preserve the full source spec in each materialized milestone file.
- Add checkbox-backed execution items derived from acceptance criteria or completion evidence.
- If no auditable checklist can be derived, enter `EvidenceBlocked`.
- Write transition-started state before invoking the execution bridge and transition-completed/failed state afterward.
- Mark active epic and current execution artifacts as `Executing` while the bridge runs.
- Run invariant validation immediately before bridge invocation.
- Invoke the existing execution loop through `IRoadmapExecutionBridge`.
- Tests must use a fake bridge and must not run real Codex.

Acceptance:

- `MilestoneSpecsReady` can transition to `EpicCompletionDetected` when fake bridge reports epic completion.
- Bridge failures route according to the mapping table above.

## Task 15: Completion Certification and Context Update

Files:

- Extend `RoadmapStateMachine.cs`
- Add completion tests.

Requirements:

- Run `EvaluateEpicCompletionAndDrift` after execution completion detection.
- Persist evaluation evidence.
- Route by `Closure Recommendation`.
- Run `UpdateRoadmapCompletionContext` only for `Close Epic` and `Close With Follow-Up`.
- Write updated `.agents/north-star/roadmap-completion-context.md`.
- Append decision ledger entries for completion routing and roadmap-completion-context update.
- Mark completed active epic/specs as `Completed` only after roadmap completion context is updated.
- Enforce the invariant that the machine cannot return to selection after closable completion until roadmap completion context update succeeds.
- Return to `SelectNextStrategicInitiative` after successful update.

Tests:

- Close Epic updates completion context and loops.
- Continue Epic returns to execution.
- Reopen Epic returns to audit.
- Gather More Evidence enters `EvidenceBlocked`.

## Task 16: CLI Entrypoint and Cancellation

Files:

- Complete `Program.cs`
- Create `CliArguments.cs`
- Create `LoopConsole.cs`
- Create `AgentSpecs.cs`

Requirements:

- `CommandCenter.Roadmap.CLI <REPO_DIR>`
- Print phase markers for each state.
- Surface Codex executable path like existing CLIs.
- Ctrl+C cancels the current turn and writes final cancelled state.
- Exit codes:
  - `0`: completed or paused in an intentional terminal state.
  - `1`: failed.
  - `2`: CLI argument error.
  - `4`: preflight blocked.
  - `130`: cancelled.

Tests:

- Argument parsing validates repo path.
- Cancelled state maps to exit code 130 through testable runner wrapper.

## Task 17: Verification

Run:

```powershell
dotnet build CommandCenter.slnx
dotnet test tests\CommandCenter.Roadmap.CLI.Tests\CommandCenter.Roadmap.CLI.Tests.csproj
```

If generated prompt signatures differ from the assumed render calls, update only the prompt invocation adapter and tests. Do not hardcode prompt template text in the CLI.

## Implementation Notes

- Existing prompt files under `src/CommandCenter.Core/Prompts/Planning` and `src/CommandCenter.Core/Prompts/Projections` are already the correct source of agent instructions. The CLI should render generated prompt classes rather than duplicating prompt bodies.
- The planning prompt classes may have signatures such as `Render(projectContext)`, `Render(projectContext, completedEpics)`, `Render(projectContext, epic)`, `Render(projectContext, newEpicProposal)`, or `Render(projectContext, codebaseAudit)`. Confirm exact generated signatures during build and isolate them behind `RoadmapPromptCatalog`.
- Multi-file prompt outputs are not trusted until parsed and validated.
- The state machine should stop on explicit uncertainty rather than inventing missing evidence.
- Completion context update must happen after certification, never directly after execution bridge completion.
- Runtime prompt context assembly is the main correctness boundary. Keep it small, explicit, and covered by tests.
