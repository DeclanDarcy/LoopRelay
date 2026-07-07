# Bug: Milestone generation can fail while state.json records MilestoneSpecsReady

## Summary

The roadmap CLI can persist `.agents/state.json` as if milestone-spec generation completed successfully even when the required milestone-spec artifacts were never created.

Observed in:

- Repository: `C:\kernritsu\SemanticCompiler`
- State file: `C:\kernritsu\SemanticCompiler\.agents\state.json`
- Current LoopRelay workspace: `C:\kernritsu\LoopRelay`

The persisted state claims the workflow reached `MilestoneSpecsReady`, but the authoritative output directory `.agents/specs` is missing. This forces the operator to manually repair state in addition to fixing the actual milestone-generation failure.

## Observed bad state

`C:\kernritsu\SemanticCompiler\.agents\state.json` contains:

```json
{
  "CurrentState": "MilestoneSpecsReady",
  "LastTransition": {
    "From": "ActiveEpicReady",
    "To": "MilestoneSpecsReady",
    "Prompt": "GenerateMilestoneDeepDivesForEpic",
    "Projection": ".agents/projections/milestone-deep-dive.md",
    "Output": ".agents/specs",
    "Decision": "Completed",
    "Status": "Completed"
  },
  "Blockers": [],
  "TransitionIntent": {
    "Intent": "None",
    "DispatchState": "RoadmapCompletionContextReady",
    "EvidencePaths": []
  },
  "NextValidTransitions": [
    "GenerateOperationalContext"
  ]
}
```

But the required outputs are not present:

- `C:\kernritsu\SemanticCompiler\.agents\specs` does not exist.
- `C:\kernritsu\SemanticCompiler\.agents\execution-preparation-manifest.json` does not exist.
- `C:\kernritsu\SemanticCompiler\.agents\execution-prompt.md` is missing, which is expected before later execution readiness, but the state already advertises `GenerateOperationalContext` as the next transition.

There are files under `.agents/milestones`, but those are not the authoritative milestone specs for the roadmap CLI. In `RoadmapArtifactPaths`, the state-machine source milestone specs are `.agents/specs/*.md`; `.agents/milestones` is the later execution compatibility output.

## Why this is wrong

`MilestoneSpecsReady` should mean all of these are true:

1. `GenerateMilestoneDeepDivesForEpic` produced a valid bundle with `# FILE: .agents/specs/*.md` markers.
2. The bundle extractor wrote the extracted files under `.agents/specs`.
3. `.agents/specs/bundle-manifest.md` was written.
4. Each spec was marked ready in artifact lifecycle state.
5. `ExecutionPreparationProvenanceService.RecordMilestoneSpecsAsync(...)` recorded fresh provenance.
6. `InvariantValidator.ValidateAsync(RoadmapState.MilestoneSpecsReady, ...)` passed.

In the observed repository, at least items 2 and 5 are false. Therefore `MilestoneSpecsReady` is not a truthful persisted state.

## Likely root cause

The milestone-generation path uses `RunPromptTransitionAsync(...)` with the final target state `MilestoneSpecsReady` before bundle extraction and validation occur.

Relevant code:

- `src\LoopRelay.Roadmap.Cli\RoadmapStateMachine.cs`
- `GenerateMilestoneSpecsAsync(...)`
- `RunPromptTransitionWithCompletionAsync(...)`

Current milestone path:

```csharp
string output = await RunPromptTransitionAsync(
    RoadmapState.ActiveEpicReady,
    RoadmapState.MilestoneSpecsReady,
    runtimePrompt,
    projection.Definition.ProjectionPath,
    context,
    string.Empty,
    [RoadmapArtifactPaths.SpecsDirectory],
    cancellationToken);

BundleExtractionResult bundle = bundleExtractor.Extract(output);
if (bundle.IsBlocked || bundle.Files.Count == 0)
{
    throw new RoadmapStepException(bundle.BlockedReason ?? "Milestone deep dive output did not contain specs.");
}

await bundleExtractor.WriteExtractedFilesAsync(artifacts, bundle);
await bundleManifestWriter.WriteAsync(...);
await executionPreparation.RecordMilestoneSpecsAsync(...);
InvariantValidationResult invariant = await invariantValidator.ValidateAsync(...);
```

The generic transition helper records the target state as completed immediately after the agent turn returns:

```csharp
string output = await promptRunner.RunRuntimePromptAsync(...);
await journalStore.AppendAsync(new TransitionJournalRecord(... "TransitionCompleted" ...));
await SaveStateAsync(
    to,
    TransitionStatus.Completed,
    from,
    to,
    prompt,
    projectionPath,
    string.Join(", ", outputs),
    "Completed",
    started,
    completed,
    null,
    null);
return new PromptTransitionCompletion(...);
```

For `GenerateMilestoneDeepDivesForEpic`, this is premature. The prompt returning text is not the same thing as milestone specs being materialized and validated.

If bundle extraction, file writing, provenance recording, or invariant validation fails after this point, the catch path in `RunAsync(...)` reports an ephemeral blocker but leaves the already-advanced durable state in place.

## Why the current behavior is painful

This is not just a noisy blocker. It creates contradictory state:

- The state file says the workflow can continue from `MilestoneSpecsReady`.
- The filesystem says milestone specs do not exist.
- Resume planning will likely fail on missing or stale milestone spec provenance.
- The operator has to repair `state.json` manually even after fixing the real issue.

That defeats the purpose of durable workflow state. Durable state should either be true or explicitly blocked with evidence.

## Related prior failure in the same repo

The transition journal also records an earlier `SelectNextEpic` failure caused by app-server input size:

```text
Input exceeds the maximum length of 1048576 characters.
actual_chars: 1602571
```

That earlier failure was later resolved by changing the roadmap inputs and rerunning selection. It is separate from the current milestone-generation issue, but it demonstrates the same operator concern: runtime/prompt failures can leave the user navigating state-machine recovery instead of just fixing the source condition.

## Expected behavior

A prompt turn should not advance the durable workflow to a final domain state until all post-processing gates for that transition have succeeded.

For milestone generation specifically:

- If the agent turn fails before producing output, preserve a recoverable failure state with transition context.
- If the agent turn succeeds but the output has no valid `.agents/specs/*.md` bundle, do not persist `MilestoneSpecsReady`.
- If extraction or validation fails, either:
  - keep the workflow at `ActiveEpicReady` with a clear transient error, or
  - persist `EvidenceBlocked` with a specific recovery intent and evidence containing the raw failed output.
- Only save `MilestoneSpecsReady` after `.agents/specs` files, bundle manifest, lifecycle entries, execution-preparation provenance, and invariants are all valid.

## Recommended fix direction

Separate "prompt turn completed" from "domain transition completed".

One practical implementation:

1. Add or reuse a helper that runs the prompt and records `PromptCompleted` without saving the final target state as `Completed`.
2. Use that helper for transitions that require post-processing, especially:
   - `GenerateMilestoneDeepDivesForEpic`
   - any bundle-producing prompt
   - any prompt whose output must be promoted, parsed, or validated before becoming authoritative
3. In `GenerateMilestoneSpecsAsync(...)`, save `MilestoneSpecsReady` only after:
   - bundle extraction succeeds
   - extracted files are written
   - bundle manifest is written
   - lifecycle is updated
   - execution-preparation provenance is recorded
   - invariant validation passes
4. On post-processing failure, persist a truthful state.

Suggested blocked-state design:

```text
CurrentState: EvidenceBlocked
LastTransition:
  From: ActiveEpicReady
  To: MilestoneSpecsReady
  Prompt: GenerateMilestoneDeepDivesForEpic
  Output: <path to raw failed output evidence or .agents/specs if no raw evidence is available>
  Decision: Milestone Spec Generation Failed
  Status: Failed or Paused
TransitionIntent:
  Intent: ResolveMilestoneSpecGenerationFailure
  DispatchState: EvidenceBlocked
  EvidencePaths:
    - <raw output evidence path>
```

If a new unblock handler is not implemented yet, the state should still be truthful and should preserve evidence. Avoid `Intent: None` for a failed post-processing gate.

## Test coverage to add

Add a regression test around `GenerateMilestoneDeepDivesForEpic` where the agent returns text without any valid `# FILE: .agents/specs/*.md` markers.

Assertions:

- Outcome is failed or paused according to the chosen recovery design.
- `state.CurrentState` is not `MilestoneSpecsReady`.
- `state.LastTransition.Status` is not `Completed`.
- `.agents/specs` is not treated as ready.
- `TransitionIntent` is not stale `None`.
- If blocked persistence is chosen, evidence paths include the raw failed output or a generated blocker artifact.

Add another regression test where extraction succeeds but invariant validation fails.

Assertions:

- The state does not remain as a successful `MilestoneSpecsReady` transition.
- Validator evidence is preserved.
- The journal records the failure after prompt completion and before any final successful domain transition.

## Existing tests that should be revisited

`RoadmapFailurePersistenceTests.Prompt_transition_failures_are_owned_by_the_transition_layer` covers agent-turn failures, but not successful agent turns followed by failed post-processing.

`Invariant_failure_preserves_validator_evidence_state_and_journal` covers an invariant failure after milestone bundle output. Keep that behavior, but verify there is no moment where final `MilestoneSpecsReady` remains persisted if later invariant handling fails.

## Concrete SemanticCompiler repair note

For the observed `SemanticCompiler` repo, the state is ahead of artifacts. A correct repair should regenerate milestone specs into:

```text
.agents/specs/*.md
```

and recreate execution-preparation provenance, or roll the state back to a truthful pre-milestone state before rerunning milestone generation.

Manual edits to `state.json` should not be the normal recovery path. The CLI should make this state impossible or provide an explicit recovery command that validates the repaired artifacts before advancing.
