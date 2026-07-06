# Roadmap CLI blocked rerun dead end

## Severity

Medium

Confidence: High.

## Audit status

Verified against the current codebase.

The issue is real for persisted `EvidenceBlocked`, `ExecutionBlocked`, and `Failed`
states. A normal CLI rerun reports those states and exits before project-context
preflight, resume planning, invariant checks, execution disposition parsing, or any
runtime call. This contradicts several persisted blocker messages that tell the
operator to repair the underlying artifact and rerun the roadmap CLI.

One scope correction: an active-workflow preflight failure is not the same dead end.
`WritePreflightInterruptedStateAsync` preserves the interrupted active state and only
appends a preflight blocker, so a rerun can retry preflight after repair because the
current state remains active. The dead end is strongest for fresh preflight blockers
written as `EvidenceBlocked`, terminal execution blockers written as
`ExecutionBlocked`, and runtime failures written as `Failed`.

## Verified behavior

The CLI has no command verb today. `CliArguments.TryParse` accepts only
`<REPO_DIR>`, and `Program` always constructs `RoadmapStateMachine` and calls
`RunAsync`.

Startup planning is the first hard stop:

- `RoadmapStartupPlanner.Plan` maps `EvidenceBlocked` to
  `ReportBlockedWorkflow` with `RoadmapPreflightRequirement.None`.
- The same planner maps `Failed` to `ReportFailedWorkflow` with
  `RoadmapPreflightRequirement.None`.
- `RoadmapWorkflowStateClassifier.IsTerminalPauseState` includes
  `ExecutionBlocked`; terminal pause states are also reported with no preflight.
- `RoadmapStateMachine.RunAsync` immediately returns
  `startupPlan.ReportOutcome ?? RoadmapOutcome.Paused` whenever the startup
  plan has `PreflightRequirement.None`. That return happens before
  `ProjectContextLoader.LoadAsync`, before `RoadmapResumePlanner.PlanAsync`, and
  before all invariant or evidence revalidation.
- `RoadmapResumePlanner.PlanAsync` also treats report-only states as terminal if it
  is called directly, so the resume planner has no unblock path either.

The tests explicitly lock in the behavior:

- `RoadmapStartupPlannerTests.Blocked_workflow_is_reported_without_preflight`
  asserts `EvidenceBlocked` uses `ReportBlockedWorkflow`,
  `RoadmapPreflightRequirement.None`, and `RoadmapOutcome.Paused`.
- `RoadmapStartupPlannerTests.Failed_workflow_is_reported_without_preflight`
  asserts `Failed` uses `ReportFailedWorkflow`,
  `RoadmapPreflightRequirement.None`, and `RoadmapOutcome.Failed`.
- `RoadmapStateMachineResumeTests.Existing_blocked_state_is_loaded_before_startup_can_overwrite_it`
  asserts a persisted `EvidenceBlocked` state returns `Paused`, makes zero runtime
  calls, preserves the old blocker, and does not write a preflight transition.
- `RoadmapStateMachineResumeTests.Existing_blocked_state_survives_project_context_preflight_failure`
  demonstrates the even sharper case: with a persisted blocked state and missing
  Project Context, the CLI still does not run preflight and still returns `Paused`.
- `RoadmapStateMachineResumeTests.Report_only_state_skips_project_context_preflight_failure`
  covers terminal pause states, `Completed`, and `Failed`; all skip project-context
  preflight and make zero runtime calls.
- `RoadmapResumePlannerTests.Evidence_blocked_state_remains_paused` asserts the
  resume planner returns a terminal paused plan for `EvidenceBlocked`.

## Misleading persisted guidance

Multiple write paths instruct the operator to repair something and rerun, but the
rerun cannot inspect the repair:

| Source | Persisted state | Intent | Persisted next step | Actual rerun behavior |
|---|---|---|---|---|
| Fresh preflight failure via `WriteBlockedStateAsync` | `EvidenceBlocked` | `ResolveBlocker` | `Address the blocker and rerun the roadmap CLI.` | Reports paused before preflight, even after Project Context is added. |
| Artifact promotion blocked or rejected via `PromoteActiveEpicAsync` | `EvidenceBlocked` | `ResolveArtifactPromotionBlocker` | `Review <evidence> and rerun...` | Reports paused; never revalidates the evidence or reruns promotion. |
| Split output blocked or rejected via `BlockSplitEpicAsync` | `EvidenceBlocked` | `ResolveSplitEpicBlocker` | `Review <evidence> and rerun...` | Reports paused; never reinterprets the split bundle. |
| Malformed execution output via `PersistMalformedExecutionOutputAsync` | `EvidenceBlocked` | `ResolveMalformedExecutionOutput` | `repair the execution disposition output, and rerun.` | Reports paused; never reparses execution evidence. |
| Execution blocked via `PersistExecutionBlockedAsync` | `ExecutionBlocked` | `ResolveExecutionBlocker` | `resolve the execution blocker, and rerun.` | Reports paused as a terminal state; never resumes execution. |
| Execution runtime failure via `PersistExecutionRuntimeFailureAsync` | `Failed` | `RepairExecutionRuntimeFailure` | `repair the execution runtime failure, and rerun.` | Reports failed before preflight or bridge readiness checks. |
| Prompt/runtime transition failure via `RunPromptForPromotionAsync` | `EvidenceBlocked` | `ResolveTransitionFailure` | `Review the transition failure and rerun.` | Reports paused; never retries the transition. |
| Invalid completion certification via `PersistInvalidCompletionCertificationAsync` | `EvidenceBlocked` | `ResolveInvalidCompletionCertification` | `correct the certification decision, and rerun the roadmap CLI.` | Reports paused; never reparses or reroutes certification. |

`TransitionIntent` is useful but not currently sufficient as a generic dispatch key.
For many blockers, `TransitionIntent.DispatchState` is written as `EvidenceBlocked`,
`ExecutionBlocked`, or `Failed`, not as the last safe resumable state. A repair flow
must combine `TransitionIntent.Intent`, `TransitionIntent.EvidencePaths`,
`LastTransition.From`, `LastTransition.To`, and current artifact/projection
freshness rather than blindly dispatching to `DispatchState`.

## Impact

The persisted instructions are operationally misleading. A user can repair the
artifact named in the blocker, rerun the CLI, and still remain stuck because startup
planning treats the state as report-only.

This also weakens automation semantics. The first fresh preflight failure returns
`RoadmapOutcome.PreflightBlocked`, which `Program` maps to exit code `4`. After that
state has been persisted as `EvidenceBlocked`, a rerun returns `RoadmapOutcome.Paused`,
which `Program` maps to exit code `0`, even though no recovery was attempted.

Recovery then requires undocumented manual edits to `.agents/state.md`, deleting
state, or manually promoting repaired artifacts. Those workarounds can lose
provenance, bypass lifecycle checks, reuse stale projections, or skip safety gates
that the roadmap state machine otherwise enforces.

## Meaningful solution options

### Option 1: Add explicit commands: `run`, `status`, and `unblock`

Add a command shape to `CliArguments`, for example:

- `LoopRelay.Roadmap.CLI <repo> run`
- `LoopRelay.Roadmap.CLI <repo> status`
- `LoopRelay.Roadmap.CLI <repo> unblock`

Recommended semantics:

- `status` is always read-only and reports persisted state, blockers, transition
  intent, evidence paths, and whether the state appears unblockable.
- `run` resumes active workflow states only. For repairable blocked states, it should
  print an actionable message telling the user to run `unblock` after repair.
- `unblock` is the only command that can inspect repaired evidence and mutate a
  blocked/failed state into a resumable state.

Pros:

- Clear operator intent and safer automation.
- Avoids surprising mutations from a command that used to be report-only.
- Lets terminal business states such as `StrategicInvestigationRequired` remain
  report-only unless an explicit continuation command is later introduced.

Cons:

- Requires CLI parsing changes and migration of tests that assume the single-command
  interface.
- Requires users and scripts to learn the new verb.

This is the most robust long-term design.

### Option 2: Keep the single command, but add automatic unblock review

Keep the current `LoopRelay.Roadmap.CLI <repo>` shape. In
`RoadmapStateMachine.RunAsync`, before the early `PreflightRequirement.None` return,
detect repairable states with non-empty `TransitionIntent` and run a dedicated
unblock planner.

Suggested flow:

1. If the persisted state is `EvidenceBlocked`, `ExecutionBlocked`, or `Failed`,
   check whether `TransitionIntent.Intent` is a known repairable intent.
2. Run project-context preflight.
3. Evaluate intent-specific repair checks.
4. If checks pass, save an `UnblockReviewed` transition/journal event and dispatch to
   a safe state.
5. If checks fail, keep the state report-only and update blocker text with the exact
   unresolved condition.

Pros:

- Preserves the existing CLI shape and makes the current "rerun" text mostly true.
- Smaller user-facing change.

Cons:

- A rerun can mutate state, which may surprise operators who expected status-only
  behavior for blocked states.
- Needs strict idempotency and evidence hashing to avoid looping or repeatedly
  rewriting blockers.

This is a pragmatic short-term path if command verbs are not desired yet.

### Option 3: Implement targeted unblock handlers first

Add a small `RoadmapUnblockPlanner` but initially support only the safest, highest
value intents:

- `ResolveBlocker` for fresh preflight blockers: rerun Project Context preflight and,
  if it passes, move to `CoreReady` or fresh initialization.
- `ResolveMalformedExecutionOutput`: re-read the execution evidence path, re-run
  `RoadmapExecutionOutcomeInterpreter`, and route to execution continuation,
  execution blocker, completion certification, or remain blocked.
- `ResolveInvalidCompletionCertification`: re-read the evaluation evidence, re-run
  `CompletionEvaluationParser` and `CompletionCertificationPolicy`, and route through
  `CompletionCertificationRouter`.
- `RepairExecutionRuntimeFailure`: verify execution prerequisites and bridge
  readiness, then return to `ExecutionLoop` or `ExecutionPromptReady`.

Leave artifact-promotion and split-promotion blockers report-only until their repair
semantics are explicit.

Pros:

- Lower blast radius.
- Fixes the most obvious "I repaired the named artifact and reran" cases.
- Builds reusable unblock infrastructure before tackling ambiguous promotion cases.

Cons:

- Some persisted "rerun" guidance remains false.
- Requires per-intent messaging so unsupported blockers do not look repairable.

This is a good incremental implementation plan.

### Option 4: Documentation-only/status-only correction

Do not add automatic recovery. Instead, change all persisted blocker text to stop
claiming that rerun will recover. For each blocker, write the exact supported manual
operation, such as editing `.agents/state.md`, deleting state, regenerating a prompt
artifact, or manually promoting an active epic.

Pros:

- Smallest code change.
- Avoids new state-machine mutation paths.

Cons:

- Leaves recovery manual and fragile.
- Normalizes bypassing provenance and safety checks.
- Does not help unattended automation.

This is only acceptable if the product intentionally does not support blocked-state
recovery.

### Option 5: Treat blocked states as normal resumable states

Remove `EvidenceBlocked`, `ExecutionBlocked`, and/or `Failed` from report-only
classification and let `RoadmapResumePlanner` handle them.

Pros:

- Conceptually simple.
- Reuses existing preflight and resume planner structure.

Cons:

- Unsafe as a standalone fix. Blocked states do not all have the same recovery
  semantics, and `DispatchState` often points back to the blocked state.
- Could accidentally rerun prompts, overwrite evidence, or bypass the reason the
  state was blocked.

This option should only be used with explicit per-intent guards, which makes it
effectively a variation of Option 2 or Option 3.

## Recommended approach

Use Option 1 as the target design and Option 3 as the first implementation slice.

The first slice should introduce the unblock planner, journal evidence, and CLI
surface without attempting every blocker family. Unsupported intents should remain
read-only but should say exactly why no automatic unblock is available.

## Implementation details to preserve safety

The unblock planner should record a durable review event before mutating state:

- Event name: `UnblockReviewed`.
- Reviewed current state.
- Reviewed transition intent.
- Reviewed blocker rows.
- Reviewed evidence paths and content hashes.
- Project Context hash used for the unblock decision.
- Projection manifest freshness summary.
- Chosen recovery action and rationale.

Intent handlers should be deterministic:

- `ResolveBlocker`: rerun preflight and only recover fresh initialization blockers
  when Project Context and roadmap source are valid.
- `ResolveMalformedExecutionOutput`: require exactly one execution evidence path,
  require the evidence file to exist, reparse disposition, and route using the same
  execution disposition protocol as a fresh execution result.
- `ResolveInvalidCompletionCertification`: require the evaluation artifact, reparse
  it, validate policy, and route through the existing completion router.
- `RepairExecutionRuntimeFailure`: verify active epic, milestone specs, operational
  context, execution prompt, and compatibility artifacts before returning to an
  execution state.
- `ResolveArtifactPromotionBlocker`: do not treat edited blocker evidence as an
  active epic. Either rerun the original authoring prompt from `LastTransition.From`
  after validating input freshness, or require an explicit human-promoted artifact.
- `ResolveSplitEpicBlocker`: do not write child files from previously rejected output
  unless the split bundle is revalidated from durable evidence and all target paths
  still pass `BundleFileExtractor` and `SplitEpicBundleInterpreter` checks.
- `ResolveTransitionFailure`: use `LastTransition.Prompt`, `LastTransition.From`,
  `LastTransition.To`, and current contract input readiness to decide whether a retry
  is safe; do not use `TransitionIntent.DispatchState` alone.

State mutation should clear or supersede blockers only after a successful review.
Failed unblock attempts should keep the previous blocker evidence and append a new
review note rather than overwriting the original failure context.

## Acceptance criteria

- `status` or report-only reruns clearly distinguish unresolved blocked states from
  repairable blocked states.
- A fresh repository that was blocked by missing Project Context can resume after
  Project Context and roadmap source are added, without manual state edits.
- A corrected malformed execution disposition can be reparsed and routed without
  rerunning unrelated roadmap selection or promotion steps.
- A corrected invalid completion certification can be reparsed and routed without
  mutating active-epic lifecycle before policy validation passes.
- A repaired execution runtime failure can return to execution only after active epic,
  specs, operational context, execution prompt, and compatibility artifacts pass
  readiness checks.
- Unsupported blocked intents remain report-only and explain the exact unsupported
  recovery path.
- Every successful unblock writes an `UnblockReviewed` journal/provenance record with
  reviewed evidence hashes.
- Tests cover that unresolved blocked states remain paused, resolved blocked states
  progress, and repeated unblock attempts are idempotent.
