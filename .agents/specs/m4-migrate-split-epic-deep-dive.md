# Milestone 4 Deep Dive: Migrate `SplitEpic`

## 1. Milestone Summary

| Field | Value |
|---|---|
| Milestone Identifier | Milestone 4 |
| Milestone Name | Migrate `SplitEpic` |
| Roadmap Position | 4 of 5 |
| Implementation Role | Complete prompt-owned migration for the most complex in-scope roadmap artifact-authoring prompt. |
| Short Description | Move `SplitEpic` implementation-first guidance into generated prompt-owned sections while preserving split bundle semantics, child epic files, lineage persistence, HITL capture, and selected-child promotion. |

Primary outcomes:

- `SplitEpic` strict rendering injects prompt-owned split-specific guidance and auxiliary-artifact limits.
- Strict policy treats the split bundle, split plan, and child epic file sections as primary contracted output, not auxiliary artifacts.
- Allowed auxiliary mode omits strict sections without weakening bundle requirements.
- `RoadmapPromptRunner` skips `ImplementationFirstPromptPolicyComposer` for `SplitEpic`.
- Transition prompt-policy identity records `split-epic-prompt-owned-v1` with prompt and section source-hash provenance.
- Existing split bundle interpretation, family persistence, HITL capture, selected-child promotion, and epic validation remain unchanged.

## 2. Normative Basis

Roadmap authority:

- `.agents/specs/roadmap.md` Objective, Scope Boundaries, Reference Pattern, Migration Order, Milestone 4, Cross-Milestone Rules, Verification Matrix, and Done Definition.

Architectural authority:

- `.agents/specs/audit.md` Architecture Observations, SplitEpic audit section, Section Reuse Observations, Emerging Organization Pattern, Transition and Provenance Observations, Settings Flow Observations, Testing Observations, and Risks.

Implementation authority:

- `src/LoopRelay.Core/Prompts/Planning/SplitEpic.prompt`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptRunner.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/PromptContractRegistry.cs`
- `src/LoopRelay.Roadmap.Cli/Services/EpicTransitions/SplitEpicTransition.cs`
- `src/LoopRelay.Roadmap.Cli/Services/ArtifactBundles/BundleFileExtractor.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Splits/SplitEpicBundleInterpreter.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Splits/SplitFamilyStore.cs`
- `src/LoopRelay.Roadmap.Cli/Services/TransitionCoordination/HitlArtifactCapture.cs`
- `src/LoopRelay.Roadmap.Cli/Services/TransitionCoordination/ActiveEpicPromotionCoordinator.cs`
- `src/LoopRelay.Roadmap.Cli/Services/ArtifactManagement/EpicArtifactValidator.cs`

Supporting context:

- `tests/LoopRelay.Roadmap.Cli.Tests/Services/Prompts/CreateNewEpicPromptPolicyTests.cs`
- Existing split interpreter, split family store, prompt contract, state machine, and promotion tests.

No authority is introduced outside the roadmap, audit, and existing split implementation contracts.

## 3. Objective

Implement prompt-owned implementation-first policy for `SplitEpic` while preserving the split bundle as the sole sanctioned operational output boundary and keeping child epic files, split lineage, HITL evidence, and selected-child promotion behavior intact.

## 4. Non-Goals

- Do not change split proposal suitability rules or capability partitioning semantics outside prompt-owned policy wording.
- Do not change `SplitEpicTransition`, `BundleFileExtractor`, `SplitEpicBundleInterpreter`, `SplitFamilyStore`, HITL artifact evidence capture, selected-child promotion, or `EpicArtifactValidator`.
- Do not change the split bundle output protocol or child epic file sections.
- Do not treat child epics as invalid auxiliary artifacts.
- Do not split by implementation phases, file areas, technical layers, task lists, or work breakdowns.
- Do not authorize companion inventories, RFCs, governance notes, research documents, design reports, or explanatory appendices unless required by an existing machine-consumed contract.
- Do not delete the legacy composer or change non-roadmap consumers.
- Do not introduce a generic prompt-policy framework for out-of-scope prompts.

## 5. Runtime / System State Before

- `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`, and `GenerateMilestoneDeepDivesForEpic` are expected to be prompt-owned after Milestones 1 through 3.
- `SplitEpic` still renders directly through `Core.Prompts.Planning.SplitEpic.Render(projectContext, secondaryInput)`.
- `RoadmapPromptRunner` appends the legacy composer to `SplitEpic`.
- Transition prompt-policy identity for `SplitEpic` still uses the legacy composer branch.
- Prompt contract registry identifies `SplitEpic` as writing split families and active epic through `SplitEpicBundleInterpreter+SplitFamilyStore+ArtifactPromotionService`, with parser boundary `BundleFileExtractor+SplitEpicBundleInterpreter+EpicArtifactValidator`.
- Existing split transition handles bundle extraction, child epic validation, split family persistence, HITL capture, and selected child promotion.

## 6. Runtime / System State After

- `SplitEpic` owns two generated non-implementation section bodies.
- `SplitEpic.prompt` has explicit placeholders for selected sections.
- `RoadmapPromptCatalog.RenderRuntime()` renders `SplitEpic` through a prompt-specific helper.
- `UsesPromptOwnedNonImplementationPolicy("SplitEpic")` returns true.
- `RoadmapPromptRunner` sends `SplitEpic` without the legacy composer heading.
- `RoadmapRuntimePromptPolicy.CreateIdentity("SplitEpic")` emits `split-epic-prompt-owned-v1`.
- Strict and allowed modes preserve split bundle, split plan, and child epic section requirements.
- Existing split transition persistence and selected-child promotion remain behaviorally unchanged.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| M4-C1 Prompt-owned SplitEpic guidance | `SplitEpic.prompt` plus generated sections | Explain sanctioned split bundle and capability partitioning | Project context, split proposal, auxiliary policy | Rendered prompt with split-specific strict section markers | Prompt generator and section source files | Strict render includes markers and sanctions split bundle plus child epic sections | `RoadmapPromptRunner`, transition snapshot |
| M4-C2 Bundle primary-output preservation | Base prompt and section wording | Keep split plan and `# FILE: .agents/epic-N.md` child epic sections as primary contracted output | Split proposal and projection context | Multi-file split bundle | Existing prompt contract | Strict and allowed renders do not treat child epics as auxiliary artifacts | `SplitEpicTransition` |
| M4-C3 Auxiliary side-artifact limits | Generated auxiliary limits section | Prohibit extra inventories, RFCs, governance notes, research docs, design reports, and appendices | Strict policy branch | Prompt constraints | Roadmap Section Semantics | Strict render distinguishes split bundle from invalid side artifacts | Agent output behavior |
| M4-C4 Runtime composer skip | Catalog and runner | Treat `SplitEpic` as prompt-owned | Runtime prompt name | Prompt without legacy composer heading | Catalog classification | Captured prompt lacks composer heading | Agent runtime |
| M4-C5 Transition policy provenance | `RoadmapRuntimePromptPolicy` | Record prompt-owned mode and source hashes | Runtime prompt name, policy setting, source hashes | `TransitionPromptPolicyIdentity` | Selector | Strict/allowed identities differ; strict records active section hashes | Transition persistence |
| M4-C6 Split transition preservation | Existing split services | Preserve parsing, lineage, HITL, validation, promotion | Agent output bundle | Split family state and selected active epic | Existing services | Existing split tests remain passing | Roadmap state machine |

## 8. Architectural Responsibilities

- `SplitEpic.prompt` owns split reasoning, bundle requirements, and section placement.
- Generated section files own prompt-specific implementation-first and auxiliary-artifact text.
- Prompt-specific selector owns strict/allowed section branch selection.
- `RoadmapPromptCatalog` owns render helper routing and prompt-owned classification.
- `RoadmapRuntimePromptPolicy` owns transition policy identity.
- `BundleFileExtractor` owns `# FILE:` parsing.
- `SplitEpicBundleInterpreter` owns split bundle semantic validation and child epic interpretation.
- `SplitFamilyStore` owns persisted split lineage.
- Existing promotion services own selected-child promotion to `.agents/epic.md`.
- No component other than existing split services should own split lineage or child promotion state.

## 9. Components and Modules

| Component | Purpose | Responsibilities | Owned State | Consumed State | Public Contracts | Internal Contracts | Dependencies | Tests Required |
|---|---|---|---|---|---|---|---|---|
| `SplitEpicImplementationFirstGuidance.prompt` | Split-specific implementation-first guidance | Sanction split bundle and capability partitioning as implementation-bearing roadmap output | Generated text and source hash | None | Marker heading and generated class | Must not copy single-artifact epic guidance unchanged | Prompt generator | Marker and C# body scan tests |
| `SplitEpicAuxiliaryArtifactLimits.prompt` | Strict side-artifact limits | Forbid companion inventories, RFCs, governance notes, research docs, design reports, and appendices | Generated text and source hash | None | Marker heading and generated class | Empty in allowed auxiliary mode | Prompt generator | Selector tests |
| `SplitEpic.prompt` | Owning planning prompt | Host placeholders and preserve split bundle protocol | Prompt template and source hash | Project context, split proposal, injected sections | Generated render method | Placeholders fully substituted | Prompt generator | Render tests |
| `SplitEpicPromptSections` | Section selector | Select strict versus omitted sections | None | Auxiliary policy | Selector method | Active hashes align with injected sections | Generated sections | Unit tests |
| `RoadmapPromptCatalog` | Render and classification | Add helper and classify `SplitEpic` as prompt-owned | None | Runtime prompt name and policy | `RenderRuntime`, `UsesPromptOwnedNonImplementationPolicy` | No parser or writer changes | Selector | Render and runner tests |
| `RoadmapRuntimePromptPolicy` | Policy identity | Emit `split-epic-prompt-owned-v1` | Identity input map | Prompt and section hashes | `CreateIdentity("SplitEpic")` | Deterministic sorted keys | Selector | Identity tests |
| Existing split services | Bundle semantics and persistence | Interpret bundle, persist lineage, capture HITL, promote selected child | Split family records and active epic | Agent output bundle | Existing prompt contract | No behavioral change | Existing services | Regression tests |

## 10. Repository and File Impact

Expected changes:

- Add `src/LoopRelay.Core/Prompts/NonImplementation/SplitEpicImplementationFirstGuidance.prompt`.
- Add `src/LoopRelay.Core/Prompts/NonImplementation/SplitEpicAuxiliaryArtifactLimits.prompt`.
- Modify `src/LoopRelay.Core/Prompts/Planning/SplitEpic.prompt`.
- Add `src/LoopRelay.Roadmap.Cli/Services/Prompts/SplitEpicPromptSections.cs`.
- Modify `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`.
- Modify `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`.
- Add focused prompt policy tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/Prompts`.
- Extend transition identity tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState`.

Expected unchanged areas:

- `SplitEpicTransition`.
- `BundleFileExtractor`.
- `SplitEpicBundleInterpreter`.
- `SplitFamilyStore`.
- `HitlArtifactCapture`.
- `ActiveEpicPromotionCoordinator`.
- `EpicArtifactValidator`.
- Prompt contract writer/parser boundaries.

## 11. Public Contracts

- `SplitEpic` remains the runtime prompt for split proposal decomposition.
- Required input remains `.agents/selection` through the roadmap prompt context.
- Required outputs remain split family state and selected active epic as declared by prompt contracts.
- Output remains a multi-file Markdown bundle consumed by `BundleFileExtractor` and `SplitEpicBundleInterpreter`.
- Split plan and `# FILE: .agents/epic-N.md` child epic sections are primary contracted outputs.
- `# Split Epic Blocked` remains the blocked output contract.
- Strict render includes `# SplitEpic Implementation-First Guidance` and `# SplitEpic Auxiliary Artifact Limits`.
- Allowed auxiliary render omits strict sections and leaves no placeholders.
- `TransitionPromptPolicyIdentity.Mode` for `SplitEpic` becomes `split-epic-prompt-owned-v1`.

## 12. Internal Contracts

- `SplitEpicPromptSections.ForAuxiliaryArtifactPolicy(false)` returns both generated sections and active section hashes.
- `SplitEpicPromptSections.ForAuxiliaryArtifactPolicy(true)` returns empty strings and no active hashes.
- Render and identity use equivalent branch selection.
- `UsesPromptOwnedNonImplementationPolicy("SplitEpic")` must be true before runner composer selection.
- Prompt-owned strict policy must not label child epic files as auxiliary artifacts.
- Split bundle parsing remains exclusively owned by `BundleFileExtractor+SplitEpicBundleInterpreter`.
- Split lineage persistence remains exclusively owned by `SplitFamilyStore`.
- Selected-child promotion remains exclusively owned by existing promotion services.
- No fallback path may append the legacy composer to `SplitEpic`.

## 13. Data and State Model

| State Object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Downstream Consumers |
|---|---|---|---|---|---|---|---|---|
| Split guidance section | `.prompt` source | Source -> generated class -> render input | Repository source and build output | Source controlled | Source hash | Marker and source-location tests | Rebuild generated prompts | Prompt catalog |
| Split auxiliary limits section | `.prompt` source | Source -> generated class -> render input | Repository source and build output | Source controlled | Source hash | Selector tests | Rebuild generated prompts | Prompt catalog |
| Rendered `SplitEpic` prompt | `RoadmapPromptCatalog` | Policy selection -> render -> agent request | Ephemeral | Per invocation | Runtime prompt name and rendered text | Render and runner tests | Re-render from source and policy | Agent runtime |
| Prompt policy identity | `RoadmapRuntimePromptPolicy` | Snapshot creation | Persisted transition input evidence | Immutable after snapshot | Mode and sorted input hashes | Identity tests | Recompute from source and policy | Transition persistence |
| Split output bundle | Agent output and bundle extractor | Agent completion -> extraction -> interpretation | Ephemeral until interpreted and persisted | Per invocation | `# FILE:` paths and content | Extractor and interpreter | Re-run split prompt | Split transition |
| Split family lineage | `SplitFamilyStore` | Interpretation -> persistence -> later selection/promotion | Persisted split family store | Append/update through transition | Split family identity and child epic order | Split family tests | Existing recovery path | Roadmap state machine |
| Selected active epic | Existing promotion services | Child epic validation -> promotion to `.agents/epic.md` | Retained file | Mutated by promotion | Artifact path and content hash | `EpicArtifactValidator` | Existing artifact recovery | Later roadmap prompts |

## 14. Lifecycle and State Transitions

Prompt and split lifecycle:

```text
Policy Loaded
  -> Split Sections Selected
  -> Split Prompt Rendered
  -> Prompt Policy Identity Created
  -> Agent Runtime Invoked
  -> Bundle Extracted
  -> Split Bundle Interpreted
  -> Split Family Persisted
  -> Selected Child Promoted
```

| Transition | Trigger | Preconditions | Result | Failure Mode | Audit or Evidence Record |
|---|---|---|---|---|---|
| Policy Loaded -> Sections Selected | Prompt execution starts | Runtime policy exists | Strict or omitted section set | Missing generated class | Unit test or runtime exception |
| Sections Selected -> Prompt Rendered | Catalog helper runs | Placeholders exist | Prompt preserves split bundle | Placeholder remains or child file contract missing | Render test |
| Prompt Rendered -> Identity Created | Transition snapshot resolves | Prompt name is `SplitEpic` | Prompt-owned identity mode | Legacy identity emitted | Identity test |
| Identity Created -> Agent Runtime Invoked | Runner sends prompt | Catalog classifies prompt-owned | No legacy composer heading | Composer appended | Runner test |
| Agent Runtime Invoked -> Bundle Extracted | Agent completes | Output is bundle or blocked | Extracted files or blocked output | Malformed `# FILE:` sections | Extractor diagnostics |
| Bundle Extracted -> Split Bundle Interpreted | Interpreter runs | Bundle files available | Valid child epics or rejection | Overlap, missing split plan, invalid child path | Interpreter rejection |
| Interpreted -> Split Family Persisted | Valid split interpretation | Family metadata and child epics valid | Split lineage persisted | Persistence failure | Store diagnostics |
| Persisted -> Selected Child Promoted | Selected child known and valid | Child passes validator | `.agents/epic.md` updated | Validation or promotion failure | Existing promotion evidence |

## 15. Execution Flow

Startup or initialization:

- Generated prompt classes expose new section sources and updated `SplitEpic.Render(...)`.
- Runtime policy is built from artifact policy settings.

Normal operation:

```text
RoadmapPromptRunner
  -> RoadmapPromptCatalog.RenderRuntime("SplitEpic", policy)
  -> SplitEpicPromptSections.ForAuxiliaryArtifactPolicy(...)
  -> Core.Prompts.Planning.SplitEpic.Render(projectContext, splitProposal, selectedSections)
  -> Runner skips legacy composer
  -> Agent returns split bundle
  -> Existing transition extracts bundle
  -> Split interpreter validates split plan and child epics
  -> Split family store persists lineage
  -> HITL evidence capture records child artifacts
  -> Selected child is promoted through existing epic validation
```

Failure flow:

- Render or identity failures stop before agent execution.
- Blocked output begins with `# Split Epic Blocked`.
- Malformed bundle fails in extractor or interpreter.
- Invalid child epic fails `EpicArtifactValidator`.
- Persistence or promotion failures remain existing transition failures.

Recovery flow:

- Deterministic render and identity can be recomputed from source and settings.
- Existing roadmap resume and state persistence handle incomplete split transitions.

Shutdown or completion:

- No new shutdown behavior is introduced.

## 16. Dependency Closure

| Dependency | Classification | Required State |
|---|---|---|
| Milestone 3 primary bundle distinction | Soft prerequisite | Establishes semantics for primary multi-file outputs under strict auxiliary policy. |
| `SplitEpic.prompt` | Hard prerequisite | Existing prompt owns split bundle contract and capability partitioning rules. |
| Prompt source generator | Supporting infrastructure | New section files and render signature compile. |
| Existing split transition services | Inherited capability | Bundle extraction, interpretation, lineage, HITL, and promotion are available. |
| `AllowAuxiliaryNonImplementationFiles` | Supporting infrastructure | Controls strict section injection only. |
| `AllowHitlRequestedNonImplementationFiles` | Explicitly unavailable dependency | Must not select prompt-owned sections. |
| Milestone 5 retirement checkpoint | Future dependency | Uses this milestone's migration in aggregate regression tests. |

This milestone enables:

- Milestone 5 by completing all four newly migrated roadmap artifact-authoring prompts.
- Final in-scope roadmap prompt-owned policy coverage alongside pre-existing `CreateNewEpic`.

## 17. Failure Modes

| Failure Mode | Detection Method | Expected System Behavior | Recovery Path | Diagnostic Output | Test Coverage Required |
|---|---|---|---|---|---|
| Child epic files treated as auxiliary | Render assertion | Test fails | Reword guidance to classify child files as primary bundle | Missing sanction assertion | Strict render test |
| Split bundle requirement weakened in allowed mode | Render assertion | Test fails | Keep bundle contract in base prompt | Missing bundle language | Allowed render test |
| Legacy composer appended | Runner capture | Test fails | Update catalog classification | Composer heading in captured prompt | Runtime runner test |
| Identity remains legacy | Identity assertion | Test fails | Add prompt-owned branch | Mode mismatch | Transition identity test |
| Split interpreter behavior changes | Existing interpreter tests | Test fails | Revert unrelated changes | Rejection mismatch | Interpreter tests |
| Split family persistence changes | Existing store tests | Test fails | Revert unrelated persistence changes | Store mismatch | Split family tests |
| Selected child promotion changes | Existing promotion tests | Test fails | Keep promotion path unchanged | Promotion diagnostics | State machine/promotion tests |
| Prompt encourages task split | Prompt text assertion | Test or review fails | Reinforce capability partitioning | Assertion on invalid split basis | Prompt-specific render test |

## 18. Validation and Invariants

| Invariant | Source Authority | Enforcement Point | Failure Behavior | Test Strategy |
|---|---|---|---|---|
| Split bundle is the sanctioned output boundary | Roadmap Section Semantics | Prompt-owned guidance and base prompt | Prompt emits side documents or weakens bundle | Render assertions |
| Child epic files are primary, not auxiliary | Roadmap Section Semantics | Prompt-owned guidance | Strict policy forbids valid child outputs | Prompt-specific render test |
| Splits are capability partitions, not task/file/layer splits | Roadmap Section Semantics and audit | Prompt-owned guidance | Low-quality split accepted by prompt | Prompt text assertions and existing interpreter tests |
| Runtime prompt skips legacy composer | Roadmap Acceptance | Catalog and runner | Composer heading appears | Runner test |
| Prompt-owned identity records source hashes | Roadmap Reference Pattern | `RoadmapRuntimePromptPolicy` | Prompt policy changes not traceable | Identity tests |
| Existing writer/parser/promotion boundaries remain unchanged | Roadmap Scope Boundaries | Existing split services | Transition behavior regresses | Existing split regression tests |

## 19. Testing Strategy

- Unit tests: `SplitEpicPromptSections` strict/allowed selection.
- Render tests: strict markers, split bundle sanctioning, split plan and `# FILE: .agents/epic-N.md` child sections treated as valid, allowed mode placeholder removal.
- Runtime tests: no legacy composer for `SplitEpic`; out-of-scope prompt still receives composer.
- Transition identity tests: `split-epic-prompt-owned-v1`, prompt source hash, active section source hashes, strict/allowed branch difference.
- Prompt contract tests: writer and parser boundaries for `SplitEpic` remain unchanged.
- Split interpreter tests: valid bundle, invalid bundle, overlap, missing child, invalid child path, blocked output.
- Split family persistence tests: lineage and child metadata remain correct.
- HITL capture tests: child artifact evidence remains recorded.
- Promotion tests: selected child promotion still writes `.agents/epic.md` through existing validation.

## 20. Fixtures and Test Data

- Minimal strict render input: project context plus split proposal.
- Allowed render input: same context and proposal with auxiliary files allowed.
- Valid split bundle fixture: split plan plus at least two `# FILE: .agents/epic-N.md` child epic sections.
- Invalid task split fixture: output partitions by files, phases, layers, or work items rather than capability.
- Invalid auxiliary fixture: companion inventory, RFC, governance note, research document, design report, or explanatory appendix outside the contracted bundle.
- Blocked fixture: `# Split Epic Blocked`.
- Split lineage fixture: valid child epics with stable order and selected child.
- Legacy control fixture: an unmigrated prompt still receiving composer.

## 21. Acceptance Demonstration

Setup:

- Build the solution after adding split prompt sections and runtime wiring.
- Use existing split transition test support with a scripted valid split bundle.

Input:

- Runtime prompt name `SplitEpic`.
- Project context containing a split proposal.
- Strict policy with `AllowAuxiliaryNonImplementationFiles=false`.

Execution steps:

1. Render `RoadmapPromptCatalog.RenderRuntime("SplitEpic", context, proposal, strictPolicy)`.
2. Capture `RoadmapPromptRunner.RunRuntimePromptAsync("SplitEpic", ...)`.
3. Compute strict and allowed `CreateIdentity("SplitEpic")`.
4. Run existing split bundle interpreter, split family store, HITL capture, and promotion tests.

Expected output:

- Strict prompt contains `# SplitEpic Implementation-First Guidance`.
- Strict prompt contains `# SplitEpic Auxiliary Artifact Limits`.
- Strict and allowed prompts both preserve split bundle and child epic output requirements.
- Captured runner prompt lacks `ImplementationFirstPromptPolicyComposer.SectionHeading`.
- Valid split bundle is interpreted and persisted through existing services.

Expected persisted state:

- Prompt-policy identity mode is `split-epic-prompt-owned-v1`.
- Strict identity includes prompt source hash and active section source hashes.
- Split lineage state is persisted by `SplitFamilyStore`.
- Selected child epic is promoted to `.agents/epic.md`.

Expected diagnostics:

- Malformed bundle failures come from extractor or interpreter.
- Invalid child epics report validator diagnostics.

Verification command:

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~SplitEpicPromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests|FullyQualifiedName~SplitEpicBundleInterpreterTests|FullyQualifiedName~SplitFamilyStoreTests"
```

## 22. Certification Evidence

- Passing `SplitEpic` prompt policy tests for strict/allowed render, runner skip, and body-not-hard-coded behavior.
- Passing transition identity tests for `split-epic-prompt-owned-v1`.
- Passing prompt contract tests for `SplitEpic`.
- Passing bundle interpreter tests.
- Passing split family persistence tests.
- Passing HITL evidence capture tests.
- Passing selected-child promotion and epic validator tests.
- Passing regression test proving out-of-scope prompts still receive the legacy composer.

## 23. Implementation Plan

| Step | Purpose | Deliverables | Dependencies | Completion Criteria |
|---|---|---|---|---|
| Add SplitEpic section sources | Move split-specific policy text into prompt-owned source | Two `SplitEpic*.prompt` files | Roadmap Section Semantics | Generated classes expose marker headings and source hashes |
| Add placeholders to `SplitEpic.prompt` | Inject selected sections without changing bundle contract | Placeholder additions and render signature update | Section files | Strict and allowed render tests pass |
| Implement selector | Match prompt-specific migration shape | `SplitEpicPromptSections` | Generated section classes | Selector tests pass |
| Update catalog | Render through helper and classify prompt-owned | Catalog helper and classification update | Selector | Runner skip test passes |
| Update identity | Record prompt-owned provenance | `split-epic-prompt-owned-v1` branch | Selector and prompt hash | Identity tests pass |
| Add split bundle regression tests | Prove child epics are primary outputs | Render and contract assertions | Prompt text | Strict and allowed prompts preserve bundle |
| Run split transition regressions | Prove lineage and promotion unchanged | Test results | Compileable implementation | Existing split tests pass |

## 24. Parallel Work Opportunities

| Parallel Lane | Scope | Owner Type | Dependencies | Synchronization Point | Integration Risk |
|---|---|---|---|---|---|
| Section wording | Draft bundle-specific guidance and limits | Prompt/runtime engineer | Roadmap and audit semantics | Before render assertions finalize | High |
| Runtime wiring | Selector, catalog helper, classification | Runtime engineer | Section class names | Compile and render tests | Low |
| Identity branch | Prompt-owned identity and tests | Runtime/test engineer | Selector API | Transition identity tests | Medium |
| Split regression | Interpreter, family, HITL, promotion tests | Test engineer | Compileable implementation | Certification | Medium |

## 25. Risks and Mitigations

| Risk | Class | Impact | Likelihood | Earliest Detection Point | Mitigation | Fallback |
|---|---|---|---|---|---|---|
| Split bundle treated like invalid docs | Architectural | Prompt cannot produce required operational bundle | High | Render tests | Explicitly define bundle as primary machine-consumed output | Reword sections |
| Child epic files called auxiliary | Architectural | Strict mode blocks valid split output | High | Prompt-specific tests | Sanction child sections in guidance | Tighten wording |
| Split quality shifts to task breakdown | Implementation | Resulting epics lose strategic capability ownership | Medium | Prompt text tests and interpreter regressions | Emphasize capability partitioning and invalid split bases | Revise guidance |
| Lineage persistence regresses | Data | Split family state becomes unreliable | Low | Split family tests | Do not alter store | Revert unrelated changes |
| Selected-child promotion regresses | Integration | Active epic is not updated correctly | Low | Promotion/state tests | Keep promotion path unchanged | Revert unrelated changes |
| Identity lacks source hashes | Operational | Prompt-policy changes not auditable | Medium | Identity tests | Include prompt and active section hashes | Fix identity branch |

## 26. Observability and Diagnostics

- Render tests expose prompt branch, marker, and bundle-contract behavior.
- Transition prompt-policy identity exposes mode and source-hash provenance.
- `BundleFileExtractor` diagnostics remain the first signal for malformed file markers.
- `SplitEpicBundleInterpreter` rejection details remain the signal for invalid split semantics.
- `SplitFamilyStore` persistence tests and state snapshots expose lineage state.
- Existing promotion and validation diagnostics expose selected-child failures.
- No new metrics or health checks are required.

## 27. Performance and Scalability Considerations

- Baseline rendering remains generated string substitution.
- Likely bottleneck remains existing split bundle size and interpretation, unchanged by this milestone.
- Scaling risk: prompt text must stay concise so the split prompt does not grow unnecessarily.
- Measurement strategy: existing split tests and normal build time are sufficient.
- Deferred optimization: no generic registry or caching unless final checkpoint demonstrates harmful duplication.

## 28. Security and Safety Considerations

- Preserve bundle path validation so `# FILE:` output cannot write arbitrary paths.
- Preserve epic validation before selected child promotion.
- Treat model bundle content as untrusted until extracted, interpreted, and validated.
- Do not authorize extra files beyond the split bundle contract.
- Preserve HITL evidence capture semantics without broadening artifact acceptance.
- Keep source-hash provenance deterministic.
- Do not alter runtime privileges or artifact writer boundaries.

## 29. Documentation Updates

No documentation-only repository deliverable is required.

Allowed source-adjacent text changes:

- Runtime `.prompt` section bodies and planning prompt placeholders.
- Test names or comments that clarify split bundle output ownership.

Do not create split migration reports, design docs, RFCs, inventories, governance notes, or explanatory appendices as implementation outputs.

## 30. Exit Criteria

Milestone 4 is complete only when:

- Both `SplitEpic` section source files exist and are generated.
- `SplitEpic.prompt` accepts and substitutes section placeholders.
- Strict render includes both `SplitEpic` section markers.
- Allowed render omits strict markers and removes placeholders.
- Strict and allowed renders preserve the split bundle, split plan, and child epic file section requirements.
- `RoadmapPromptRunner` does not append the legacy composer to `SplitEpic`.
- `RoadmapRuntimePromptPolicy.CreateIdentity("SplitEpic")` emits `split-epic-prompt-owned-v1`.
- Strict identity includes prompt source hash and active section source hashes.
- Existing prompt contract, bundle interpreter, split family persistence, HITL capture, selected-child promotion, and validator tests pass.
- No future milestone capability is falsely claimed.

## 31. Transition to Next Milestone

Milestone 4 hands off:

- Prompt-owned implementation-first policy for the last newly migrated roadmap artifact-authoring prompt.
- Completed in-scope set: `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`, `GenerateMilestoneDeepDivesForEpic`, and `SplitEpic`.
- Regression evidence that primary multi-file bundles remain valid under strict auxiliary policy.
- Split prompt identity branch and runner-skip coverage for final retirement hardening.

Limitations carried forward:

- Aggregate regression hardening across all migrated prompts remains Milestone 5.
- Legacy composer remains required for non-roadmap and out-of-scope consumers.
- Physical composer deletion remains outside this roadmap.
