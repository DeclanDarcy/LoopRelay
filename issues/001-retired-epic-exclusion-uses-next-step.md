# Retired Epic Exclusion Stores Next Step Instead Of Epic Identity

## Verification Status

Confirmed, with one important expansion: the retire branch first records the wrong value, then the outer failure handler can overwrite the saved exclusion entirely.

The original bug is real:

- `RoadmapStateMachine.AuditAndPrepareExistingEpicAsync` appends `decision.RecommendedNextStep` to `RetiredEpicExclusions` when `Disposition == "Retire"` (`src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:161`).
- `EpicPreparationAuditParser` restricts `Recommended Next Step` to workflow actions, including `Retire Epic`, not selected epic identity (`src/CommandCenter.Roadmap.CLI/EpicPreparationAuditParser.cs:13`).
- `RoadmapPromptContextBuilder.BuildSelectionContextAsync` renders retired exclusions directly into the next selection prompt (`src/CommandCenter.Roadmap.CLI/RoadmapPromptContextBuilder.cs:9`).

The durable behavior is worse than the original issue stated:

- After saving the malformed exclusion, the retire branch throws `RoadmapStepException` (`src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:166`).
- `RunAsync` catches that exception and calls `WriteBlockedStateAsync` (`src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:86`).
- `WriteBlockedStateAsync` calls `SaveStateAsync(..., [], blockers)` and therefore rewrites `.agents/state.md` with an empty retired-exclusion list (`src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:455`).

So the final `.agents/state.md` may not contain `Retire Epic`; it may contain no retired exclusion at all. Either way, the selected epic identity is not durably excluded.

## Code Path

1. `RunAsync` stores the parsed `SelectionDecision` in a local variable (`src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:51`).
2. For `Select Existing Epic`, `RunAsync` calls `AuditAndPrepareExistingEpicAsync(northStar, cancellationToken)` without passing the selection identity (`src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:54`).
3. `SelectNextInitiativeAsync` already parses useful identity fields:
   - `Recommended Initiative`
   - `Initiative Type`
   - `Confidence`
   - `Primary Reason`
   (`src/CommandCenter.Roadmap.CLI/SelectionParser.cs:24`)
4. The audit parser deliberately exposes only disposition routing fields. It does not expose selected epic identity (`src/CommandCenter.Roadmap.CLI/EpicPreparationAuditParser.cs:21`).
5. On retire, the state machine appends `decision.RecommendedNextStep`, then throws (`src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:164`).
6. The top-level `RoadmapStepException` handler rewrites state through the generic blocked-state path, losing the newly calculated exclusion list (`src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:455`).
7. On the next run, `SelectNextInitiativeAsync` loads `existing?.RetiredEpicExclusions` and passes it into selection context (`src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:121`).

## Why `RecommendedNextStep` Cannot Work

`RecommendedNextStep` is a routing command, not an identity. The allowed values are:

- `Realign Epic`
- `Reimagine Epic`
- `Retire Epic`
- `Gather More Evidence`

For a retired epic, the stored value would be `Retire Epic`, which cannot distinguish:

- which roadmap epic was selected
- whether it came from `.agents/roadmap.md` or `.agents/roadmap/*.md`
- the selected epic name or ID
- why this specific epic should be excluded from immediate reselection

The selection prompt already requires `Recommended Initiative` and `Initiative Type` in its summary (`src/CommandCenter.Core/Prompts/Planning/SelectNextEpic.prompt:498`). For existing roadmap epics, it also asks for `Epic ID` and `Epic Name` in the existing-epic section (`src/CommandCenter.Core/Prompts/Planning/SelectNextEpic.prompt:604`), but the current parser ignores that section.

## Additional Observations

- `RoadmapStateStore` only persists retired exclusions as raw bullet strings (`src/CommandCenter.Roadmap.CLI/RoadmapStateStore.cs:91`).
- `RoadmapStateStore.LoadAsync` only parses those bullet strings back out (`src/CommandCenter.Roadmap.CLI/RoadmapStateStore.cs:150`).
- `RoadmapArtifacts.ReadRoadmapSourceAsync` concatenates roadmap files without adding source-path markers (`src/CommandCenter.Roadmap.CLI/RoadmapArtifacts.cs:36`). If source path is needed, it must be added either to the prompt context or parsed from a stronger prompt output contract.
- There is no state-machine regression test for the retire path. `RoadmapStateMachineEpicPreparationTests` is currently a placeholder, and `rg` only found parser-level coverage for `Retire`.

## Impact

The retire transition is not durable.

If the malformed save were the final state, the next `SelectNextEpic` run would receive `Retire Epic` as an exclusion. That is not actionable and does not prevent selecting the same roadmap epic again.

In the current control flow, the final blocked-state save is likely to remove the exclusion list entirely. The next run can therefore select the same retired epic again with no prompt-level memory of the retirement.

Consequences:

- A strategically obsolete or invalid epic can be repeatedly selected, audited, and retired.
- The CLI reports "Rerun selection with the retired exclusion recorded" even though the final state may not contain that exclusion.
- The state file does not provide a human-actionable retirement record.
- Deduplication is based on raw string equality, which would collapse all retired epics to one value if `Retire Epic` did persist.

## Root Cause

The retire branch conflates three different concepts:

1. The selected initiative identity from `SelectionDecision`.
2. The audit disposition and next routing action from `EpicPreparationAuditDecision`.
3. The durable retry/selection exclusion that should guide the next selection run.

It also treats retirement as an exception flow. That causes the generic blocked-state handler to overwrite domain state that was intentionally saved by the retire branch.

## Solution Options

### Option A - Minimal Identity Fix

Pass the parsed `SelectionDecision` into `AuditAndPrepareExistingEpicAsync` and append a stable string derived from `selection.RecommendedInitiative` plus `selection.InitiativeType`.

Example persisted string:

```text
Existing Roadmap Epic: Epic A
```

Changes:

- Change `AuditAndPrepareExistingEpicAsync` to accept `SelectionDecision selection`.
- In the retire branch, append `FormatRetiredExclusion(selection)`.
- Preserve existing state-store shape as `IReadOnlyList<string>`.
- Ensure the outer catch does not overwrite retired exclusions after the retire transition.

Pros:

- Smallest production change.
- Uses data already parsed today.
- Backward-compatible with existing `.agents/state.md` files.
- Easy to test.

Cons:

- Still stringly typed.
- Does not capture audit rationale, audit evidence path, or retirement time.
- May be ambiguous if two roadmap entries share a name.
- Does not use `Epic ID` from the richer selection output.

### Option B - Parse Existing-Epic Identity From Selection Output

Extend `SelectionParser` and `SelectionDecision` to parse the `## If Existing Roadmap Epic Selected` table when the outcome is `Select Existing Epic`.

Candidate model:

```csharp
internal sealed record SelectionDecision(
    string RecommendedOutcome,
    string RecommendedInitiative,
    string InitiativeType,
    string Confidence,
    string PrimaryReason,
    string? ExistingEpicId,
    string? ExistingEpicName);
```

Changes:

- Parse the existing-epic section fields `Epic ID` and `Epic Name`.
- Require them when `RecommendedOutcome == "Select Existing Epic"`.
- Use those fields for retired exclusions.
- Keep state persistence as strings or combine this with Option C.

Pros:

- Better identity than display name alone.
- Aligns implementation with the existing prompt output contract.
- Still moderate in scope.

Cons:

- Depends on model output consistently filling the optional section.
- Still lacks source path unless the prompt contract is expanded.
- Requires parser and fixture updates.

### Option C - Structured Retired Exclusion Records

Replace raw string exclusions with a domain record and render structured entries into selection context.

Candidate model:

```csharp
internal sealed record RetiredEpicExclusion(
    string InitiativeName,
    string InitiativeType,
    string? EpicId,
    string? EpicName,
    string? SourceReference,
    string Reason,
    string AuditEvidencePath,
    DateTimeOffset RetiredAt);
```

Changes:

- Add structured exclusions to `RoadmapStateDocument`.
- Persist them either in `.agents/state.md` as a parseable table or in a dedicated JSON artifact.
- Render them in `BuildSelectionContextAsync` with explicit labels.
- Deduplicate by stable identity fields, not rationale text.
- Load old bullet-list exclusions safely as legacy records or ignore non-identity workflow values with a warning.

Pros:

- Correct domain model.
- Human-actionable state.
- Supports durable audit traceability.
- Enables better deduplication and future tooling.

Cons:

- Larger migration.
- Requires careful markdown or JSON parsing.
- More tests needed around backward compatibility.

### Option D - Dedicated Runtime-State Artifact

Persist retired exclusions in a dedicated artifact, for example:

```text
.agents/runtime/retired-epic-exclusions.json
```

Keep `.agents/state.md` as a human summary only.

Pros:

- Avoids complex markdown parsing.
- Makes structured state easier to version and validate.
- Keeps state-machine data separate from display state.

Cons:

- Introduces a new runtime artifact location.
- Requires deciding how `.agents/state.md` and JSON state stay consistent.
- Slightly higher operational complexity.

### Option E - Treat Retirement As A Completed Domain Transition

Stop throwing `RoadmapStepException` for a successful retire decision.

Instead, return or route through a non-error outcome that saves the retired exclusion once and exits intentionally.

Possible approaches:

- Add a `RoadmapOutcome.Paused` return after saving the retire transition.
- Add a dedicated `RoadmapOutcome.RetiredEpic` if callers need to distinguish it.
- Add an internal result from `AuditAndPrepareExistingEpicAsync` so `RunAsync` can immediately rerun selection in the same process.

Pros:

- Fixes the state overwrite.
- Makes retirement a normal state-machine transition rather than a failure.
- Aligns behavior with the message "Rerun selection with the retired exclusion recorded."

Cons:

- Requires checking callers or scripts that interpret `RoadmapOutcome.Failed`.
- If automatic reselection is added, guard against infinite retire loops.

## Recommended Fix

Implement Option E plus Option B or C.

The minimum robust fix is:

1. Make retirement a normal paused transition, not a `RoadmapStepException`.
2. Persist selected epic identity from the selection result, not audit `RecommendedNextStep`.
3. Render that identity into the next selection context.
4. Add a regression test proving the final `.agents/state.md` contains the selected epic identity after a retire decision.

If scope must stay small, implement Option A plus the Option E control-flow fix first. Option C can follow when the runtime-state model is ready for structured persistence.

## Acceptance Criteria

- Retiring an existing epic records the selected epic identity, not `Retire Epic`.
- The final saved `.agents/state.md` after a retire decision still contains the retired exclusion.
- A rerun passes that exact identity into `SelectNextEpic`.
- Duplicate retired exclusions are deduplicated by stable identity.
- Existing `.agents/state.md` files without structured exclusions still load safely.
- The retire path exits as an intentional pause or retry state, not as a generic failed blocked state that discards retirement data.

## Suggested Tests

- State-machine retire test:
  - `SelectNextEpic` selects `Epic A`.
  - `EpicPreparationAudit` returns `Disposition | Retire` and `Recommended Next Step | Retire Epic`.
  - Final `.agents/state.md` contains `Epic A`.
  - Final `.agents/state.md` does not contain `- Retire Epic` as the only exclusion.
- Rerun selection-context test:
  - Seed `.agents/state.md` with a retired `Epic A` exclusion.
  - Verify `BuildSelectionContextAsync` renders `Epic A` in `## Retired Epic Exclusions`.
- Persistence regression test:
  - Prove the retire path is not overwritten by `WriteBlockedStateAsync`.
- Parser test if Option B is chosen:
  - Parse `Epic ID` and `Epic Name` from the existing-epic section.
  - Reject `Select Existing Epic` outputs that omit existing-epic identity.
- Backward-compatibility test if Option C or D is chosen:
  - Load an old bullet-list `.agents/state.md`.
  - Preserve real-looking exclusions.
  - Ignore or quarantine legacy workflow-only exclusions such as `Retire Epic`.
