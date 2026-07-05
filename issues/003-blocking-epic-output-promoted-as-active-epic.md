# Blocking Epic Output Can Be Promoted As Active Epic

## Verification Status

Verified by static audit against the current roadmap CLI implementation.

The issue is real. The affected prompt files explicitly allow blocking Markdown outputs, while the state machine treats any successful prompt turn from the single-file epic authoring prompts as a promotable `.agents/epic.md` artifact.

## Finding

`CreateNewEpic`, `RealignEpic`, and `ReimagineEpic` can return blocking documents instead of an epic:

- `src/CommandCenter.Core/Prompts/Planning/CreateNewEpic.prompt`
  - defines `# Create New Epic Blocked`
  - instructs the prompt to block when the proposal is not a bounded new epic, conflicts with the projection, lacks evidence, requires investigation, requires roadmap revision, or is actually split/reimagination work
- `src/CommandCenter.Core/Prompts/Planning/RealignEpic.prompt`
  - defines `# Epic Realignment Blocked`
  - instructs the prompt to block when the audit disposition is not `Realign`
- `src/CommandCenter.Core/Prompts/Planning/ReimagineEpic.prompt`
  - defines `# Epic Reimagination Blocked`
  - instructs the prompt to block when the audit disposition is not `Reimagine`

The valid output contract for these prompts is also single-file Markdown for `.agents/epic.md`, with an expected `# Epic: ...` heading and epic metadata sections.

The state machine does not distinguish those cases:

- `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs`
  - `CreateNewEpicAsync` calls `RunPromptTransitionAsync(...)`, then writes the returned text directly to `RoadmapArtifactPaths.ActiveEpic`, then marks `.agents/epic.md` `ArtifactLifecycleState.Ready`
  - `RewriteActiveEpicAsync` does the same for both `RealignEpic` and `ReimagineEpic`

There is no classifier or artifact validator between prompt output and active-epic promotion. A returned document beginning with any of these headings can therefore be persisted as `.agents/epic.md`:

```markdown
# Create New Epic Blocked
```

```markdown
# Epic Realignment Blocked
```

```markdown
# Epic Reimagination Blocked
```

## Mechanics

The failure path is more than a missing parser:

1. `RunPromptTransitionAsync` saves the destination state as started, runs the prompt, then saves the destination state as completed before the caller writes or validates the artifact.
2. `CreateNewEpicAsync` uses `RoadmapState.NewEpicProposed -> RoadmapState.ActiveEpicReady` and passes `.agents/epic.md` as the output path.
3. `RewriteActiveEpicAsync` uses `RoadmapState.RealignEpic` or `RoadmapState.ReimagineEpic -> RoadmapState.ActiveEpicReady` and passes `.agents/epic.md` as the output path.
4. The caller writes the raw output to `.agents/epic.md`.
5. The caller marks `.agents/epic.md` `Ready`.
6. `RunAsync` then proceeds to milestone generation and execution context generation.

Existing defenses do not catch this:

- `RoadmapPromptRunner` only verifies that the agent turn completed. It does not inspect output semantics.
- `BundleFileExtractor` can treat missing `# FILE:` markers as blocked for multi-file bundle prompts, but these epic authoring prompts are single-file outputs and do not go through bundle extraction.
- `InvariantValidator` checks Project Context hash, projection manifest state, active epic lifecycle counts, spec ownership, and execution prerequisites. It does not validate that `.agents/epic.md` has epic structure.
- The current state machine does not run an `ActiveEpicReady` invariant check immediately after create/realign/reimagine.

## Impact

A prompt-level refusal can become the authoritative active epic.

For `CreateNewEpic`, this creates a false `ActiveEpicReady` state from a document that says epic creation should not proceed. The next transition can generate milestone specs from blocker prose and then materialize execution artifacts from those specs.

For `RealignEpic` and `ReimagineEpic`, the blast radius is larger because an existing active epic can be overwritten by a blocked response. That can destroy the previous executable epic artifact unless it is recoverable from version control or external evidence.

This undermines the explicit blocking contract in the prompts. A blocked output is meant to stop execution and preserve evidence, not masquerade as an epic.

## Solution Options

### Option A: Add a dedicated epic authoring output classifier

Introduce a small deterministic classifier used by `CreateNewEpicAsync` and `RewriteActiveEpicAsync` before writing `.agents/epic.md`.

Suggested shape:

```csharp
internal enum EpicAuthoringOutputKind
{
    Epic,
    Blocked,
    Ambiguous
}

internal sealed record EpicAuthoringOutput(
    EpicAuthoringOutputKind Kind,
    string Content,
    string? Reason);
```

Classifier rules should be conservative:

- Treat known blocked headings as blocked:
  - `# Create New Epic Blocked`
  - `# Epic Realignment Blocked`
  - `# Epic Reimagination Blocked`
- Treat explicit roadmap blocked artifacts as blocked:
  - `# Roadmap Transition Blocked`
- Require positive epic structure before promotion:
  - top-level `# Epic:`
  - `## Epic Metadata`
  - at least one durable epic section such as `## Strategic Purpose`, `## Strategic Continuity`, or `## Desired Capability`
- Treat ambiguous output as blocked, not promotable.

Blocked handling should:

- write the returned blocker document to `.agents/evidence/blockers/`
- preserve the blocker verbatim
- save `RoadmapState.EvidenceBlocked`
- include the blocker evidence path and required next step in `.agents/state.md`
- not overwrite `.agents/epic.md`
- not mark `.agents/epic.md` `Ready`

For rewrite prompts, the existing active epic should remain untouched when the output is blocked or ambiguous.

This is the recommended first fix because it is scoped to the failing surface and does not require prompt-format migration.

Tradeoffs:

- Fastest robust fix.
- Keeps current prompt contracts intact.
- Requires careful state handling so the generic outer `RoadmapStepException` catch does not write a second, less useful blocker artifact over the specific prompt blocker.

### Option B: Move output classification into transition contracts

Extend prompt contracts so each runtime prompt declares its output contract:

```csharp
internal sealed record PromptOutputContract(
    IReadOnlyList<string> BlockedHeadings,
    IReadOnlyList<string> RequiredPromotableHeadings,
    PromptOutputShape Shape);
```

`RunPromptTransitionAsync` would classify output before saving the transition as completed. It could return a richer result:

```csharp
internal sealed record PromptTransitionResult(
    PromptOutputDisposition Disposition,
    string Output,
    string? EvidencePath);
```

This addresses the ordering problem where the transition is marked completed before output validation.

Tradeoffs:

- Better long-term architecture.
- Can cover other prompts that support refusal or blocked outputs.
- Larger refactor because call sites, transition journal records, and state persistence need to understand `Completed`, `Blocked`, and possibly `InvalidOutput` separately.

### Option C: Add an active epic artifact validator as defense in depth

Create an `EpicArtifactValidator` and call it before `.agents/epic.md` is marked `Ready`. Also wire it into `InvariantValidator` for `RoadmapState.ActiveEpicReady`, `MilestoneSpecsReady`, and execution states.

Minimum validation:

- rejects known blocked headings
- rejects empty output
- requires top-level epic heading
- requires metadata section
- optionally validates required fields in the metadata table

Tradeoffs:

- Valuable even if Option A is implemented.
- Catches manually corrupted `.agents/epic.md` and future prompt drift.
- By itself, it does not preserve the prompt's blocker document as first-class evidence unless combined with blocked-output routing.

### Option D: Change single-file authoring prompts to structured status output

Modify epic authoring prompts to produce an explicit status wrapper, for example:

```markdown
# Epic Authoring Result

| Field | Value |
|---|---|
| Status | Epic / Blocked |
| Target Path | .agents/epic.md |
```

The state machine would parse the wrapper and only extract epic content when `Status = Epic`.

Tradeoffs:

- Most explicit protocol.
- Reduces reliance on heading heuristics.
- Requires prompt changes and migration of existing tests/context assumptions.
- Adds ceremony to prompts that currently output direct file content.

### Option E: Minimal blocked-heading guard

As an emergency patch, detect only the known blocked headings immediately after prompt return and throw before writing `.agents/epic.md`.

Tradeoffs:

- Very small patch.
- Prevents the most obvious blocker promotion.
- Does not handle ambiguous malformed output.
- Does not establish a reusable output-validation boundary.
- Should be treated as a temporary version of Option A, not the final design.

## Recommended Path

Implement Option A first and pair it with the validation portion of Option C.

Practical sequence:

1. Add `EpicAuthoringOutputClassifier` with unit tests.
2. Add a helper that persists blocked authoring output to `.agents/evidence/blockers/` and saves `EvidenceBlocked`.
3. Update `CreateNewEpicAsync` to classify before writing `.agents/epic.md`.
4. Update `RewriteActiveEpicAsync` to classify before overwriting `.agents/epic.md`.
5. Add an `EpicArtifactValidator` check before marking active epic `Ready`.
6. Add `InvariantValidator` defense for active epic states.
7. Later, consider Option B if more prompts need typed blocked-output routing.

## Acceptance Criteria

- `CreateNewEpic` returning `# Create New Epic Blocked` never writes `.agents/epic.md`.
- `RealignEpic` returning `# Epic Realignment Blocked` does not overwrite an existing `.agents/epic.md`.
- `ReimagineEpic` returning `# Epic Reimagination Blocked` does not overwrite an existing `.agents/epic.md`.
- Blocked authoring output is preserved verbatim under `.agents/evidence/blockers/`.
- `.agents/state.md` moves to `EvidenceBlocked` and references the blocker evidence path.
- `.agents/epic.md` is not marked `ArtifactLifecycleState.Ready` for blocked or ambiguous output.
- Ambiguous output without valid epic structure blocks rather than advancing.
- Valid epic output still writes `.agents/epic.md`, marks it `Ready`, and proceeds.
- The state machine does not continue to milestone generation after blocked authoring output.

## Suggested Tests

- Classifier test: known blocked headings classify as `Blocked`.
- Classifier test: valid `# Epic:` document with metadata classifies as `Epic`.
- Classifier test: generic prose, empty output, or missing metadata classifies as `Ambiguous` or blocked-equivalent.
- State-machine test: `SelectNextEpic` chooses `Select New Intermediary Epic`, `CreateNewEpic` returns `# Create New Epic Blocked`, outcome is blocked/failed according to current CLI semantics, `.agents/epic.md` is absent, and blocker evidence exists.
- State-machine test: existing active epic plus audit disposition `Realign`, then `RealignEpic` returns `# Epic Realignment Blocked`; original active epic content is unchanged.
- State-machine test: existing active epic plus audit disposition `Reimagine`, then `ReimagineEpic` returns `# Epic Reimagination Blocked`; original active epic content is unchanged.
- State-machine test: ambiguous `CreateNewEpic` output does not advance to milestone generation.
- State-machine test: valid `CreateNewEpic` output still writes `.agents/epic.md` and marks it `Ready`.
- Invariant test: `.agents/epic.md` containing a known blocked heading is rejected when validating active epic states.
