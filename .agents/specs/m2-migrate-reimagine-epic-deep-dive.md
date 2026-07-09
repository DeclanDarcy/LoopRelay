# Milestone 2 Deep Dive: Migrate `ReimagineEpic`

## 1. Milestone Summary

| Field | Value |
|---|---|
| Milestone Identifier | Milestone 2 |
| Milestone Name | Migrate `ReimagineEpic` |
| Roadmap Position | 2 of 5 |
| Implementation Role | Extend the prompt-owned active-epic rewrite pattern from minimal realignment to audit-grounded full replacement. |
| Short Description | Move `ReimagineEpic` implementation-first guidance into generated prompt-owned sections while preserving the existing `.agents/epic.md` replacement transition. |

Primary outcomes:

- `ReimagineEpic` strict rendering injects prompt-owned guidance and auxiliary-artifact limits.
- Allowed auxiliary mode omits strict sections without weakening the replacement epic contract.
- `RoadmapPromptRunner` skips `ImplementationFirstPromptPolicyComposer` for `ReimagineEpic`.
- Transition prompt-policy identity records `reimagine-epic-prompt-owned-v1` with prompt source hash, policy branch, section mode, and active section source hashes.
- The active epic rewrite transition, promotion, blocked output, and validation path remain unchanged.

## 2. Normative Basis

Roadmap authority:

- `.agents/specs/roadmap.md` Objective, Scope Boundaries, Reference Pattern, Migration Order, Milestone 2, Cross-Milestone Rules, Verification Matrix, and Done Definition.

Architectural authority:

- `.agents/specs/audit.md` Architecture Observations, CreateNewEpic Baseline, ReimagineEpic audit section, Section Reuse Observations, Emerging Organization Pattern, Transition and Provenance Observations, Settings Flow Observations, and Testing Observations.

Implementation authority:

- `src/LoopRelay.Core/Prompts/Planning/ReimagineEpic.prompt`
- `src/LoopRelay.Core/Prompts/Planning/CreateNewEpic.prompt`
- `src/LoopRelay.Core/Prompts/NonImplementation/CreateNewEpicImplementationFirstGuidance.prompt`
- `src/LoopRelay.Core/Prompts/NonImplementation/CreateNewEpicAuxiliaryArtifactLimits.prompt`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/CreateNewEpicPromptSections.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptRunner.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/PromptContractRegistry.cs`

Supporting context:

- `tests/LoopRelay.Roadmap.Cli.Tests/Services/Prompts/CreateNewEpicPromptPolicyTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState/TransitionInputResolverTests.cs`

No authority is introduced outside the roadmap, audit, and current implementation pattern.

## 3. Objective

Implement prompt-owned implementation-first policy for `ReimagineEpic` by adding prompt-owned section sources, injecting them through generated prompt rendering, skipping the legacy composer at runtime, and recording prompt-policy provenance while preserving audit-grounded full replacement of `.agents/epic.md`.

## 4. Non-Goals

- Do not migrate `GenerateMilestoneDeepDivesForEpic` or `SplitEpic`.
- Do not modify `RealignEpic` behavior except where shared regression tests require recognizing it as already migrated.
- Do not make `ReimagineEpic` a free-form design or architecture proposal prompt.
- Do not delete or globally alter `ImplementationFirstPromptPolicyComposer`.
- Do not change `ActiveEpicRewriteTransition`, `ActiveEpicPromotionCoordinator`, blocked output classification, or `EpicArtifactValidator` unless an existing bug unrelated to policy ownership is exposed.
- Do not introduce a generic registry for unmigrated prompts.
- Do not let `AllowAuxiliaryNonImplementationFiles` weaken the primary `.agents/epic.md` replacement requirement.
- Do not authorize companion design reports, rationale appendices, research notes, architecture proposals, governance notes, or raw audit-detail files.

## 5. Runtime / System State Before

- `CreateNewEpic` is prompt-owned and `RealignEpic` is expected to be prompt-owned after Milestone 1.
- `ReimagineEpic` still renders directly through `Core.Prompts.Planning.ReimagineEpic.Render(projectContext, secondaryInput)`.
- `RoadmapPromptRunner` appends the legacy composer to `ReimagineEpic`.
- Transition snapshots for `ReimagineEpic` identify the legacy composer branch.
- The existing active epic rewrite path can promote a replacement `.agents/epic.md` or classify `# Epic Reimagination Blocked`.

## 6. Runtime / System State After

- `ReimagineEpic` owns two generated section bodies in `src/LoopRelay.Core/Prompts/NonImplementation`.
- `ReimagineEpic.prompt` has placeholders for those sections.
- `RoadmapPromptCatalog.RenderRuntime()` uses a `ReimagineEpic` helper that injects selected sections.
- `UsesPromptOwnedNonImplementationPolicy("ReimagineEpic")` returns true.
- `RoadmapPromptRunner` sends `ReimagineEpic` without the legacy composer heading.
- `RoadmapRuntimePromptPolicy.CreateIdentity("ReimagineEpic")` emits `reimagine-epic-prompt-owned-v1`.
- The replacement epic remains audit-grounded through existing transition and promotion services.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| M2-C1 Prompt-owned ReimagineEpic guidance | `ReimagineEpic.prompt` plus generated non-implementation sections | Strict implementation-first guidance for audit-grounded full replacement | Project context, audit input, auxiliary artifact policy | Rendered prompt containing `# ReimagineEpic Implementation-First Guidance` and `# ReimagineEpic Auxiliary Artifact Limits` in strict mode | Prompt generator and section files | Strict prompt sanctions replacement `.agents/epic.md` and coherent replacement milestone roadmap | `RoadmapPromptRunner`, transition snapshots |
| M2-C2 Auxiliary branch selection | `ReimagineEpicPromptSections` | Strict versus omitted section selection | `AllowAuxiliaryNonImplementationFiles` | Section text and source hash map | `RoadmapRuntimePromptPolicy` | Allowed mode removes placeholders and keeps `.agents/epic.md` required | Prompt catalog, identity |
| M2-C3 Runtime composer skip | `RoadmapPromptCatalog` and `RoadmapPromptRunner` | Treat `ReimagineEpic` as prompt-owned | Runtime prompt name | Prompt without legacy composer heading | Catalog classification | Runner capture lacks composer heading for `ReimagineEpic` | Agent runtime |
| M2-C4 Transition policy provenance | `RoadmapRuntimePromptPolicy` | Prompt-specific policy identity | Prompt source hash, section hashes, policy branch | `TransitionPromptPolicyIdentity` mode `reimagine-epic-prompt-owned-v1` | Selector and generated prompt source hash | Strict and allowed identities differ; strict includes active section hashes | Transition snapshotting |
| M2-C5 Replacement contract preservation | Existing active epic services | Preserve audit-grounded `.agents/epic.md` replacement path | Prompt output or blocked output | Promoted replacement epic or blocked classification | Existing classifier, validator, promotion path | Existing active epic rewrite tests remain passing | Roadmap state machine |

## 8. Architectural Responsibilities

- `ReimagineEpic.prompt` owns the replacement reasoning model and injected section placement.
- Generated non-implementation `.prompt` files own section body text and source hashes.
- A prompt-specific selector owns strict/allowed section choice.
- `RoadmapPromptCatalog` owns render routing and prompt-owned policy classification.
- `RoadmapRuntimePromptPolicy` owns prompt-policy identity and provenance inputs.
- Existing active epic rewrite services own artifact classification, validation, and promotion.
- Audit evidence remains the source authority for material redesign decisions; prompt sections constrain artifact boundaries but do not reinterpret audit content.

## 9. Components and Modules

| Component | Purpose | Responsibilities | Owned State | Consumed State | Public Contracts | Internal Contracts | Dependencies | Tests Required |
|---|---|---|---|---|---|---|---|---|
| `ReimagineEpicImplementationFirstGuidance.prompt` | Replacement-specific implementation-first guidance | Sanction `.agents/epic.md` replacement only when audit disposition requires it | Generated text and source hash | None | Marker heading and generated class | Do not copy `CreateNewEpic` text unchanged | Prompt generator | Marker and C# body scan tests |
| `ReimagineEpicAuxiliaryArtifactLimits.prompt` | Strict side-artifact prohibition | Forbid design reports, raw audit appendices, rationale docs, research notes, and governance notes | Generated text and source hash | None | Marker heading and generated class | Empty in allowed auxiliary mode | Prompt generator | Selector tests |
| `ReimagineEpic.prompt` | Owning planning prompt | Preserve audit-grounded full replacement contract and placeholders | Prompt template and source hash | Project context, audit input, injected sections | Generated render method | Placeholders fully substituted | Prompt generator | Render tests |
| `ReimagineEpicPromptSections` | Section selector | Return strict or omitted section set | None | Auxiliary policy | Selector method | Active hashes align with injected sections | Generated section classes | Unit tests |
| `RoadmapPromptCatalog` | Render and classification | Add helper and classify prompt-owned | None | Runtime prompt name and policy | `RenderRuntime`, `UsesPromptOwnedNonImplementationPolicy` | Helper does not affect other prompts | Selector | Render and runner tests |
| `RoadmapRuntimePromptPolicy` | Identity | Emit prompt-specific identity branch | Identity input map | Prompt and section hashes | `CreateIdentity("ReimagineEpic")` | Sorted deterministic inputs | Selector | Identity tests |

## 10. Repository and File Impact

Expected changes:

- Add `src/LoopRelay.Core/Prompts/NonImplementation/ReimagineEpicImplementationFirstGuidance.prompt`.
- Add `src/LoopRelay.Core/Prompts/NonImplementation/ReimagineEpicAuxiliaryArtifactLimits.prompt`.
- Modify `src/LoopRelay.Core/Prompts/Planning/ReimagineEpic.prompt`.
- Add `src/LoopRelay.Roadmap.Cli/Services/Prompts/ReimagineEpicPromptSections.cs`.
- Modify `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`.
- Modify `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`.
- Add focused tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/Prompts`.
- Extend transition identity coverage under `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState` if not already generalized from Milestone 1.

No changes are expected to artifact promotion, active epic transition, validators, parser boundaries, repository write semantics, split handling, spec generation, or projection freshness.

## 11. Public Contracts

- `ReimagineEpic` remains the runtime prompt for audit-grounded full replacement of `.agents/epic.md`.
- `# Epic Reimagination Blocked` remains the blocked output contract.
- Strict render includes `# ReimagineEpic Implementation-First Guidance` and `# ReimagineEpic Auxiliary Artifact Limits`.
- Allowed auxiliary render omits strict sections without removing the required `.agents/epic.md` replacement output.
- `RoadmapPromptRunner` does not append `ImplementationFirstPromptPolicyComposer.SectionHeading` to `ReimagineEpic`.
- `TransitionPromptPolicyIdentity.Mode` for `ReimagineEpic` becomes `reimagine-epic-prompt-owned-v1`.
- Prompt contract registry remains unchanged: input `.agents/epic.md`, output `.agents/epic.md`, decision `Reimagine`, writer `ArtifactPromotionService`, parser `EpicAuthoringOutputClassifier+EpicArtifactValidator`.

## 12. Internal Contracts

- The selector uses only `AllowAuxiliaryNonImplementationFiles`.
- Strict mode injects both generated sections and exposes their source hashes.
- Allowed mode passes empty section strings and an empty active hash map.
- Render and identity paths must call equivalent section selection logic.
- The prompt source hash key must be `reimagineEpicSourceHash` or another deterministic prompt-specific name used consistently by tests.
- A render failure must not fall back to appending the legacy composer.
- Existing blocked output and validation paths must receive the same model output shape as before the migration.

## 13. Data and State Model

| State Object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Downstream Consumers |
|---|---|---|---|---|---|---|---|---|
| Reimagine guidance section | `.prompt` source | Source -> generated class -> render input | Repository source and build output | Source controlled | Source hash | Marker and source-location tests | Rebuild generated prompts | Prompt catalog |
| Reimagine auxiliary limits section | `.prompt` source | Source -> generated class -> render input | Repository source and build output | Source controlled | Source hash | Selector tests | Rebuild generated prompts | Prompt catalog |
| Rendered `ReimagineEpic` prompt | `RoadmapPromptCatalog` | Policy selection -> render -> agent request | Ephemeral | Per invocation | Runtime prompt name and rendered text | Render and runner tests | Re-render from source and settings | Agent runtime |
| Prompt policy identity | `RoadmapRuntimePromptPolicy` | Snapshot creation | Persisted transition input evidence | Immutable after snapshot | Mode plus sorted input hashes | Transition identity tests | Recompute from source and policy | Transition persistence |
| Replacement active epic | Existing promotion path | Author output -> classify -> validate -> promote | Retained `.agents/epic.md` | Mutated by promotion only | Artifact path and content hash | Existing validator | Existing failure persistence | Later roadmap prompts |

## 14. Lifecycle and State Transitions

Prompt-owned policy lifecycle:

```text
Policy Loaded
  -> Reimagine Sections Selected
  -> Reimagine Prompt Rendered
  -> Prompt Policy Identity Created
  -> Agent Runtime Invoked
  -> Existing Rewrite Promotion Path
```

| Transition | Trigger | Preconditions | Result | Failure Mode | Audit or Evidence Record |
|---|---|---|---|---|---|
| Policy Loaded -> Sections Selected | Prompt execution starts | Runtime policy exists | Strict or omitted section set | Selector references missing generated class | Unit test or runtime exception |
| Sections Selected -> Prompt Rendered | Catalog helper runs | Placeholders exist | Prompt has replacement-specific guidance | Placeholder remains or prompt signature mismatch | Render test |
| Prompt Rendered -> Identity Created | Transition snapshot resolved | Prompt name is `ReimagineEpic` | Identity mode is prompt-owned | Legacy mode emitted | Identity test |
| Identity Created -> Agent Runtime Invoked | Runner sends prompt | Catalog classifies prompt-owned | No legacy composer heading | Composer appended | Runner capture test |
| Agent Output -> Promotion Path | Model completes | Output is replacement epic or blocked document | Existing classifier validates/promotes or blocks | Invalid epic rejected | Existing transition evidence |

## 15. Execution Flow

Startup or initialization:

- Generated prompt classes include the new `ReimagineEpic` render parameters and section source hashes.
- Runtime policy is built from `NonImplementationArtifactPolicyOptions`.

Normal operation:

```text
RoadmapPromptRunner
  -> RoadmapPromptCatalog.RenderRuntime("ReimagineEpic", policy)
  -> ReimagineEpicPromptSections.ForAuxiliaryArtifactPolicy(...)
  -> Core.Prompts.Planning.ReimagineEpic.Render(projectContext, auditInput, selectedSections)
  -> UsesPromptOwnedNonImplementationPolicy("ReimagineEpic") == true
  -> Agent runtime receives prompt
  -> Existing active epic promotion path handles output
```

Failure flow:

- Prompt render or identity failures fail before agent execution.
- Invalid replacement epic content fails through existing classifier or validator behavior.
- Blocked output remains `# Epic Reimagination Blocked`.

Recovery flow:

- Re-running after correcting prompt source or generated code recomputes deterministic source hashes.
- Existing transition persistence records and recovers failed runtime or validation states.

Shutdown or completion:

- No new shutdown behavior is introduced.

## 16. Dependency Closure

| Dependency | Classification | Required State |
|---|---|---|
| Milestone 1 `RealignEpic` migration | Soft prerequisite | Confirms active-epic rewrite pattern, but this milestone can use `CreateNewEpic` if Milestone 1 implementation has not produced shared helper changes. |
| `CreateNewEpic` reference pattern | Hard prerequisite | Generated section files, selector, catalog helper, and identity branch exist as implementation model. |
| `ReimagineEpic.prompt` | Hard prerequisite | Existing replacement prompt remains authoritative for audit-grounded full replacement behavior. |
| Active epic rewrite services | Inherited capability | Existing transition, classifier, validator, and promotion behavior are available. |
| Prompt source generator | Supporting infrastructure | New prompt source and modified render signature compile. |
| `AllowHitlRequestedNonImplementationFiles` | Explicitly unavailable dependency | Must not affect prompt-owned section selection. |
| Later spec and split migrations | Future dependency | Not required to begin this milestone. |

This milestone enables:

- Milestone 3 by proving prompt-owned policy can preserve a broader roadmap artifact contract.
- Milestone 5 by adding `ReimagineEpic` to the migrated prompt set.

## 17. Failure Modes

| Failure Mode | Detection Method | Expected System Behavior | Recovery Path | Diagnostic Output | Test Coverage Required |
|---|---|---|---|---|---|
| Strict render lacks Reimagine markers | Render assertion | Test fails | Fix section placeholders or selector | Missing marker assertion | Render test |
| Prompt implies design reports are valid | Text assertion or review | Test fails or prompt review blocks | Tighten auxiliary limits section | Assertion on prohibited terms | Prompt-specific render test |
| Allowed mode weakens `.agents/epic.md` contract | Render assertion | Test fails | Keep primary output language in base prompt | Missing `.agents/epic.md` assertion | Allowed render test |
| Legacy composer appended | Captured runner prompt | Test fails | Update catalog classification | Composer heading in prompt | Runner test |
| Identity lacks prompt source hash or section hashes | Identity assertion | Test fails | Add deterministic identity inputs | Missing key diagnostics | Transition identity test |
| Full replacement path is altered | Existing active epic tests | Test fails | Remove unrelated transition changes | Existing test output | Regression tests |
| `CreateNewEpic` text copied unchanged | Source/prompt comparison or review | Test or review fails | Write Reimagine-specific section text | Matched body lines if automated | Section body/source scan |

## 18. Validation and Invariants

| Invariant | Source Authority | Enforcement Point | Failure Behavior | Test Strategy |
|---|---|---|---|---|
| Replacement epic is sanctioned only as `.agents/epic.md` | Roadmap Milestone 2 | Prompt section text and base prompt | Side artifacts are allowed or primary output weakened | Render assertions |
| Strategic need and desired capability remain authoritative unless audit justifies boundary changes | Roadmap Section Semantics | Reimagine guidance text | Prompt encourages ungrounded redesign | Prompt-specific assertions |
| Material redesign decisions trace to audit, projection, or repository-grounded capability needs | Roadmap Section Semantics and audit | Prompt guidance | Free-form architecture proposal behavior | Prompt text assertions |
| Prompt-owned identity uses `reimagine-epic-prompt-owned-v1` | Roadmap Acceptance | `RoadmapRuntimePromptPolicy` | Legacy identity emitted | Identity test |
| Existing artifact contracts remain unchanged | Roadmap Scope Boundaries | Prompt contract and existing services | Writer/parser boundaries drift | Existing contract tests |
| Legacy composer remains for out-of-scope prompts | Roadmap Scope Boundaries | Runner branch | Composer accidentally removed globally | Companion runner test |

## 19. Testing Strategy

- Unit tests: `ReimagineEpicPromptSections` strict/allowed selection and source hash map.
- Render tests: strict markers, replacement `.agents/epic.md` sanction, coherent replacement milestone roadmap language, no side design report implication, placeholder removal.
- Runtime runner tests: no legacy composer for `ReimagineEpic`, legacy composer retained for an out-of-scope prompt.
- Transition identity tests: prompt-owned mode, prompt source hash, strict section hashes, strict/allowed branch difference.
- Regression tests: existing active epic rewrite, promotion, blocked-output, classifier, validator, and prompt contract tests.
- Failure-path tests: invalid replacement epic output remains rejected by existing validators.
- Performance smoke tests: render remains generated-prompt string substitution with no runtime file IO.

## 20. Fixtures and Test Data

- Minimal strict render fixture: project context plus audit input requiring a full epic replacement.
- Allowed auxiliary fixture: same inputs with `AllowAuxiliaryNonImplementationFiles=true`.
- Replacement sanction fixture: prompt text containing `.agents/epic.md` and coherent replacement milestone roadmap language.
- Invalid auxiliary fixture terms: companion design reports, rationale appendices, research notes, architecture proposals, governance notes, raw audit-detail files.
- Legacy control prompt: an unmigrated roadmap runtime prompt that must still receive the composer.
- Identity fixtures: strict and allowed `RoadmapRuntimePromptPolicy` values.
- Blocked output fixture: `# Epic Reimagination Blocked`.

## 21. Acceptance Demonstration

Setup:

- Build the solution with the new prompt files and modified generated render signature.
- Use the scripted agent runtime already used by prompt policy tests.

Input:

- Runtime prompt name `ReimagineEpic`.
- Project context `"project context"`.
- Secondary input `"audit requires reimagination"`.
- Strict policy with `AllowAuxiliaryNonImplementationFiles=false`.

Execution steps:

1. Render `RoadmapPromptCatalog.RenderRuntime("ReimagineEpic", context, audit, strictPolicy)`.
2. Capture a `RoadmapPromptRunner.RunRuntimePromptAsync("ReimagineEpic", ...)` invocation.
3. Compute strict and allowed `CreateIdentity("ReimagineEpic")`.
4. Run focused prompt policy and transition identity tests.

Expected output:

- Strict prompt contains `# ReimagineEpic Implementation-First Guidance`.
- Strict prompt contains `# ReimagineEpic Auxiliary Artifact Limits`.
- Strict and allowed prompts both retain `.agents/epic.md` as the primary output.
- Captured runner prompt lacks `ImplementationFirstPromptPolicyComposer.SectionHeading`.

Expected persisted state:

- Transition identity mode is `reimagine-epic-prompt-owned-v1`.
- Strict identity records prompt source hash and active section source hashes.
- Allowed identity records omitted section mode and differs from strict identity.

Expected diagnostics:

- No diagnostics on success.
- Test failures identify missing markers, composer leakage, or identity mismatch.

Verification command:

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~ReimagineEpicPromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests"
```

## 22. Certification Evidence

- Passing `ReimagineEpic` selector and render tests.
- Passing runtime runner composer-skip test for `ReimagineEpic`.
- Passing companion legacy-prompt composer test.
- Passing prompt policy identity tests for strict and allowed branches.
- Passing source scan proving section text is not hard-coded in C#.
- Passing existing active epic rewrite, promotion, classifier, validator, blocked-output, and prompt contract tests.

## 23. Implementation Plan

| Step | Purpose | Deliverables | Dependencies | Completion Criteria |
|---|---|---|---|---|
| Add Reimagine section sources | Move replacement-specific policy text into prompt-owned source | Two `ReimagineEpic*.prompt` files | Roadmap Section Semantics | Generated classes expose marker headings and source hashes |
| Add placeholders to `ReimagineEpic.prompt` | Enable generated section injection | Placeholder additions and render signature update | Section source names | Strict and allowed render tests substitute placeholders |
| Implement selector | Match `CreateNewEpic` shape for this prompt | `ReimagineEpicPromptSections` | Generated section classes | Selector tests pass |
| Update catalog | Route rendering and classify prompt-owned | `RenderReimagineEpic`; classification update | Selector | Runtime render and skip tests pass |
| Update policy identity | Record prompt-owned branch and hashes | `reimagine-epic-prompt-owned-v1` identity branch | Selector and prompt source hash | Identity tests pass |
| Add focused tests | Certify behavior and no text hard-coding | Prompt policy tests plus identity assertions | Implementation steps | All focused tests pass |
| Run affected regressions | Prove transition behavior preserved | Test results | Compiled implementation | Existing active-epic tests pass |

## 24. Parallel Work Opportunities

| Parallel Lane | Scope | Owner Type | Dependencies | Synchronization Point | Integration Risk |
|---|---|---|---|---|---|
| Section wording | Write replacement-specific prompt-owned sections | Prompt/runtime engineer | Roadmap and audit semantics | Before marker and prohibited-artifact tests finalize | Medium |
| Runtime wiring | Selector, catalog helper, classification | Runtime engineer | Agreed section class names | When generated prompt compiles | Low |
| Policy identity | Identity branch and tests | Runtime/test engineer | Selector API | Before transition snapshot tests | Medium |
| Regression preservation | Active epic rewrite tests | Test engineer | Compileable implementation | Certification | Low |

## 25. Risks and Mitigations

| Risk | Class | Impact | Likelihood | Earliest Detection Point | Mitigation | Fallback |
|---|---|---|---|---|---|---|
| Full replacement becomes free-form redesign | Architectural | Prompt may produce unsupported architecture proposals | Medium | Prompt text assertions | Tie material changes to audit, projection, or repository capability needs | Tighten guidance section |
| Allowed auxiliary mode weakens primary artifact | Implementation | Prompt may omit replacement `.agents/epic.md` | Low | Allowed render test | Keep primary output requirement in base prompt | Restore base prompt wording |
| Transition identity incomplete | Data | Prompt policy changes not visible in provenance | Medium | Identity tests | Include prompt source hash, branch, section mode, active hashes | Fix identity branch |
| Active rewrite services changed unnecessarily | Integration | Existing behavior regresses | Low | Existing tests | Keep services unchanged | Revert unrelated changes |
| Shared helper added prematurely | Maintainability | New abstraction affects unmigrated prompts | Medium | Diff review | Keep prompt-specific selector | Remove abstraction |

## 26. Observability and Diagnostics

- Rendered prompt tests expose strict markers, allowed omission, and primary artifact retention.
- Transition identity records mode and source hashes for provenance inspection.
- Runtime runner diagnostics remain unchanged and include agent stderr on failed turns.
- Existing artifact validation diagnostics remain the authority for malformed replacement epics.
- No new metrics or health checks are required.
- Debug views are limited to captured prompts and transition snapshots.

## 27. Performance and Scalability Considerations

- Baseline: generated prompt string rendering only.
- Likely bottleneck: no new runtime IO or heavy computation.
- Scaling risk: switch branches grow as more prompts migrate.
- Measurement: normal build and focused test runtime.
- Deferred optimization: a small identity helper may be introduced only if `CreateNewEpic`, `RealignEpic`, and `ReimagineEpic` reveal obvious mechanical duplication.

## 28. Security and Safety Considerations

- Preserve validation before promotion to prevent malformed or unsafe `.agents/epic.md` writes.
- Do not authorize any file outside the active epic artifact.
- Treat audit input and model output as untrusted text until existing validators accept it.
- Preserve blocked output behavior without side-channel explanation files.
- Keep policy provenance deterministic so prompt-source changes are traceable.
- Do not broaden permissions or artifact write surfaces.

## 29. Documentation Updates

No documentation-only repository deliverable is required.

Allowed source-adjacent text changes:

- Runtime `.prompt` section bodies and placeholders.
- Test names or comments that accurately describe prompt-owned policy behavior.

Do not create architecture docs, migration notes, reports, ADRs, RFCs, or audit appendices as implementation outputs.

## 30. Exit Criteria

Milestone 2 is complete only when:

- Both `ReimagineEpic` section source files exist and are generated.
- `ReimagineEpic.prompt` accepts and substitutes section placeholders.
- Strict render includes both `ReimagineEpic` section markers.
- Allowed render omits strict markers, removes placeholders, and retains the `.agents/epic.md` contract.
- `RoadmapPromptRunner` does not append the legacy composer to `ReimagineEpic`.
- `RoadmapRuntimePromptPolicy.CreateIdentity("ReimagineEpic")` emits `reimagine-epic-prompt-owned-v1`.
- Strict identity includes prompt source hash and active section source hashes.
- Existing active epic rewrite, promotion, blocked-output, validator, parser, and prompt contract tests remain passing.
- No future milestone capability is falsely claimed.

## 31. Transition to Next Milestone

Milestone 2 hands off:

- Prompt-owned active-epic rewrite coverage for both minimal realignment and full reimagination.
- Proven strict/allowed policy behavior for `.agents/epic.md` artifact-authoring prompts.
- Additional identity branch pattern for the final retirement checkpoint.
- Evidence that prompt-owned policy can preserve audit-grounded replacement without the legacy composer.

Limitations carried forward:

- `GenerateMilestoneDeepDivesForEpic` and `SplitEpic` still use the legacy composer.
- The strongest semantic tension, sanctioned `.agents/specs/*.md` generation, remains unresolved until Milestone 3.
- Composer deletion remains out of scope.
