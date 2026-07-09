# Refined Plan: Bool-Gated Non-Implementation Sections for CreateNewEpic

## Goal

Refactor `CreateNewEpic.prompt` so it conditionally injects prompt-owned non-implementation guidance from a user setting:

`AllowAuxiliaryNonImplementationFiles`

The setting controls whether `CreateNewEpic` receives strict guidance against auxiliary non-implementation artifacts. It does not authorize runtime artifact output, bypass promotion validation, bypass non-implementation review, or permit arbitrary files. `.agents/epic.md` remains the required sanctioned output for successful epic creation.

This implementation migrates only the `CreateNewEpic` runtime prompt away from `ImplementationFirstPromptPolicyComposer`. The composer remains intentional transitional technical debt for all remaining prompt consumers until each consumer is migrated to prompt-owned sections in a later pass.

## Architectural Decisions

### 1. Migrate `CreateNewEpic` only

`ImplementationFirstPromptPolicyComposer` currently applies to many prompts through runner-level appending. Removing it globally would change behavior outside this feature.

Decision:

* `CreateNewEpic` must stop depending on the composer.
* Non-`CreateNewEpic` roadmap runtime prompts must continue receiving the existing composer output for now.
* Plan CLI, Loop CLI, and Completion prompt paths must continue using the composer.
* Complete composer removal is deferred until the final consumer has been migrated.

### 2. Add the new setting without deleting the legacy HITL setting

`AllowHitlRequestedNonImplementationFiles` and `AllowAuxiliaryNonImplementationFiles` are not equivalent.

The old flag controls HITL-requested non-implementation exception wording in the legacy composer. The new flag controls whether strict `CreateNewEpic` auxiliary-artifact guidance is injected.

Decision:

* Add `AllowAuxiliaryNonImplementationFiles`.
* Preserve `AllowHitlRequestedNonImplementationFiles` as a deprecated legacy setting while the composer still has consumers.
* Do not alias the old flag into the new flag.
* Do not remove the old property from the options model until the composer is fully removed.

### 3. Use `CreateNewEpic`-specific prompt sections

The existing `NonImplementation/InvalidContent.prompt` is too roadmap-oriented for `CreateNewEpic`. It discourages documentation, planning, analysis, milestone planning, and human-readable artifacts in language that can contradict the required `.agents/epic.md` strategic epic artifact.

Decision:

* Do not inject the existing `InvalidContent.prompt` into `CreateNewEpic`.
* Do not inject the existing `ImplementationFirstThinking.prompt` unless its wording is rewritten in a `CreateNewEpic`-specific section.
* Create `CreateNewEpic`-specific non-implementation section prompts that explicitly preserve the `.agents/epic.md` contract.
* Include the substance of `ArtifactCreationInstructions.prompt`: only create artifacts that are machine-required or explicitly requested.

### 4. Keep prompt section selection near roadmap prompt rendering

The new section selection is specific to `CreateNewEpic` and the roadmap runtime prompt path.

Decision:

* Place the section selector in `LoopRelay.Roadmap.Cli`, near `RoadmapPromptCatalog`.
* Do not place a `CreateNewEpic`-specific selector in `LoopRelay.Orchestration.Primitives`.
* Keep prompt bodies in `.prompt` files under `LoopRelay.Core`.

### 5. Record prompt-policy identity in transition evidence

With bool-gated sections, two runs can have the same projection, prompt context, and secondary input while sending different rendered prompts. The transition snapshot should reflect that policy-controlled difference.

Decision:

* Add a compact prompt-policy identity to `TransitionInputSnapshot`.
* Include that identity in `SnapshotHash`.
* Record hashes and policy values, not the full rendered prompt.

## Current State

* `src/LoopRelay.Core/Prompts/Planning/CreateNewEpic.prompt` renders `{projectContext}` and `{newEpicProposal}`.
* `RoadmapPromptCatalog.RenderRuntime("CreateNewEpic", ...)` calls `CreateNewEpic.Render(projectContext, secondaryInput)`.
* `RoadmapPromptRunner.RunRuntimePromptAsync(...)` appends `ImplementationFirstPromptPolicyComposer` output to every roadmap runtime prompt.
* `RoadmapCliComposition` composes legacy prompt-policy text from `settings.ArtifactPolicy`.
* `NonImplementationArtifactPolicyOptions` exposes `AllowHitlRequestedNonImplementationFiles`, defaulting to `false`.
* `TransitionInputSnapshot` does not include runtime prompt source hash, injected section hashes, prompt policy hash, or artifact policy values.
* `CreateNewRoadmap` is an extensionless prompt-like reference artifact, not generated production prompt authority.

## Target Behavior

* `CreateNewEpic.prompt` owns its non-implementation guidance through explicit placeholders.
* When `AllowAuxiliaryNonImplementationFiles == false`, `CreateNewEpic` receives strict `CreateNewEpic`-specific guidance against auxiliary non-implementation artifacts.
* When `AllowAuxiliaryNonImplementationFiles == true`, those strict `CreateNewEpic` sections render as empty strings.
* Both modes still require output for `.agents/epic.md`.
* The setting does not change `ArtifactPromotionService`, `EpicAuthoringOutputClassifier`, `EpicArtifactValidator`, HITL capture, ledger behavior, or completion review.
* The `CreateNewEpic` runtime prompt path no longer uses `ImplementationFirstPromptPolicyComposer`.
* Other roadmap runtime prompts continue receiving the legacy composer output until migrated.
* Plan CLI, Loop CLI, and Completion continue using the legacy composer.
* Transition evidence distinguishes prompt runs that differ only by prompt-policy branch.

## Implementation Steps

### 1. Evolve artifact policy settings

Update:

* `src/LoopRelay.Permissions/Models/Policy/NonImplementationArtifactPolicyOptions.cs`
* `src/LoopRelay.Permissions/Services/Configuration/CliSettingsLoader.cs`
* `config/settings.default.json`
* permission/settings tests

Change `NonImplementationArtifactPolicyOptions` from a single-flag record to a two-flag record:

```csharp
public sealed record NonImplementationArtifactPolicyOptions(
    bool AllowHitlRequestedNonImplementationFiles,
    bool AllowAuxiliaryNonImplementationFiles)
{
    public static NonImplementationArtifactPolicyOptions Default { get; } =
        new(
            AllowHitlRequestedNonImplementationFiles: false,
            AllowAuxiliaryNonImplementationFiles: false);
}
```

Update the settings document mapper to read:

* `artifactPolicy.allowHitlRequestedNonImplementationFiles`
* `artifactPolicy.allowAuxiliaryNonImplementationFiles`

Mapping rules:

* Missing `artifactPolicy` means both flags default to `false`.
* Missing individual flag means that flag defaults to `false`.
* The old flag remains supported for legacy composer behavior.
* The old flag must not imply the new flag.
* The new flag must not imply the old flag.

Update `config/settings.default.json` to expose both fields while the composer remains active:

```json
"artifactPolicy": {
  "allowHitlRequestedNonImplementationFiles": false,
  "allowAuxiliaryNonImplementationFiles": false
}
```

Do not remove `AllowHitlRequestedNonImplementationFiles` from tests or production code in this pass. Mark it deprecated in code comments only if the repository already uses that style; otherwise rely on naming and tests to show its legacy role.

### 2. Fix roadmap CLI settings packaging and dependency clarity

Because this feature is settings-driven, `LoopRelay.Roadmap.Cli` should have the same default settings availability as the other CLIs.

Update:

* `src/LoopRelay.Roadmap.Cli/LoopRelay.Roadmap.Cli.csproj`

Add a content link for `config/settings.default.json` matching `LoopRelay.Cli` and `LoopRelay.Plan.Cli`:

```xml
<Content Include="..\..\config\settings.default.json" Link="settings.default.json">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
</Content>
```

Also add explicit project references for public types used directly by Roadmap CLI if they are currently available only transitively, especially:

* `LoopRelay.Orchestration.Primitives`
* `LoopRelay.Permissions`

Do not introduce new cross-project dependencies for the `CreateNewEpic` section selector.

### 3. Add `CreateNewEpic`-specific section prompts

Add generated prompt-authority files under:

* `src/LoopRelay.Core/Prompts/NonImplementation/CreateNewEpicImplementationFirstGuidance.prompt`
* `src/LoopRelay.Core/Prompts/NonImplementation/CreateNewEpicAuxiliaryArtifactLimits.prompt`

The exact wording can be concise, but it must satisfy these semantic requirements:

`CreateNewEpicImplementationFirstGuidance.prompt`:

* State that the required `.agents/epic.md` output is a sanctioned LoopRelay operational artifact.
* Preserve strategic epic authoring, repository grounding, assumptions, dependencies, constraints, risks, and milestone roadmap content because `CreateNewEpic.prompt` explicitly requires them.
* Keep the implementation-first boundary: the epic should define capability outcomes, constraints, and milestone signals, not implementation tasks, file-level plans, code changes, execution prompts, or auxiliary design documents.

`CreateNewEpicAuxiliaryArtifactLimits.prompt`:

* State that the prompt must output only the `.agents/epic.md` content or the documented blocked response.
* Reject auxiliary non-implementation files such as separate analysis reports, roadmap revisions, governance documents, RFCs, ADRs, research reports, inventories, or implementation-support documents.
* Include the core artifact rule: only create artifacts that are machine-required or explicitly requested.
* Clarify that allowing auxiliary files merely omits this extra strict guidance; it does not change runtime promotion or review policy.

Do not copy these section bodies into C# strings.

### 4. Update `CreateNewEpic.prompt`

Update:

* `src/LoopRelay.Core/Prompts/Planning/CreateNewEpic.prompt`

Add explicit placeholders after the authority rules and before the detailed epic definition / operating principles:

```text
{epicImplementationFirstGuidance}

{epicAuxiliaryArtifactLimits}
```

Use descriptive placeholder names rather than reusing `{implementationFirstThinking}` and `{invalidContent}`. The generated render signature should become conceptually:

```csharp
CreateNewEpic.Render(
    projectContext,
    newEpicProposal,
    epicImplementationFirstGuidance,
    epicAuxiliaryArtifactLimits)
```

Rendered prompt requirements:

* Strict mode must be internally coherent with the `.agents/epic.md` requirement.
* Allowed mode may leave blank lines where sections are omitted, but should not leave visible placeholder artifacts or awkward empty headings.
* Both modes must preserve all current blocking behavior and required output structure.

### 5. Add a local section selector

Add:

* `src/LoopRelay.Roadmap.Cli/Services/Prompts/CreateNewEpicPromptSections.cs`

Suggested shape:

```csharp
internal sealed record CreateNewEpicPromptSectionSet(
    string EpicImplementationFirstGuidance,
    string EpicAuxiliaryArtifactLimits,
    IReadOnlyDictionary<string, string> ActiveSectionSourceHashes);

internal static class CreateNewEpicPromptSections
{
    public static CreateNewEpicPromptSectionSet ForAuxiliaryArtifactPolicy(
        bool allowAuxiliaryNonImplementationFiles)
    {
        if (allowAuxiliaryNonImplementationFiles)
        {
            return new("", "", new Dictionary<string, string>());
        }

        return new(
            Core.Prompts.NonImplementation.CreateNewEpicImplementationFirstGuidance.Text,
            Core.Prompts.NonImplementation.CreateNewEpicAuxiliaryArtifactLimits.Text,
            new Dictionary<string, string>
            {
                ["CreateNewEpicImplementationFirstGuidance"] =
                    Core.Prompts.NonImplementation.CreateNewEpicImplementationFirstGuidance.SourceHash,
                ["CreateNewEpicAuxiliaryArtifactLimits"] =
                    Core.Prompts.NonImplementation.CreateNewEpicAuxiliaryArtifactLimits.SourceHash,
            });
    }
}
```

The selector may use immutable/frozen dictionaries if that matches local style. Keep it internal to Roadmap CLI unless another production prompt genuinely needs the same `CreateNewEpic`-specific behavior.

### 6. Introduce a small roadmap runtime prompt policy profile

Avoid passing a naked boolean through every prompt API while still deriving behavior from the requested setting.

Add a small internal policy record near roadmap prompt services, for example:

* `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`

Suggested shape:

```csharp
internal sealed record RoadmapRuntimePromptPolicy(
    bool AllowAuxiliaryNonImplementationFiles,
    string LegacyImplementationFirstPromptPolicy)
{
    public static RoadmapRuntimePromptPolicy FromArtifactPolicy(
        NonImplementationArtifactPolicyOptions artifactPolicy) =>
        new(
            artifactPolicy.AllowAuxiliaryNonImplementationFiles,
            ImplementationFirstPromptPolicyComposer.Compose(artifactPolicy));
}
```

This profile exists to carry the new bool and the legacy composer text during the transition. It is not a general prompt-policy framework.

### 7. Wire policy through roadmap composition

Update:

* `src/LoopRelay.Roadmap.Cli/Services/Cli/RoadmapCliComposition.cs`

Replace the single `promptPolicy` local with:

```csharp
RoadmapRuntimePromptPolicy runtimePromptPolicy =
    RoadmapRuntimePromptPolicy.FromArtifactPolicy(settings.ArtifactPolicy);
```

Use it as follows:

* Pass `runtimePromptPolicy` to `RoadmapPromptRunner`.
* Pass the same policy or a policy-identity provider to transition input resolution so snapshots can include prompt-policy identity.
* Continue passing `runtimePromptPolicy.LegacyImplementationFirstPromptPolicy` to `AgentCompletionPromptRunner`.

Do not change Plan CLI or Loop CLI composition except as required by the expanded settings record constructor.

### 8. Update runtime prompt rendering and legacy composer phase-out

Update:

* `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
* `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptRunner.cs`

Change runtime rendering so `CreateNewEpic` receives prompt-owned sections:

```csharp
"CreateNewEpic" =>
{
    CreateNewEpicPromptSectionSet sections =
        CreateNewEpicPromptSections.ForAuxiliaryArtifactPolicy(
            policy.AllowAuxiliaryNonImplementationFiles);

    return Core.Prompts.Planning.CreateNewEpic.Render(
        projectContext,
        secondaryInput,
        sections.EpicImplementationFirstGuidance,
        sections.EpicAuxiliaryArtifactLimits);
}
```

Preserve existing rendering for all other runtime prompts.

Change `RoadmapPromptRunner.RunRuntimePromptAsync(...)` so:

* It does not append `ImplementationFirstPromptPolicyComposer` for `CreateNewEpic`.
* It continues appending the legacy composer policy for every non-migrated roadmap runtime prompt.

A minimal approach is acceptable:

```csharp
string prompt = RoadmapPromptCatalog.RenderRuntime(
    runtimePromptName,
    projectContext,
    secondaryInput,
    _runtimePromptPolicy);

if (!RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy(runtimePromptName))
{
    prompt = ImplementationFirstPromptPolicyComposer.AppendPromptPolicy(
        prompt,
        _runtimePromptPolicy.LegacyImplementationFirstPromptPolicy);
}
```

For this pass, `UsesPromptOwnedNonImplementationPolicy("CreateNewEpic")` returns `true`; all other runtime prompts return `false`.

This intentionally leaves the runner with transitional composer logic. Future prompt migrations should move prompts one at a time to prompt-owned sections and then add them to the prompt-owned policy set. Delete the composer only after the set covers all current consumers.

### 9. Add prompt-policy identity to transition evidence

Update:

* `src/LoopRelay.Roadmap.Cli/Models/TransitionInputs/TransitionInputRequest.cs`
* `src/LoopRelay.Roadmap.Cli/Models/TransitionInputs/TransitionInputSnapshot.cs`
* `src/LoopRelay.Roadmap.Cli/Services/TransitionState/TransitionInputs.cs`
* `src/LoopRelay.Roadmap.Cli/Services/TransitionCoordination/RoadmapPromptTransitionRunner.cs`
* tests that construct `TransitionInputSnapshot`

Add a compact policy identity model, for example:

```csharp
internal sealed record TransitionPromptPolicyIdentity(
    string Mode,
    IReadOnlyDictionary<string, string> Inputs,
    string Hash)
{
    public static TransitionPromptPolicyIdentity None { get; } =
        Create("none-v1", new Dictionary<string, string>());

    public static TransitionPromptPolicyIdentity Create(
        string mode,
        IReadOnlyDictionary<string, string> inputs)
    {
        // Hash canonical mode + sorted inputs.
    }
}
```

Add it to `TransitionInputRequest` with a default of `None` or nullable default, so non-policy call sites can remain simple.

Add it to `TransitionInputSnapshot` and include it in `ComputeSnapshotHash`.

Policy identity rules:

* For `CreateNewEpic`, use mode `create-new-epic-prompt-owned-v1`.
* Include `allowAuxiliaryNonImplementationFiles`.
* Include `Core.Prompts.Planning.CreateNewEpic.SourceHash`.
* Include active section source hashes only when the sections are injected.
* Include an explicit `sectionMode` input such as `strict` or `omitted`.
* For legacy composer prompts, use mode `legacy-implementation-first-composer-v1` and include a hash of `LegacyImplementationFirstPromptPolicy`.
* For transition records that do not run an agent prompt or do not apply prompt policy, use `none-v1`.

Do not store the full rendered prompt in the snapshot.

### 10. Keep runtime artifact controls unchanged

Do not change:

* `ArtifactPromotionService`
* `ActiveEpicPromotionCoordinator`
* `EpicAuthoringOutputClassifier`
* `EpicArtifactValidator`
* `NonImplementationArtifactClassifier`
* `ExplicitHitlNonImplementationRequestCaptureService`
* `NonImplementationCompletionReviewService`
* ledger persistence

The new setting is prompt guidance selection only. If future work wants true auxiliary artifact authorization, that must be a separate runtime policy change with validator/review updates.

### 11. Clean up composer usage carefully

Search for:

```powershell
rg -n "ImplementationFirstPromptPolicyComposer|AllowHitlRequestedNonImplementationFiles|AllowAuxiliaryNonImplementationFiles" src tests
```

Expected after this pass:

* `CreateNewEpic` rendering no longer depends on `ImplementationFirstPromptPolicyComposer`.
* `RoadmapPromptRunner` still references the composer for non-migrated runtime prompts.
* `AgentCompletionPromptRunner` still receives legacy prompt-policy text.
* Plan CLI and Loop CLI still compose and append legacy prompt-policy text.
* Tests still cover legacy composer behavior because it remains production behavior.

Unexpected after this pass:

* C# literals containing the body text of the new section prompts.
* Old HITL setting removed from the options model.
* Old HITL setting mapped into `AllowAuxiliaryNonImplementationFiles`.
* `AllowAuxiliaryNonImplementationFiles` used to bypass runtime artifact validation.

## Testing Strategy

### Settings tests

Update `tests/LoopRelay.Permissions.Tests/Services/PermissionSettingsTests.cs`.

Cover:

* Missing `artifactPolicy` defaults both flags to `false`.
* Missing `allowAuxiliaryNonImplementationFiles` defaults it to `false`.
* Explicit `allowAuxiliaryNonImplementationFiles: true` maps to `true`.
* Explicit `allowHitlRequestedNonImplementationFiles: true` maps to the legacy flag only.
* Both flags can be specified independently.
* `config/settings.default.json` contains the new flag and keeps the legacy flag while composer consumers remain.

### Prompt section tests

Add focused tests under roadmap prompt services or orchestration primitive tests only if the helper is placed there. Preferred location is Roadmap CLI tests because the helper is Roadmap CLI-owned.

Cover:

* `CreateNewEpicPromptSections.ForAuxiliaryArtifactPolicy(false)` returns both section strings.
* `true` returns empty strings.
* Strict mode returns active section source hashes.
* Allowed mode uses `sectionMode = omitted` and does not include active section source hashes.
* No section body text is duplicated in C# files.

### Rendered prompt tests

Add focused tests around `RoadmapPromptCatalog.RenderRuntime(...)`.

Cover:

* `CreateNewEpic` with `AllowAuxiliaryNonImplementationFiles == false` includes the new `CreateNewEpic` section headings or stable markers.
* `CreateNewEpic` with `true` omits those markers.
* Both modes include the `.agents/epic.md` required output instruction.
* Strict mode does not include the old roadmap-oriented `InvalidContent.prompt` markers.
* Strict mode does not contain obvious contradictions such as forbidding the strategic epic content that `CreateNewEpic.prompt` requires.

### Runner behavior tests

Use existing fake runtime / captured prompt patterns or add a small focused fake.

Cover:

* `RunRuntimePromptAsync("CreateNewEpic", ...)` does not append `ImplementationFirstPromptPolicyComposer.SectionHeading`.
* `RunRuntimePromptAsync("SelectNextEpic", ...)` or another non-migrated runtime prompt still appends `ImplementationFirstPromptPolicyComposer.SectionHeading`.
* `AgentCompletionPromptRunner` behavior remains covered by existing tests or updated tests.

### Transition evidence tests

Update `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState/TransitionInputResolverTests.cs` and journal/promotion tests affected by the snapshot constructor.

Cover:

* `CreateNewEpic` snapshots differ when `allowAuxiliaryNonImplementationFiles` differs and all other inputs are identical.
* `CreateNewEpic` strict snapshots include prompt-owned policy mode and active section source hashes.
* `CreateNewEpic` allowed snapshots include policy mode and `sectionMode = omitted`.
* Legacy composer prompts include a legacy composer policy hash.
* `none-v1` is used for non-agent routing transitions where appropriate.

### Regression tests

Preserve or update existing tests for:

* `ImplementationFirstPromptPolicyComposer.Compose(...)`
* legacy HITL exception wording
* no hard-coded composer body duplication
* prompt rendering signature changes
* transition journal serialization/deserialization with the expanded snapshot
* SQLite persistence round trip for snapshots, if covered by existing tests

Prefer stable headings, mode names, hashes, and required output markers over asserting full prompt bodies.

## Verification

Run focused suites first:

```powershell
dotnet test tests/LoopRelay.Permissions.Tests/LoopRelay.Permissions.Tests.csproj
dotnet test tests/LoopRelay.Roadmap.Cli.Tests/LoopRelay.Roadmap.Cli.Tests.csproj
dotnet test tests/LoopRelay.Orchestration.Primitives.Tests/LoopRelay.Orchestration.Primitives.Tests.csproj
```

Then run the full solution:

```powershell
dotnet test
```

If generated prompt classes are produced by build, run the normal build/test path that triggers source generation rather than manually editing generated files.

## Acceptance Criteria

* `CreateNewEpic.prompt` uses explicit placeholders for conditionally injected non-implementation guidance.
* The injected sections are `CreateNewEpic`-specific and do not reuse the roadmap-oriented `InvalidContent.prompt`.
* Prompt section bodies live in `.prompt` files, not C# string literals.
* `CreateNewEpic.Render(...)` receives section strings selected from `AllowAuxiliaryNonImplementationFiles`.
* `AllowAuxiliaryNonImplementationFiles` defaults to `false`.
* `AllowHitlRequestedNonImplementationFiles` remains available for legacy composer consumers.
* The old HITL flag is not aliased into the new auxiliary-artifact flag.
* With auxiliary files disabled, `CreateNewEpic` includes strict guidance against auxiliary non-implementation artifacts.
* With auxiliary files enabled, `CreateNewEpic` omits those strict sections.
* Both render modes still require `.agents/epic.md` and preserve CreateNewEpic blocking behavior.
* `CreateNewEpic` runtime prompt execution no longer appends or depends on `ImplementationFirstPromptPolicyComposer`.
* Non-migrated roadmap runtime prompts still receive the existing composer output.
* Plan CLI, Loop CLI, and Completion composer behavior remains operational.
* Transition snapshots include prompt-policy identity and change when the `CreateNewEpic` policy branch changes.
* Runtime artifact validation, promotion, HITL capture, and non-implementation review are unchanged.
* Roadmap CLI has access to `settings.default.json` consistently with the other CLIs.

## Deferred Work

Do not perform these changes in this pass:

* Delete `ImplementationFirstPromptPolicyComposer`.
* Migrate Plan CLI, Loop CLI, Completion, or non-`CreateNewEpic` roadmap prompts to prompt-owned sections.
* Convert `AllowAuxiliaryNonImplementationFiles` into runtime artifact authorization.
* Redesign the full non-implementation policy model.
* Replace post-execution non-implementation review.
* Rework `CreateNewRoadmap`; it is reference text, not production prompt authority.

Future prompt migrations should move one prompt at a time from legacy composer append to prompt-owned generated sections, preserving behavior for all remaining consumers until the composer has no production references.

## Risks and Notes

* Editing `CreateNewEpic.prompt` changes its generated `SourceHash`; this is expected.
* Adding prompt-owned section placeholders means `CreateNewEpic.SourceHash` alone no longer identifies every instruction sent to the model. The prompt-policy identity added to transition snapshots covers the active section hashes and branch value.
* The new boolean name can sound like runtime authorization. Tests and section wording should make clear that it only controls prompt guidance injection in this pass.
* Keeping the composer for non-migrated prompts creates a temporary mixed model. This is intentional and lower risk than removing policy from unrelated prompts.
* Empty rendered sections may create extra whitespace; catch this in rendered prompt tests rather than adding formatting logic unless the output is visibly poor.
