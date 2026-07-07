# Roadmap Epic File-Authoring Refactor Plan

## Goal

Refactor Roadmap CLI active-epic authoring so the agent writes the epic artifact as a file, and Roadmap CLI validates the file on disk instead of using `AgentTurnResult.Output` as the content for `.agents/epic.md`.

The core rule after this refactor:

- Agent final response output is never persisted as `.agents/epic.md`.
- The authored epic must come from a file the agent wrote.
- Roadmap CLI validates and promotes that file only after artifact-boundary checks pass.

This directly addresses the observed failure mode where agent reasoning or commentary appeared at the top of `.agents/epic.md`.

## Current Failure Mode

The current Roadmap CLI flow treats the model's final response as the authoritative epic candidate:

1. `RoadmapStateMachine.RunPromptForPromotionAsync(...)` calls `RoadmapPromptRunner.RunRuntimePromptAsync(...)`.
2. `RoadmapPromptRunner` returns `AgentTurnResult.Output`.
3. `PromoteActiveEpicAsync(...)` passes that output to `ArtifactPromotionService`.
4. `ArtifactPromotionService.PromoteAsync(...)` writes the candidate string directly to `.agents/epic.md`.
5. `EpicAuthoringOutputClassifier` accepts the content if the first top-level `# ` heading found anywhere is `# Epic: ...`.

That means this shape can be promoted:

```markdown
I reviewed the repository and decided the safest framing is:

# Epic: Valid Epic

## Epic Metadata
...
```

The prompt says "Do not include commentary" and "Do not include hidden analysis", but the CLI does not enforce that the artifact begins at `# Epic:`. The invariant validator uses the same epic validator, so a contaminated but structurally complete file can continue through later states.

## Scope

Primary scope:

- `CreateNewEpic`
- `RealignEpic`
- `ReimagineEpic`
- active epic promotion into `.agents/epic.md`
- validation, lifecycle, journal, state-machine persistence around those transitions

Secondary scope:

- `SplitEpic` selected-child promotion should be audited after this change because it also promotes model-authored content into the active epic. It can either keep the bundle protocol temporarily with stricter epic-boundary validation, or move to the same file-authoring runner in a follow-up.

Out of scope:

- Changing milestone spec bundle extraction.
- Changing roadmap selection, audit, or completion-evaluation reports.
- Connecting Roadmap CLI to Plan CLI or Main CLI.
- Allowing arbitrary agent writes to the repository.

## Target Behavior

For epic authoring prompts, the agent must be instructed to write an artifact file:

- success target: `.agents/epic.md` in the repository, or a mapped `epic.md` target in an isolated authoring workspace that Roadmap CLI later copies back to `.agents/epic.md`
- blocked target: a CLI-provided blocker evidence path when the prompt cannot safely author an epic
- final response: a small status summary only, ignored for epic content

Roadmap CLI then:

1. Runs the agent.
2. Reads the authored file from the configured target path.
3. Validates the file as an epic artifact.
4. Promotes/copies the validated file to `.agents/epic.md`.
5. Records lifecycle, journal, and state-machine entries.
6. Treats missing, invalid, ambiguous, or blocked artifacts as transition blockers.

The model's final response can still be streamed to the console and used for human diagnostics, but it must not be used as active epic content.

## Recommended Architecture

Introduce a dedicated Roadmap epic authoring boundary:

- `RoadmapArtifactAuthoringRunner`
- `RoadmapArtifactAuthoringRequest`
- `RoadmapArtifactAuthoringOutcome`
- `RoadmapArtifactAuthoringTargets`
- `RoadmapArtifactWriteGuard`
- `EpicArtifactBoundaryValidator`

This boundary replaces `ArtifactPromotionService` for active epic authoring transitions. `ArtifactPromotionService` can remain for non-agent-file promotion or be retired after callers are migrated.

### Recommended Execution Model

Use a sandboxed file-authoring workspace, then copy back only validated target files.

Reasoning:

- Direct workspace-write lets the agent inspect the real repository, but it can also dirty unrelated files.
- A pure output-based protocol is what caused the contamination.
- A sandboxed file protocol lets the agent write a real file while the host controls what gets copied into the repository.

Recommended implementation:

1. Create a temporary authoring workspace.
2. Seed it with the repository content needed for codebase audit.
3. Seed or map relevant `.agents` artifacts into visible target paths.
4. Instruct the agent to write `epic.md` in the authoring workspace.
5. Validate `epic.md`.
6. Copy the validated content to repository `.agents/epic.md`.

Because Codex may hide dot-directories from normal workspace listings, prefer an explicit visible mapping in the authoring workspace:

| Repository Path | Authoring Workspace Path | Notes |
|---|---|---|
| `.agents/epic.md` | `epic.md` | Success target for CreateNewEpic, RealignEpic, ReimagineEpic |
| `.agents/evidence/blockers/<reserved>.md` | `blocked.md` | Blocked target when no safe epic can be authored |
| `.agents/selection.md` | `selection.md` | Seed for CreateNewEpic |
| `.agents/projections/*.md` | `projection.md` or `projections/*.md` | Seeded projection input |
| `.agents/evidence/audits/*.md` | `audit.md` | Seed for RealignEpic/ReimagineEpic |

The prompt can state both paths clearly:

```text
Write the successful epic artifact to `epic.md` in this authoring workspace.
Roadmap CLI will validate and publish that file as `.agents/epic.md`.
Do not rely on your final response to carry artifact content.
```

### Acceptable Interim Execution Model

If a full repository authoring sandbox is too large for the first cut, an interim direct-write implementation is acceptable only with strict guards:

- run the agent with explicit `--sandbox workspace-write`
- pre-snapshot `.agents/epic.md`
- reserve a blocker evidence path before the turn
- after the turn, verify only allowed paths changed
- validate `.agents/epic.md`
- restore the pre-snapshot on invalid, blocked, or ambiguous outcomes
- preserve any invalid candidate as numbered blocker evidence

The sandboxed copy-back path is preferred because it avoids needing to repair unrelated agent writes in the real repository.

## Agent Sandbox Requirements

`CodexAgentArgumentBuilder` currently omits `--sandbox` for one-shot exec turns. That must be revisited for file-authoring turns.

Required behavior:

- read-only prompts stay read-only
- file-authoring prompts run with `workspace-write`
- no network access
- approval policy remains `never`
- model effort remains `xhigh` unless changed intentionally

Implementation options:

1. Update one-shot argument building to emit `--sandbox <spec.Sandbox.Identifier>` for all one-shots.
2. If changing all one-shots is too broad, add an explicit `AgentSessionSpec.StartupOptions` or execution-mode flag that makes sandbox emission opt-in for Roadmap authoring first.

Acceptance requirement:

- Roadmap epic authoring must not depend on the user's default Codex config for write posture.

## Prompt Changes

Update these prompts:

- `src/LoopRelay.Core/Prompts/Planning/CreateNewEpic.prompt`
- `src/LoopRelay.Core/Prompts/Planning/RealignEpic.prompt`
- `src/LoopRelay.Core/Prompts/Planning/ReimagineEpic.prompt`

Remove or replace the current output-content contract:

```text
Output only the full Markdown content for:
.agents/epic.md
```

Replace it with a file-authoring contract:

```text
## Required Artifact Write

Write the successful epic artifact to:

epic.md

Roadmap CLI will validate and publish this file as `.agents/epic.md`.

Do not put the epic content in your final response.
Do not wrap the epic in a code fence.
Do not include commentary, hidden analysis, or preface text in `epic.md`.
The first non-whitespace content in `epic.md` must be `# Epic:`.

If epic authoring is blocked, do not modify `epic.md`.
Instead write the blocked document to:

blocked.md

Your final response must contain only a compact status table:

| Field | Value |
|---|---|
| Status | Authored / Blocked / Failed |
| Artifact | epic.md / blocked.md / none |
| Reason | one sentence |
```

Prompt-specific notes:

- `CreateNewEpic` writes a new `epic.md`.
- `RealignEpic` receives the current epic as seeded `epic.md` and must rewrite that file only if the audit disposition is Realign.
- `ReimagineEpic` receives the current epic as seeded `epic.md` and must rewrite that file only if the audit disposition is Reimagine.
- Blocked outputs must go to `blocked.md`, never into `epic.md`.

## State Machine Changes

Replace this current pattern:

```csharp
PromptTransitionCompletion completion = await RunPromptForPromotionAsync(...);
return await PromoteActiveEpicAsync(..., completion);
```

with:

```csharp
RoadmapArtifactAuthoringOutcome outcome = await authoringRunner.RunActiveEpicAuthoringAsync(...);
return await PersistActiveEpicAuthoringOutcomeAsync(..., outcome);
```

### New Authoring Request

Suggested shape:

```csharp
internal sealed record RoadmapArtifactAuthoringRequest(
    RoadmapState From,
    RoadmapState PromotionTarget,
    string RuntimePrompt,
    string ProjectionPath,
    string RenderedPrompt,
    IReadOnlyList<RoadmapAuthoringSeed> Seeds,
    RoadmapArtifactAuthoringTargets Targets,
    TransitionInputSnapshot InputSnapshot,
    bool RequireTargetChanged);
```

`RequireTargetChanged` should be:

- `false` for `CreateNewEpic` when no active epic exists
- `true` for `RealignEpic`
- `true` for `ReimagineEpic`

### New Targets

Suggested shape:

```csharp
internal sealed record RoadmapArtifactAuthoringTargets(
    string WorkspaceEpicPath,
    string RepositoryEpicPath,
    string WorkspaceBlockedPath,
    string RepositoryBlockedPath);
```

For the preferred mapped sandbox:

- `WorkspaceEpicPath = "epic.md"`
- `RepositoryEpicPath = ".agents/epic.md"`
- `WorkspaceBlockedPath = "blocked.md"`
- `RepositoryBlockedPath = await artifacts.NextNumberedPathAsync(".agents/evidence/blockers", "active-epic-promotion")`

### New Outcome

Suggested shape:

```csharp
internal enum RoadmapArtifactAuthoringStatus
{
    Authored,
    Blocked,
    MissingArtifact,
    InvalidArtifact,
    AmbiguousArtifacts,
    UnexpectedWorkspaceChanges,
    RuntimeFailed
}

internal sealed record RoadmapArtifactAuthoringOutcome(
    RoadmapArtifactAuthoringStatus Status,
    string? EpicContent,
    string? BlockedContent,
    string? EvidencePath,
    string Reason,
    AgentTurnResult TurnResult);
```

Important: `EpicContent` must come from the authored file, not `TurnResult.Output`.

## Authoring Runner Flow

### Success Path

1. Resolve transition inputs.
2. Reserve blocker evidence path.
3. Save state as `Started`.
4. Append `TransitionStarted`.
5. Create authoring workspace.
6. Seed projection, selection/audit, current epic when needed, and repository evidence.
7. Run Codex with file-authoring prompt.
8. Ignore `AgentTurnResult.Output` for artifact content.
9. Read `epic.md`.
10. Verify `blocked.md` is absent or empty.
11. Validate `epic.md`.
12. If rewrite, verify `epic.md` differs from the seeded current epic.
13. Copy validated content to `.agents/epic.md`.
14. Upsert lifecycle entry for `.agents/epic.md` as `Ready`.
15. Append `ArtifactPromoted`.
16. Save state as `ActiveEpicReady`.

### Blocked Path

1. Read `blocked.md`.
2. Verify `epic.md` was not created or was unchanged from the seed.
3. Copy `blocked.md` to the reserved numbered evidence path.
4. Upsert lifecycle entry for evidence as `Blocked`.
5. Append `ArtifactPromotionBlocked`.
6. Save state as `EvidenceBlocked` with `ResolveArtifactPromotionBlocker`.
7. Preserve any existing `.agents/epic.md`.

### Invalid Artifact Path

If `epic.md` exists but fails validation:

1. Do not promote it.
2. Persist the invalid `epic.md` content as numbered blocker evidence.
3. Preserve existing `.agents/epic.md`.
4. Save state as `EvidenceBlocked`.
5. Record failure category `Artifact Promotion Invalid`.

### Missing Artifact Path

If the agent final response contains epic content but no file was written:

1. Do not parse the final response as fallback content.
2. Save state as `EvidenceBlocked`.
3. Persist the final response only as diagnostic evidence if useful.
4. Decision should be `Artifact Authoring Missing`.

This case is the main regression guard for the original bug.

### Ambiguous Artifact Path

If both `epic.md` and `blocked.md` are written:

1. Do not promote either as authoritative.
2. Preserve both as blocker evidence.
3. Save state as `EvidenceBlocked`.
4. Require human repair or rerun.

## Validation Changes

Strengthen epic validation even after moving to file-authoring. File-authoring removes the main cause, but validation should still enforce the artifact boundary.

Add checks:

- after optional UTF-8 BOM and whitespace, content must start with `# Epic:`
- blocked documents must start with `# ... Blocked`
- successful epic content must not have any non-whitespace preface before `# Epic:`
- successful epic content must include exactly one top-level `# Epic:` heading
- successful epic content must not be wrapped in a Markdown code fence
- existing required sections still apply:
  - `## Epic Metadata`
  - `## Strategic Purpose` or `## Strategic Continuity`
  - `## Desired Capability`
  - `## Acceptance Criteria`
  - `## Milestone Roadmap`
- milestone roadmap validation remains unchanged

Tests should prove that this is rejected:

```markdown
I reasoned about the repository first.

# Epic: Valid-Looking Epic
...
```

## Split Epic Follow-Up

`SplitEpic` currently extracts child epic files from a model output bundle, validates them, writes `.agents/epic-N.md`, and promotes the selected child into `.agents/epic.md`.

Minimum required follow-up:

- apply the stricter epic-boundary validator to extracted child contents
- reject any child file whose content does not start at `# Epic:`
- preserve rejected bundle output as evidence

Preferred later refactor:

- make `SplitEpic` a file-authoring prompt that writes `epic-1.md`, `epic-2.md`, and `split-family.json` in the authoring workspace
- copy back only validated child epic files
- promote the selected child from the authored file, not model response text

## Files To Change

Roadmap CLI:

- `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapPromptRunner.cs`
- `src/LoopRelay.Roadmap.Cli/AgentSpecs.cs`
- `src/LoopRelay.Roadmap.Cli/EpicArtifactPromotion.cs`
- `src/LoopRelay.Roadmap.Cli/ArtifactPromotion.cs` if kept as shared promotion helper
- add `src/LoopRelay.Roadmap.Cli/RoadmapArtifactAuthoringRunner.cs`
- add `src/LoopRelay.Roadmap.Cli/RoadmapArtifactAuthoring.cs`
- add `src/LoopRelay.Roadmap.Cli/RoadmapArtifactWriteGuard.cs`

Prompt files:

- `src/LoopRelay.Core/Prompts/Planning/CreateNewEpic.prompt`
- `src/LoopRelay.Core/Prompts/Planning/RealignEpic.prompt`
- `src/LoopRelay.Core/Prompts/Planning/ReimagineEpic.prompt`
- optionally `src/LoopRelay.Core/Prompts/Planning/SplitEpic.prompt`

Agent infrastructure:

- `src/LoopRelay.Agents/Services/CodexAgentArgumentBuilder.cs`
- `tests/LoopRelay.Agents.Tests/CodexAgentArgumentBuilderTests.cs`

Optional shared sandbox infrastructure:

- add repository-authoring sandbox support under `src/LoopRelay.Orchestration.Primitives`
- or implement Roadmap-local sandbox seeding first and extract later

Tests:

- `tests/LoopRelay.Roadmap.Cli.Tests/EpicArtifactPromotionTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/ArtifactPromotionServiceTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapStateMachinePromotionTests.cs`
- add `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapArtifactAuthoringRunnerTests.cs`
- add or update split tests if SplitEpic validator changes

## Test Plan

### Unit Tests

Add tests for epic boundary validation:

- valid epic starting with `# Epic:` passes
- leading commentary before `# Epic:` fails
- code-fenced epic fails
- two top-level `# Epic:` headings fail
- blocked document starts with blocked heading and classifies as blocked
- malformed `# Epic` still fails

Add tests for authoring runner:

- authored `epic.md` is copied to `.agents/epic.md`
- final response containing a valid epic is ignored when `epic.md` is missing
- final response containing reasoning does not pollute `.agents/epic.md`
- invalid `epic.md` is saved as blocker evidence and not promoted
- blocked `blocked.md` preserves existing active epic
- both `epic.md` and `blocked.md` produces ambiguous blocker state
- Realign/Reimagine require changed content
- CreateNewEpic accepts a new valid file when no active epic exists
- existing active epic is preserved on invalid rewrite

Add tests for agent arguments:

- Roadmap file-authoring one-shots emit `--sandbox workspace-write`
- read-only planning one-shots remain read-only or explicitly emit `--sandbox read-only`
- approval policy remains `never`
- network remains disabled by spec/prompt policy

### State Machine Tests

Update or add tests:

- `CreateNewEpic` promotes only file-authored `epic.md`
- `CreateNewEpic` pauses when agent returns epic in output but writes no file
- `RealignEpic` preserves existing active epic when blocked file is written
- `ReimagineEpic` preserves existing active epic when invalid file is written
- successful rewrite continues to milestone-spec generation
- state and transition journal use the same decisions as today where possible
- blocker evidence paths are numbered and lifecycle-marked

### Regression Test For Observed Bug

Add a test that simulates:

```markdown
I inspected the repository and here is the result.

# Epic: Clean Looking Epic
...
```

Expected behavior:

- if this text is only `AgentTurnResult.Output`, no active epic is written
- if this text is written into `epic.md`, validation rejects it
- existing `.agents/epic.md` remains unchanged
- the contaminated candidate is preserved only as blocker evidence

## Migration Plan

1. Add stricter epic boundary validation first.
2. Add tests proving prefaced epic content is rejected.
3. Add file-authoring runner behind `CreateNewEpic` only.
4. Update `CreateNewEpic.prompt` to write `epic.md`.
5. Update CreateNewEpic state-machine tests.
6. Migrate `RealignEpic`.
7. Migrate `ReimagineEpic`.
8. Audit `SplitEpic` and add stricter child validation.
9. Remove or narrow `PromoteActiveEpicAsync` so no active epic path writes from `AgentTurnResult.Output`.
10. Run Roadmap CLI test suite.

## Acceptance Criteria

- `.agents/epic.md` is never written from `AgentTurnResult.Output`.
- `CreateNewEpic`, `RealignEpic`, and `ReimagineEpic` require an authored file.
- A final response containing valid-looking epic Markdown cannot create or replace `.agents/epic.md`.
- A file with leading reasoning before `# Epic:` is rejected.
- Existing active epic content is preserved on invalid, missing, blocked, or ambiguous authoring results.
- Blocked and invalid candidates are preserved as evidence, not promoted.
- State machine persistence still records started, completed, paused, and failed transitions coherently.
- Transition journal records the source as file-authoring/promotion, not raw response promotion.
- Tests cover the original contamination failure mode.
- Codex write posture for epic authoring is explicit and does not depend on user-level Codex defaults.

## Open Decisions

1. Should the first implementation use a full repository authoring sandbox or direct workspace-write with guards?
   - Recommended: full repository authoring sandbox.
   - Interim acceptable: direct workspace-write with snapshot/restore and strict changed-path guard.

2. Should blocked documents be written by the agent to a reserved file, or should final response remain blocker evidence?
   - Recommended: reserved blocked file to keep the no-output-as-artifact rule consistent.
   - Acceptable: final response as blocker evidence only, never as active epic content.

3. Should one-shot sandbox emission be changed globally?
   - Recommended: yes, because `AgentSessionSpec.Sandbox` currently advertises posture that one-shot exec does not enforce.
   - Lower-risk alternative: emit sandbox only for file-authoring requests first.

4. Should `ArtifactPromotionService` be deleted after migration?
   - Recommended: keep it temporarily for non-active-epic evidence handling, then prune once no active epic promotion uses raw output.
