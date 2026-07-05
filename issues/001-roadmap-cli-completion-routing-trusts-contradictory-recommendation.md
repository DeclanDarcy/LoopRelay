# Roadmap CLI completion routing trusts contradictory recommendation

## Audit status

Confirmed on 2026-07-05.

`CompletionEvaluationParser` captures the fields needed to detect an internally inconsistent completion evaluation, but no runtime policy currently validates those fields together. The close routes can still be selected from `Closure Recommendation` alone.

## Finding

`CompletionEvaluationParser.Parse` accepts `Overall Completion Status`, `Overall Drift Classification`, and `Closure Recommendation` as independent allowed values. `CompletionCertificationRouter.Route` then routes solely on `decision.ClosureRecommendation`.

A contradictory evaluation such as:

| Field | Value |
|---|---|
| Overall Completion Status | Not Complete |
| Overall Drift Classification | None |
| Closure Recommendation | Close Epic |

is structurally valid to the parser because each individual value is allowed. The router will choose the `Close Epic` route even though the status says the epic is not complete.

## Impact

The workflow can mark an active epic completed even when the certification artifact states it is not complete or is otherwise inconclusive. On close routes the state machine can:

- run `UpdateRoadmapCompletionContext` from the contradictory evaluation artifact,
- mark `.agents/epic.md` as `Completed` in the artifact lifecycle,
- persist a `Completed` transition back to `SelectNextStrategicInitiative`,
- return `RoadmapOutcome.Completed`.

Because the completion evaluator is an agent output, the router must defend against internally inconsistent table values rather than assuming the final recommendation is coherent. This is a workflow authority boundary, not just a formatting concern.

## Evidence

- `src/CommandCenter.Roadmap.CLI/CompletionEvaluationParser.cs` defines allowed completion statuses, allowed drift classifications, and allowed closure recommendations, then returns a `CompletionEvaluationDecision` containing all three fields. It validates membership in each allowed set, but not cross-field coherence.
- `src/CommandCenter.Roadmap.CLI/CompletionCertificationRouter.cs` defines close routes where `Close Epic` and `Close With Follow-Up` target `SelectNextStrategicInitiative`, use `TransitionStatus.Completed`, return `RoadmapOutcome.Completed`, require a roadmap completion context update, and set the active epic lifecycle to `Completed`.
- `CompletionCertificationRouter.Route` is a dictionary lookup on `decision.ClosureRecommendation`. `decision.OverallCompletionStatus` and `decision.OverallDriftClassification` are not consulted.
- `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs` writes the raw evaluation evidence, parses it, calls `completionRouter.Route(decision)`, optionally updates roadmap completion context, applies `route.ActiveEpicLifecycleState`, persists the route, and returns `route.CliOutcome`.
- `PersistCompletionRouteAsync` includes completion status and drift classification in the routing input snapshot text, but this happens after the route has already been accepted.
- `src/CommandCenter.Roadmap.CLI/PromptContractRegistry.cs` records the allowed completion decisions for `EvaluateEpicCompletionAndDrift`, but it does not record allowed status/drift/recommendation combinations.
- `src/CommandCenter.Core/Prompts/Planning/EvaluateEpicCompletionAndDrift.prompt` asks the agent to emit the relevant fields and a rationale. Prompt wording is not a deterministic runtime guard.

## Existing test gap

- `RoadmapStateMachineCompletionTests.Completion_recommendations_route_as_domain_transitions` varies only `Closure Recommendation`; its helper hardcodes `Overall Completion Status = Functionally Complete` and `Overall Drift Classification = None`.
- `MarkdownParserTests.Completion_parser_parses_closure_recommendation` asserts only the recommendation, even though the parser returns status and drift as well.
- Other roadmap state-machine tests use `Partially Complete` with `Continue Epic`; they do not exercise contradictory close recommendations.
- There is no test that proves `Not Complete + Close Epic`, `Partially Complete + Close Epic`, `Inconclusive + Close With Follow-Up`, or `Unknown drift + Close Epic` is rejected before lifecycle mutation.

## Failure mechanics

For `Overall Completion Status = Not Complete` and `Closure Recommendation = Close Epic`:

1. `CompletionEvaluationParser.Parse` succeeds because both values are individually allowed.
2. `CompletionCertificationRouter.Route` returns the `Close Epic` route.
3. `RunCompletionCertificationAsync` calls `UpdateRoadmapCompletionContextAsync` because the route requires a completion-context update.
4. The active epic lifecycle is upserted as `Completed`.
5. `PersistCompletionRouteAsync` saves a completed transition to `SelectNextStrategicInitiative`.
6. The CLI returns `RoadmapOutcome.Completed`.

The raw evaluation evidence is preserved, but preservation alone does not stop the lifecycle transition.

## Solution options

### Option A - Minimal fail-closed close-route guard

Add a guard before close-route side effects:

- `Close Epic` and `Close With Follow-Up` require `Overall Completion Status` to be `Fully Complete` or `Functionally Complete`.
- Optionally require `Overall Drift Classification` to be `None` or `Positive` for `Close Epic`.
- Any violation is persisted as invalid completion certification output and pauses the workflow.

Pros:

- Smallest change.
- Directly closes the dangerous false-completion path.
- Can reuse the existing evidence-blocked pattern from malformed execution output.

Cons:

- Does not fully define semantics for `Continue Epic`, `Reopen Epic`, or `Gather More Evidence`.
- Leaves routing as mostly recommendation-driven.
- Future recommendations could repeat the same weakness.

### Option B - Full completion routing policy table

Move routing from a single-field lookup to a policy table over the full `CompletionEvaluationDecision`.

Define allowed combinations of:

- overall completion status,
- drift classification,
- closure recommendation.

A candidate starting policy:

| Recommendation | Completion status allowed | Drift allowed | Notes |
|---|---|---|---|
| Close Epic | Fully Complete, Functionally Complete | None, Positive | No blocking residual work and no unresolved negative drift. |
| Close With Follow-Up | Fully Complete, Functionally Complete | None, Positive, Mixed | Follow-up must be non-blocking; decide explicitly whether Negative drift can ever close with follow-up. |
| Continue Epic | Partially Complete, Not Complete | None, Positive, Negative, Mixed | Implementation still needs work under the current epic. |
| Reopen Epic | Partially Complete, Not Complete, Inconclusive, or completed statuses with Negative/Mixed drift | Negative, Mixed, Unknown | Use when the epic needs preparation/audit rather than straight execution continuation. |
| Gather More Evidence | Inconclusive | Unknown, Mixed, Negative | Also consider parsing `Evidence Strength` so weak/unclear evidence can route here deterministically. |

Pros:

- Makes completion routing an explicit protocol.
- Covers all recommendations, not just closure.
- Gives tests a compact exhaustive matrix.
- Makes future route additions fail until policy is updated.

Cons:

- Requires product/domain decisions for borderline cases such as `Functionally Complete + Negative drift`.
- More test cases and maintenance than a close-only guard.

### Option C - Typed parser plus validated routing result

Convert the parsed fields to enums and make the router return a typed result:

- `CompletionStatus`
- `DriftClassification`
- `ClosureRecommendation`
- `CompletionRouteResult.Valid(route)`
- `CompletionRouteResult.Invalid(reason, decision)`

`RunCompletionCertificationAsync` would branch on the result. Invalid results should call a new persistence path such as `PersistInvalidCompletionEvaluationAsync`.

That invalid path should:

- leave `.agents/epic.md` in `Executing` or `Ready`, never `Completed`,
- not call `UpdateRoadmapCompletionContextAsync`,
- keep the raw evaluation evidence path,
- save `RoadmapState.EvidenceBlocked` with `TransitionStatus.Paused`,
- set an intent such as `ResolveInvalidCompletionEvaluation`,
- return `RoadmapOutcome.Paused`.

Pros:

- Removes repeated string comparisons from policy code.
- Provides a clean place to explain rejected combinations.
- Aligns with existing malformed execution output handling.

Cons:

- Broader code churn than a string policy.
- Requires touching tests and any helper constructors for `CompletionEvaluationDecision`.

### Option D - Extend prompt contracts with combination policy

Extend `PromptContractRegistry` or an adjacent contract model to expose valid completion combinations, not only allowed recommendation strings. Emit those combinations into `.agents/contracts/prompt-contracts.md` so the prompt contract and router policy stay visible together.

Pros:

- Makes the runtime contract auditable.
- Helps prevent prompt text, parser behavior, and router behavior from diverging.

Cons:

- This should supplement runtime validation, not replace it.
- It adds contract surface area that must be kept stable.

### Option E - Prompt-only mitigation

Tighten `EvaluateEpicCompletionAndDrift.prompt` to say that close recommendations must not be used for incomplete statuses.

Pros:

- Low effort.
- May reduce malformed agent output frequency.

Cons:

- Not sufficient as a fix. The runtime must still reject contradictory output because the evaluator is model-generated and semistructured.

## Recommended path

Implement Option B with Option C's typed result shape. Option A can be used as an immediate safety patch if the broader matrix needs domain review, but the final fix should make every recommendation/status/drift combination either accepted or explicitly rejected.

Also parse `Evidence Strength` if `Gather More Evidence` policy needs to distinguish `Weak` or `Unclear` evidence from a true completion/drift finding.

## Acceptance criteria

- `Not Complete + Close Epic` and `Partially Complete + Close Epic` pause as invalid completion certification output and do not update roadmap completion context.
- `Inconclusive + Close Epic` and `Inconclusive + Close With Follow-Up` pause unless the explicit policy allows them.
- Invalid completion combinations preserve the raw evaluation artifact and put the evidence path in the transition intent.
- Invalid completion combinations do not mark `.agents/epic.md` as `Completed`.
- Valid close routes continue to update roadmap completion context, set the active epic lifecycle to `Completed`, persist routing evidence, and return `RoadmapOutcome.Completed`.
- Valid non-close routes continue to route to execution, preparation audit, or evidence gathering as today.
- Route tests cover every allowed combination and representative rejected combinations for each recommendation.
- Parser tests assert status, drift, and recommendation, not just recommendation.

## Non-goals

- Do not solve stale post-close selection reuse here; that is tracked separately in `issues/004-roadmap-cli-stale-selection-replayed-after-close.md`.
- Do not solve execution disposition `Next Step` coherence here; that is tracked separately in `issues/002-roadmap-cli-execution-disposition-next-step-ignored.md`.
