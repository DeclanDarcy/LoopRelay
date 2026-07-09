# Milestone 3 Deep Dive: Migrate `GenerateMilestoneDeepDivesForEpic`

## 1. Milestone Summary

| Field | Value |
|---|---|
| Milestone Identifier | Milestone 3 |
| Milestone Name | Migrate `GenerateMilestoneDeepDivesForEpic` |
| Roadmap Position | 3 of 5 |
| Implementation Role | Resolve the strongest semantic conflict between sanctioned implementation-planning specs and the legacy anti-auxiliary-artifact composer. |
| Short Description | Move `GenerateMilestoneDeepDivesForEpic` to prompt-owned implementation-first guidance while preserving `.agents/specs/*.md` as the required primary output bundle. |

Primary outcomes:

- `GenerateMilestoneDeepDivesForEpic` strict rendering injects prompt-owned guidance and auxiliary-artifact limits.
- Strict and allowed auxiliary modes both keep one `.agents/specs/*.md` output per epic milestone as the required contracted bundle.
- `RoadmapPromptRunner` skips `ImplementationFirstPromptPolicyComposer` for this prompt.
- Transition prompt-policy identity records `generate-milestone-deep-dives-for-epic-prompt-owned-v1` with source-hash provenance.
- `GenerateMilestoneDeepDivesTransition`, bundle extraction, manifest writing, specs-ready marking, HITL evidence capture, execution preparation provenance, and roadmap invariant validation remain unchanged.

## 2. Normative Basis

Roadmap authority:

- `.agents/specs/roadmap.md` Objective, Scope Boundaries, Reference Pattern, Migration Order, Milestone 3, Cross-Milestone Rules, Verification Matrix, and Done Definition.

Architectural authority:

- `.agents/specs/audit.md` Architecture Observations, GenerateMilestoneDeepDivesForEpic audit section, Section Reuse Observations, Transition and Provenance Observations, Settings Flow Observations, Testing Observations, and Composer Retirement Readiness.

Implementation authority:

- `src/LoopRelay.Core/Prompts/Planning/GenerateMilestoneDeepDivesForEpic.prompt`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptRunner.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/PromptContractRegistry.cs`
- `src/LoopRelay.Roadmap.Cli/Services/ArtifactBundles/BundleFileExtractor.cs`
- `src/LoopRelay.Roadmap.Cli/Services/ArtifactBundles/BundleManifestWriter.cs`
- `src/LoopRelay.Roadmap.Cli/Services/ExecutionPreparation/*`
- `src/LoopRelay.Roadmap.Cli/Services/ArtifactManagement/InvariantValidator.cs`

Supporting context:

- `tests/LoopRelay.Roadmap.Cli.Tests/Services/Prompts/CreateNewEpicPromptPolicyTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState/TransitionInputResolverTests.cs`
- Existing bundle extraction, execution preparation, invariant, and roadmap state machine tests.

No authority is introduced outside the roadmap, audit, and existing implementation contracts.

## 3. Objective

Implement prompt-owned implementation-first policy for `GenerateMilestoneDeepDivesForEpic` while explicitly preserving `.agents/specs/*.md` as the primary machine-consumed implementation-planning artifact bundle required for later execution preparation.

## 4. Non-Goals

- Do not migrate `SplitEpic`; it remains Milestone 4.
- Do not change milestone extraction, bundle extraction, manifest writing, specs-ready marking, HITL evidence capture, execution preparation provenance, invariant validation, or roadmap state transitions.
- Do not change the required output from one spec per milestone under `.agents/specs/*.md`.
- Do not let strict auxiliary policy suppress, weaken, or reclassify the primary spec bundle.
- Do not allow allowed auxiliary mode to replace specs with narrative.
- Do not generate code, execution prompts, implementation scripts, design essays, governance reports, research notes, or companion planning documents.
- Do not alter artifact authorization, validators, parser boundaries, repository write semantics, or projection freshness tracking.
- Do not introduce a generic policy framework for prompts outside the migration scope.

## 5. Runtime / System State Before

- `CreateNewEpic`, `RealignEpic`, and `ReimagineEpic` are expected to be prompt-owned after Milestones 1 and 2.
- `GenerateMilestoneDeepDivesForEpic` still renders directly through `Core.Prompts.Planning.GenerateMilestoneDeepDivesForEpic.Render(projectContext)`.
- `RoadmapPromptRunner` appends the legacy composer, whose generic planning prohibitions semantically conflict with this prompt's required `.agents/specs/*.md` output.
- Prompt contract registry identifies required input `.agents/epic.md`, required output `.agents/specs`, decision `Generate Specs`, writer `SpecBundleWriter`, and parser `BundleFileExtractor`.
- The transition can extract a multi-file bundle, write specs, write a bundle manifest, mark specs ready, capture HITL evidence, record execution preparation provenance, and validate invariants.

## 6. Runtime / System State After

- `GenerateMilestoneDeepDivesForEpic` owns prompt-specific implementation-first and auxiliary-artifact sections.
- `GenerateMilestoneDeepDivesForEpic.prompt` has explicit placeholders for injected sections.
- `RoadmapPromptCatalog.RenderRuntime()` renders this prompt through a prompt-specific helper.
- `UsesPromptOwnedNonImplementationPolicy("GenerateMilestoneDeepDivesForEpic")` returns true.
- `RoadmapPromptRunner` sends the prompt without the legacy composer heading.
- `RoadmapRuntimePromptPolicy.CreateIdentity("GenerateMilestoneDeepDivesForEpic")` emits `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.
- Strict and allowed modes both preserve the contracted spec bundle.
- Existing transition materialization and invariant validation remain authoritative for persisted state.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| M3-C1 Prompt-owned spec-generation guidance | `GenerateMilestoneDeepDivesForEpic.prompt` plus generated sections | Define why `.agents/specs/*.md` is sanctioned implementation-bearing planning output | Project context, auxiliary artifact policy | Rendered prompt with strict section markers | Prompt generator and section source files | Strict render includes markers and still requires `.agents/specs/*.md` | `RoadmapPromptRunner`, transition snapshot |
| M3-C2 Primary spec bundle preservation | Base prompt and prompt-owned sections | Keep one spec per epic milestone required in both policy modes | Selected epic in project context | Multi-file bundle targeting `.agents/specs/*.md` | Existing prompt contract | Allowed auxiliary mode does not weaken or omit the spec bundle contract | `GenerateMilestoneDeepDivesTransition`, execution preparation |
| M3-C3 Auxiliary side-artifact limits | Generated auxiliary limits section | Forbid extra design essays, code-edit scripts, execution prompts, governance reports, research notes, and companion planning documents | Strict policy branch | Prompt constraints | Roadmap Section Semantics | Strict render differentiates primary specs from invalid auxiliary artifacts | Agent output behavior |
| M3-C4 Runtime composer skip | Catalog and runner | Treat this prompt as prompt-owned | Runtime prompt name | Prompt without legacy composer heading | Catalog classification | Captured prompt lacks composer heading | Agent runtime |
| M3-C5 Transition policy provenance | `RoadmapRuntimePromptPolicy` | Record prompt-owned mode, branch, prompt hash, and section hashes | Runtime prompt name and policy settings | `TransitionPromptPolicyIdentity` | Selector and generated source hashes | Strict and allowed identities differ; strict records active section hashes | Transition persistence |
| M3-C6 Transition behavior preservation | Existing transition services | Keep bundle extraction and specs-ready behavior unchanged | Prompt output bundle | Written spec files, bundle manifest, execution preparation provenance, invariant result | Existing services | Existing bundle, manifest, provenance, and invariant tests remain passing | Roadmap state machine, later execution preparation |

## 8. Architectural Responsibilities

- Prompt-owned section text owns the semantic distinction between sanctioned `.agents/specs/*.md` and invalid auxiliary planning artifacts.
- `GenerateMilestoneDeepDivesForEpic.prompt` owns the base requirement to produce one spec per milestone.
- `BundleFileExtractor` owns parsing `# FILE:` sections from model output; this milestone does not move that boundary.
- The spec materialization path owns persisted spec files and bundle manifest state.
- Execution preparation provenance owns downstream readiness identity for active epic and milestone specs.
- `InvariantValidator` owns roadmap invariant validation after spec materialization.
- `RoadmapRuntimePromptPolicy` owns prompt-policy identity; projection freshness tracking remains separate.

## 9. Components and Modules

| Component | Purpose | Responsibilities | Owned State | Consumed State | Public Contracts | Internal Contracts | Dependencies | Tests Required |
|---|---|---|---|---|---|---|---|---|
| `GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance.prompt` | Spec-generation implementation-first guidance | State that `.agents/specs/*.md` is primary contracted output consumed by later execution preparation | Generated text and source hash | None | Marker heading and generated class | Must not copy generic invalid-content text | Prompt generator | Marker and C# body scan tests |
| `GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits.prompt` | Strict side-artifact limits | Prohibit extra planning, design, governance, research, execution-prompt, and code-edit-script artifacts outside the contracted bundle | Generated text and source hash | None | Marker heading and generated class | Empty in allowed auxiliary mode | Prompt generator | Selector tests |
| `GenerateMilestoneDeepDivesForEpic.prompt` | Owning planning prompt | Host placeholders and preserve one-spec-per-milestone bundle contract | Prompt template and source hash | Project context and injected sections | Generated render method | Placeholders fully substituted | Prompt generator | Render tests |
| `GenerateMilestoneDeepDivesForEpicPromptSections` | Section selector | Return strict or omitted section set | None | Auxiliary policy | Selector method | Active hashes align with injected sections | Generated section classes | Unit tests |
| `RoadmapPromptCatalog` | Render and classification | Add render helper and prompt-owned classification | None | Runtime prompt name and policy | `RenderRuntime`, `UsesPromptOwnedNonImplementationPolicy` | Does not alter parser or writer | Selector | Render and runner tests |
| `RoadmapRuntimePromptPolicy` | Identity | Emit `generate-milestone-deep-dives-for-epic-prompt-owned-v1` | Identity input map | Source hashes and policy setting | `CreateIdentity(...)` | Deterministic sorted keys | Selector | Identity tests |
| Existing spec transition services | Materialization and validation | Extract, write, manifest, record provenance, validate invariants | Spec files, manifest, provenance | Agent output bundle | Existing prompt contract | No behavioral change | Existing services | Regression tests |

## 10. Repository and File Impact

Expected changes:

- Add `src/LoopRelay.Core/Prompts/NonImplementation/GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance.prompt`.
- Add `src/LoopRelay.Core/Prompts/NonImplementation/GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits.prompt`.
- Modify `src/LoopRelay.Core/Prompts/Planning/GenerateMilestoneDeepDivesForEpic.prompt`.
- Add `src/LoopRelay.Roadmap.Cli/Services/Prompts/GenerateMilestoneDeepDivesForEpicPromptSections.cs`.
- Modify `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`.
- Modify `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`.
- Add prompt policy tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/Prompts`.
- Extend transition identity tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState`.

Expected unchanged areas:

- `GenerateMilestoneDeepDivesTransition`.
- `BundleFileExtractor`.
- `BundleManifestWriter`.
- Execution preparation manifest/provenance services.
- `InvariantValidator`.
- Prompt contract registry output semantics.

## 11. Public Contracts

- `GenerateMilestoneDeepDivesForEpic` remains the runtime prompt for generating milestone specs from the active epic.
- Required input remains `.agents/epic.md`.
- Required output remains `.agents/specs`.
- Output bundle still uses `# FILE:` sections parsed by `BundleFileExtractor`.
- Strict render includes `# GenerateMilestoneDeepDivesForEpic Implementation-First Guidance` and `# GenerateMilestoneDeepDivesForEpic Auxiliary Artifact Limits`.
- Allowed auxiliary render omits strict sections and leaves no placeholders.
- Both strict and allowed renders require `.agents/specs/*.md` as primary contracted output.
- `TransitionPromptPolicyIdentity.Mode` becomes `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.

## 12. Internal Contracts

- Section selection depends only on `AllowAuxiliaryNonImplementationFiles`.
- Strict mode returns two section bodies and two active section source hashes.
- Allowed mode returns empty section bodies and no active section hashes.
- Render helper passes selected sections into the generated prompt render method.
- Identity branch uses the same section selection as rendering.
- The prompt-owned strict section may constrain auxiliary artifacts but must not constrain the primary bundle.
- Existing bundle extraction and materialization remain the only writer/parser boundary for specs.
- No retry, fallback, or error path may append the legacy composer after prompt-owned classification.

## 13. Data and State Model

| State Object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Downstream Consumers |
|---|---|---|---|---|---|---|---|---|
| Spec-generation guidance section | `.prompt` source | Source -> generated class -> render input | Repository source and build output | Source controlled | Source hash | Marker and source-location tests | Rebuild generated prompts | Prompt catalog |
| Spec-generation auxiliary limits section | `.prompt` source | Source -> generated class -> render input | Repository source and build output | Source controlled | Source hash | Selector tests | Rebuild generated prompts | Prompt catalog |
| Rendered spec-generation prompt | `RoadmapPromptCatalog` | Policy selection -> render -> agent request | Ephemeral | Per invocation | Runtime prompt name and rendered text | Render and runner tests | Re-render from source and policy | Agent runtime |
| Prompt policy identity | `RoadmapRuntimePromptPolicy` | Snapshot creation | Persisted transition input evidence | Immutable after snapshot | Mode and sorted input hashes | Identity tests | Recompute from source and policy | Transition persistence |
| Milestone spec files | Existing spec writer | Agent bundle -> extraction -> retained files | `.agents/specs/*.md` | Rewritten by spec generation transition | File path and content hash | Bundle extraction and invariant validation | Existing state recovery and regeneration | Execution preparation, later execution prompts |
| Bundle manifest | `BundleManifestWriter` | Extraction result -> manifest write | Retained file under specs directory | Rewritten with bundle | Manifest path and rendered content | Existing tests | Regenerate from bundle | Diagnostics and provenance |
| Execution preparation manifest | Execution preparation services | Specs materialized -> authoritative inputs recorded | `.agents/execution-preparation-manifest.json` or structured store | Updated by transition | Active epic and spec identities | Existing provenance tests | Rebuild from current artifacts when supported | Operational context generation |

## 14. Lifecycle and State Transitions

Prompt policy lifecycle:

```text
Policy Loaded
  -> Spec Sections Selected
  -> Spec Prompt Rendered
  -> Prompt Policy Identity Created
  -> Agent Runtime Invoked
  -> Bundle Extracted
  -> Specs Materialized
  -> Invariants Validated
  -> MilestoneSpecsReady
```

| Transition | Trigger | Preconditions | Result | Failure Mode | Audit or Evidence Record |
|---|---|---|---|---|---|
| Policy Loaded -> Sections Selected | Runtime prompt execution starts | Runtime policy exists | Strict or omitted section set | Missing generated section class | Unit test or runtime exception |
| Sections Selected -> Prompt Rendered | Catalog helper runs | Prompt placeholders exist | Prompt keeps spec bundle contract | Placeholder remains or specs contract missing | Render test |
| Prompt Rendered -> Identity Created | Transition input snapshot resolves | Runtime prompt name matches | Prompt-owned identity mode | Legacy identity emitted | Transition identity test |
| Identity Created -> Agent Runtime Invoked | Runner sends prompt | Catalog classifies prompt-owned | No legacy composer heading | Composer appended | Runner capture test |
| Agent Runtime Invoked -> Bundle Extracted | Agent completes | Output includes `# FILE:` sections | Extracted spec files | Malformed bundle | Existing extractor diagnostics |
| Bundle Extracted -> Specs Materialized | Transition writes bundle | Valid extraction result | `.agents/specs/*.md` files and manifest | Write failure or invalid path | Existing transition failure evidence |
| Specs Materialized -> Invariants Validated | Post-materialization validation | Specs and active epic available | Valid invariants or invariant failure | Spec/epic mismatch | Validator evidence |

## 15. Execution Flow

Startup or initialization:

- Generated prompt classes include new section classes and updated render signature.
- Runtime policy is created from artifact policy settings.

Normal operation:

```text
RoadmapPromptRunner
  -> RoadmapPromptCatalog.RenderRuntime("GenerateMilestoneDeepDivesForEpic", policy)
  -> GenerateMilestoneDeepDivesForEpicPromptSections.ForAuxiliaryArtifactPolicy(...)
  -> Core.Prompts.Planning.GenerateMilestoneDeepDivesForEpic.Render(projectContext, selectedSections)
  -> Runner skips legacy composer
  -> Agent returns multi-file bundle
  -> Existing transition extracts and writes `.agents/specs/*.md`
  -> Manifest, HITL evidence, execution preparation provenance, and invariants are processed
```

Failure flow:

- Render or identity failures stop before agent execution.
- Malformed bundles fail in `BundleFileExtractor`.
- Materialization failures leave specs not ready through existing transition failure persistence.
- Invariant failures persist validator evidence and require invariant resolution.

Recovery flow:

- Re-rendering with the same source and settings reproduces policy identity.
- Existing resume planner can regenerate milestone specs when required by state and artifact status.

Shutdown or completion:

- Successful transition ends at `MilestoneSpecsReady` as before.

## 16. Dependency Closure

| Dependency | Classification | Required State |
|---|---|---|
| Milestones 1 and 2 active-epic migrations | Soft prerequisite | Establish repeated prompt-owned migration pattern, but no runtime dependency on their code beyond catalog/identity coexistence. |
| `GenerateMilestoneDeepDivesForEpic.prompt` | Hard prerequisite | Existing prompt owns the one-spec-per-milestone contract. |
| Prompt source generator | Supporting infrastructure | New section files and modified prompt render signature compile. |
| Existing spec transition services | Inherited capability | Bundle extraction, writing, manifest, HITL evidence, provenance, and invariant validation are available. |
| `AllowAuxiliaryNonImplementationFiles` | Supporting infrastructure | Controls strict section injection only. |
| `AllowHitlRequestedNonImplementationFiles` | Explicitly unavailable dependency | Must not drive prompt-owned section selection. |
| `SplitEpic` migration | Future dependency | Not required for spec generation migration. |

This milestone enables:

- Milestone 4 by clarifying primary bundle versus auxiliary artifact semantics before split bundle migration.
- Milestone 5 by adding the spec-generation prompt to the migrated no-composer set.

## 17. Failure Modes

| Failure Mode | Detection Method | Expected System Behavior | Recovery Path | Diagnostic Output | Test Coverage Required |
|---|---|---|---|---|---|
| Strict policy suppresses spec bundle | Render assertion | Test fails; implementation blocked | Move primary bundle requirement to base prompt and section wording | Missing `.agents/specs/*.md` assertion | Strict render test |
| Allowed policy weakens spec contract | Render assertion | Test fails | Ensure allowed mode only omits strict side-artifact text | Missing spec contract in allowed render | Allowed render test |
| Legacy composer appended | Runner capture | Test fails | Update prompt-owned classification | Composer heading found | Runtime runner test |
| Identity omits active section hashes | Identity assertion | Test fails | Add source hash inputs | Missing key diagnostics | Identity test |
| Primary specs treated as auxiliary artifacts | Prompt text assertion or regression test | Test fails | Reword guidance to distinguish primary bundle | Assertion on sanctioned output language | Prompt-specific tests |
| Bundle extraction regresses | Existing extractor tests | Transition fails | Revert unrelated extractor changes | Extractor rejection details | Bundle extractor tests |
| Execution preparation provenance regresses | Existing provenance tests | Later execution prep becomes stale or missing | Revert unrelated provenance changes | Manifest mismatch | Execution preparation tests |
| Invariant validation bypassed | Existing state/invariant tests | Invalid specs become ready | Restore transition validation | Invariant failure missing | Invariant tests |

## 18. Validation and Invariants

| Invariant | Source Authority | Enforcement Point | Failure Behavior | Test Strategy |
|---|---|---|---|---|
| One deep-dive spec per epic milestone remains required | Roadmap Milestone 3 and prompt contract | Base prompt and render assertions | Prompt allows narrative substitute | Strict and allowed render tests |
| `.agents/specs/*.md` is primary contracted output, not auxiliary | Roadmap Section Semantics | Prompt-owned guidance text | Strict policy blocks valid output | Prompt text and regression tests |
| Prompt-owned mode is `generate-milestone-deep-dives-for-epic-prompt-owned-v1` | Roadmap Acceptance | Identity branch | Legacy identity emitted | Transition identity test |
| Strict identity records active section source hashes | Roadmap Reference Pattern | Identity input map | Section changes invisible | Identity input assertions |
| Bundle extraction, manifest, provenance, and invariant validation remain unchanged | Roadmap Implementation and Acceptance | Existing transition services | Specs-ready state becomes inaccurate | Existing integration/regression tests |
| Auxiliary allowed mode does not weaken primary output contracts | Roadmap Cross-Milestone Rules | Rendered allowed prompt | Contract drift | Allowed render assertion |

## 19. Testing Strategy

- Unit tests: selector strict/allowed behavior and section source hash keys.
- Render tests: strict markers, allowed omission, placeholder removal, `.agents/specs/*.md` required in both modes, no legacy `# Invalid Content` injection.
- Runtime tests: no legacy composer for `GenerateMilestoneDeepDivesForEpic`; out-of-scope prompt still receives composer.
- Contract regression tests: prompt contract registry still declares required `.agents/specs` output and `BundleFileExtractor`.
- Transition identity tests: prompt-owned mode, prompt source hash, strict section source hashes, strict/allowed branch difference.
- Bundle tests: extraction, path validation, manifest writing, and materialization remain passing.
- Execution preparation tests: milestone specs are recorded as authoritative inputs.
- Invariant tests: spec/epic mismatch and duplicate active epic failures still persist validator evidence.
- Failure-path tests: malformed bundle or invariant failure does not mark specs ready.

## 20. Fixtures and Test Data

- Minimal valid epic context with two milestones and ordered dependencies.
- Strict render policy fixture with `AllowAuxiliaryNonImplementationFiles=false`.
- Allowed render policy fixture with `AllowAuxiliaryNonImplementationFiles=true`.
- Valid output bundle fixture containing two `# FILE: .agents/specs/*.md` sections.
- Invalid narrative-only fixture with no `# FILE:` sections.
- Invalid auxiliary fixture containing design essay, code-edit script, execution prompt, governance report, research note, or companion planning document outside the bundle.
- Invariant mismatch fixture where generated spec names or contents do not match the active epic.
- Legacy prompt control fixture for composer retention.
- Identity fixture comparing strict and allowed policy identities.

## 21. Acceptance Demonstration

Setup:

- Build the solution after adding the prompt-owned sections and catalog/identity wiring.
- Use scripted agent runtime and existing temp repository test support.

Input:

- Runtime prompt name `GenerateMilestoneDeepDivesForEpic`.
- Project context containing an active epic with two milestones.
- Strict policy with `AllowAuxiliaryNonImplementationFiles=false`.

Execution steps:

1. Render `RoadmapPromptCatalog.RenderRuntime("GenerateMilestoneDeepDivesForEpic", context, "", strictPolicy)`.
2. Capture a runner invocation for the same prompt.
3. Compute strict and allowed identities for `GenerateMilestoneDeepDivesForEpic`.
4. Run a transition or state-machine test with a valid two-file spec bundle.

Expected output:

- Strict render contains both prompt-owned section markers.
- Strict and allowed renders both require `.agents/specs/*.md`.
- Captured prompt lacks `ImplementationFirstPromptPolicyComposer.SectionHeading`.
- Valid bundle materializes spec files and bundle manifest through the existing transition.

Expected persisted state:

- Prompt-policy identity mode is `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.
- Strict identity includes prompt source hash and active section source hashes.
- Spec files are persisted under `.agents/specs/`.
- Execution preparation provenance records active epic and milestone spec identities.
- Roadmap state can reach `MilestoneSpecsReady` when invariants pass.

Expected diagnostics:

- Malformed bundle failures identify extraction problems.
- Invariant failures identify validator failure category and evidence path.

Verification command:

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~GenerateMilestoneDeepDivesForEpicPromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests|FullyQualifiedName~BundleFileExtractorTests|FullyQualifiedName~InvariantValidatorTests"
```

## 22. Certification Evidence

- Passing focused prompt policy tests for strict, allowed, runner, and body-not-hard-coded behavior.
- Passing transition identity tests for `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.
- Passing prompt contract registry tests confirming `.agents/specs` remains the required output.
- Passing bundle extraction and manifest writer tests.
- Passing execution preparation provenance tests for milestone specs.
- Passing invariant validation and roadmap state transition tests for spec generation.
- Captured diagnostics for malformed bundle and invariant failure paths.

## 23. Implementation Plan

| Step | Purpose | Deliverables | Dependencies | Completion Criteria |
|---|---|---|---|---|
| Add spec-generation section sources | Move prompt-specific policy text into prompt-owned source | Two `GenerateMilestoneDeepDivesForEpic*.prompt` files | Roadmap Section Semantics | Generated marker headings and source hashes exist |
| Add placeholders to planning prompt | Inject sections without changing primary bundle contract | Placeholder additions and render signature update | Section files | Strict and allowed render tests pass |
| Implement selector | Select strict versus omitted section set | `GenerateMilestoneDeepDivesForEpicPromptSections` | Generated section classes | Selector tests pass |
| Update catalog | Render through helper and classify prompt-owned | Catalog helper and classification update | Selector | Runner skip test passes |
| Update identity | Record prompt-owned provenance | Identity mode branch and hash inputs | Selector and prompt source hash | Identity tests pass |
| Add primary-bundle regression tests | Prove specs are not auxiliary | Render and contract tests | Catalog and prompt text | `.agents/specs/*.md` required in both policy modes |
| Run transition regressions | Prove materialization unchanged | Test results for extractor, manifest, provenance, invariants | Implementation compile | Existing transition tests pass |

## 24. Parallel Work Opportunities

| Parallel Lane | Scope | Owner Type | Dependencies | Synchronization Point | Integration Risk |
|---|---|---|---|---|---|
| Section wording | Draft sanctioned-spec and invalid-auxiliary language | Prompt/runtime engineer | Roadmap Section Semantics | Before render assertions finalize | High: wording can accidentally invalidate primary specs |
| Runtime wiring | Selector, catalog helper, classification | Runtime engineer | Section class names | Compile and render tests | Low |
| Identity branch | Policy identity mode and tests | Runtime/test engineer | Selector API | Transition identity tests | Medium |
| Transition regression | Bundle, manifest, provenance, invariant tests | Test engineer | Compileable implementation | Certification | Medium: this prompt has persisted downstream state |

## 25. Risks and Mitigations

| Risk | Class | Impact | Likelihood | Earliest Detection Point | Mitigation | Fallback |
|---|---|---|---|---|---|---|
| Primary spec bundle classified as auxiliary | Architectural | Prompt stops producing required specs | High | Render and prompt text tests | Explicitly state specs are primary contracted output | Reword sections before shipping |
| Allowed auxiliary mode permits narrative substitute | Implementation | Transition cannot materialize specs | Medium | Allowed render and bundle tests | Keep bundle contract in base prompt | Restore output requirements |
| Transition provenance lacks section hashes | Data | Prompt policy changes invisible | Medium | Identity tests | Include prompt and section hashes | Fix identity branch |
| Bundle materialization accidentally changed | Integration | Specs-ready state breaks | Low | Existing transition tests | Keep transition services unchanged | Revert unrelated edits |
| Generic invalid-content section reused | Architectural | Contradicts valid implementation planning specs | Medium | Prompt text review | Write prompt-specific sections | Remove reused text |
| Extra planning documents become allowed | Operational | Artifact clutter and policy drift | Medium | Prompt-specific assertions | Auxiliary limits section names invalid outputs | Tighten strict section |

## 26. Observability and Diagnostics

- Render tests provide observable prompt branch behavior.
- Transition prompt-policy identity provides mode and source-hash provenance.
- Bundle extraction diagnostics remain the first signal for malformed output bundles.
- Bundle manifest records materialized files and extraction validity through existing logic.
- Execution preparation manifest records authoritative active epic and milestone spec inputs.
- Invariant validator evidence records spec/epic mismatch and related failures.
- No new metrics or health checks are required; this is deterministic prompt rendering plus existing transition processing.

## 27. Performance and Scalability Considerations

- Baseline: prompt rendering remains generated string substitution.
- Likely bottleneck: model output bundle size and existing file materialization, unchanged by this milestone.
- Scaling risk: section text length can increase prompt size; keep sections concise and prompt-specific.
- Measurement strategy: focused tests and normal transition tests are sufficient.
- Deferred optimization: no caching or registry is required; source hashes are generated at build time.

## 28. Security and Safety Considerations

- Preserve `BundleFileExtractor` path validation for `# FILE:` sections.
- Do not allow prompt text to authorize writes outside `.agents/specs/*.md` for this prompt.
- Treat generated bundle content as untrusted until extracted, validated, and passed through invariant checks.
- Keep invariant validation mandatory before `MilestoneSpecsReady`.
- Preserve deterministic policy identity to make prompt-source changes traceable.
- Do not broaden agent permissions or runtime write authority.

## 29. Documentation Updates

No documentation-only repository deliverable is required.

Allowed source-adjacent text changes:

- Runtime `.prompt` section bodies and planning prompt placeholders.
- Test names or comments that clarify primary contracted specs versus invalid auxiliary artifacts.

Do not create implementation guides, planning reports, ADRs, RFCs, governance documents, research notes, or companion summaries as implementation outputs.

## 30. Exit Criteria

Milestone 3 is complete only when:

- Both `GenerateMilestoneDeepDivesForEpic` section source files exist and are generated.
- The planning prompt accepts and substitutes section placeholders.
- Strict render includes both prompt-owned section markers.
- Allowed render omits strict markers and removes placeholders.
- Both strict and allowed renders preserve the `.agents/specs/*.md` required output bundle.
- `RoadmapPromptRunner` does not append the legacy composer to this prompt.
- `RoadmapRuntimePromptPolicy.CreateIdentity("GenerateMilestoneDeepDivesForEpic")` emits `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.
- Strict identity includes prompt source hash and active section source hashes.
- Existing bundle extraction, manifest writing, HITL evidence capture, execution preparation provenance, invariant validation, and specs-ready tests pass.
- No future milestone capability is falsely claimed.

## 31. Transition to Next Milestone

Milestone 3 hands off:

- A resolved model for distinguishing primary machine-consumed planning artifacts from invalid auxiliary planning documents.
- Prompt-owned policy for the spec-generation roadmap artifact-authoring prompt.
- Regression coverage proving strict auxiliary policy does not weaken primary artifact contracts.
- Identity and runner-skip coverage for the final retirement checkpoint.

Limitations carried forward:

- `SplitEpic` still uses the legacy composer.
- Multi-file split bundle semantics and child epic output remain unresolved until Milestone 4.
- `ImplementationFirstPromptPolicyComposer` remains active for out-of-scope consumers.
