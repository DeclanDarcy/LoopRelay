# Transition Journal Records Empty Input Hashes

## Audit Status

Verified against the current codebase.

## Finding

`TransitionJournalRecord` has an `InputArtifactHashes` field, but every prompt transition writes an empty dictionary for started, completed, and failed journal records.

Affected code:

- `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs`
- `RunPromptTransitionAsync`

The direct cause is that `RunPromptTransitionAsync` only accepts output paths. It then calls:

```csharp
await HashInputsAsync([])
```

for all three journal events:

- `TransitionStarted`
- `TransitionCompleted`
- `TransitionFailed`

`HashInputsAsync` can hash artifacts when paths are provided, but no call site provides any input paths.

## Additional Verification

The issue is not just a missing argument at one call site. The current shape makes input provenance impossible to recover after the fact:

- `RunPromptTransitionAsync` is called by all runtime prompt transitions with only `outputs`.
- The prompt context builders read durable artifacts before calling `RunPromptTransitionAsync`.
- Some prompt inputs are file sets discovered at runtime, not single constants.
- `PromptContract.RequiredInputs` exists, but it is not used for journaling and is currently incomplete for exact prompt provenance.

Concrete examples:

- `BuildSelectionContextAsync` reads `.agents/core/roadmap-completion-context.md`, the roadmap source, and retired exclusions from `.agents/state.md`.
- `ReadRoadmapSourceAsync` can combine `.agents/roadmap.md` and every `.agents/roadmap/*.md` file, so hashing only `.agents/roadmap.md` is incomplete.
- `BuildCompletionEvaluationContextAsync` reads `.agents/epic.md` plus every `.agents/specs/*.md` file.
- `BuildCompletionUpdateContextAsync` reads the current completion context, active epic, and the latest numbered evaluation evidence path.
- Every runtime context embeds projection content, so the projection file, or an explicit projection hash, is also a real input to every prompt transition.

## Impact

This weakens auditability and reproducibility. When a generated epic, spec bundle, or completion evaluation is questioned, the transition journal cannot answer which exact selection, projection, audit, active epic, specs, completion context, or evaluation evidence caused the prompt result.

It also makes stale-state debugging harder because repeated transitions look identical even if their inputs changed between runs.

There is an additional concurrency/reproducibility problem: if hashes are computed separately for started and completed records, an artifact that changes while a prompt is running could produce different hash sets for the same correlation id. The fix should compute the input snapshot once before the `TransitionStarted` record and reuse it for completed or failed records.

## Input Surface By Transition

Minimum durable inputs to capture:

| Runtime prompt | Durable inputs to hash |
|---|---|
| `CreateRoadmapCompletionContext` | Projection file, or separate projection hash. |
| `SelectNextEpic` | Projection file, `.agents/core/roadmap-completion-context.md`, `.agents/roadmap.md` if present, ordered `.agents/roadmap/*.md`, `.agents/state.md` when retired exclusions are present. |
| `EpicPreparationAudit` | Projection file, `.agents/selection.md`. |
| `RealignEpic` | Projection file, selected audit evidence path, and `.agents/epic.md` if present, otherwise `.agents/selection.md`. |
| `ReimagineEpic` | Projection file, selected audit evidence path, and `.agents/epic.md` if present, otherwise `.agents/selection.md`. |
| `CreateNewEpic` | Projection file, `.agents/selection.md`. |
| `SplitEpic` | Projection file, `.agents/selection.md`. |
| `GenerateMilestoneDeepDivesForEpic` | Projection file, `.agents/epic.md`. |
| `EvaluateEpicCompletionAndDrift` | Projection file, `.agents/epic.md`, ordered `.agents/specs/*.md`. |
| `UpdateRoadmapCompletionContext` | Projection file, `.agents/core/roadmap-completion-context.md`, `.agents/epic.md`, latest evaluation evidence path. |

Artifact hashes alone still do not capture everything. Several prompts instruct the agent to inspect repository reality in read-only mode. If exact audit replay matters, the journal also needs a repository source fingerprint, such as git commit plus dirty diff hash, or a declared statement that repository inspection is outside transition input hashing.

## Solution Options

### Option 1: Add Explicit Input Paths To `RunPromptTransitionAsync`

Change the transition helper to accept input paths:

```csharp
private async Task<string> RunPromptTransitionAsync(
    RoadmapState from,
    RoadmapState to,
    string prompt,
    string projectionPath,
    string projectContext,
    string secondaryInput,
    IReadOnlyList<string> inputPaths,
    IReadOnlyList<string> outputPaths,
    CancellationToken cancellationToken)
```

Each call site passes the durable input paths from the matrix above. `RunPromptTransitionAsync` computes one ordered hash dictionary before appending `TransitionStarted` and reuses it for completed or failed journal records.

Pros:

- Smallest implementation.
- Preserves the current journal schema.
- Easy to test with the existing state-machine tests.

Cons:

- Input lists can drift from the context-builder logic.
- Directory-backed inputs still need helper methods that expand to ordered file paths.
- Does not capture rendered in-memory prompt context unless a separate hash is added.

### Option 2: Add A Structured Transition Input Snapshot

Introduce a small model that separates durable artifact hashes from rendered prompt hashes:

```csharp
internal sealed record TransitionInputSnapshot(
    IReadOnlyDictionary<string, string> ArtifactHashes,
    string ProjectionHash,
    string PromptContextHash,
    string? SecondaryInputHash,
    string? RepositoryFingerprint);
```

Then update `TransitionJournalRecord` to store this snapshot, or add these fields alongside `InputArtifactHashes` for backward compatibility.

Pros:

- Captures the exact rendered context passed to the agent.
- Avoids pretending in-memory strings are artifact paths.
- Can represent repository reality explicitly.

Cons:

- Requires a journal schema change.
- Existing consumers of `TransitionJournalRecord` may need updates.
- Needs a clear policy for repository fingerprints and prompt-content sensitivity.

### Option 3: Make Prompt Contracts The Source Of Truth

Extend `PromptContract` so each runtime prompt can resolve its actual inputs, including dynamic file sets and runtime evidence paths. The state machine asks the contract/resolver for inputs instead of duplicating lists at each call site.

Possible shape:

```csharp
internal interface ITransitionInputResolver
{
    Task<IReadOnlyList<string>> ResolveInputsAsync(
        string runtimePrompt,
        TransitionInputContext context,
        CancellationToken cancellationToken);
}
```

Pros:

- Aligns prompt contracts, journal provenance, and validation.
- Reduces long-term call-site drift.
- Gives one place to encode dynamic inputs such as specs, roadmap files, and latest evaluation evidence.

Cons:

- More infrastructure than the immediate bug requires.
- Existing `PromptContract.RequiredInputs` needs to be corrected and expanded.
- Some inputs are branch-dependent, such as `RealignEpic` using active epic when present and selection otherwise.

### Option 4: Persist The Rendered Runtime Prompt Or Context

Write the exact rendered prompt or runtime context to a content-addressed journal artifact, then store its path and hash in the transition journal.

Example output:

```text
.agents/journal/prompts/{correlationId}.md
```

Pros:

- Best replay/debugging story.
- Captures derived in-memory content exactly.
- Makes prompt diffs straightforward when behavior changes.

Cons:

- More storage.
- May duplicate large context blocks.
- Needs retention and redaction rules if contexts can contain sensitive repository data.

## Recommended Path

Use Option 1 as the narrow fix, but add `PromptContextHash` from Option 2 at the same time if schema compatibility allows it.

That gives immediate non-empty artifact hashes while also covering derived inputs such as:

- absence of optional state,
- rendered retired exclusions,
- the exact ordering and formatting of aggregated file content,
- duplicated `secondaryInput` content in prompts that use it.

If the roadmap CLI is expected to grow more prompt transitions, follow with Option 3 so the input lists live beside the prompt contracts instead of being hand-maintained in state-machine call sites.

## Implementation Notes

- Include `projectionPath` in every transition input set, or add a separate `ProjectionHash` field.
- Do not hash directory paths directly. Expand `.agents/roadmap/*.md` and `.agents/specs/*.md` to ordered file paths before hashing.
- De-duplicate and ordinal-sort input paths before hashing to keep JSON output deterministic.
- Skip missing optional inputs instead of recording empty hashes.
- Hash the content returned by `artifacts.ReadAsync(path)`, matching the current `RoadmapHash.Sha256` helper.
- Compute the input snapshot once before `TransitionStarted`.
- Reuse the exact same snapshot for `TransitionCompleted` and `TransitionFailed`.
- Keep output paths separate from input hashes; outputs are result locations, not causal inputs.

## Acceptance Criteria

- Prompt transition journal records contain non-empty input hashes whenever durable prompt inputs exist.
- Started, completed, and failed records with the same correlation id use the same input hash set.
- Projection content is represented either in `InputArtifactHashes` or in a dedicated projection hash field.
- Directory-backed inputs are expanded to the exact ordered files read by the context builder.
- Missing optional inputs are omitted rather than recorded as empty hashes.
- A changed input artifact changes the recorded hash on the next transition.
- The transition journal can distinguish two runs of the same prompt with different input artifact contents.

## Suggested Tests

- Selection transition records hashes for projection, completion context, roadmap source files, and state when retired exclusions exist.
- Changing roadmap source content changes the selection transition hash.
- Milestone generation records the active epic hash.
- Completion evaluation records hashes for active epic and every generated spec file.
- Completion update records current completion context, active epic, and the selected evaluation evidence hash.
- Failed prompt transition records the same input hashes as its started record.
- Missing optional inputs are omitted from `InputArtifactHashes`.
- Existing `TransitionJournalStore` append/deserialization behavior remains compatible.
