# Milestone 1 Deep Dive: Migrate `RealignEpic`

## 1. Milestone Summary

| Field | Value |
|---|---|
| Milestone Identifier | Milestone 1 |
| Milestone Name | Migrate `RealignEpic` |
| Roadmap Position | 1 of 5 |
| Implementation Role | First remaining roadmap artifact-authoring prompt migration; validates the `CreateNewEpic` reference pattern against minimal active-epic rewrite behavior. |
| Short Description | Move `RealignEpic` implementation-first guidance from the legacy runtime composer into prompt-owned generated sections while preserving the existing `.agents/epic.md` rewrite path. |

Primary outcomes:

- `RealignEpic` strict rendering injects prompt-owned implementation-first and auxiliary-artifact sections.
- `RealignEpic` allowed auxiliary mode omits those strict sections and removes placeholders.
- `RoadmapPromptRunner` no longer appends `ImplementationFirstPromptPolicyComposer` to `RealignEpic`.
- Transition prompt-policy identity records `realign-epic-prompt-owned-v1`, the prompt source hash, the auxiliary branch, section mode, and active section source hashes.
- `ActiveEpicRewriteTransition`, `ActiveEpicPromotionCoordinator`, `EpicAuthoringOutputClassifier`, and `EpicArtifactValidator` remain behaviorally unchanged.

## 2. Normative Basis

Roadmap authority:

- `.agents/specs/roadmap.md` Objective, Scope Boundaries, Reference Pattern, Migration Order, Milestone 1, Cross-Milestone Rules, Verification Matrix, and Done Definition.

Architectural authority:

- `.agents/specs/audit.md` Architecture Observations, CreateNewEpic Baseline, RealignEpic audit section, Section Reuse Observations, Emerging Organization Pattern, Transition and Provenance Observations, Settings Flow Observations, and Testing Observations.

Implementation authority:

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

Implement prompt-owned implementation-first policy for `RealignEpic` by adding prompt-owned section sources, injecting them through generated prompt rendering, skipping the legacy composer at runtime, and recording prompt-policy provenance without changing the active epic rewrite contract.

## 4. Non-Goals

- Do not migrate `ReimagineEpic`, `GenerateMilestoneDeepDivesForEpic`, or `SplitEpic`.
- Do not alter `CreateNewEpic` except for regression coverage shared by the existing reference behavior.
- Do not delete or globally weaken `ImplementationFirstPromptPolicyComposer`.
- Do not change `AllowHitlRequestedNonImplementationFiles`; it remains a legacy composer concern.
- Do not change artifact authorization, promotion, parser boundaries, validators, repository write semantics, projection freshness tracking, or active-epic transition semantics.
- Do not introduce a generic prompt-policy registry or shared framework.
- Do not rewrite unaffected `RealignEpic` prompt content for style or completeness.
- Do not authorize side-channel audit reports, rationale appendices, governance notes, research notes, repository re-audit outputs, or companion design documents.

## 5. Runtime / System State Before

- `CreateNewEpic` already owns prompt-specific implementation-first guidance through generated prompt sections.
- `RealignEpic` renders through `Core.Prompts.Planning.RealignEpic.Render(projectContext, secondaryInput)` with no prompt-owned section injection.
- `RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy()` returns true only for `CreateNewEpic`.
- `RoadmapPromptRunner.RunRuntimePromptAsync()` appends the legacy composer to `RealignEpic`.
- Transition snapshots for `RealignEpic` use `legacy-implementation-first-composer-v1`.
- The active epic rewrite path, promotion coordinator, classifier, validator, and blocked output contract already exist.

## 6. Runtime / System State After

- `RealignEpic` owns two generated section bodies under `src/LoopRelay.Core/Prompts/NonImplementation`.
- `RealignEpic.prompt` contains explicit placeholders for the selected generated sections.
- `RoadmapPromptCatalog.RenderRuntime()` renders `RealignEpic` through a prompt-specific helper.
- `RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy("RealignEpic")` returns true.
- `RoadmapPromptRunner` sends `RealignEpic` without appending `ImplementationFirstPromptPolicyComposer.SectionHeading`.
- `RoadmapRuntimePromptPolicy.CreateIdentity("RealignEpic")` emits `realign-epic-prompt-owned-v1`.
- Strict auxiliary mode records active section source hashes; allowed auxiliary mode records omitted section mode.
- `.agents/epic.md` realignment behavior and `# Epic Realignment Blocked` classification remain unchanged.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| M1-C1 Prompt-owned RealignEpic guidance | `RealignEpic.prompt` plus generated non-implementation sections | Strict implementation-first guidance for audit-driven minimal realignment | Project context, audit input, auxiliary artifact policy | Rendered prompt containing `# RealignEpic Implementation-First Guidance` and `# RealignEpic Auxiliary Artifact Limits` in strict mode | Prompt generator and section source files | Strict render contains both markers and sanctions `.agents/epic.md` minimal realignment | `RoadmapPromptRunner`, transition snapshots |
| M1-C2 Auxiliary branch selection | `RealignEpicPromptSections` | Select strict sections when auxiliary non-implementation files are disabled; omit when allowed | `AllowAuxiliaryNonImplementationFiles` | Section text and active section hash map | `RoadmapRuntimePromptPolicy` | Allowed mode removes placeholders and does not inject strict markers | Prompt catalog, policy identity |
| M1-C3 Runtime composer skip | `RoadmapPromptCatalog` and `RoadmapPromptRunner` | Treat `RealignEpic` as prompt-owned | Runtime prompt name | Prompt without legacy composer heading | UsesPromptOwnedNonImplementationPolicy | Runtime prompt lacks `ImplementationFirstPromptPolicyComposer.SectionHeading` | Agent runtime |
| M1-C4 Transition policy provenance | `RoadmapRuntimePromptPolicy` | Record prompt-owned identity branch | Runtime prompt name, prompt source hash, section source hashes, auxiliary setting | `TransitionPromptPolicyIdentity` mode `realign-epic-prompt-owned-v1` | Section selector | Identity changes between strict and allowed modes and includes active section hashes in strict mode | `RoadmapPromptTransitionRunner`, `TransitionInputResolver` |
| M1-C5 Existing rewrite contract preservation | Active epic transition and artifact components | Preserve current `.agents/epic.md` minimal patch flow | Existing realign prompt output or blocked output | Promoted active epic or blocked classification | Existing classifier, validator, promotion path | Existing transition and promotion tests remain passing | Roadmap state machine |

## 8. Architectural Responsibilities

- Prompt text ownership belongs to generated `.prompt` files in `LoopRelay.Core`.
- Section selection belongs near roadmap prompt rendering in `LoopRelay.Roadmap.Cli/Services/Prompts`.
- Runtime composer selection belongs to `RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy()`.
- Prompt-policy identity belongs to `RoadmapRuntimePromptPolicy`.
- Artifact validation and promotion remain owned by existing active-epic services.
- Data authority for the active epic remains `.agents/epic.md`; the new policy sections do not own artifact content.
- Failure authority remains split: render/identity failures are prompt-policy failures, blocked model output remains an epic authoring output, and invalid promoted content remains validator failure.

## 9. Components and Modules

| Component | Purpose | Responsibilities | Owned State | Consumed State | Public Contracts | Internal Contracts | Dependencies | Tests Required |
|---|---|---|---|---|---|---|---|---|
| `RealignEpicImplementationFirstGuidance.prompt` | Realign-specific implementation-first guidance | Explain sanctioned `.agents/epic.md` minimal patch behavior and invalid side artifacts | Generated prompt text and source hash | None | Generated `Text` and `SourceHash` | Body text not hard-coded in C# | Prompt generator | Marker and body-not-hard-coded tests |
| `RealignEpicAuxiliaryArtifactLimits.prompt` | Strict auxiliary artifact limits | Prohibit audit reports, rationale appendices, governance notes, research notes, and companion docs | Generated prompt text and source hash | None | Generated `Text` and `SourceHash` | Empty when auxiliary files are allowed | Prompt generator | Strict/allowed selector tests |
| `RealignEpic.prompt` | Owning planning prompt | Host placeholders and preserve audit-driven realignment contract | Prompt template and source hash | Project context, audit input, injected sections | Generated `Render(...)` overload | Placeholders must be fully substituted | Prompt generator | Render tests |
| `RealignEpicPromptSections` | Section selector | Return section set and active source hashes | None | Auxiliary artifact policy | Selector method | Strict returns two sections; allowed returns empty strings and empty hashes | Generated section classes | Selector tests |
| `RoadmapPromptCatalog` | Runtime prompt rendering and prompt-owned policy classification | Route `RealignEpic` through helper and classify it as prompt-owned | None | Runtime prompt name, policy | `RenderRuntime`, `UsesPromptOwnedNonImplementationPolicy` | Helper passes injected text only to `RealignEpic` | Selector | Runtime render and runner tests |
| `RoadmapRuntimePromptPolicy` | Transition policy identity | Emit `realign-epic-prompt-owned-v1` inputs | Identity input map | Policy settings, prompt/section source hashes | `CreateIdentity("RealignEpic")` | Source hash keys remain deterministic | Selector, `RoadmapHash` | Identity branch tests |

## 10. Repository and File Impact

Expected changes:

- Add `src/LoopRelay.Core/Prompts/NonImplementation/RealignEpicImplementationFirstGuidance.prompt`.
- Add `src/LoopRelay.Core/Prompts/NonImplementation/RealignEpicAuxiliaryArtifactLimits.prompt`.
- Modify `src/LoopRelay.Core/Prompts/Planning/RealignEpic.prompt` to include placeholders.
- Add `src/LoopRelay.Roadmap.Cli/Services/Prompts/RealignEpicPromptSections.cs`.
- Modify `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`.
- Modify `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`.
- Add focused prompt policy tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/Prompts`.
- Extend transition identity tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState` if needed.

No repository changes are expected in transition, promotion, validator, split, spec generation, or projection freshness modules unless an existing bug blocks this milestone.

## 11. Public Contracts

- `RealignEpic` runtime prompt remains the contract for audit-driven `.agents/epic.md` minimal realignment.
- `# Epic Realignment Blocked` remains the blocked response contract.
- Strict render includes `# RealignEpic Implementation-First Guidance` and `# RealignEpic Auxiliary Artifact Limits`.
- Allowed auxiliary render omits those strict sections and leaves no raw placeholders.
- `RoadmapPromptRunner` sends `RealignEpic` without the legacy composer heading.
- `TransitionPromptPolicyIdentity.Mode` for `RealignEpic` becomes `realign-epic-prompt-owned-v1`.
- Prompt contract registry remains unchanged: required input `.agents/epic.md`, required output `.agents/epic.md`, decision `Realign`, writer `ArtifactPromotionService`, parser `EpicAuthoringOutputClassifier+EpicArtifactValidator`.

## 12. Internal Contracts

- `RealignEpicPromptSections.ForAuxiliaryArtifactPolicy(false)` returns both section bodies and active section hashes.
- `RealignEpicPromptSections.ForAuxiliaryArtifactPolicy(true)` returns empty strings and no active section hashes.
- `RoadmapPromptCatalog.RenderRuntime("RealignEpic", ...)` must pass selected sections into the generated prompt render method.
- `UsesPromptOwnedNonImplementationPolicy("RealignEpic")` must be true before `RoadmapPromptRunner` decides whether to append the composer.
- Identity construction must use the same selector branch as rendering.
- Source hash keys must be deterministic and prompt-specific.
- Failures in prompt-owned rendering must fail before agent execution rather than silently falling back to legacy composer behavior.
- No retry path may append the legacy composer after a prompt-owned render failure.

## 13. Data and State Model

| State Object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Downstream Consumers |
|---|---|---|---|---|---|---|---|---|
| Realign implementation guidance section | `.prompt` source | Source -> generated class -> render input | Repository source and build output | Source controlled | Source hash | Marker and body-location tests | Rebuild generated prompts | Prompt catalog |
| Realign auxiliary limits section | `.prompt` source | Source -> generated class -> render input | Repository source and build output | Source controlled | Source hash | Selector tests | Rebuild generated prompts | Prompt catalog |
| Rendered `RealignEpic` prompt | `RoadmapPromptCatalog` | Policy selection -> render -> agent request | Ephemeral | Per invocation | Runtime prompt name and prompt text | Runtime runner tests | Re-render from source and settings | Agent runtime |
| Prompt policy identity | `RoadmapRuntimePromptPolicy` | Transition input resolution -> snapshot | Persisted in transition input evidence | Immutable after snapshot | Mode plus sorted input hashes | Transition identity tests | Recompute from same source and settings | Transition persistence |
| Active epic artifact | Existing artifact promotion path | Existing rewrite lifecycle | Retained file `.agents/epic.md` | Mutated only by promotion | Artifact path and content hash | Existing validator | Existing state recovery | Later roadmap prompts |

## 14. Lifecycle and State Transitions

Prompt-owned policy selection:

```text
Policy Loaded
  -> Realign Sections Selected
  -> Realign Prompt Rendered
  -> Prompt Policy Identity Created
  -> Agent Runtime Invoked
```

Transitions:

| Transition | Trigger | Preconditions | Result | Failure Mode | Audit or Evidence Record |
|---|---|---|---|---|---|
| Policy Loaded -> Realign Sections Selected | Runtime prompt execution starts | `RoadmapRuntimePromptPolicy` available | Strict or omitted section set selected | Missing generated section type or selector failure | Unit test failure; runtime exception before agent call |
| Realign Sections Selected -> Realign Prompt Rendered | Catalog render helper invoked | `RealignEpic.prompt` has placeholders | Prompt contains selected sections or empty replacements | Placeholder remains or wrong section injected | Prompt render test |
| Realign Prompt Rendered -> Prompt Policy Identity Created | Transition input snapshot resolved | Prompt name is `RealignEpic` | Identity mode `realign-epic-prompt-owned-v1` | Legacy identity emitted | Transition identity test |
| Prompt Policy Identity Created -> Agent Runtime Invoked | `RoadmapPromptRunner` sends prompt | Catalog classifies prompt-owned | Agent receives no legacy composer heading | Composer appended | Runtime runner test |

## 15. Execution Flow

Startup or initialization:

- Runtime policy is created from `NonImplementationArtifactPolicyOptions`.
- Generated prompt classes expose the modified `RealignEpic.Render(...)` signature and new section `Text`/`SourceHash` values.

Normal operation:

```text
RoadmapPromptRunner
  -> RoadmapPromptCatalog.RenderRuntime("RealignEpic", policy)
  -> RealignEpicPromptSections.ForAuxiliaryArtifactPolicy(policy.AllowAuxiliaryNonImplementationFiles)
  -> Core.Prompts.Planning.RealignEpic.Render(projectContext, auditInput, selectedSections)
  -> UsesPromptOwnedNonImplementationPolicy("RealignEpic") == true
  -> Agent runtime receives prompt without legacy composer
```

Failure flow:

- Render failures stop before agent execution.
- Invalid output still flows through the existing classifier and validator.
- Blocked output still uses `# Epic Realignment Blocked`.

Recovery flow:

- Re-running with the same source and settings produces the same rendered prompt branch and identity inputs.
- Existing roadmap failure persistence handles runtime, validation, or promotion failures.

Shutdown or completion:

- No new shutdown behavior is introduced.

## 16. Dependency Closure

| Dependency | Classification | Required State |
|---|---|---|
| `CreateNewEpic` prompt-owned pattern | Hard prerequisite | Existing generated sections, selector, catalog helper, runner skip, and policy identity are present. |
| Prompt source generator | Supporting infrastructure | New `.prompt` files produce classes with `Text`, `Render(...)`, and `SourceHash`. |
| `RealignEpic.prompt` | Hard prerequisite | Existing prompt remains the owner of realignment behavior and accepts new placeholders. |
| `ActiveEpicRewriteTransition` path | Inherited capability | Existing realignment transition and promotion path works before and after migration. |
| `AllowAuxiliaryNonImplementationFiles` setting | Supporting infrastructure | Selection input is available from runtime prompt policy. |
| `AllowHitlRequestedNonImplementationFiles` | Explicitly unavailable dependency | Must not drive prompt-owned section selection. |
| Later prompt migrations | Future dependency | Not required for this milestone. |

This milestone enables:

- Milestone 2 by proving the active-epic rewrite prompt-owned pattern on the narrower realignment case.
- Milestone 5 by adding one prompt to the final no-legacy-composer regression set.

## 17. Failure Modes

| Failure Mode | Detection Method | Expected System Behavior | Recovery Path | Diagnostic Output | Test Coverage Required |
|---|---|---|---|---|---|
| Strict sections not injected | Render test missing markers | Test fails; runtime should not be accepted | Fix placeholders, selector, or render helper | Assertion showing missing marker | Strict render test |
| Allowed mode leaves placeholders | Render test finds `{realign...}` token | Test fails before release | Ensure empty strings are passed through render method | Assertion showing placeholder | Allowed render test |
| Legacy composer still appended | Runtime runner prompt capture | Test fails; prompt contains legacy heading | Update `UsesPromptOwnedNonImplementationPolicy` | Captured prompt includes `ImplementationFirstPromptPolicyComposer.SectionHeading` | Runner skip test |
| Identity still uses legacy mode | Transition identity test | Test fails; provenance remains inaccurate | Add `RealignEpic` identity branch | Mode mismatch | Identity mode test |
| Strict identity omits section hashes | Identity input assertion | Test fails; prompt section changes would be invisible | Add active section source hash inputs | Missing key diagnostics | Identity hash test |
| Section body hard-coded in C# | Source scan test | Test fails; violates prompt ownership | Move text back into `.prompt` source | File path and matched line | Body-location regression |
| Realignment contract drifts | Existing transition or validator tests | Test fails; artifact behavior changed | Revert unrelated transition changes | Existing test diagnostics | Existing regression tests |

## 18. Validation and Invariants

| Invariant | Source Authority | Enforcement Point | Failure Behavior | Test Strategy |
|---|---|---|---|---|
| `RealignEpic` owns its implementation-first guidance after migration | Roadmap Milestone 1 | Prompt-owned render helper and runner skip | Legacy composer heading appears or section markers missing | Render and runner tests |
| `.agents/epic.md` remains the only sanctioned implementation-bearing artifact for realignment | Roadmap Section Semantics | Section text and existing prompt contract | Prompt permits side-channel outputs | Prompt text assertions |
| Audit-driven minimal realignment remains intact | Roadmap Milestone 1 and audit RealignEpic section | Existing prompt body and transition path | Prompt encourages full rewrite or style rewrite | Prompt-specific assertions |
| Prompt-policy identity changes by auxiliary branch | Roadmap Reference Pattern | `RoadmapRuntimePromptPolicy.CreateIdentity` | Strict and allowed branches hash identically | Identity comparison test |
| Active section source hashes are recorded in strict mode | Roadmap Reference Pattern | Identity input map | Prompt section changes lack provenance | Identity input key assertions |
| Artifact writer/parser boundaries remain unchanged | Roadmap Scope Boundaries | Prompt contract registry and existing services | Writer/parser changes without need | Existing contract tests |

## 19. Testing Strategy

- Unit tests: selector strict/allowed behavior and source hash map contents.
- Render tests: strict markers, `.agents/epic.md` sanctioning, milestone roadmap preservation wording, placeholder removal, no `# Invalid Content`.
- Runtime tests: `RoadmapPromptRunner` skips composer for `RealignEpic` and still appends it to an out-of-scope prompt.
- Contract tests: prompt contract registry remains unchanged for `RealignEpic`.
- Transition identity tests: strict/allowed identities differ, mode is `realign-epic-prompt-owned-v1`, prompt source hash and active section hashes are present.
- Regression tests: existing active epic rewrite, promotion, classifier, validator, and blocked-output tests remain passing.
- Failure-path tests: invalid realignment output still fails through existing validator behavior.
- Performance smoke tests: prompt rendering remains in-memory and has no runtime file IO.

## 20. Fixtures and Test Data

- Minimal valid strict render input: `"project context"` plus an audit input requiring a small `.agents/epic.md` correction.
- Allowed auxiliary render input: same inputs with `AllowAuxiliaryNonImplementationFiles=true`.
- Invalid side artifact phrase fixture: prompt text must reject repository re-audit reports, raw audit summaries, rationale appendices, governance notes, research notes, and companion design documents.
- Legacy prompt fixture: `SelectNextEpic` or another unmigrated roadmap runtime prompt that should still receive the composer.
- Identity fixture: strict and allowed `RoadmapRuntimePromptPolicy` instances constructed from `NonImplementationArtifactPolicyOptions`.
- Blocked fixture: output beginning `# Epic Realignment Blocked`.
- Regression fixture: existing valid realignment output accepted by `EpicAuthoringOutputClassifier+EpicArtifactValidator`.

## 21. Acceptance Demonstration

Setup:

- Build the solution after adding the new `.prompt` files and selector.
- Use a test runtime that captures prompts sent by `RoadmapPromptRunner`.

Input:

- Runtime prompt name `RealignEpic`.
- Project context `"project context"`.
- Secondary audit input `"audit requires minimal realignment"`.
- Strict policy with `AllowAuxiliaryNonImplementationFiles=false`.

Execution steps:

1. Render `RoadmapPromptCatalog.RenderRuntime("RealignEpic", context, audit, strictPolicy)`.
2. Run `RoadmapPromptRunner.RunRuntimePromptAsync("RealignEpic", context, audit, CancellationToken.None)` with a scripted completed agent result.
3. Compute `strictPolicy.CreateIdentity("RealignEpic")`.
4. Run the focused prompt policy and transition identity tests.

Expected output:

- Rendered prompt contains `# RealignEpic Implementation-First Guidance`.
- Rendered prompt contains `# RealignEpic Auxiliary Artifact Limits`.
- Rendered prompt contains `.agents/epic.md`.
- Captured runtime prompt does not contain `ImplementationFirstPromptPolicyComposer.SectionHeading`.

Expected persisted state:

- Transition prompt-policy identity mode is `realign-epic-prompt-owned-v1` when transition input snapshotting runs.
- Strict identity contains the `RealignEpic` prompt source hash and both section source hashes.

Expected diagnostics:

- No diagnostics in successful path.
- Test failure names identify marker, composer, or identity mismatches.

Verification command:

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~RealignEpicPromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests"
```

## 22. Certification Evidence

- Passing focused `RealignEpic` prompt policy tests.
- Passing transition identity tests for strict and allowed branches.
- Passing runtime runner test proving no legacy composer for `RealignEpic`.
- Passing regression assertion that at least one out-of-scope prompt still receives the legacy composer.
- Passing source scan proving section body text is not hard-coded in C#.
- Passing existing active-epic rewrite, promotion, classifier, validator, and prompt contract tests.
- Captured rendered prompt samples for strict and allowed modes if the test suite emits diagnostics on failure.

## 23. Implementation Plan

| Step | Purpose | Deliverables | Dependencies | Completion Criteria |
|---|---|---|---|---|
| Add Realign section sources | Move policy text into generated prompt ownership | Two `RealignEpic*.prompt` files | Prompt generator | Generated classes expose `Text` and `SourceHash`; section markers are prompt-specific |
| Add placeholders to `RealignEpic.prompt` | Let owning prompt receive generated sections | Placeholder additions and render signature update | Section source names | Strict and allowed renders substitute placeholders |
| Implement selector | Centralize strict/allowed section choice | `RealignEpicPromptSections` and section set record | Generated section classes | Selector tests pass |
| Update catalog rendering | Route `RealignEpic` through prompt-specific helper | `RenderRealignEpic` and classification update | Selector | Render tests pass; runner skips composer |
| Update policy identity | Record prompt-owned provenance | `realign-epic-prompt-owned-v1` branch | Selector and prompt source hash | Identity tests pass |
| Add regression tests | Prove behavior and boundaries | Focused tests beside CreateNewEpic prompt policy tests | All implementation steps | Tests cover strict/allowed, skip, legacy prompt, identity, and C# body scan |
| Run focused and affected tests | Certify no behavioral drift | Test results | Completed code changes | All focused and affected regression tests pass |

## 24. Parallel Work Opportunities

| Parallel Lane | Scope | Owner Type | Dependencies | Synchronization Point | Integration Risk |
|---|---|---|---|---|---|
| Section wording | Draft prompt-specific `.prompt` bodies | Prompt/runtime engineer | Roadmap Section Semantics | Before render tests assert markers and key terms | Medium: wording can accidentally over-authorize side artifacts |
| Selector and catalog wiring | Add selector, helper, and classification | Runtime engineer | Section class names or agreed names | When generated classes compile | Low: follows `CreateNewEpic` pattern |
| Identity tests | Add expected mode and hash assertions | Test engineer | Final selector API | After identity branch exists | Medium: identity must use same branch as rendering |
| Regression preservation | Run existing active-epic tests | Test engineer | Catalog and identity compile | Before milestone certification | Low: transition components should remain unchanged |

## 25. Risks and Mitigations

| Risk | Class | Impact | Likelihood | Earliest Detection Point | Mitigation | Fallback |
|---|---|---|---|---|---|---|
| Section text permits side-channel audit artifacts | Architectural | Realign prompt violates implementation-first scope | Medium | Prompt text review and render assertions | Use explicit invalid artifact limits from roadmap | Tighten section text without changing runtime wiring |
| Identity branch drifts from render branch | Implementation | Transition provenance misrepresents actual prompt | Medium | Identity strict/allowed tests | Use the same selector in render and identity paths | Extract a tiny shared helper only if duplication causes mismatch |
| Generic abstraction introduced too early | Maintainability | Registry/framework complexity before need is proven | Medium | Code review and diff scope | Keep prompt-specific selector | Remove abstraction and match `CreateNewEpic` shape |
| Existing realignment behavior changes | Integration | Active epic rewrite regressions | Low | Existing transition/promotion tests | Avoid changing transition services | Revert unrelated transition edits |
| `AllowHitlRequestedNonImplementationFiles` gets reused | Operational | Settings semantics blur | Low | Policy tests | Keep selection solely on `AllowAuxiliaryNonImplementationFiles` | Restore legacy-only HITL behavior |

## 26. Observability and Diagnostics

- Render diagnostics are primarily test-based: marker presence, placeholder absence, and composer heading absence.
- Transition snapshots expose prompt-policy identity mode and sorted input hashes.
- Runtime failures continue to surface through `RoadmapPromptRunner` diagnostics from agent stderr.
- Artifact validation diagnostics remain owned by `EpicArtifactValidator`.
- No new metrics or health checks are required because this milestone changes deterministic prompt rendering, not a long-running service.
- Debug inspection should be possible by rendering strict and allowed prompts in tests.

## 27. Performance and Scalability Considerations

- Baseline expectation: rendering remains in-memory and comparable to `CreateNewEpic`.
- Likely bottleneck: none beyond generated prompt string size.
- Scaling risk: repeated prompt-specific branches could grow `RoadmapRuntimePromptPolicy.CreateIdentity()` and `RoadmapPromptCatalog.RenderRuntime()` switches.
- Measurement strategy: focused tests and normal build/test timing are sufficient.
- Deferred optimization: shared identity helper only after repeated migrations prove mechanical duplication is harmful.

## 28. Security and Safety Considerations

- Prevent unsafe writes by preserving the existing artifact writer and validator path for `.agents/epic.md`.
- Treat model output as untrusted until classified and validated by existing services.
- Do not permit prompt text to authorize arbitrary repository writes or extra artifact files.
- Preserve blocked output handling so explanatory artifacts are not created after `# Epic Realignment Blocked`.
- Preserve deterministic source-hash provenance so prompt-policy changes are auditable.
- Do not broaden privilege boundaries; `RoadmapPromptRunner` remains a read-only planning agent invocation followed by existing artifact promotion.

## 29. Documentation Updates

No documentation-only repository deliverable is required for this milestone.

Allowed source-adjacent text changes:

- Prompt-owned `.prompt` section bodies, because they are runtime prompt source.
- Test names or comments only when needed to describe actual prompt-policy ownership accurately.

Do not create ADRs, RFCs, migration reports, audit summaries, or companion design documents as part of implementation.

## 30. Exit Criteria

Milestone 1 is complete only when:

- Both `RealignEpic` section source files exist and are generated.
- `RealignEpic.prompt` accepts and substitutes section placeholders.
- `RoadmapPromptCatalog.RenderRuntime("RealignEpic", ...)` uses prompt-specific section injection.
- `UsesPromptOwnedNonImplementationPolicy("RealignEpic")` returns true.
- `RoadmapPromptRunner` does not append the legacy composer to `RealignEpic`.
- `RoadmapRuntimePromptPolicy.CreateIdentity("RealignEpic")` emits `realign-epic-prompt-owned-v1`.
- Strict identity includes prompt source hash and active section source hashes.
- Allowed identity records omitted section mode and differs from strict identity.
- All required tests pass.
- Existing artifact contracts, validators, promotion behavior, blocked output handling, and transition flow remain unchanged.
- No future milestone capability is claimed.

## 31. Transition to Next Milestone

Milestone 1 hands off:

- A working prompt-owned migration pattern for an active-epic rewrite prompt.
- `RealignEpic` section selector and identity structure as a concrete sibling to `CreateNewEpic`.
- Regression evidence that the runner can skip the legacy composer for more than one roadmap artifact-authoring prompt.
- Preserved `.agents/epic.md` rewrite contract and blocked output behavior.

Limitations carried forward:

- `ReimagineEpic`, `GenerateMilestoneDeepDivesForEpic`, and `SplitEpic` still use the legacy composer.
- Shared helper extraction remains deferred unless duplication becomes clearly harmful in later milestones.
- `ImplementationFirstPromptPolicyComposer` remains active for non-roadmap and out-of-scope consumers.
