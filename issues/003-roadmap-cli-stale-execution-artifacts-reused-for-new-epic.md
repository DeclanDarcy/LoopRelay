# Roadmap CLI stale execution artifacts reused for new epic

## Audit status

Confirmed. The implementation has some structural validation around active epics and spec declarations, but execution preparation freshness is still inferred from file presence and permissive lifecycle state. A newly promoted `.agents/epic.md` can therefore coexist with specs, operational context, execution prompt, `.agents/plan.md`, and `.agents/milestones/*.md` generated for an older active epic.

## Finding

Promoting a new active epic overwrites `.agents/epic.md` and marks that path `Ready`, but it does not invalidate downstream execution-preparation artifacts. `EnsureExecutionReadinessAsync` regenerates `.agents/operational_context.md` and `.agents/execution-prompt.md` only when those files are missing or empty. `RoadmapResumePlanner` similarly treats operational context and execution prompt as usable when present and when lifecycle state is `Ready`, `Executing`, or `Completed`.

The result is a causal-input mismatch: current active epic content can change while derived execution artifacts remain marked ready.

## Impact

The execution bridge can run a prompt assembled for a previous epic against a newly promoted active epic. Depending on which stale files are present, this can:

- execute the wrong acceptance criteria,
- materialize `.agents/plan.md` and `.agents/milestones/*.md` from obsolete specs,
- certify completion against milestone specs that do not belong to the current epic content,
- resume `ExecutionPromptReady` or `ExecutionLoop` from stale artifacts after a process restart,
- make artifact lifecycle and state output report execution readiness even though derived inputs have drifted.

The risk is highest after a repository has already prepared or run one epic, then the CLI selects, creates, realigns, reimagines, or promotes a different active epic without cleaning or proving freshness of execution-preparation outputs.

## Verified mechanics

- `ArtifactPromotionService.PromoteAsync` writes the candidate content directly to the requested target path and upserts only that target lifecycle entry. For active epics, `PromoteActiveEpicAsync` passes `.agents/epic.md` as the target and `Ready` as the promoted lifecycle state. There is no downstream invalidation hook in either method.
- `GenerateMilestoneSpecsAsync` writes extracted bundle files and `.agents/specs/bundle-manifest.md`, but does not delete or supersede specs from a previous generation before writing the new bundle.
- `BundleFileExtractor.WriteExtractedFilesAsync` writes only the paths present in the new bundle. If the new bundle omits a previously generated spec path, the old spec remains on disk and can still be listed as an active milestone spec.
- `RoadmapArtifactPaths.IsMilestoneSpecPath` treats any `.agents/specs/*.md` file except `bundle-manifest.md` as a milestone spec. There is no generation identity or active-epic hash filter.
- `EnsureExecutionReadinessAsync` checks `GetStatusAsync(.agents/operational_context.md) != Present` and `GetStatusAsync(.agents/execution-prompt.md) != Present`. When files already exist, it records "Artifact Ready" and skips regeneration.
- `RoadmapResumePlanner.ValidateExecutionPreparationAsync` requires active epic validity and, depending on state, spec, operational context, and execution prompt presence. It does not compare current active epic or spec hashes to the artifacts being resumed.
- `InvariantValidator` checks project-context hash, projection freshness, duplicate active lifecycle entries, active epic structure, and whether spec files explicitly declare another epic path. For execution states it checks only that active epic, operational context, execution prompt, and at least one milestone spec exist.
- The existing spec guard is useful but insufficient: specs generated for the previous contents of `.agents/epic.md` commonly declare `Epic Path | .agents/epic.md`, which still matches after the file has been overwritten.
- `OperationalContextGenerator` and `ExecutionPromptGenerator` include current active epic/spec content when they run, but neither writes a manifest proving what inputs were used.
- `ExecutionCompatibilityMaterializer` regenerates `.agents/plan.md` and numbered milestone files from whatever specs, operational context, and execution prompt are currently present. It overwrites generated paths it touches, but it does not clear stale milestone files beyond the current spec count.
- `TransitionInputResolver` and `TransitionJournalRecord` already capture input artifact hashes for prompt-driven transitions, including `GenerateMilestoneDeepDivesForEpic` and completion evaluation. That infrastructure can be reused, but it is currently audit evidence rather than an execution-readiness gate.

## Code evidence

| Area | Evidence |
|---|---|
| Active epic promotion | `src/CommandCenter.Roadmap.CLI/ArtifactPromotion.cs:94` writes the promoted candidate to the target path; `src/CommandCenter.Roadmap.CLI/ArtifactPromotion.cs:97` updates only that target lifecycle entry; `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:1039` and `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:1047` promote `.agents/epic.md`. |
| Spec generation | `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:550` starts milestone generation; `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:564` writes extracted specs; `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:565` writes the bundle manifest; no adjacent cleanup removes old specs. |
| Bundle extraction | `src/CommandCenter.Roadmap.CLI/BundleFileExtractor.cs:40` through `src/CommandCenter.Roadmap.CLI/BundleFileExtractor.cs:49` writes only extracted files from the current bundle. |
| Active spec set | `src/CommandCenter.Roadmap.CLI/RoadmapArtifactPaths.cs:55` through `src/CommandCenter.Roadmap.CLI/RoadmapArtifactPaths.cs:58` classify every `.agents/specs/*.md` except `bundle-manifest.md` as a milestone spec. |
| Operational context readiness | `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:582` skips generation when `.agents/operational_context.md` is present; `src/CommandCenter.Roadmap.CLI/OperationalContextGenerator.cs:8`, `src/CommandCenter.Roadmap.CLI/OperationalContextGenerator.cs:32`, and `src/CommandCenter.Roadmap.CLI/OperationalContextGenerator.cs:58` show the generator derives content from active epic and ordered specs when it actually runs. |
| Execution prompt readiness | `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:634` skips generation when `.agents/execution-prompt.md` is present; `src/CommandCenter.Roadmap.CLI/ExecutionPromptGenerator.cs:8`, `src/CommandCenter.Roadmap.CLI/ExecutionPromptGenerator.cs:9`, and `src/CommandCenter.Roadmap.CLI/ExecutionPromptGenerator.cs:65` show the generator derives content from operational context and active epic when it actually runs. |
| Resume checks | `src/CommandCenter.Roadmap.CLI/RoadmapResumePlanner.cs:327` validates execution preparation; `src/CommandCenter.Roadmap.CLI/RoadmapResumePlanner.cs:358` and `src/CommandCenter.Roadmap.CLI/RoadmapResumePlanner.cs:363` check usable operational context and execution prompt by presence/lifecycle. |
| Invariants | `src/CommandCenter.Roadmap.CLI/InvariantValidator.cs:86` through `src/CommandCenter.Roadmap.CLI/InvariantValidator.cs:93` check execution prerequisites by existence; `src/CommandCenter.Roadmap.CLI/InvariantValidator.cs:166` through `src/CommandCenter.Roadmap.CLI/InvariantValidator.cs:174` only reject specs that explicitly declare another epic path. |
| Compatibility materialization | `src/CommandCenter.Roadmap.CLI/ExecutionCompatibilityMaterializer.cs:42` iterates current specs; `src/CommandCenter.Roadmap.CLI/ExecutionCompatibilityMaterializer.cs:51` through `src/CommandCenter.Roadmap.CLI/ExecutionCompatibilityMaterializer.cs:74` write numbered milestones and `.agents/plan.md` without clearing old numbered milestone files. |
| Existing hash evidence | `src/CommandCenter.Roadmap.CLI/TransitionInputs.cs:5` through `src/CommandCenter.Roadmap.CLI/TransitionInputs.cs:35` construct input snapshots; `src/CommandCenter.Roadmap.CLI/TransitionJournal.cs:3` through `src/CommandCenter.Roadmap.CLI/TransitionJournal.cs:18` persist input hashes and optional snapshots in journal records. |

## Existing partial protections

- The active epic must be structurally valid before execution-preparation states proceed.
- Specs that explicitly declare a different epic path are rejected by both `InvariantValidator` and `RoadmapResumePlanner`.
- Projection artifacts have provenance and stale checks against project context and prompt source hashes.
- Missing or empty operational context, execution prompt, or specs are blocked or regenerated.

These protections do not establish that the execution-preparation artifacts were derived from the current active epic content and current ordered spec set.

## Solution options

### Option 1: Tactical invalidation on active epic promotion

On every successful active epic promotion, delete or mark `Superseded` for:

- `.agents/specs/*.md`,
- `.agents/specs/bundle-manifest.md`,
- `.agents/operational_context.md`,
- `.agents/execution-prompt.md`,
- `.agents/plan.md`,
- `.agents/milestones/*.md`.

Pros:

- Smallest implementation.
- Forces regeneration and prevents the most obvious stale reuse path.
- Fits the current file-presence based readiness model.

Cons:

- Does not prove freshness if files are manually restored or copied back.
- Does not protect against partial generation leaving mixed old/new spec files unless the spec directory cleanup is complete.
- Adds imperative cleanup rules that must be remembered by every future promotion path.

This is acceptable as a short-term mitigation, but weak as the long-term boundary.

### Option 2: Execution-preparation manifest

Introduce a manifest such as `.agents/execution-preparation/manifest.md` or `.agents/execution-preparation/manifest.json` that records:

- active epic path and SHA-256,
- ordered milestone spec paths and SHA-256 values,
- specs bundle manifest hash,
- operational context hash,
- execution prompt hash,
- generated compatibility artifact hashes for `.agents/plan.md` and `.agents/milestones/*.md`,
- source transition correlation IDs or input snapshot hashes,
- generation timestamps.

`OperationalContextGenerator`, `ExecutionPromptGenerator`, and `ExecutionCompatibilityMaterializer` would update the manifest. `EnsureExecutionReadinessAsync`, `RoadmapResumePlanner`, and `InvariantValidator` would treat artifacts as ready only when the manifest matches the current active epic and ordered spec hash set.

Pros:

- Directly models derived artifact freshness.
- Preserves resume behavior when inputs are unchanged.
- Makes stale files diagnosable instead of silently reused.
- Can reuse `RoadmapHash`, `TransitionInputSnapshot`, and existing manifest-store patterns.

Cons:

- Requires a new persistence format and parser.
- Needs migration behavior for repositories with existing artifacts but no manifest.
- Requires careful update ordering so failed generation does not certify partially written artifacts.

This is the most focused durable fix.

### Option 3: General derived-artifact provenance service

Create a shared derived artifact provenance layer for any artifact produced from other artifacts. Execution preparation would be the first consumer, but selection, completion updates, projections, and future planning artifacts could also use it.

Pros:

- Avoids one-off freshness systems.
- Aligns with existing projection provenance concepts.
- Creates a reusable contract for "artifact X is fresh with respect to inputs Y".

Cons:

- Larger design surface than this issue requires.
- Higher risk of refactoring churn across roadmap CLI internals.
- Could delay fixing the execution safety gap.

This is attractive if multiple stale-artifact issues are being solved together, but it is probably too broad as the first fix.

### Option 4: Journal-derived freshness check

Use `TransitionJournalRecord.InputSnapshot` to find the most recent generation transition for milestone specs, then compare its recorded active epic hash to the current `.agents/epic.md` hash. Do similar checks for completion evaluation and execution transitions.

Pros:

- Reuses existing hash evidence.
- Adds less new storage than a manifest.
- Useful for diagnostics and migration.

Cons:

- The journal currently records prompt-driven transitions, while operational context, execution prompt, plan, and milestones are generated locally without equivalent input snapshots.
- JSONL history is less direct than a current-state manifest for readiness checks.
- Harder to reason about after manual artifact edits, journal truncation, or partial failed runs.

This is useful as supporting evidence, but not sufficient by itself unless local generators also start journaling their input snapshots.

## Recommended path

Implement Option 2, with a small part of Option 1 as a compatibility safety net.

1. Add an execution-preparation manifest model and store.
2. On active epic promotion, mark existing execution-preparation manifest and downstream artifacts stale or superseded.
3. Before milestone generation, clear or supersede existing `.agents/specs/*.md` and `.agents/specs/bundle-manifest.md` so a new bundle cannot mix with old specs.
4. After milestone generation, record active epic hash and ordered spec hashes.
5. Regenerate operational context when the current active epic hash or ordered spec hash set differs from the manifest.
6. Regenerate execution prompt when the operational context hash, active epic hash, or ordered spec hash set differs from the manifest.
7. Regenerate compatibility artifacts when execution prompt, operational context, or ordered spec hashes differ from the manifest.
8. Teach `RoadmapResumePlanner` and `InvariantValidator` to reject or regenerate stale execution-preparation artifacts instead of treating presence as readiness.
9. Leave stale artifacts on disk as evidence only if they are marked `Superseded`; otherwise delete generated compatibility files that are not evidence-bearing.

## Regression tests to add

- Promoting a second active epic after a first epic has specs, operational context, execution prompt, plan, and milestones invalidates or supersedes all downstream execution-preparation artifacts.
- A new milestone bundle with fewer spec files cannot leave an old extra `.agents/specs/*.md` file in the active spec set.
- Existing operational context is regenerated when `.agents/epic.md` changes.
- Existing execution prompt is regenerated when specs or operational context change.
- `RoadmapResumePlanner` blocks or regenerates from `ExecutionPromptReady` when the manifest active epic hash differs from the current active epic hash.
- `InvariantValidator` rejects `ExecutionPromptReady` and `ExecutionLoop` when the execution-preparation manifest is missing, stale, or inconsistent.
- A valid unchanged manifest still allows resume from `ExecutionPromptReady` without rerunning milestone generation.
- Stale `.agents/milestones/*.md` files beyond the current spec count are deleted or marked superseded.

## Acceptance criteria

- A new active epic cannot execute with specs, operational context, execution prompt, plan, or milestones generated for a different active epic content hash.
- Execution readiness can be resumed only when derived artifact provenance matches the current active epic and ordered spec hash set.
- Regeneration is automatic for stale derived artifacts where inputs are valid and present.
- Corrupted, missing, or unverifiable provenance is evidence-blocked before execution.
- Existing crash-resume behavior remains intact for artifacts whose recorded inputs still match current repository state.
