# Audit: Legacy `ImplementationFirstPromptPolicyComposer` Retirement

This audit captures architectural observations needed to design a later roadmap for retiring `ImplementationFirstPromptPolicyComposer` from the remaining prompt paths. It is not an implementation plan and does not define milestones.

The audit assumes the `CreateNewEpic` migration is complete and treats that migration as the reference pattern.

## Scope Notes

Observation: `CreateNewEpic` is the only roadmap runtime prompt currently marked as owning its non-implementation policy. `RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy()` returns true only for `CreateNewEpic`.

Observation: `RealignEpic`, `ReimagineEpic`, `SplitEpic`, and `GenerateMilestoneDeepDivesForEpic` still receive the legacy composer through `RoadmapPromptRunner.RunRuntimePromptAsync()`.

Observation: `AdversarialPlanReview` is included in the requested audit scope, but current source does not show it as a direct `ImplementationFirstPromptPolicyComposer` consumer. `ReviewStep.RunAsync()` sends `AdversarialPlanReview.Render(projectContextProjection, plan)` exactly, and `ReviewStepTests.RunAsync_SendsAdversarialPlanReviewRenderedWithPlan_Exactly` asserts that exact prompt. The composer is applied to adjacent Plan CLI prompts (`WritePlan` and `RevisePlan`) through `PlanSession`, not to the adversarial review prompt itself.

Interpretation: the four roadmap prompts are true remaining roadmap runtime consumers. `AdversarialPlanReview` should be treated as an adjacent prompt-policy audit item, not as a blocker for the roadmap composer retirement unless another runtime path is found to append the composer to it.

## Architecture Observations

Observation: prompt templates are source-generated from `.prompt` files in `src/LoopRelay.Core/Prompts/**`. The generator creates static classes with `Text`, `Template`, `Render(...)`, and `SourceHash`. This makes prompt text and injected section text traceable at build time without runtime file IO.

Observation: the legacy composer lives in `src/LoopRelay.Orchestration.Primitives/Services/NonImplementationReview/ImplementationFirstPromptPolicyComposer.cs`. It generates one generic policy body and appends it under `## Implementation-First Prompt Policy`.

Observation: the legacy composer uses `NonImplementationArtifactPolicyOptions.AllowHitlRequestedNonImplementationFiles` to vary HITL exception wording. It does not use `AllowAuxiliaryNonImplementationFiles`.

Observation: `RoadmapRuntimePromptPolicy` bridges settings into roadmap prompt rendering. It carries `AllowAuxiliaryNonImplementationFiles` for prompt-owned sections and carries `LegacyImplementationFirstPromptPolicy` for non-migrated prompts.

Observation: `CreateNewEpic` owns two generated sections:

- `CreateNewEpicImplementationFirstGuidance.prompt`
- `CreateNewEpicAuxiliaryArtifactLimits.prompt`

Observation: `CreateNewEpicPromptSections.ForAuxiliaryArtifactPolicy(false)` injects both sections and records their source hashes. `ForAuxiliaryArtifactPolicy(true)` returns empty sections and no active section hashes.

Observation: `CreateNewEpic.prompt` owns explicit placeholders for those sections, and `RoadmapPromptCatalog.RenderCreateNewEpic()` passes selected section text into the generated `CreateNewEpic.Render(...)` method.

Interpretation: the current reference pattern is prompt-owned text in `.prompt` files, C# selection logic in the owning runtime project, and prompt-policy identity in transition snapshots. The section body should not be hard-coded in C#.

Observation: transition provenance already accounts for prompt policy. `RoadmapPromptTransitionRunner` resolves a `TransitionInputSnapshot` before running a prompt and passes `effectivePolicy.CreateIdentity(prompt)`.

Observation: for `CreateNewEpic`, the prompt-policy identity mode is `create-new-epic-prompt-owned-v1` and includes:

- `allowAuxiliaryNonImplementationFiles`
- `createNewEpicSourceHash`
- `sectionMode`
- active section source hashes when strict sections are injected

Observation: for legacy prompts, the prompt-policy identity mode is `legacy-implementation-first-composer-v1` and includes only a hash of the composed legacy policy text.

Interpretation: every migrated prompt needs equivalent identity coverage. A prompt can stop using the composer only when the active prompt-owned branch, prompt template hash, policy branch value, and section source hashes are represented in transition snapshots.

Observation: `ImplementationFirstThinking.prompt` and `InvalidContent.prompt` exist under `Prompts/NonImplementation`, but current source does not reference them. They are generated assets without runtime consumers.

Interpretation: those generic sections are available raw material, not an established reuse pattern.

## CreateNewEpic Baseline

Observation: `CreateNewEpic` writes the sanctioned primary artifact `.agents/epic.md` or emits a blocked response. It performs repository grounding and strategic epic authoring, but explicitly prohibits implementation tasks, execution prompts, roadmap modification, projection modification, and side-channel artifacts.

Observation: the successful migration avoided injecting `InvalidContent.prompt` because that section broadly forbids planning, analysis, audits, and documentation in ways that would conflict with the valid strategic content required inside `.agents/epic.md`.

Observation: the `CreateNewEpic` sections frame `.agents/epic.md` as an implementation-bearing operational artifact, not as an exception to implementation-first thinking.

Interpretation: the baseline does not preserve behavior by copying the legacy composer. It preserves behavior by naming which non-code artifact is sanctioned, why it exists in the implementation loop, and which auxiliary artifacts remain invalid.

Interpretation: future migrations should preserve the prompt's reasoning model, not simply insert common text.

## Prompt-by-Prompt Audit

### RealignEpic

Observation: `RealignEpic.prompt` updates `.agents/epic.md` after an `EpicPreparationAudit` disposition of `Realign`. It must apply the smallest strategic patch necessary while preserving epic identity, strategic purpose, intended capability, and existing milestone roadmap content where present.

Observation: `ActiveEpicRewriteTransition.ExecuteAsync()` supplies a prompt context containing projection content, current epic content, audit output, and repository inspection instructions. The secondary input is the audit content. Transition inputs include the active epic or selection and the audit evidence path.

Observation: runtime output is promoted through `ActiveEpicPromotionCoordinator`, using `EpicAuthoringOutputClassifier+EpicArtifactValidator`. Blocking output starts with `# Epic Realignment Blocked`.

Observation: the current composer reinforces implementation-first behavior by discouraging autonomous documentation, documentation-centric milestones, governance artifacts, and theory-protection artifacts. In `RealignEpic`, that policy currently acts as a late generic guard after a prompt whose primary output is itself a strategic Markdown artifact.

Interpretation: `RealignEpic` should not reuse `CreateNewEpic` sections unchanged. It shares the `.agents/epic.md` sanctioned-artifact concept, but its reasoning model is audit-driven minimal patching, not new epic synthesis.

Interpretation: a `RealignEpic` section should explicitly preserve valid realignment behaviors that the generic composer might over-constrain:

- updating the active epic artifact is sanctioned
- preserving or audit-adapting milestone roadmap content is sanctioned
- audit findings may be compressed into scope, constraints, dependencies, acceptance criteria, risks, and follow-up
- repository re-audit and side-channel audit reports remain invalid
- unaffected epic content should not be rewritten for style

Interpretation: partial reuse is semantically appropriate only at the concept level: "the active epic is the implementation-bearing artifact; auxiliary explanatory artifacts are invalid." The actual text should be prompt-specific.

### ReimagineEpic

Observation: `ReimagineEpic.prompt` replaces `.agents/epic.md` after an `EpicPreparationAudit` disposition of `Reimagine`. It preserves the strategic need and desired capability while replacing obsolete or misfit epic framing.

Observation: `ReimagineEpic` shares the same transition and promotion path as `RealignEpic`, but its allowed transformation is broader. It must include a coherent replacement milestone roadmap and may replace title, scope, decomposition, dependencies, assumptions, acceptance criteria, success conditions, risks, non-goals, and capability boundary when audit-supported.

Observation: the prompt already prohibits new repository audits, implementation tasks, milestone deep dives, execution prompts, roadmap changes, projection changes, speculative architecture, and raw audit detail.

Interpretation: `ReimagineEpic` needs prompt-owned implementation-first guidance because the generic composer cannot distinguish valid strategic replacement from invalid design-document production.

Interpretation: a `ReimagineEpic` section should authorize a complete replacement epic as the sanctioned operational artifact while requiring every material redesign decision to trace to the audit. The section should prevent the model from producing companion design reports, research notes, rationale appendices, or architecture proposals outside `.agents/epic.md`.

Interpretation: `CreateNewEpic` text is closer to `ReimagineEpic` than to `RealignEpic` because both author a full epic shape, but unchanged reuse would be wrong. `ReimagineEpic` is constrained by the preserved strategic need and audit disposition, not by a new proposal.

### SplitEpic

Observation: `SplitEpic.prompt` decomposes a split proposal into multiple repository-grounded epic specifications. Its required output is a multi-file Markdown bundle with a split plan plus `# FILE: .agents/epic-N.md` sections.

Observation: `SplitEpicTransition` extracts the bundle, validates it through `SplitEpicBundleInterpreter`, persists split family lineage, captures HITL artifact evidence for child epics, and promotes the selected child epic into `.agents/epic.md`.

Observation: `PromptContractRegistry` declares `SplitEpic` as writing both `.agents/splits` and `.agents/epic.md`, with `SplitEpicBundleInterpreter+SplitFamilyStore+ArtifactPromotionService` as the artifact writer and `BundleFileExtractor+SplitEpicBundleInterpreter+EpicArtifactValidator` as the parser boundary.

Observation: `SplitEpic.prompt` says each resulting epic should be authored using the same standard as `CreateNewEpic`, but the prompt also owns additional semantics: capability partitioning, coverage proof, non-overlap, sibling dependencies, selected child promotion, and split lineage.

Interpretation: `SplitEpic` has the highest risk of accidental over-broad reuse. A single-artifact `CreateNewEpic` auxiliary limit does not fit a sanctioned multi-file bundle.

Interpretation: prompt-owned sections should make the split bundle itself the sanctioned output boundary. The split plan and child epic files are valid because they are machine-consumed operational artifacts in the split transition. Additional companion reports, inventories, RFCs, governance notes, or research documents remain invalid unless explicitly requested or required by a machine-consumed contract.

Interpretation: the implementation-first model for `SplitEpic` should emphasize capability partitioning over documentation quality. The model should reject split pieces that are merely planning phases, file areas, implementation layers, or work breakdowns.

Interpretation: `CreateNewEpic` guidance can inform the per-child epic standard, but `SplitEpic` needs its own bundle-specific artifact limits and partition-specific reasoning guidance.

### GenerateMilestoneDeepDivesForEpic

Observation: `GenerateMilestoneDeepDivesForEpic.prompt` transforms the active epic's milestone roadmap into one `.agents/specs/*.md` implementation-planning specification per milestone.

Observation: `GenerateMilestoneDeepDivesTransition` extracts the output bundle, writes files, writes a bundle manifest, marks specs ready, captures HITL artifact evidence, records execution preparation provenance, and validates roadmap invariants before completing the transition.

Observation: `TransitionInputResolver` records `.agents/epic.md` as the required artifact input for this prompt. The prompt has no secondary input.

Observation: this prompt's primary output is explicitly non-code planning material, but it is also the sanctioned input for later execution-prompt generation. The generic composer's ban on "implementation planning", "milestone planning", and "implementation-supporting artifacts" creates the strongest semantic tension here.

Interpretation: this prompt should receive new, prompt-specific sections rather than reuse `InvalidContent.prompt` or `CreateNewEpic` text unchanged.

Interpretation: prompt-owned guidance should state that `.agents/specs/*.md` is the sanctioned implementation-bearing planning artifact for milestone expansion. The specs are valid when they preserve epic architecture, dependencies, constraints, validation strategy, and completion evidence for later implementation. They are invalid when they become standalone documentation, design essays, code-edit plans, execution prompts, or extra governance artifacts.

Interpretation: this migration should be careful with `AllowAuxiliaryNonImplementationFiles`. Disabling auxiliary files must not suppress the primary specs; it should only constrain extra side-channel artifacts. Enabling auxiliary files should not weaken the requirement that the prompt produce the contracted spec bundle.

### AdversarialPlanReview

Observation: `AdversarialPlanReview.prompt` reviews `.agents/plan.md` against a generated Project Context projection and emits findings, missing decisions, false closure tests, authority drift, projection blind spots, silent drift risks, suggested patch edits, a final adversarial question, and a verdict.

Observation: `ReviewStep` runs the review in a read-only planning session and writes no artifact. The output is returned and then supplied to `PlanSession.ReviseAsync()`. The Plan CLI does not publish after the review step because the review writes nothing.

Observation: the current implementation does not append `ImplementationFirstPromptPolicyComposer` to the review prompt. The adjacent `WritePlan` and `RevisePlan` prompts do receive the composer.

Interpretation: there is no direct composer-retirement work for `AdversarialPlanReview` in current source. If roadmap design still wants a prompt-owned non-implementation section here, the reason would be behavioral tightening, not removal of a direct legacy dependency.

Interpretation: a review-specific section, if added, should be lightweight and should not distract from adversarial review. It would tell the reviewer not to require autonomous non-implementation deliverables as plan corrections unless those deliverables are sanctioned, machine-consumed, or explicitly HITL-requested. The revise prompt still remains the stronger enforcement point.

Interpretation: before any roadmap counts `AdversarialPlanReview` as migrated, confirm whether the intended scope includes Plan CLI prompt policy generally (`WritePlan`/`RevisePlan`) or only the review prompt.

## Section Reuse Observations

Observation: the existing generic sections are not wired into runtime rendering. Reusing them would create a new behavior, not preserve an established behavior.

Observation: `InvalidContent.prompt` is roadmap-oriented but too broad for prompts whose primary output is a sanctioned Markdown artifact. It forbids planning, milestone planning, analysis, audits, and documentation in terms that can contradict `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`, `SplitEpic`, and `GenerateMilestoneDeepDivesForEpic`.

Observation: `ImplementationFirstThinking.prompt` is conceptually aligned with the composer but too small to encode prompt-specific sanctioned artifact boundaries.

Interpretation: section reuse should be semantic, not textual. The reusable idea is:

- sanctioned roadmap artifacts are valid when they are consumed by the implementation loop
- auxiliary artifacts are invalid when their primary result is human understanding, governance, or coordination
- implementation-first reasoning means "what must become true of the software", even when the current prompt writes a planning artifact

Interpretation: unchanged reuse is not appropriate for the audited roadmap prompts. Prompt-specific adaptations are safer for all four true remaining roadmap consumers.

## Emerging Organization Pattern

Observation: `CreateNewEpic` placed prompt-owned section files under `src/LoopRelay.Core/Prompts/NonImplementation` and placed selection logic under `src/LoopRelay.Roadmap.Cli/Services/Prompts`.

Observation: the generated prompt namespace follows folder structure. More files under `Prompts/NonImplementation` will remain discoverable if names retain the prompt prefix, such as `RealignEpicImplementationFirstGuidance`.

Observation: selection code currently lives in a prompt-specific class, `CreateNewEpicPromptSections`, rather than a generic section registry.

Interpretation: the natural next pattern is still prompt-specific selectors and prompt-prefixed generated section files. A generic registry or abstraction is not yet proven necessary.

Interpretation: once a second or third prompt is migrated, duplication in `RoadmapRuntimePromptPolicy.CreateIdentity()` and `RoadmapPromptCatalog.RenderRuntime()` may become the right evidence for a small shared helper. The current audit does not justify designing that abstraction up front.

Observation: `NonImplementationReviewOwnership.RoadmapPlanningPromptPolicy` still identifies centralized composer ownership.

Interpretation: ownership terminology will need to evolve when prompt-owned policy becomes the norm. That change should happen when the architecture has actually crossed that boundary, not before.

## Transition and Provenance Observations

Observation: prompt rendering and transition input snapshotting are separate. A prompt can render correctly while transition provenance still fails to represent the selected policy branch.

Observation: `CreateNewEpic` transition snapshots change when `AllowAuxiliaryNonImplementationFiles` changes, because policy identity includes branch value and section hashes.

Interpretation: each migrated prompt needs tests proving:

- rendered strict mode includes prompt-owned section markers
- rendered allowed mode omits strict auxiliary-artifact sections where that is intended
- runtime execution no longer appends `ImplementationFirstPromptPolicyComposer.SectionHeading`
- transition snapshot hash changes when the policy branch changes
- active section source hashes are included in `TransitionPromptPolicyIdentity`

Observation: projection freshness tracks projection prompt source hashes, but runtime prompt-owned section hashes are tracked through transition input identity, not projection manifests.

Interpretation: roadmap design should keep runtime section provenance in transition snapshots rather than trying to overload projection provenance.

## Settings Flow Observations

Observation: `CliSettingsLoader` exposes both `allowHitlRequestedNonImplementationFiles` and `allowAuxiliaryNonImplementationFiles`.

Observation: legacy composer behavior changes only with `allowHitlRequestedNonImplementationFiles`.

Observation: `CreateNewEpic` prompt-owned section selection changes only with `allowAuxiliaryNonImplementationFiles`.

Interpretation: future prompt-owned migrations should preserve this separation unless the product semantics intentionally change. HITL-requested deliverables and auxiliary non-implementation side artifacts are different concerns.

Interpretation: for sanctioned primary outputs (`.agents/epic.md`, split child epics, `.agents/specs/*.md`), `allowAuxiliaryNonImplementationFiles` should not determine whether the prompt may produce its contracted output. It should determine only whether additional non-implementation side artifacts are tightly prohibited by injected guidance.

## Testing Observations

Observation: the existing `CreateNewEpicPromptPolicyTests` provide a strong baseline:

- selector test for strict vs allowed branch
- render test for section markers and placeholder elimination
- runtime runner test proving composer is skipped only for `CreateNewEpic`
- hard-coded-body test proving section text remains in `.prompt` files

Observation: `TransitionInputResolverTests` already cover policy identity for `CreateNewEpic` and legacy prompt policy hashes.

Observation: `PromptContractRegistryTests` assert artifact writer and parser boundaries for epic authoring and split prompts.

Interpretation: future migrations should add equivalent focused tests rather than broad snapshots of entire prompts. Stable markers, source hashes, and behavior boundaries are more maintainable than full rendered prompt snapshots.

Interpretation: additional tests should cover prompt-specific risks:

- `RealignEpic`: existing milestone roadmap content remains allowed and no legacy composer section is appended.
- `ReimagineEpic`: replacement milestone roadmap remains allowed and no side design report is implied.
- `SplitEpic`: bundle output remains sanctioned despite strict auxiliary-artifact policy; split plan and child files are not treated as auxiliary artifacts.
- `GenerateMilestoneDeepDivesForEpic`: `.agents/specs/*.md` remains sanctioned despite strict auxiliary-artifact policy; extra reports remain prohibited.
- `AdversarialPlanReview`: current direct render behavior stays exact unless intentionally changed.

## Migration Order Observations

These are sequencing observations only, not a roadmap.

Observation: `RealignEpic` and `ReimagineEpic` share the closest mechanical path with `CreateNewEpic`: generated planning prompt, active epic promotion, epic validator, blocked-output classifier, and transition policy identity.

Interpretation: those prompts are useful early validation points for generalizing the `CreateNewEpic` pattern because they exercise the same `.agents/epic.md` sanctioned artifact while adding audit-driven semantics.

Observation: `RealignEpic` has a narrower behavior surface than `ReimagineEpic`; it should preserve identity and apply minimal audit-grounded changes. `ReimagineEpic` intentionally replaces the epic framing.

Interpretation: if sequencing matters, `RealignEpic` is the lower-risk active-epic rewrite case, while `ReimagineEpic` validates the broader replacement case.

Observation: `GenerateMilestoneDeepDivesForEpic` has a strong semantic conflict with the legacy composer because its primary output is implementation-planning specs.

Interpretation: migrating it has high architectural value because it clarifies the distinction between sanctioned implementation-planning artifacts and invalid auxiliary planning documents. The risk is accidental wording that weakens the spec contract.

Observation: `SplitEpic` has the most complex post-processing path among the audited roadmap prompts: bundle extraction, split interpretation, family persistence, HITL capture, and selected child promotion.

Interpretation: `SplitEpic` migration should be informed by the active-epic authoring migrations but should not be treated as a copy of them. Its section design must account for multi-file bundle semantics.

Observation: `AdversarialPlanReview` is not a direct composer consumer today.

Interpretation: it should not be counted in the same migration sequence without first clarifying the intended Plan CLI scope.

## Composer Retirement Readiness

Observation: deleting `ImplementationFirstPromptPolicyComposer` is not possible after migrating only the audited roadmap prompts. Current direct consumers also include:

- `RoadmapPromptRunner` legacy branch for non-migrated roadmap runtime prompts
- `RoadmapRuntimePromptPolicy.FromArtifactPolicy()` and legacy policy identity
- `PlanSession` for `WritePlan` and `RevisePlan`
- `PlanCliComposition`
- `LoopCliComposition`
- `ExecutionStep`
- `DecisionSession`
- `AgentCompletionPromptRunner`
- existing tests that assert composer behavior

Observation: the requested roadmap prompt retirement can reduce roadmap dependence while leaving composer as transitional infrastructure for Loop CLI, Plan CLI authoring, and Completion.

Interpretation: physical deletion requires a broader program than this audit scope. A safer completion condition for this scope is "no audited roadmap prompt receives the legacy composer"; a separate condition is required for "composer can be deleted from the codebase."

Retirement readiness conditions:

- every runtime path that still appends the composer has either migrated to prompt-owned generated sections or has been explicitly removed from the composer retirement scope
- `RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy()` no longer relies on a one-off `CreateNewEpic` branch for migrated roadmap prompts
- each migrated prompt has prompt-policy identity with prompt source hash, policy branch inputs, and active section source hashes
- tests prove migrated prompts do not contain `ImplementationFirstPromptPolicyComposer.SectionHeading`
- tests prove non-migrated prompts continue receiving the composer until intentionally migrated
- post-execution non-implementation review remains active and is not treated as replaced by prompt guidance
- settings semantics for HITL-requested deliverables and auxiliary artifacts remain explicit
- ownership constants and documentation no longer describe roadmap prompt policy as centralized once centralization is no longer true

## Risks

High-impact risks:

- Behavioral drift from preserving familiar wording instead of preserving implementation-first reasoning.
- Reusing generic invalid-content text where it contradicts sanctioned roadmap artifacts.
- Treating `allowAuxiliaryNonImplementationFiles` as permission to weaken primary output contracts.
- Omitting section source hashes from transition snapshots, causing prompt-policy changes to be invisible in provenance.
- Migrating `AdversarialPlanReview` based on an incorrect assumption that it is a direct composer consumer.

Medium-impact risks:

- Prompt-specific sections proliferate without naming discipline.
- Common concepts diverge across prompts because wording is copied manually and then edited independently.
- The legacy branch in `RoadmapRuntimePromptPolicy.CreateIdentity()` grows into a brittle switch as more prompts migrate.
- A migrated prompt accidentally receives both prompt-owned sections and the legacy composer.
- Tests assert full prompt text too rigidly and make normal prompt evolution costly.

Lower-impact but persistent risks:

- Section folder discoverability degrades as `Prompts/NonImplementation` grows.
- The term "non-implementation" remains overloaded between invalid auxiliary files, sanctioned roadmap artifacts, HITL-requested deliverables, and post-execution review.
- Existing non-roadmap composer consumers make it easy to confuse "roadmap prompt migration complete" with "composer deletable."

## Architectural Opportunities

Observation: the migration reveals a useful concept: sanctioned operational artifacts are not exceptions to implementation-first behavior when they are consumed by later implementation stages.

Observation: prompt-owned sections can make that concept explicit per prompt, instead of relying on one generic policy appended after the main prompt.

Observation: transition prompt-policy identity is already capable of tracking prompt-owned section choices.

Opportunities:

- clearer ownership of non-implementation guidance by the prompt that needs it
- stronger provenance for runtime prompt behavior
- cleaner distinction between primary contracted outputs and auxiliary side artifacts
- better prompt contracts around artifact writer and parser boundaries
- reduced semantic tension between roadmap authoring prompts and a generic anti-documentation policy
- future consolidation only after repeated prompt-owned patterns prove stable

## Roadmap Guidance Observations

These observations can inform a later roadmap without becoming one.

Logical migration boundaries:

- active epic authoring and rewriting: `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`
- split authoring and lineage: `SplitEpic`
- milestone spec generation: `GenerateMilestoneDeepDivesForEpic`
- plan review and revision: `AdversarialPlanReview` plus adjacent Plan CLI prompts, if included later

Validation checkpoints:

- after each migrated prompt, rendered output should have prompt-owned section markers and no legacy composer heading
- transition snapshots should identify the prompt-owned policy branch and active section hashes
- contracted primary artifacts should remain allowed in strict auxiliary-artifact mode
- blocked outputs should remain valid and should not authorize extra explanatory artifacts
- post-processing paths should remain unchanged unless prompt semantics require it

Natural completion criteria for this audit scope:

- `RealignEpic`, `ReimagineEpic`, `SplitEpic`, and `GenerateMilestoneDeepDivesForEpic` own their non-implementation guidance through generated prompt sections
- `RoadmapPromptRunner` no longer appends the legacy composer for those prompts
- transition provenance differentiates prompt-owned policy modes for those prompts
- tests prove behavior for strict and allowed auxiliary-artifact policy branches
- `AdversarialPlanReview` scope is explicitly resolved based on current direct-consumer evidence

## Summary Interpretation

The safest retirement strategy is evolutionary and prompt-specific. `CreateNewEpic` established the right architectural baseline: generated section text owned by the prompt, small runtime selection logic, no hard-coded section body in C#, and transition provenance that records policy branch and section hashes.

The remaining prompts should not receive identical section text. `RealignEpic` and `ReimagineEpic` need audit-driven active-epic guidance. `SplitEpic` needs multi-file bundle and capability-partition guidance. `GenerateMilestoneDeepDivesForEpic` needs sanctioned spec-generation guidance. `AdversarialPlanReview` is currently not a direct composer consumer and should be clarified before being included in composer-retirement sequencing.

The legacy composer should remain transitional debt until every direct consumer, including non-roadmap consumers, has either migrated or been deliberately left out of the deletion scope.
