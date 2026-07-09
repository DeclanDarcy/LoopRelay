# Project Context 09 Eval Details Implementation Plan

## Goal

Expand the canonical Project Context source set from eight ordered files to nine ordered files by adding:

- `.agents/ctx/09-eval-details.md`

The implementation must make `09-eval-details.md` a first-class canonical Project Context artifact everywhere Project Context authority is loaded, validated, projected, hashed, persisted, diagnosed, and tested.

The change must preserve the existing authority model:

- Project Context source files are repository-owned authoritative inputs.
- Projection artifacts are derived interpretation boundaries.
- Runtime prompt contexts consume projections and runtime artifacts, not raw Project Context.
- Existing roadmap, planning, decision, completion, execution, persistence, and provenance boundaries remain intact.

## Source Of Truth

Treat `audit.md` as the factual architectural authority for the current state and for expansion implications.

Repository inspection confirms these additional planning constraints:

- `LoopRelay.Roadmap.Cli` and `LoopRelay.Projections` duplicate the ordered Project Context source list today.
- `LoopRelay.Roadmap.Cli` does not directly reference `LoopRelay.Projections`, while both projects already reference `LoopRelay.Core`.
- Projection prompt templates consume one full `{projectContext}` input rather than one parameter per source file.
- Runtime prompt builders reject raw Project Context markers after projection generation.
- Project Context source files are retained filesystem artifacts, not structured SQLite or snapshot domains.

## Non-Goals

Do not implement code in this planning pass.

Do not create:

- `roadmap.md`
- milestone specifications
- ADRs
- migration scripts
- pseudocode

Do not make `09-eval-details.md` optional in production loading. Optional loading would create two semantic projection universes and would weaken the canonical contract.

Do not make persistence author or migrate Project Context source content. The files remain repository-owned inputs.

## Target Behavior

After implementation:

- The canonical ordered Project Context files are exactly:
  - `.agents/ctx/01-purpose.md`
  - `.agents/ctx/02-capability-model.md`
  - `.agents/ctx/03-invariants.md`
  - `.agents/ctx/04-strategic-structure.md`
  - `.agents/ctx/05-authority-model.md`
  - `.agents/ctx/06-evaluation-model.md`
  - `.agents/ctx/07-drift-and-false-success.md`
  - `.agents/ctx/08-vocabulary.md`
  - `.agents/ctx/09-eval-details.md`
- Both Roadmap-local and shared projection loaders use the same ordered source contract.
- Missing `09-eval-details.md` is a Project Context contract violation.
- Any numbered `.agents/ctx/*.md` file not in the canonical source set remains a contract violation.
- The concatenated Project Context includes `09-eval-details.md` in fixed order with the same file boundary markers used for the first eight files.
- The Project Context hash changes when the ninth file is added, intentionally invalidating all stale projections generated from the old eight-file context.
- New projection manifest entries record all nine Project Context source files and the new full Project Context hash.
- Old manifests and old projections are treated through existing freshness behavior: stale, unknown, regenerated, or blocked depending on the consumer path and policy.
- Runtime prompt contexts and execution prompts continue to reject raw Project Context markers.

## Architectural Decisions

### 1. Centralize the Canonical Source Contract

Create one shared Project Context contract in a layer both Roadmap and shared projections can depend on. The recommended home is `LoopRelay.Core` because:

- it is already the base prompt-authority layer;
- both `LoopRelay.Roadmap.Cli` and `LoopRelay.Projections` reference it;
- placing the source contract there avoids making Roadmap depend on shared projection infrastructure or making shared projections depend on Roadmap.

The shared contract should own:

- Project Context directory path;
- ordered source file paths;
- canonical file names;
- numbered-source detection rules or reusable validation metadata;
- diagnostic wording helpers that do not hard-code `01 through 08`.

`RoadmapArtifactPaths.ProjectContextSourceFiles` and `ProjectionArtifactPaths.ProjectContextSourceFiles` should no longer carry independent source lists. They may expose forwarding properties if that minimizes call-site churn, but the ordered data must come from the shared contract.

### 2. Keep The Contract Strict

The new canonical contract is exactly the nine-file set. Existing repositories with only eight files must fail preflight or shared projection loading with a clear actionable diagnostic.

Do not silently synthesize an empty `09-eval-details.md`.

Do not generate placeholder source content from production services.

Do not permit an eight-file compatibility mode for projection generation.

### 3. Treat Projection Invalidation As Expected

Adding `09-eval-details.md` changes the concatenated Project Context and therefore changes the Project Context hash. This must be treated as an intentional invalidation boundary for every projection that consumes the full context.

Roadmap-local projection paths with blocking stale policies may block until projections are regenerated or repaired. Shared projection consumers may regenerate when their refresh policy allows it.

### 4. Preserve Runtime Boundaries

The ninth file belongs upstream of projection generation. It must not be injected directly into roadmap runtime prompts, completion runtime prompts, execution prompts, decision prompts, or operational context generation.

Runtime prompts should continue to receive projection content plus run-specific artifacts.

### 5. Improve For Future Source Additions Without Speculative Systems

Use this change to remove hard-coded eight-file assumptions and duplicated source lists. Do not design a generalized schema or dynamic source discovery system beyond what the architecture currently needs.

Future additions such as `10-*` should require editing one canonical contract and tests, not parallel artifact-path lists and duplicated diagnostics.

## Implementation Areas

### 1. Canonical Context Contract

Update the canonical Project Context definition.

Required work:

- Add `.agents/ctx/09-eval-details.md` as the final ordered source.
- Replace duplicated source lists in:
  - `src/LoopRelay.Roadmap.Cli/Services/Artifacts/RoadmapArtifactPaths.cs`
  - `src/LoopRelay.Projections/Models/ProjectionArtifacts/ProjectionArtifactPaths.cs`
- Move the ordered source list to the shared contract.
- Keep `.agents/ctx` as the source directory.
- Keep ordering explicit rather than filesystem-derived.
- Generalize diagnostic text from `01 through 08` to the current canonical file set.
- Ensure completeness diagnostics list all missing canonical files, including `09-eval-details.md`.
- Ensure extra numbered-file diagnostics still reject numbered files not present in the canonical list.
- Ensure non-numbered markdown files under `.agents/ctx` remain outside numbered source validation unless a separate architectural rule is intentionally added.

Reasoning:

The source list is the semantic contract for ordering, completeness, hashing, and provenance. Leaving it duplicated creates the highest consistency risk identified by the audit.

### 2. Loading Pipeline

Update both Project Context loaders:

- `src/LoopRelay.Roadmap.Cli/Services/Projections/ProjectContextLoader.cs`
- `src/LoopRelay.Projections/Services/Context/ProjectContextLoader.cs`

Required work:

- Iterate the shared canonical source list.
- Preserve fixed-order concatenation.
- Preserve `<!-- BEGIN PROJECT-CONTEXT FILE: ... -->` and `<!-- END PROJECT-CONTEXT FILE: ... -->` markers.
- Include `09-eval-details.md` in concatenated content after `08-vocabulary.md`.
- Return source-file provenance containing all nine paths.
- Compute the Project Context hash over the full rendered context, including the ninth file and boundary markers.
- Use shared validation or shared diagnostics wherever practical to avoid future divergence.
- Preserve loader-specific exception types:
  - Roadmap loader throws `RoadmapStepException`.
  - Shared projection loader throws `ProjectionException`.

Reasoning:

The loaders are where the canonical contract becomes bytes. Any ordering or diagnostic divergence here will produce inconsistent projection hashes and inconsistent operator behavior.

### 3. Projection Architecture

Update projection generation and freshness behavior without changing the projection model.

Required work:

- Ensure every projection generated through `LoopRelay.Projections` receives the nine-file concatenated Project Context.
- Ensure every Roadmap-local projection generated through `ProjectionCache` receives the nine-file concatenated Project Context.
- Ensure projection provenance factories record all nine `ProjectContextFiles`.
- Ensure manifest entries generated after the change persist all nine paths.
- Preserve existing freshness evaluation based on current provenance versus manifest provenance.
- Treat old eight-file manifest entries as stale or unknown through current freshness evaluation.
- Keep existing projection identities and projection paths unchanged.
- Do not add a new projection artifact solely because `09-eval-details.md` exists.
- Do not split projection prompts into per-source-file parameters.

Prompt-template review:

- Audit projection templates under `src/LoopRelay.Core/Prompts/Projections/` for any text that assumes the old eight-file taxonomy.
- Especially review:
  - `ProjectionForEvaluateEpicCompletionAndDrift.prompt`
  - `ProjectionForCreateRoadmapCompletionContext.prompt`
  - `ProjectionForUpdateRoadmapCompletionContext.prompt`
  - `ProjectionForAdversarialPlanReview.prompt`
  - `ProjectionForGenerateMilestoneDeepDivesForEpic.prompt`
- Update prompt text only if it conflicts with the new `09-eval-details.md` semantic role or would cause evaluation details to be excluded incorrectly.
- If any projection prompt is edited, accept the resulting generated `SourceHash` changes and test freshness behavior.

Reasoning:

All projections already consume the full Project Context. The implementation should preserve that shape and rely on deterministic hash drift to invalidate derived artifacts.

### 4. Prompt Architecture

Required work:

- Keep projection prompt rendering through generated prompt classes.
- Keep runtime prompt rendering through generated prompt classes and existing prompt catalogs.
- Verify `ProjectionPromptCatalog` and `RoadmapPromptCatalog` do not need signature changes for the ninth file.
- Preserve runtime context builders that receive projection content, not raw Project Context.
- Preserve raw-context marker guards in:
  - `RoadmapPromptContextBuilder`
  - `OperationalContextGenerator`
  - `ExecutionPromptGenerator`
- Consider adding the same raw-marker guard to completion runtime context construction only if current completion paths can plausibly receive raw Project Context content.

Reasoning:

The prompt architecture already has the correct boundary: Project Context source files are interpreted into projections, and runtime prompts consume projections plus runtime evidence. The new file should not create a parallel injection path.

### 5. Roadmap Planning Pipeline

The Roadmap CLI consumes Project Context through Roadmap-local projections across:

- roadmap completion context bootstrap;
- next-epic selection;
- epic preparation audit;
- realignment;
- reimagination;
- new epic creation;
- split epic;
- milestone deep dives;
- completion evaluation;
- roadmap completion context update.

Required work:

- Ensure `RoadmapStateMachine` preflight loads the nine-file context.
- Ensure `RoadmapResumePlanner` validates projection freshness against nine-file provenance.
- Ensure `PromptContractRegistry` remains projection-name aligned; no new prompt contract is required solely for the ninth source file.
- Ensure transition input snapshots and selection provenance continue to hash projection content rather than raw context.
- Ensure `RoadmapUnblockPlanner` hashes all nine Project Context source files when recording unblock evidence.
- Ensure `RoadmapLogicalArtifactServices` treats the ninth source file as retained filesystem content.

Reasoning:

Roadmap planning behavior changes semantically because all projections may change, but the state machine and prompt contract graph do not require a new transition.

### 6. Plan CLI Pipeline

The Plan CLI consumes Project Context through shared projections for adversarial plan review.

Required work:

- Ensure `PlanPipeline` and `ReviewStep` continue to consume the generated `AdversarialPlanReview` projection.
- Ensure shared projection generation for `AdversarialPlanReview` includes `09-eval-details.md`.
- Ensure stale eight-file `AdversarialPlanReview` projections regenerate before review when policy allows.
- Ensure publish behavior after projection generation remains unchanged.

Reasoning:

Evaluation details may affect adversarial review through false-success and evidence-quality semantics. That should arrive through the projection artifact, not through direct prompt changes unless the projection prompt excludes the new semantics.

### 7. Completion Pipeline

Completion certification consumes shared projections for:

- `EvaluateEpicCompletionAndDrift`
- `CreateRoadmapCompletionContext`
- `UpdateRoadmapCompletionContext`

Required work:

- Ensure completion certification uses the fresh nine-file projections.
- Keep `CompletionCertificationService` routing and parsers unchanged unless prompt-template review identifies an incompatible output-shape requirement.
- Ensure completion runtime contexts continue to combine projection content with active epic, plan, milestone evidence, execution evidence, handoffs, and review summaries.
- Verify that richer evaluation details do not require parser enum changes for completion status, drift classification, or closure recommendations.

Reasoning:

`09-eval-details.md` has the densest semantic overlap with completion certification. The implementation must intentionally refresh the projection frame without changing the completion authority boundary.

### 8. Decision And Execution Pipelines

Decision sessions consume shared Project Context projection content for `DecisionSession`.

Required work:

- Ensure `DecisionSession` freshness evaluation treats old eight-file projections as stale or unknown.
- Preserve the behavior that stale or missing decision projections clear persisted decision session state before opening a fresh session.
- Ensure a fresh decision process receives the nine-file-derived decision projection when `seeded == false`.
- Keep warm-process behavior unchanged: do not resend projections to an already seeded process.

Execution preparation consumes Project Context indirectly through generated artifacts.

Required work:

- Do not add direct `.agents/ctx` reads to execution prompt generation.
- Ensure execution prompt raw-marker checks continue to reject raw Project Context leakage.
- Ensure execution preparation provenance remains focused on active epic, specs, operational context, execution prompt, decision ledger, and evidence.

Reasoning:

The execution pipeline should observe the semantic effects of `09-eval-details.md` only through upstream roadmap and decision artifacts.

### 9. Persistence And Provenance

Projection manifests and snapshots already carry Project Context provenance metadata.

Required work:

- Ensure new manifest entries store all nine `ProjectContextFiles`.
- Ensure `ProjectContextHash` is the hash of the nine-file rendered context.
- Ensure `ProjectionCausalInput` for Project Context uses the new hash.
- Preserve manifest JSON shape unless a structural need is discovered during implementation.
- Do not bump SQLite schema solely for the ninth source file; `projection_manifest_entries` stores JSON documents and does not require table changes for a longer file list.
- Ensure filesystem snapshot import/export preserves projection manifest entries with nine-file metadata.
- Ensure SQLite import/export round trips projection manifest entries with nine-file metadata.
- Ensure retained filesystem logical artifact providers include `.agents/ctx/09-eval-details.md`.
- Ensure legacy markdown manifest parsing remains tolerant of older rows, but do not trust old rows as fresh provenance unless current freshness evaluation proves them fresh.

Reasoning:

This is a metadata content change, not a persistence schema change. Source files remain retained artifacts; projection metadata records their causal role.

### 10. Compatibility And Migration Behavior

Compatibility must be explicit and fail-closed.

Repositories with only eight context files:

- New loaders fail with a Project Context source contract violation.
- Diagnostics must identify `.agents/ctx/09-eval-details.md` as missing.
- The message should tell operators that the current canonical Project Context contract requires nine files.
- No projection should be generated from an eight-file context.

Repositories with existing `.agents/ctx/09-eval-details.md`:

- The file becomes valid and is loaded after `08-vocabulary.md`.
- Existing projections generated before the change become stale because the hash changes.

Repositories with `.agents/ctx/09-extra.md` or other numbered extras:

- The extra file remains invalid unless it exactly matches the canonical source path.
- If both `09-eval-details.md` and another numbered extra exist, the extra file is still rejected.

Stale projection artifacts:

- Shared projection service regenerates when policy is `RegenerateWhenStale`.
- Roadmap-local projection cache blocks when prompt contract policy is `Block`.
- Decision session resume state is cleared when the decision projection is stale or missing.

Mixed-version repositories:

- Old binaries may reject `09-eval-details.md` as an unexpected numbered file.
- New binaries require it.
- The safe migration order is: deploy new code, add `09-eval-details.md`, regenerate projections or allow shared projection services to regenerate them.

Reasoning:

Silent compatibility would allow stale or incomplete authority to drive projections. Blocking with precise diagnostics is safer than optional fallback.

### 11. Validation And Diagnostics

Required validation updates:

- Completeness validation must require all nine canonical files.
- Ordering validation must prove `09-eval-details.md` follows `08-vocabulary.md`.
- Extra numbered-file validation must use the shared source set.
- Projection freshness validation must detect old context hashes.
- Prompt runtime validation must continue to reject raw Project Context markers.
- Roadmap invariant validation must compare current nine-file Project Context hash to the preflight hash.
- Unblock diagnostics must include per-file hashes for all present canonical source files.

Diagnostic quality requirements:

- Error messages must not say `01 through 08`.
- Error messages should list the current canonical source set or at least current first and last canonical names.
- Missing and extra files should be reported together where the loaders already do so.
- Diagnostics should distinguish missing canonical files from unexpected numbered files.

Semantic validation:

- Do not add automated cross-file semantic contradiction checks in this pass unless the implementation uncovers an existing mechanism that naturally supports it.
- Instead, require prompt-template review and targeted tests to ensure `09-eval-details.md` is not excluded by projection instructions.

Reasoning:

The architecture validates source shape and derived-artifact freshness. It does not currently validate semantic consistency between Project Context prose files, and adding that system would exceed the natural scope of this expansion.

### 12. Testing

Update focused tests first, then broader affected suites.

Canonical contract tests:

- Add or update tests proving the shared contract contains exactly nine ordered paths.
- Assert the last path is `.agents/ctx/09-eval-details.md`.
- Assert Roadmap and shared projection path surfaces expose the same ordered source list.
- Assert diagnostic helper text reflects the current contract and does not mention `01 through 08`.

Loader tests:

- Update Roadmap `ProjectContextLoaderTests`.
- Add shared projection loader tests if the shared loader currently lacks equivalent coverage.
- Cover fixed order from `01-purpose.md` through `09-eval-details.md`.
- Cover missing-file reporting includes `09-eval-details.md`.
- Cover `09-eval-details.md` is no longer an extra file.
- Cover `09-extra.md` remains invalid.
- Cover non-numbered markdown is ignored by numbered extra validation.
- Cover returned `SourceFiles` has nine paths.
- Cover hash changes when `09-eval-details.md` changes.

Projection tests:

- Update projection service fixtures that seed `ProjectionArtifactPaths.ProjectContextSourceFiles`.
- Assert generated manifest entries include nine Project Context files.
- Assert changing `09-eval-details.md` produces `ProjectContextDrift`.
- Assert old eight-file manifest/projection metadata is stale or unknown as appropriate.
- Assert fresh projection reuse still avoids an agent call when all nine inputs match.
- Assert manifest upsert preserves unrelated projection entries.

Roadmap projection tests:

- Update `ProjectionCacheTests` seeded provenance.
- Update legacy manifest tests only where expectations mention source-file lists.
- Add a stale-projection test where only `09-eval-details.md` differs.
- Verify blocked stale projection evidence still reports `ProjectContextDrift`.

Roadmap lifecycle tests:

- Update `TempRepo.SeedProjectContext()` behavior through the shared list.
- Update state machine preflight tests for missing context files.
- Update resume planner tests that construct projection manifest entries.
- Update invariant validator tests for Project Context drift using the ninth source.
- Update unblock planner tests to expect nine source hashes where present.
- Update logical artifact resolver or snapshot tests that assert retained Project Context paths.

Plan CLI tests:

- Update shared projection test fixtures used by Plan CLI.
- Assert adversarial review projection generation succeeds with nine context files.
- Assert stale `AdversarialPlanReview` projection regenerates or is treated according to existing service policy after Project Context hash drift.

Completion tests:

- Update completion certification tests that use fake or seeded projection services if they depend on Project Context file count.
- Add a focused test proving `EvaluateEpicCompletionAndDrift` projection freshness changes when `09-eval-details.md` changes.
- Verify completion parser and router behavior remains stable with updated projection content.

Decision tests:

- Update decision session tests around stale or missing projection freshness.
- Add or update coverage proving stale decision projections clear resume state under the nine-file contract.

Persistence tests:

- Update filesystem snapshot projection manifest fixtures to include or tolerate nine-file Project Context lists.
- Update SQLite projection manifest round-trip tests to include nine-file metadata.
- Verify no schema version change is required by round-tripping the expanded manifest entry.
- Verify retained-source logical artifact resolution includes `.agents/ctx/09-eval-details.md`.

Prompt and runtime boundary tests:

- Keep existing raw Project Context marker rejection tests.
- Add coverage only if a completion runtime marker guard is introduced.
- If projection prompt templates are edited, update generated prompt source-hash expectations and freshness tests.

Regression tests:

- Test that no production source list still contains the old eight-file sequence independently.
- Test that no diagnostics still state `01 through 08`.
- Test that future source-list changes can be made from the shared contract without Roadmap/shared projection divergence.

Verification:

- Run targeted tests for:
  - `tests/LoopRelay.Projections.Tests/LoopRelay.Projections.Tests.csproj`
  - `tests/LoopRelay.Roadmap.Cli.Tests/LoopRelay.Roadmap.Cli.Tests.csproj`
  - `tests/LoopRelay.Plan.Cli.Tests/LoopRelay.Plan.Cli.Tests.csproj`
  - `tests/LoopRelay.Cli.Tests/LoopRelay.Cli.Tests.csproj`
  - `tests/LoopRelay.Completion.Tests/LoopRelay.Completion.Tests.csproj`
- Run the full solution test suite after focused suites pass.

### 13. Documentation

Update only architecture documentation needed to describe implemented behavior.

Required documentation updates:

- `docs/architecture.md`: add or update the Project Context and projection-boundary description if it currently omits the canonical source contract and projection invalidation behavior.
- `docs/prompt-architecture.md`: clarify that projection prompts receive the full canonical Project Context and that runtime prompts receive projections, not raw source files, if this is not already clear after implementation.
- `docs/roadmap-unblock-review.md`: update Project Context health wording if it references the old source set or omits the ninth-file preflight behavior.

Do not add process documentation, migration scripts, or ADRs for this change.

### 14. Future Extensibility

Improvements to include:

- A single canonical source contract in `LoopRelay.Core`.
- Loader diagnostics derived from the canonical contract rather than hard-coded ordinal text.
- Tests that compare Roadmap and shared projection exposed source lists against the shared contract.
- Fixture helpers that seed canonical Project Context from the shared contract.
- No prompt or persistence assumptions that `08-vocabulary.md` is permanently the last file.

Improvements to avoid in this pass:

- Dynamic discovery of arbitrary numbered files as canonical sources.
- Optional source-file compatibility modes.
- Semantic cross-file contradiction engines.
- New migration command infrastructure.
- New projection families solely for evaluation details.

Reasoning:

The simplest durable extensibility improvement is removing duplicated static contract definitions. Anything beyond that risks designing a speculative Project Context platform rather than implementing this expansion.

## Implementation Sequence

1. Introduce the shared Project Context contract with the current eight-file list and wire both Roadmap and shared projection surfaces to it.
2. Add or update contract-level tests proving Roadmap and shared projection surfaces derive from the same source.
3. Expand the shared contract to include `.agents/ctx/09-eval-details.md`.
4. Update loader diagnostics and loader tests for the nine-file contract.
5. Update retained-path and unblock evidence paths to derive from the shared contract.
6. Update projection, provenance, manifest, freshness, and persistence tests for nine-file metadata.
7. Review projection prompt templates for hard-coded old taxonomy or evaluation-details exclusion.
8. Update prompt source-hash and freshness tests only if prompt templates change.
9. Update affected Roadmap, Plan, Completion, Decision, and persistence fixtures.
10. Run focused tests, then full solution tests.
11. Update necessary architecture documentation.

Sequencing constraint:

Do not add the ninth file to one duplicated list before the source list is centralized. That would temporarily create exactly the divergence this work is meant to remove.

## Risks

Highest compatibility risk:

- Existing repositories with only eight context files will fail preflight after the new contract is deployed.
- Mitigation: fail with precise missing-file diagnostics and no partial projection generation.

Highest consistency risk:

- Roadmap and shared projections could diverge if source lists remain duplicated.
- Mitigation: centralize the list and add tests that prevent divergence.

Highest semantic risk:

- `09-eval-details.md` may duplicate or contradict `06-evaluation-model.md`, `07-drift-and-false-success.md`, `08-vocabulary.md`, or evaluation projection prompts.
- Mitigation: treat semantic coherence as source-authoring and prompt-review work, not as hidden loader behavior; update projection prompt inclusion/exclusion criteria if needed.

Broadest invalidation risk:

- All projection artifacts become stale because Project Context hash changes.
- Mitigation: treat this as expected and rely on existing regeneration/blocking policies.

Broadest test risk:

- Fixture helpers that seed all canonical context files will affect many tests.
- Mitigation: update central fixture helpers first, then adjust tests that explicitly assert old first/last or file-count behavior.

Persistence risk:

- Snapshot and SQLite tests may appear to require schema changes even though the manifest shape is unchanged.
- Mitigation: keep persistence structural schema unchanged and update metadata-content fixtures only.

## Acceptance Criteria

- `09-eval-details.md` is part of the canonical ordered Project Context source contract.
- There is one authoritative source list shared by Roadmap and shared projection paths.
- Roadmap and shared projection loaders require all nine files.
- `09-eval-details.md` appears after `08-vocabulary.md` in concatenated Project Context.
- Numbered extra files under `.agents/ctx` remain invalid unless they are canonical.
- Loader diagnostics do not mention `01 through 08`.
- New Project Context hashes include the ninth file.
- New projection manifest entries record all nine source files.
- Old eight-file projection manifests are stale or unknown under existing freshness rules.
- Roadmap preflight, resume, invariant validation, and unblock diagnostics use the nine-file contract.
- Plan CLI adversarial review projection uses the nine-file context.
- Completion certification projections use the nine-file context.
- Decision session projection freshness uses the nine-file context and clears stale resume state as before.
- Execution prompt generation does not directly read raw Project Context.
- Runtime prompt contexts still reject raw Project Context markers.
- Filesystem snapshot and SQLite persistence round trip projection manifest entries with nine-file provenance.
- Necessary architecture docs describe the nine-file Project Context and projection boundary.
- Focused and full solution tests pass.
