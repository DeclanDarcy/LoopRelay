# Milestone 5 Deep Dive: Retirement Checkpoint and Regression Hardening

## 1. Milestone Summary

| Field | Value |
|---|---|
| Milestone Identifier | Milestone 5 |
| Milestone Name | Retirement Checkpoint and Regression Hardening |
| Roadmap Position | 5 of 5 |
| Implementation Role | Certify completion of the in-scope roadmap runtime artifact-authoring prompt migration and preserve legacy behavior for out-of-scope consumers. |
| Short Description | Add aggregate regression coverage proving migrated prompts skip the legacy composer, prompt-owned policy identity is complete, primary artifact contracts are preserved, and non-migrated consumers still use `ImplementationFirstPromptPolicyComposer`. |

Primary outcomes:

- All five prompt-owned roadmap artifact-authoring prompts skip the legacy composer at runtime: pre-existing `CreateNewEpic` plus newly migrated `RealignEpic`, `ReimagineEpic`, `GenerateMilestoneDeepDivesForEpic`, and `SplitEpic`.
- Prompt-policy identity tests cover all migrated prompt-owned modes and source-hash inputs.
- `AllowAuxiliaryNonImplementationFiles` changes identity for every migrated prompt without weakening primary artifact contracts.
- `AllowHitlRequestedNonImplementationFiles` remains isolated to legacy composer behavior.
- Out-of-scope legacy prompt paths continue receiving the composer.
- Narrow ownership terminology in comments or test names no longer falsely describes migrated roadmap artifact-authoring policy as centralized.

## 2. Normative Basis

Roadmap authority:

- `.agents/specs/roadmap.md` Objective, Scope Boundaries, Reference Pattern, Migration Order, Milestone 5, Cross-Milestone Rules, Verification Matrix, Done Definition, and broader composer-retirement caveat.

Architectural authority:

- `.agents/specs/audit.md` Architecture Observations, Transition and Provenance Observations, Settings Flow Observations, Testing Observations, Composer Retirement Readiness, Risks, and Summary Interpretation.

Implementation authority:

- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptRunner.cs`
- Prompt-specific section selectors for `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`, `GenerateMilestoneDeepDivesForEpic`, and `SplitEpic`
- `src/LoopRelay.Orchestration.Primitives/Services/NonImplementationReview/ImplementationFirstPromptPolicyComposer.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/PromptContractRegistry.cs`

Supporting context:

- Prompt policy tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/Prompts`.
- Transition identity tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState`.
- Existing non-roadmap composer tests under orchestration, plan CLI, loop CLI, completion, decision, and execution test projects where present.

No authority is introduced outside the roadmap, audit, and current implementation contracts.

## 3. Objective

Certify that the roadmap runtime artifact-authoring prompts in this roadmap are prompt-owned for implementation-first policy, that their transition provenance accurately records prompt-owned selection, and that `ImplementationFirstPromptPolicyComposer` remains intact for out-of-scope legacy consumers.

## 4. Non-Goals

- Do not delete `ImplementationFirstPromptPolicyComposer`.
- Do not migrate Plan CLI prompts, Loop CLI prompts, completion prompts, decision prompts, execution prompts, `AdversarialPlanReview`, or any roadmap prompt outside the artifact-authoring migration scope.
- Do not introduce a generic prompt-policy framework unless narrowly required to remove proven mechanical duplication without changing behavior.
- Do not change artifact authorization, promotion, validators, parser boundaries, repository write semantics, projection freshness, HITL behavior, or post-execution non-implementation review.
- Do not alter primary output contracts for `.agents/epic.md`, `.agents/specs/*.md`, split bundles, or child epic files.
- Do not remove non-roadmap composer tests.
- Do not create administrative completion reports or documentation-only deliverables.

## 5. Runtime / System State Before

- `CreateNewEpic` is already prompt-owned.
- Milestones 1 through 4 have migrated `RealignEpic`, `ReimagineEpic`, `GenerateMilestoneDeepDivesForEpic`, and `SplitEpic`.
- `RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy()` should return true for all five migrated prompts.
- `RoadmapPromptRunner` should skip the legacy composer for all migrated prompts and append it for legacy prompts.
- `RoadmapRuntimePromptPolicy.CreateIdentity()` should contain prompt-owned identity branches for all five migrated prompts.
- The legacy composer remains present for out-of-scope consumers.

## 6. Runtime / System State After

- Aggregate tests prove migrated prompts do not contain `ImplementationFirstPromptPolicyComposer.SectionHeading` in strict mode.
- Companion regression tests prove at least one out-of-scope roadmap runtime prompt still receives the composer.
- Identity tests cover all prompt-owned modes:
  - `create-new-epic-prompt-owned-v1`
  - `realign-epic-prompt-owned-v1`
  - `reimagine-epic-prompt-owned-v1`
  - `generate-milestone-deep-dives-for-epic-prompt-owned-v1`
  - `split-epic-prompt-owned-v1`
- Strict mode records prompt source hash and active section source hashes for every migrated prompt.
- Allowed auxiliary mode is explicitly represented and changes identity for every migrated prompt.
- `AllowHitlRequestedNonImplementationFiles` remains a legacy composer setting.
- All existing artifact contracts, parser boundaries, promotion behavior, validators, and transition semantics remain intact.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| M5-C1 Aggregate no-composer regression | Prompt policy test suite | Run all in-scope artifact-authoring prompts through runner in strict mode | Migrated prompt names and strict policy | Captured prompts without legacy heading | Milestones 1-4 | All five migrated prompts lack `ImplementationFirstPromptPolicyComposer.SectionHeading` | Roadmap runtime safety |
| M5-C2 Legacy composer preservation | Prompt runner and non-roadmap tests | Prove out-of-scope prompt paths still receive composer | Legacy prompt name and default/strict policy | Captured prompt with legacy heading | Existing composer | Companion assertion passes | Plan/Loop/Completion/Decision/Execution paths |
| M5-C3 Prompt-owned identity matrix | `RoadmapRuntimePromptPolicy` tests | Cover all migrated identity modes and source hash inputs | Prompt names, strict/allowed policies | `TransitionPromptPolicyIdentity` values | Prompt-specific selectors | Modes and input keys match roadmap; strict/allowed identities differ | Transition snapshots |
| M5-C4 Settings separation | Policy tests | Keep auxiliary and HITL settings separate | `NonImplementationArtifactPolicyOptions` variants | Prompt-owned identity affected by auxiliary setting; legacy policy affected by HITL setting | Existing settings flow | Tests prove no prompt-owned reuse of HITL setting | CLI settings users |
| M5-C5 Primary contract preservation | Prompt render and contract tests | Verify migrated prompts still sanction their primary outputs | Rendered prompts and prompt contracts | Assertions for `.agents/epic.md`, `.agents/specs/*.md`, split bundle, child epic files | Milestones 1-4 | Contracts remain intact in strict and allowed modes | Transitions and state machine |
| M5-C6 Ownership terminology accuracy | Source-adjacent comments/test names | Remove false centralized-roadmap-policy terminology where narrow and source-adjacent | Existing test names/comments | Accurate names/comments | Completed migrations | No source-adjacent text claims migrated roadmap artifact-authoring policy is centralized | Maintainers and tests |

## 8. Architectural Responsibilities

- `RoadmapPromptCatalog` remains the authority for whether a roadmap runtime prompt owns non-implementation policy.
- Prompt-specific selectors remain the authority for active prompt-owned section sources.
- `RoadmapRuntimePromptPolicy` remains the authority for transition prompt-policy identity.
- `RoadmapPromptRunner` remains the enforcement point for appending or skipping the legacy composer.
- `ImplementationFirstPromptPolicyComposer` remains the authority for legacy out-of-scope prompt policy.
- Prompt contracts remain the authority for artifact writer/parser boundaries.
- Tests in this milestone certify architecture boundaries; they do not move ownership to a new framework.

## 9. Components and Modules

| Component | Purpose | Responsibilities | Owned State | Consumed State | Public Contracts | Internal Contracts | Dependencies | Tests Required |
|---|---|---|---|---|---|---|---|---|
| Aggregate prompt policy tests | Certify migrated prompts skip composer | Capture runner prompts for all migrated prompt names | Test fixtures only | Runtime prompt names and strict policy | Test suite behavior | Uses same runner branch as production | Milestones 1-4 | Aggregate no-composer test |
| Legacy companion test | Preserve out-of-scope composer path | Capture a legacy roadmap prompt receiving composer | Test fixtures only | Legacy prompt name and policy | Test suite behavior | Must use an unmigrated prompt | Existing composer branch | Composer-present test |
| Identity matrix tests | Certify prompt-owned provenance | Assert modes, branch inputs, source hash keys | Test fixtures only | Prompt names and policies | Test suite behavior | Strict/allowed comparison for every migrated prompt | Selectors and identity branches | Matrix identity test |
| Settings separation tests | Protect settings semantics | Assert auxiliary and HITL settings affect only intended paths | Test fixtures only | Policy options | Test suite behavior | Prompt-owned selection ignores HITL | Runtime policy | Settings tests |
| Prompt contract regressions | Preserve artifact boundaries | Confirm writer/parser and primary outputs unchanged | Test fixtures only | Prompt contract registry | Existing contract snapshot | No contract widening | Prompt contract registry | Contract tests |
| Source-adjacent terminology updates | Remove false ownership labels | Update comments/test names only when inaccurate | Source text | Completed migration behavior | None beyond compiled tests | No public rename unless already internal/test-only | Existing source | Compile and tests |

## 10. Repository and File Impact

Expected changes:

- Add or update aggregate prompt policy tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/Prompts`.
- Add or update identity matrix tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState`.
- Update existing focused prompt policy tests only to reduce duplication or add aggregate assertions.
- Update source-adjacent comments, test names, or narrow internal terminology that falsely describes migrated roadmap artifact-authoring prompt policy as centralized.

Expected unchanged areas:

- `ImplementationFirstPromptPolicyComposer.cs` remains present.
- Non-roadmap composer consumers remain present.
- Prompt contract registry behavior remains unchanged except for any generated snapshot if existing tests intentionally emit one.
- Artifact authorization, promotion, validators, parser boundaries, repository write semantics, and projection freshness remain unchanged.

## 11. Public Contracts

- Migrated roadmap artifact-authoring prompts:
  - `CreateNewEpic`
  - `RealignEpic`
  - `ReimagineEpic`
  - `GenerateMilestoneDeepDivesForEpic`
  - `SplitEpic`
- These prompts do not receive the legacy composer at runtime.
- Out-of-scope runtime prompts continue to receive the legacy composer until separately migrated.
- Prompt-owned identity modes are stable public provenance strings for transition snapshots.
- Primary artifact contracts remain:
  - `CreateNewEpic`, `RealignEpic`, and `ReimagineEpic`: `.agents/epic.md`
  - `GenerateMilestoneDeepDivesForEpic`: `.agents/specs/*.md`
  - `SplitEpic`: split bundle and child epic files with selected-child promotion
- `AllowAuxiliaryNonImplementationFiles` controls strict prompt-owned section injection.
- `AllowHitlRequestedNonImplementationFiles` remains tied to legacy composer behavior.

## 12. Internal Contracts

- Aggregate tests must call production `RoadmapPromptRunner` or `RoadmapPromptCatalog` paths rather than manually concatenating prompt strings.
- `UsesPromptOwnedNonImplementationPolicy()` must be the single runner decision point for skipping the composer.
- Identity tests must use `RoadmapRuntimePromptPolicy.CreateIdentity(promptName)` for each migrated prompt.
- Strict prompt-owned identity includes prompt source hash, `allowAuxiliaryNonImplementationFiles=false`, `sectionMode=strict`, and active section source hashes.
- Allowed prompt-owned identity includes `allowAuxiliaryNonImplementationFiles=true`, `sectionMode=omitted`, and no active section hashes.
- Legacy identity remains `legacy-implementation-first-composer-v1` and includes a hash of composed legacy policy text.
- HITL-requested allowance must not appear as a prompt-owned section selector input.

## 13. Data and State Model

| State Object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Downstream Consumers |
|---|---|---|---|---|---|---|---|---|
| Migrated prompt set | `RoadmapPromptCatalog` | Source branch -> runtime classification | Source controlled | Changed only by intentional migrations | Runtime prompt names | Aggregate runner tests | Recompute from source | Runner and identity |
| Prompt-owned section source hashes | Prompt-specific generated sections | Source -> generated class -> identity input | Repository source/build output | Source controlled | Section source hash keys | Identity tests | Rebuild generated prompts | Transition snapshots |
| Prompt policy identity matrix | `RoadmapRuntimePromptPolicy` | Prompt name + policy -> identity | Persisted per transition snapshot | Immutable after snapshot | Mode and sorted input hash | Matrix tests | Recompute from source and settings | Transition persistence |
| Legacy composer policy text | `ImplementationFirstPromptPolicyComposer` | Artifact policy -> composed text -> appended prompt | Ephemeral text, source controlled implementation | Source controlled | Hash in legacy identity | Existing composer tests | Recompute from artifact policy | Legacy runtime prompts |
| Primary artifact contracts | `PromptContractRegistry` and transition services | Contract declaration -> runtime output handling | Source controlled and persisted artifacts | Changed only by explicit contract migration | Prompt name and artifact paths | Contract tests | Existing artifact recovery | Roadmap state machine |

## 14. Lifecycle and State Transitions

Aggregate certification lifecycle:

```text
Migrated Prompt Set Resolved
  -> Strict Prompts Rendered Through Runner
  -> Composer Absence Verified
  -> Prompt-Owned Identities Verified
  -> Primary Contracts Verified
  -> Legacy Prompt Composer Verified
  -> Migration Scope Certified
```

| Transition | Trigger | Preconditions | Result | Failure Mode | Audit or Evidence Record |
|---|---|---|---|---|---|
| Prompt Set Resolved -> Strict Prompts Rendered | Aggregate test starts | Milestones 1-4 complete | Captured prompt for each migrated prompt | Missing migrated prompt branch | Aggregate test failure |
| Strict Prompts Rendered -> Composer Absence Verified | Captured prompts inspected | Runner used production classification | No legacy heading in migrated prompts | Composer leakage | Test diagnostics naming prompt |
| Composer Absence -> Identities Verified | Identity matrix executes | Identity branches exist | Prompt-owned modes and hashes present | Legacy or incomplete identity | Identity test diagnostics |
| Identities Verified -> Contracts Verified | Contract tests run | Prompt registry unchanged | Primary artifacts preserved | Contract drift | Contract test diagnostics |
| Contracts Verified -> Legacy Prompt Composer Verified | Legacy companion test runs | At least one out-of-scope prompt remains legacy | Composer heading present | Composer removed globally | Companion test diagnostics |
| Legacy Verified -> Scope Certified | All tests pass | No out-of-scope behavior changed | Roadmap migration certified | Test failure | Test output |

## 15. Execution Flow

Startup or initialization:

- Test fixtures construct strict and allowed `RoadmapRuntimePromptPolicy` instances.
- Scripted agent runtime captures prompts from `RoadmapPromptRunner`.

Normal certification flow:

```text
for each migrated prompt:
  RoadmapPromptRunner.RunRuntimePromptAsync(prompt, ...)
  assert captured prompt lacks ImplementationFirstPromptPolicyComposer.SectionHeading
  assert RoadmapRuntimePromptPolicy.CreateIdentity(prompt).Mode is prompt-owned
  assert strict identity includes active section hashes
  assert allowed identity differs and records omitted section mode

run legacy control prompt:
  assert captured prompt contains ImplementationFirstPromptPolicyComposer.SectionHeading
```

Failure flow:

- A missing prompt branch fails the aggregate no-composer or identity test.
- A changed contract fails prompt contract or transition regression tests.
- An unintended composer deletion fails legacy companion tests.

Recovery flow:

- Fix the incorrect prompt-owned classification, identity branch, selector, or accidental legacy composer change.
- Re-run aggregate and affected regression tests.

Shutdown or completion:

- No runtime shutdown behavior is introduced.

## 16. Dependency Closure

| Dependency | Classification | Required State |
|---|---|---|
| `CreateNewEpic` prior migration | Hard prerequisite | Existing prompt-owned mode `create-new-epic-prompt-owned-v1` remains passing. |
| Milestone 1 `RealignEpic` migration | Hard prerequisite | `realign-epic-prompt-owned-v1` branch and composer skip implemented. |
| Milestone 2 `ReimagineEpic` migration | Hard prerequisite | `reimagine-epic-prompt-owned-v1` branch and composer skip implemented. |
| Milestone 3 spec-generation migration | Hard prerequisite | `generate-milestone-deep-dives-for-epic-prompt-owned-v1` branch and primary specs contract preserved. |
| Milestone 4 split migration | Hard prerequisite | `split-epic-prompt-owned-v1` branch and split bundle contract preserved. |
| Legacy composer tests | Supporting infrastructure | Existing composer behavior remains testable. |
| Non-roadmap composer consumers | Explicitly unavailable for migration | They remain outside this roadmap and must not be changed. |

This milestone enables:

- Declaring the roadmap complete under its scoped Done Definition.
- Future separate composer retirement work with a clear boundary between roadmap artifact-authoring prompts and remaining legacy consumers.

## 17. Failure Modes

| Failure Mode | Detection Method | Expected System Behavior | Recovery Path | Diagnostic Output | Test Coverage Required |
|---|---|---|---|---|---|
| Migrated prompt still receives composer | Aggregate runner test | Test fails | Fix catalog classification or runner logic | Prompt name and captured heading | No-composer matrix test |
| Out-of-scope prompt stops receiving composer | Legacy companion test | Test fails | Restore legacy branch behavior | Missing composer heading | Legacy preservation test |
| Identity mode missing for migrated prompt | Identity matrix test | Test fails | Add branch or helper case | Mode mismatch | Identity matrix test |
| Strict identity omits section hashes | Identity input assertion | Test fails | Use selector active hash map | Missing key diagnostics | Strict identity test |
| Allowed identity equals strict identity | Identity comparison | Test fails | Include auxiliary branch and section mode | Equal identity hashes | Strict/allowed test |
| HITL setting affects prompt-owned branch | Settings separation test | Test fails | Remove HITL input from prompt-owned selector | Unexpected identity/input change | Settings test |
| Primary artifact contract drift | Prompt contract/render test | Test fails | Restore prompt or contract behavior | Contract diff/assertion | Contract regression |
| Non-roadmap composer tests removed or broken | Existing test suite | Test fails | Restore tests or behavior | Test failure | Non-roadmap composer tests |

## 18. Validation and Invariants

| Invariant | Source Authority | Enforcement Point | Failure Behavior | Test Strategy |
|---|---|---|---|---|
| All in-scope roadmap artifact-authoring prompts skip legacy composer | Roadmap Done Definition | Runner branch and catalog classification | Composer heading appears | Aggregate no-composer test |
| Out-of-scope consumers retain legacy composer | Roadmap Scope Boundaries | Runner legacy branch and non-roadmap consumers | Composer removed too broadly | Legacy companion and existing tests |
| Prompt-owned identity records branch and source hashes | Roadmap Reference Pattern | `RoadmapRuntimePromptPolicy` | Provenance incomplete | Identity matrix tests |
| Auxiliary setting changes prompt-owned identity | Roadmap Acceptance | Identity input map | Strict/allowed indistinguishable | Strict/allowed comparison |
| HITL setting remains legacy-composer concern | Roadmap Reference Pattern | Selector and policy tests | Settings semantics blur | Settings separation tests |
| Primary artifact contracts remain unchanged | Roadmap Cross-Milestone Rules | Prompt contracts and transition tests | Runtime outputs drift | Contract and transition regression tests |
| Composer remains transitional infrastructure | Roadmap Objective and Done Definition | Source and tests | Composer deleted or untested | Existing composer tests |

## 19. Testing Strategy

- Aggregate runner tests: execute all migrated prompts in strict mode and assert no legacy composer heading.
- Legacy control tests: execute an out-of-scope roadmap prompt and assert the legacy composer heading is present.
- Identity matrix tests: verify all five prompt-owned modes and required input keys.
- Strict/allowed identity tests: verify identity changes for each migrated prompt.
- Settings separation tests: verify `AllowAuxiliaryNonImplementationFiles` affects prompt-owned branches and `AllowHitlRequestedNonImplementationFiles` remains legacy-only.
- Prompt contract tests: verify required inputs, outputs, writer, parser, and blocked output behavior remain unchanged.
- Existing focused prompt policy tests: remain as prompt-specific behavioral guards.
- Existing transition tests: remain as artifact behavior guards.
- Existing non-roadmap composer tests: remain passing.
- Performance smoke tests: aggregate test should be in-memory scripted runtime only.

## 20. Fixtures and Test Data

- Migrated prompt name list: `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`, `GenerateMilestoneDeepDivesForEpic`, `SplitEpic`.
- Strict policy fixture: `AllowAuxiliaryNonImplementationFiles=false`, `AllowHitlRequestedNonImplementationFiles=false`.
- Allowed policy fixture: `AllowAuxiliaryNonImplementationFiles=true`, `AllowHitlRequestedNonImplementationFiles=false`.
- HITL fixture: policy variants that toggle `AllowHitlRequestedNonImplementationFiles` without changing prompt-owned selector behavior.
- Prompt input fixture map:
  - `CreateNewEpic`: project context plus proposal.
  - `RealignEpic`: project context plus audit input.
  - `ReimagineEpic`: project context plus audit input.
  - `GenerateMilestoneDeepDivesForEpic`: project context, empty secondary input.
  - `SplitEpic`: project context plus split proposal.
- Legacy control prompt fixture: an out-of-scope runtime prompt such as `SelectNextEpic`.
- Expected identity mode table for all migrated prompts.
- Expected primary artifact table for all migrated prompts.

## 21. Acceptance Demonstration

Setup:

- Build the solution after Milestones 1 through 4.
- Use scripted agent runtime to capture prompts.
- Construct strict and allowed runtime prompt policies.

Input:

- The five migrated prompt names.
- A legacy control prompt name.
- Minimal project context and secondary input per prompt.

Execution steps:

1. Run every migrated prompt through `RoadmapPromptRunner` using strict policy.
2. Assert each captured prompt lacks `ImplementationFirstPromptPolicyComposer.SectionHeading`.
3. Run the legacy control prompt through the same runner and assert the composer heading is present.
4. Compute strict and allowed identities for every migrated prompt.
5. Assert each identity mode and source-hash input set matches the roadmap.
6. Run prompt contract and existing transition regression tests.

Expected output:

- Five migrated prompts captured without legacy composer heading.
- One legacy control prompt captured with legacy composer heading.
- Five prompt-owned identity modes match expected values.
- Strict and allowed identities differ for each migrated prompt.

Expected persisted state:

- Transition snapshots created during transition tests record prompt-owned identities for migrated prompts.
- No new persisted state is introduced by the checkpoint itself.

Expected diagnostics:

- Aggregate failures name the prompt whose composer or identity behavior is wrong.
- Contract failures name changed artifact boundary.

Verification command:

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~PromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests|FullyQualifiedName~PromptContractRegistryTests|FullyQualifiedName~NonImplementationReview"
```

## 22. Certification Evidence

- Passing aggregate no-composer test for all migrated prompts.
- Passing legacy companion composer-present test.
- Passing identity matrix test for all five prompt-owned modes.
- Passing strict/allowed identity difference tests for every migrated prompt.
- Passing settings separation tests for auxiliary versus HITL policy.
- Passing prompt contract registry tests.
- Passing existing focused tests for each migrated prompt.
- Passing existing transition and state-machine regressions for active epic rewrite, spec generation, and split flows.
- Passing existing composer tests for non-roadmap and out-of-scope consumers.

## 23. Implementation Plan

| Step | Purpose | Deliverables | Dependencies | Completion Criteria |
|---|---|---|---|---|
| Add migrated prompt matrix | Centralize test fixture list for in-scope prompts | Test data list with prompt inputs and expected modes | Milestones 1-4 | All prompt names and expected modes included |
| Add aggregate runner test | Prove no migrated prompt receives composer | Captured-prompt assertions | Prompt matrix | All migrated prompts lack composer heading |
| Add legacy control assertion | Prove composer remains for out-of-scope prompt | Companion runner assertion | Existing legacy prompt | Control prompt contains composer heading |
| Add identity matrix coverage | Prove provenance completeness | Mode and source-hash assertions | Identity branches | All five prompt-owned identities pass |
| Add settings separation coverage | Preserve auxiliary/HITL semantics | Policy variant tests | Runtime policy | Prompt-owned branches ignore HITL and respond to auxiliary setting |
| Run contract and transition regressions | Prove output behavior unchanged | Test results | Existing test suites | All affected regressions pass |
| Narrow source-adjacent terminology | Remove inaccurate centralization labels | Comment/test-name updates only where needed | Passing behavior | Compiles and no public concept is renamed |

## 24. Parallel Work Opportunities

| Parallel Lane | Scope | Owner Type | Dependencies | Synchronization Point | Integration Risk |
|---|---|---|---|---|---|
| Aggregate runner tests | Prompt capture matrix | Test engineer | Milestones 1-4 compile | Before final certification | Low |
| Identity matrix tests | Prompt-owned mode and hash assertions | Runtime/test engineer | Final identity branches | Before final certification | Medium |
| Legacy preservation tests | Out-of-scope composer control | Test engineer | Existing composer branch | Before final certification | Low |
| Contract regressions | Prompt contract and transition suites | Test engineer | Compileable code | Certification | Medium |
| Terminology cleanup | Narrow comments/test names | Runtime engineer | Confirmed migrated behavior | After tests identify stable names | Low |

## 25. Risks and Mitigations

| Risk | Class | Impact | Likelihood | Earliest Detection Point | Mitigation | Fallback |
|---|---|---|---|---|---|---|
| Composer removed too broadly | Architectural | Non-roadmap prompts lose required policy | Medium | Legacy companion and existing tests | Keep legacy branch and composer intact | Restore composer call |
| Aggregate tests bypass production path | Testing | False confidence in runner behavior | Medium | Test implementation review | Use `RoadmapPromptRunner` or catalog production calls | Rewrite tests to capture runner prompts |
| Identity matrix misses a prompt | Testing | Provenance gap remains | Medium | Expected prompt list review | Use explicit five-prompt table from roadmap | Add missing case |
| Helper extraction changes behavior | Maintainability | New abstraction affects migrated or legacy prompts | Medium | Regression tests | Avoid helper unless duplication is proven mechanical | Inline prompt-specific branches |
| Settings semantics blur | Operational | HITL and auxiliary options become interchangeable | Medium | Settings separation tests | Keep selectors auxiliary-only | Remove HITL from prompt-owned branch |
| Primary contracts drift during cleanup | Integration | Runtime transitions fail after migration | Low | Contract tests | Keep cleanup source-adjacent and narrow | Revert contract-affecting changes |

## 26. Observability and Diagnostics

- Captured runner prompts provide direct evidence of composer presence or absence.
- Transition prompt-policy identities provide prompt-owned mode and source-hash provenance.
- Test diagnostics should name each prompt case in matrix failures.
- Existing transition and artifact diagnostics remain authoritative for runtime output failures.
- No new runtime metrics, health checks, audit records, or debug views are required.
- Existing composer tests remain the diagnostic surface for legacy policy behavior.

## 27. Performance and Scalability Considerations

- Baseline: aggregate tests use in-memory prompt rendering and scripted runtime; no agent process or file-heavy workflow is required.
- Likely bottleneck: none beyond normal solution test runtime.
- Scaling risk: prompt matrix can become stale if future migrations add more prompt-owned prompts.
- Measurement strategy: normal CI/test runtime.
- Deferred optimization: future separate roadmap can consolidate identity branch construction if additional migrations make switch duplication costly.

## 28. Security and Safety Considerations

- Preserve existing artifact writer/parser boundaries to avoid unauthorized writes.
- Do not remove validation or promotion safeguards.
- Preserve legacy composer for out-of-scope prompts that still rely on it for policy constraints.
- Keep prompt-policy provenance deterministic to support auditability of prompt changes.
- Do not broaden runtime permissions or file outputs.
- Do not allow test cleanup to rename public concepts used by non-roadmap consumers.

## 29. Documentation Updates

No documentation-only repository deliverable is required.

Allowed source-adjacent text changes:

- Test names and comments that would otherwise falsely describe migrated roadmap artifact-authoring prompt policy as centralized.
- Internal comments that clarify the composer remains transitional infrastructure for out-of-scope consumers.

Do not create migration reports, readiness reports, governance notes, ADRs, RFCs, or manual certification documents as implementation outputs.

## 30. Exit Criteria

Milestone 5 is complete only when:

- Aggregate strict-mode runner tests prove all five migrated prompts skip `ImplementationFirstPromptPolicyComposer`.
- A companion assertion proves an out-of-scope prompt still receives the legacy composer.
- Identity tests cover all five prompt-owned modes and required source-hash inputs.
- Strict and allowed auxiliary branches change identity for every migrated prompt.
- `AllowHitlRequestedNonImplementationFiles` remains isolated to legacy composer behavior.
- Existing artifact contracts, parser boundaries, promotion behavior, validators, and transition semantics remain intact.
- Existing non-roadmap composer tests remain passing.
- Any source-adjacent terminology cleanup is narrow and does not rename or delete public concepts used by non-roadmap consumers.
- `ImplementationFirstPromptPolicyComposer` remains available and tested.
- No future, out-of-scope migration or composer deletion is falsely claimed.

## 31. Transition to Next Milestone

There is no next milestone in this roadmap.

This milestone hands off:

- Certified in-scope roadmap completion: `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`, `GenerateMilestoneDeepDivesForEpic`, and `SplitEpic` are prompt-owned for implementation-first policy.
- Proven runtime behavior: migrated prompts skip the legacy composer, legacy prompts still receive it.
- Proven transition provenance: prompt-owned policy selection is source-hashed and branch-specific.
- Stable boundaries for future composer retirement work outside this roadmap.

Limitations remaining after roadmap completion:

- `ImplementationFirstPromptPolicyComposer` remains transitional technical debt for Plan CLI, Loop CLI, completion, decision, execution, and other out-of-scope consumers.
- Physical composer deletion requires a separate roadmap with its own consumer inventory, migration sequence, tests, and acceptance criteria.
