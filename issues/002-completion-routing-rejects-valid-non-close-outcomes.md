# Completion Routing Rejects Valid Non-Close Outcomes

## Audit Status

Verified.

`EvaluateEpicCompletionAndDrift` is designed to return five valid closure recommendations, but `RoadmapStateMachine.RunExecutionAndCertificationAsync` only treats two of them as normal control flow. The other three are parsed as valid decisions, written to evaluation evidence and the decision ledger, and then converted into a generic `RoadmapStepException`.

## Verified Evidence

| Area | Evidence | What It Confirms |
|---|---|---|
| Runtime prompt | `src/CommandCenter.Core/Prompts/Planning/EvaluateEpicCompletionAndDrift.prompt` | The required output explicitly permits `Close Epic`, `Close With Follow-Up`, `Continue Epic`, `Reopen Epic`, and `Gather More Evidence`. |
| Projection prompt | `src/CommandCenter.Core/Prompts/Projections/ProjectionForEvaluateEpicCompletionAndDrift.prompt` | Closure recommendation semantics are part of the certification projection, not an incidental parser detail. |
| Prompt contract | `src/CommandCenter.Roadmap.CLI/PromptContractRegistry.cs` | The `EvaluateEpicCompletionAndDrift` contract lists all five values as allowed decisions. |
| Parser | `src/CommandCenter.Roadmap.CLI/CompletionEvaluationParser.cs` | The parser accepts all five values and rejects unsupported values. |
| Roadmap design | `plan.md` | The intended routing table maps close outcomes to completion update, `Continue Epic` to `ExecutionLoop`, `Reopen Epic` to `EpicPreparationAudit`, and `Gather More Evidence` to `EvidenceBlocked`. |
| State model | `src/CommandCenter.Roadmap.CLI/RoadmapState.cs` | The destination states required by the design already exist. |
| Invariants | `src/CommandCenter.Roadmap.CLI/InvariantValidator.cs` | `ExecutionLoop` is already treated like `ExecutionPromptReady` for execution prerequisite validation. |
| Current implementation | `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs` | `RunExecutionAndCertificationAsync` throws when the recommendation is not `Close Epic` or `Close With Follow-Up`. |
| Generic catch path | `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs` | `RunAsync` catches that exception, writes a generic `EvidenceBlocked` state via `WriteBlockedStateAsync`, logs an error, and returns `RoadmapOutcome.Failed`. |

## Current Behavior

| Closure Recommendation | Current Behavior | Problem |
|---|---|---|
| `Close Epic` | Runs `UpdateRoadmapCompletionContext`, writes updated completion context, marks active epic `Completed`, returns `RoadmapOutcome.Completed`. | This is the only fully implemented intended path. |
| `Close With Follow-Up` | Same as `Close Epic`; follow-up detail is only retained inside the evaluation evidence. | Acceptable for MVP, but it does not explicitly distinguish follow-up routing or ledger semantics. |
| `Continue Epic` | Writes numbered evaluation evidence, appends the evaluation decision to the decision ledger, throws `RoadmapStepException`, then top-level handling writes a generic blocker and returns `Failed`. | Valid certification output becomes a failed CLI run instead of a deliberate return to execution. |
| `Reopen Epic` | Same generic failure path as `Continue Epic`. | The audit/preparation route is lost, and the evaluation evidence is not passed forward as audit input. |
| `Gather More Evidence` | Ends in `EvidenceBlocked`, but only through the generic exception handler. | The terminal state is superficially close to the desired state, but the transition, blocker details, required next step, and process outcome are generic rather than route-specific. |

## Impact

The completion certification prompt is supposed to prevent false closure. When it says the epic should not close, that is an expected routing decision, not an exceptional runtime failure.

The current implementation has these effects:

- `Continue Epic` cannot route back to execution even though the state enum and invariant validator already support `ExecutionLoop`.
- `Reopen Epic` cannot route back through `EpicPreparationAudit`.
- `Gather More Evidence` loses the specific evidence path and required evidence request behind a generic `RoadmapStateMachine` blocker.
- `RoadmapOutcome.Failed` is returned for expected certification outcomes, so the CLI exits with failure semantics for normal non-close decisions.
- The latest evaluation path is written, but the subsequent generic blocked state overwrites the last-transition output in `.agents/state.md`.
- The decision ledger records the certification recommendation, but not a distinct routing decision.
- The transition journal records successful prompt transitions with `ParserDecision = None`, so routing observability lives only in the ledger.

The most dangerous case is `Continue Epic`: implementation may be meaningfully partial, but the state machine reports a failed run instead of preserving the evaluation as the next execution input.

## Design Constraints Found During Audit

- `RunAsync` does not currently resume from the persisted roadmap state. It always runs core preflight, ensures completion context, selects the next initiative, prepares an epic, generates execution context, and then executes. Saving `ExecutionLoop` alone is therefore not enough to make a future CLI invocation resume execution.
- `RoadmapStateStore.LoadAsync` only restores the current state and retired epic exclusions. It does not restore last evaluation evidence, last transition output, blockers, or next-transition intent.
- `RoadmapPromptContextBuilder.BuildCompletionUpdateContextAsync` can load a numbered completion evaluation for the close path, but there is no equivalent context builder for feeding completion evaluation evidence into a reopened audit or continued execution prompt.
- `ArtifactLifecycleState` already has `Blocked`, so an explicit evidence-gathering route can mark the active epic or evaluation evidence without adding a new lifecycle value.
- `RoadmapOutcome` has `Completed`, `Paused`, `PreflightBlocked`, `Failed`, and `Cancelled`. A fix must decide whether user-resolvable non-close routes return `Paused` or keep `Failed` for CI-style signaling.

## Solution Options

### Option 1: Minimal Explicit Routing Patch

Replace the binary close-or-throw check with a switch over `decision.ClosureRecommendation`.

Suggested behavior:

| Recommendation | State | CLI Outcome | Lifecycle | Completion Context Update |
|---|---|---|---|---|
| `Close Epic` | `SelectNextStrategicInitiative` after update | `Completed` | active epic `Completed` | Yes |
| `Close With Follow-Up` | `SelectNextStrategicInitiative` after update | `Completed` | active epic `Completed` | Yes |
| `Continue Epic` | `ExecutionLoop` or `ExecutionPromptReady` | `Paused` | active epic remains `Executing` | No |
| `Reopen Epic` | `EpicPreparationAudit` | `Paused` | active epic should remain `Ready` or `Executing` with notes | No |
| `Gather More Evidence` | `EvidenceBlocked` | `Paused` or `Failed` by policy | active epic `Blocked` or `Executing` with blocker notes | No |

Implementation notes:

- Change `RunExecutionAndCertificationAsync` to return `Task<RoadmapOutcome>` or a small routing result instead of `Task`.
- Save route-specific state directly instead of throwing for non-close recommendations.
- Use `RoadmapBlockedArtifact.Render` for `Gather More Evidence` with the numbered evaluation path in `Evidence Path`.
- Append a decision ledger entry for the route, or make the existing completion decision entry clearly serve as the route decision.
- Add `NextTransitions` entries for `CompletionEvaluationAndContextUpdate`, `ExecutionLoop`, and `EpicPreparationAudit`.

Pros:

- Smallest code change.
- Stops treating valid prompt output as failure.
- Preserves the existing prompt contract.

Cons:

- Does not solve durable resume from saved state.
- `Continue Epic` and `Reopen Epic` may only pause with a correct state rather than automatically continue.

### Option 2: Table-Driven Completion Router

Introduce a small immutable route table and a dedicated router method.

Example shape:

```csharp
private async Task<RoadmapOutcome> RouteCompletionEvaluationAsync(
    CompletionEvaluationDecision decision,
    string evaluationPath,
    ProjectContext projectContext,
    CancellationToken cancellationToken)
```

The route table should define:

- accepted recommendation text
- next state
- whether to run `UpdateRoadmapCompletionContext`
- lifecycle state and notes
- whether to write a blocker artifact
- next valid transitions
- returned `RoadmapOutcome`

Pros:

- Keeps allowed prompt vocabulary, state transition, lifecycle effects, and CLI outcome aligned.
- Makes missing route coverage obvious in tests.
- Avoids another nested switch as completion routing grows.

Cons:

- Slightly more structure than Option 1.
- Still needs separate resume work if automated continuation is required.

### Option 3: Resume-Aware State Machine

Implement full persisted-state dispatch so saved non-close routes are actually actionable on the next CLI run.

Required work:

- Extend `RoadmapStateDocument` to persist route-specific runtime inputs, especially the latest completion evaluation evidence path.
- Extend `RoadmapStateStore.LoadAsync` to restore last transition output, blockers, and route inputs, not only current state and retired exclusions.
- Make `RunAsync` dispatch from loaded state instead of always starting from selection.
- Add context builders that can include completion evaluation evidence when continuing execution or reopening audit.
- Decide whether `Continue Epic` immediately reruns the execution bridge or pauses for operator review.

Pros:

- Most faithful to the roadmap state-machine design.
- Makes `Continue Epic` and `Reopen Epic` operational rather than only descriptive.
- Improves resilience for all future paused states.

Cons:

- Larger blast radius.
- Requires careful migration/compatibility handling for existing `.agents/state.md`.

### Option 4: Explicit Non-Close Blocking MVP

If automatic continuation is out of scope, route all three non-close recommendations to intentional paused states with strong blocker artifacts.

Suggested routes:

- `Continue Epic`: save `ExecutionLoop`, keep the active epic `Executing`, return `Paused`, and write evaluation evidence as the reason execution must continue.
- `Reopen Epic`: save `EpicPreparationAudit`, keep the active epic available, return `Paused`, and write evaluation evidence as the audit input.
- `Gather More Evidence`: save `EvidenceBlocked`, write a blocker with the evaluation path, mark active epic or evaluation evidence `Blocked`, return `Paused` if user action is expected.

Pros:

- Honest MVP if resume is not ready.
- Removes false failure semantics.
- Keeps operators pointed at specific evidence.

Cons:

- The next invocation still will not resume correctly unless Option 3 follows.
- Operators may need manual handling between runs.

### Option 5: Tighten Contract To Binary Close/Block

Reduce `EvaluateEpicCompletionAndDrift` to only return close outcomes plus a single blocking output until the state machine can route non-close decisions.

Required work:

- Remove `Continue Epic`, `Reopen Epic`, and `Gather More Evidence` from the prompt contract, parser, projection prompt, planning prompt, plan, and tests.
- Replace them with one explicit blocked output shape.

Pros:

- Aligns implementation with a simpler MVP.
- Reduces routing surface temporarily.

Cons:

- Contradicts the current roadmap design and prompt semantics.
- Loses important distinction between more implementation, re-audit, and evidence gathering.
- Not recommended unless product direction has intentionally changed.

## Recommended Path

Use Option 2 now, with Option 4 behavior for routes that cannot yet be automated. Follow with Option 3 when durable resume becomes a priority.

This gives the CLI correct semantics immediately:

- close outcomes close
- continuation routes to execution state
- reopen routes to audit state
- evidence gaps route to a specific blocker
- no valid certification recommendation is represented as an unexpected failure

It also leaves a clean route table for later resume-aware dispatch.

## Acceptance Criteria

- Every allowed `Closure Recommendation` has an explicit route.
- `RunExecutionAndCertificationAsync` no longer throws merely because a valid non-close recommendation was returned.
- `UpdateRoadmapCompletionContext` runs only for `Close Epic` and `Close With Follow-Up`.
- `Close Epic` and `Close With Follow-Up` mark the active epic `Completed` only after completion context update succeeds.
- `Continue Epic` preserves the evaluation path, does not mark the active epic completed, and saves an execution-ready state.
- `Reopen Epic` preserves the evaluation path, does not mark the active epic completed, and saves an audit/preparation state.
- `Gather More Evidence` writes a route-specific blocked artifact with the evaluation path and required next step.
- Decision ledger or transition journal records the route decision with the completion evaluation evidence path.
- `.agents/state.md` exposes a meaningful next transition for each route.
- CLI outcome semantics are deliberate and documented for non-close routes.

## Suggested Tests

- Parser accepts all five `Closure Recommendation` values and rejects unknown values.
- `Close Epic` updates roadmap completion context and marks the active epic completed.
- `Close With Follow-Up` updates roadmap completion context, records evaluation evidence, and marks the active epic completed.
- `Continue Epic` routes to `ExecutionLoop` or `ExecutionPromptReady`, returns the chosen non-failure outcome, and does not run `UpdateRoadmapCompletionContext`.
- `Continue Epic` leaves the active epic not completed and retains the evaluation path in state, ledger, or blocker metadata.
- `Reopen Epic` routes to `EpicPreparationAudit`, returns the chosen non-failure outcome, and does not run `UpdateRoadmapCompletionContext`.
- `Gather More Evidence` enters `EvidenceBlocked` through an explicit route, writes a blocker whose `Evidence Path` is the numbered completion evaluation, and does not run `UpdateRoadmapCompletionContext`.
- Unknown parser output still fails through `MarkdownParseException` or the existing blocked transition path.
- Route table coverage test fails if a parser-allowed recommendation has no route.
- `NextTransitions` includes meaningful entries for routed non-close states.
