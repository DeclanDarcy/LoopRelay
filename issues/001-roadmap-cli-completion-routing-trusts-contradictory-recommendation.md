# Roadmap CLI completion routing trusts contradictory recommendation

## Finding

`CompletionEvaluationParser` parses `Overall Completion Status`, `Overall Drift Classification`, and `Closure Recommendation`, but `CompletionCertificationRouter` routes solely on `Closure Recommendation`. A contradictory evaluation such as `Overall Completion Status = Not Complete` and `Closure Recommendation = Close Epic` will close the epic.

## Impact

The workflow can mark an active epic completed even when the certification artifact states it is not complete. Because the completion evaluator is an agent output, the router must defend against internally inconsistent table values rather than assuming the final recommendation is coherent.

## Evidence

- `CompletionEvaluationParser.Parse` returns all three fields.
- `CompletionCertificationRouter.Route` looks up only `decision.ClosureRecommendation`.
- Existing tests include `Partially Complete` with arbitrary recommendations in helper output, which demonstrates the router currently tolerates inconsistent status/recommendation pairs.

## Proposal

Move completion routing from a single-field lookup to a policy table over the full evaluation decision.

A robust approach:

1. Define allowed combinations of:
   - overall completion status,
   - drift classification,
   - closure recommendation.
2. Reject or evidence-block contradictory combinations before any lifecycle change. Examples:
   - `Close Epic` and `Close With Follow-Up` require `Fully Complete` or `Functionally Complete`.
   - `Continue Epic` requires `Partially Complete`, `Not Complete`, or `Inconclusive`.
   - `Reopen Epic` requires negative/mixed drift or a completion status below functionally complete.
   - `Gather More Evidence` is valid for `Inconclusive` or `Unknown` drift.
3. Return a typed routing result such as `CompletionRouteResult.Valid(route)` or `CompletionRouteResult.Invalid(reason)` instead of throwing only for missing route keys.
4. Persist invalid combinations as evidence-blocked certification output, preserving the raw evaluation artifact.
5. Add parameterized tests for every allowed route and several contradictory combinations.

This turns the certification boundary into a consistency validator instead of a string dispatcher.

## Acceptance criteria

- A completion evaluation cannot close an epic unless its completion status supports closure.
- Contradictory evaluation output is persisted as evidence and pauses the workflow.
- Route tests cover both accepted and rejected status/drift/recommendation combinations.
