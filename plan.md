# Roadmap Technical Debt Resolution Plan

## Goal

Resolve `RTD-1 - Bootstrap roadmap completion context does not discover completed epics` from `roadmap-technical-debt.md`.

`CreateRoadmapCompletionContext` must receive completed epic history during bootstrap. Completed epics are detected from markdown files directly under:

```text
.agents/archive/epics/*.md
```

The archive location is the completion signal for this fix. A markdown file in `.agents/archive/epics/` is treated as a completed epic record; lifecycle state can be used as supplemental evidence later, but must not be required for detection.

## Current Failure

`RoadmapStateMachine.BootstrapRoadmapCompletionContextAsync` builds the bootstrap prompt from the roadmap completion projection and passes `string.Empty` as the secondary input. The prompt template has a completed-epics input slot, but bootstrap currently supplies no completed-epic evidence.

`TransitionInputResolver` also treats `CreateRoadmapCompletionContext` as projection-only, so transition provenance cannot show which completed epic files influenced the generated completion context.

## Target Behavior

- Bootstrap lists `.agents/archive/epics/*.md` using repository-relative artifact APIs.
- Files are sorted by repository-relative path using ordinal ordering.
- Missing `.agents/archive/epics/` is valid and renders an explicit empty evidence set.
- Empty or malformed archive files do not crash bootstrap solely because they lack expected epic metadata. They are included with weak or unclear evidence quality so the prompt can avoid overclaiming.
- The rendered completed-epic evidence is passed as the `secondaryInput` to `CreateRoadmapCompletionContext`.
- The transition input snapshot records hashes for every completed epic markdown file that contributed to the bootstrap prompt.
- The prompt contract snapshot makes the dynamic completed-epic input set visible.

## Design Decisions

Completed epic detection is intentionally flat and non-recursive:

```text
.agents/archive/epics/*.md
```

Do not implement the older suggested shape `.agents/archive/epics/*/plan.md` in this pass.

Use location as the completion signal. Do not require `Status: Complete`, lifecycle rows, closure evidence, or execution evaluation artifacts before a file can be included. Those can improve evidence quality, but absence must not hide the record from bootstrap.

Keep the renderer deterministic. It should extract and bound content; it should not call an agent to summarize completed epics before the actual bootstrap prompt.

## Implementation Plan

### 1. Add Archive Path and Role Constants

- Add `RoadmapArtifactPaths.CompletedEpicsDirectory = ".agents/archive/epics"`.
- Add `RoadmapArtifactPaths.CompletedEpicsPattern = ".agents/archive/epics/*.md"` or equivalent display constant.
- Add `TransitionInputRole.CompletedEpic = "CompletedEpic"`.

These constants keep path spelling centralized and let contracts/tests assert the user-facing archive glob.

### 2. Add Completed Epic Evidence Loading

Add a small roadmap CLI service, for example `CompletedEpicEvidenceLoader`, that depends on `RoadmapArtifacts`.

Responsibilities:

- Call `artifacts.ListAsync(RoadmapArtifactPaths.CompletedEpicsDirectory, "*.md")`.
- Sort returned paths with `StringComparer.Ordinal`.
- Read each listed file through `artifacts.ReadAsync`.
- Preserve each source path in the rendered output.
- Include non-empty records even when metadata extraction is weak.
- Skip files that disappear between list and read, or include them as missing evidence only if the existing transition input machinery can represent that cleanly.

Recommended record shape:

```csharp
internal sealed record CompletedEpicEvidence(
    string Path,
    string? Title,
    string? EpicId,
    string EvidenceQuality,
    string RenderedContent);
```

Metadata extraction should be conservative:

- Title: first level-one heading.
- Epic ID: `Epic ID` metadata table field when present, otherwise filename without extension.
- Evidence quality: `Strong` when recognizable completion or implementation evidence exists, `Weak` when only an epic-like record exists, `Unclear` for unstructured text.

### 3. Add Deterministic Evidence Rendering

Render one markdown secondary input for `CreateRoadmapCompletionContext`.

The empty case must be explicit:

```markdown
# Completed Epic Evidence

No completed epic markdown files were found under `.agents/archive/epics/*.md`.
```

The non-empty case should be compact and source-traceable:

```markdown
# Completed Epic Evidence

Completed epic source glob: `.agents/archive/epics/*.md`

## Completed Epic: <title or filename>

| Field | Value |
|---|---|
| Source Path | .agents/archive/epics/001-example.md |
| Epic ID | EPIC-001 |
| Evidence Quality | Weak |

<bounded extracted evidence>
```

Extraction priority:

- Keep sections named like `Strategic Purpose`, `Desired Capability`, `Outcome`, `Acceptance Criteria`, `Completion Evidence`, `Implementation Evidence`, `Drift`, and `Follow-Up`.
- If no known sections exist, include a bounded leading excerpt.
- Add a visible truncation note when per-file or total budget is reached.

The renderer should prevent a large archive from overwhelming bootstrap. Start with simple constants such as a per-epic character cap and a total character cap; tests should cover truncation markers.

### 4. Wire Bootstrap to Completed Epic Evidence

Update `RoadmapStateMachine.BootstrapRoadmapCompletionContextAsync`:

- Load/render completed epic evidence before calling `RunPromptTransitionAsync`.
- Continue using the projection content in the rendered context.
- Pass rendered completed epic evidence as `secondaryInput`.

The call should move from:

```csharp
string.Empty
```

to the rendered completed-epic evidence string.

No prompt template change is expected because `RoadmapPromptCatalog` already renders `CreateRoadmapCompletionContext` with `secondaryInput`.

### 5. Record Completed Epic Inputs in Provenance

Update `TransitionInputResolver.AddPromptInputsAsync` for `CreateRoadmapCompletionContext`:

- Add every file from `.agents/archive/epics/*.md` as a transition input with role `CompletedEpic`.
- Keep the projection input as it works today.
- If no files exist, do not synthesize fake artifact inputs; the non-empty secondary input hash records the explicit empty evidence set.

This ensures `TransitionJournalRecord.InputArtifactHashes` includes actual archived epic files when present.

### 6. Update Prompt Contract Metadata

`PromptContractRegistry` is static, while completed epic files are dynamic. Represent this as a dynamic input pattern rather than individual files.

Preferred shape:

- Add `.agents/archive/epics/*.md` to the `CreateRoadmapCompletionContext` contract as an optional or dynamic input.
- If adding `OptionalInputs` to the emitted contract table is small, do that and list the glob there.
- If preserving the table shape is preferred, list the glob in required inputs and document in tests that the set may be empty.

The important outcome is that `.agents/contracts/prompt-contracts.md` no longer claims `CreateRoadmapCompletionContext` has no completed-epic input.

### 7. Update Tests

Add or update focused tests:

- `TransitionInputResolverTests`: bootstrap with two archived markdown files records projection plus both completed epic inputs, sorted by path, with role `CompletedEpic` and hashes.
- `TransitionInputResolverTests`: bootstrap with no archive directory records only the projection and does not throw.
- New loader/renderer tests: missing archive renders explicit empty evidence.
- New loader/renderer tests: flat `.agents/archive/epics/*.md` files are included; nested files such as `.agents/archive/epics/old/plan.md` are not included.
- New loader/renderer tests: unstructured or malformed markdown is included with `Unclear` evidence quality rather than crashing.
- `RoadmapStateMachineSelectionTests`: missing completion context bootstraps using archived epic evidence in the prompt sent to the runtime.
- `PromptContractRegistryTests`: `CreateRoadmapCompletionContext` declares the completed-epic archive glob.
- Journal/provenance test: after bootstrap with an archived epic, transition journal input hashes include that archived epic path.

### 8. Clean Up the Debt Register

After implementation and verification pass, delete the resolved RTD-1 section from `roadmap-technical-debt.md`.

Do not leave a completed entry in the debt register; the file convention says resolved debt is deleted and preserved by git history.

## Milestone Checklist

- [ ] Add constants for completed epic archive directory, glob, and transition input role.
- [ ] Implement completed epic evidence loader and deterministic renderer.
- [ ] Pass rendered completed epic evidence into `CreateRoadmapCompletionContext` bootstrap.
- [ ] Include `.agents/archive/epics/*.md` files in transition input provenance.
- [ ] Update prompt contract metadata to disclose the dynamic completed-epic input set.
- [ ] Add resolver, renderer, state-machine, contract, and journal tests.
- [ ] Run the roadmap CLI test suite.
- [ ] Remove RTD-1 from `roadmap-technical-debt.md`.

## Validation Commands

```powershell
dotnet test tests\LoopRelay.Roadmap.Cli.Tests\LoopRelay.Roadmap.Cli.Tests.csproj
dotnet test LoopRelay.slnx
```

## Acceptance Criteria

- A repository with `.agents/archive/epics/*.md` and no `.agents/core/roadmap-completion-context.md` bootstraps a completion context from both the projection and completed epic evidence.
- A repository without `.agents/archive/epics/` still bootstraps successfully and the prompt receives an explicit "no completed epic markdown files" evidence section.
- Transition journal records include hashes for completed epic markdown files used during bootstrap.
- The prompt contract snapshot no longer describes `CreateRoadmapCompletionContext` as having no completed-epic input.
- Resolved debt is removed from `roadmap-technical-debt.md`.
