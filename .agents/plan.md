# Implementation-First Non-Implementation File Review Plan

## Purpose

Implement a post-execution review loop that detects repository files created or changed outside implementation work, confirms likely non-implementation artifacts with read-only semantic review, records durable review state, optionally synthesizes useful information, and presents the result for explicit human review at epic completion.

The default operating mode is implementation-first: autonomous repository growth should normally be code, tests, project files, runtime configuration, generated build artifacts that are intentionally tracked, or LoopRelay operational artifacts under `.agents`. Prompt-time non-implementation file generation requires both an enabled user setting and an explicit HITL request for that deliverable. Non-implementation files may still be kept when the human explicitly chooses to retain them during completion review.

This is not a commit gate, publication gate, repository acceptance system, repository knowledge platform, plan-certification system, projection-governance mechanism, or documentation debt analyzer. It is a post-execution identification and review loop plus centralized prompt guidance.

Baseline snapshots, exact content identities, stale-decision validation, and read-only runner boundaries are safeguards for the requested `git diff -> classify -> confirm -> ledger -> HITL review` loop. They do not broaden the feature into a repository governance or acceptance architecture.

## Target Capability

For every execution slice:

```text
pre-slice repository baseline
-> execution
-> post-slice repository snapshot
-> execution-produced changed-file delta
-> deterministic parallel classification
-> read-only semantic confirmation for routed candidates
-> durable non-implementation review ledger
-> review evidence published with .agents state
```

At epic completion:

```text
fresh repository review refresh
+ unresolved confirmed non-implementation files
+ false positives
+ uncertain semantic dispositions
+ optional compact synthesis
-> human keep/delete/synthesis/uncertain decisions
-> stale-decision validation
-> durable decision record
-> completion evaluation proceeds only after required review decisions exist
```

Planning, decision, roadmap, and execution prompts should receive centralized implementation-first guidance. The user setting may allow agents to honor explicit HITL-requested non-implementation deliverables only when the setting is enabled and the request is captured as explicit HITL evidence. It must never authorize autonomous non-implementation file generation.

## Vocabulary

Use these terms consistently in code, tests, persisted JSON, prompt text, and review artifacts:

- `Implementation artifact`: source code, tests, UI assets, migrations, project files, build scripts, package manifests, generated source that is intentionally tracked, and other files required to implement, build, test, or run the product.
- `Machine-required artifact`: lockfiles, solution/project files, configuration files, generated manifests, prompt resources compiled into the product, schema files, CI files, and other non-code files required by tools or runtime behavior.
- `Sanctioned operational artifact`: LoopRelay runtime files under `.agents`, including plans, milestones, handoffs, decisions, deltas, evidence, state, projections, archives, ledgers, and review artifacts.
- `Semantic review candidate`: a changed file that deterministic classification cannot safely exclude and that must be semantically reviewed.
- `Ambiguous for semantic review`: deterministic routing for an unknown or ambiguous file that still requires semantic review. This is not a final uncertainty disposition.
- `Confirmed non-implementation file`: a candidate semantically confirmed as a non-implementation artifact.
- `False positive`: a routed candidate semantically determined to be implementation, machine-required, sanctioned operational, or otherwise not a non-implementation artifact.
- `Semantic uncertainty`: a semantic confirmation result where the review agent cannot responsibly decide whether the candidate is non-implementation.
- `HITL-requested non-implementation file`: a non-implementation file tied to an explicit human request or later human keep decision.
- `Free-form insight synthesis`: compact review support extracted from confirmed non-implementation files before keep/delete decisions; it is not a structured knowledge store.

Do not use `UncertainCandidate` for deterministic routing. Reserve `Uncertain` only for semantic confirmation output.

## Architecture

Add shared review primitives under `src/LoopRelay.Orchestration.Primitives` because the capability is used by the main CLI loop, epic completion flow, and roadmap/planning prompt flow. Avoid a new project unless implementation proves the primitives cannot stay cohesive there.

Recommended folders:

- `Models/NonImplementationReview/`
- `Services/NonImplementationReview/`
- `Abstractions/NonImplementationReview/`

Primary shared components:

- `RepositorySliceBaselineStore`: captures pre-slice and post-slice git snapshots and computes execution-produced deltas.
- `RepositoryChangeSetDetector`: obtains git status/diff data and file facts. It must support untracked files without treating the whole dirty tree as current execution output.
- `NonImplementationArtifactClassifier`: deterministic classifier that runs per changed file and produces route, evidence, and exclusion reason.
- `NonImplementationReviewLedgerStore`: JSON ledger under `.agents/review/non-implementation-ledger.json` with schema versioning. A minimal schema/store exists before semantic confirmation; later milestones harden query/rendering behavior.
- `INonImplementationReviewRunner`: dedicated read-only runner contract for semantic confirmation and synthesis.
- `NonImplementationSemanticConfirmer`: confirms candidates through `INonImplementationReviewRunner` with strict structured output.
- `NonImplementationPostExecutionReviewService`: wires detector, classifier, semantic confirmer, HITL request capture, ledger write, and evidence rendering for each execution slice.
- `NonImplementationInsightSynthesizer`: optional read-only/free-form synthesis runner for confirmed non-implementation files.
- `NonImplementationCompletionReviewService`: performs a fresh review refresh, assembles the completion review set, writes the pending review request, parses human decisions, validates stale-delete safety, applies approved deletes, and records durable outcomes.
- `ExplicitHitlNonImplementationRequestCaptureService`: records explicit HITL requests for non-implementation deliverables so agents do not reconstruct intent later.
- `ImplementationFirstPromptPolicyComposer`: central source of prompt guidance derived from CLI settings.

Canonical artifact paths should live in `OrchestrationArtifactPaths` where possible:

- `.agents/review/non-implementation-ledger.json`
- `.agents/review/non-implementation-review.md`
- `.agents/review/non-implementation-decisions.md`
- `.agents/review/non-implementation-synthesis.md`
- `.agents/evidence/non-implementation/`

Completion-specific code may keep compatibility constants in `CompletionArtifactPaths`, but those constants must delegate to the orchestration paths instead of inventing duplicate literals.

## Settings

Extend the existing settings document loaded by `CliSettingsLoader` without breaking `LoadPermissionPolicy()`.

Add:

```json
"artifactPolicy": {
  "allowHitlRequestedNonImplementationFiles": false
}
```

Implementation requirements:

- Default to `false` when loading the default template.
- When `false`, prompts hard-steer away from planning or executing non-implementation deliverables.
- When `true`, prompts may honor explicit HITL-requested non-implementation deliverables, but only when the request is captured as explicit HITL request evidence.
- Enabled mode plus explicit HITL request evidence is an AND condition for prompt-time generation; neither condition is sufficient alone.
- The setting never permits agents to invent non-implementation documentation, documentation-centric milestones, or theory-protection artifacts.
- Keep `permissions` validation unchanged.
- Return a broader settings result, for example `CliSettingsLoadResult(Permissions, ArtifactPolicy, Path, IsDefaultTemplate)`.
- Preserve `LoadPermissionPolicy()` as a compatibility helper.
- Add `NonImplementationArtifactPolicyOptions` with a default implementation-first mode.
- The setting controls prompt guidance only. It does not disable the post-execution review loop.

## Milestone 0 - Architectural Foundation

(See ./milestones/m0-architectural-foundation.md)

## Milestone 1 - Planning Integration, HITL Request Capture, And Implementation-First Guidance

(See ./milestones/m1-planning-integration-hitl-request-capture-and-implementation-first-guidance.md)

## Milestone 2 - Slice Baseline And Changed File Classification

(See ./milestones/m2-slice-baseline-and-changed-file-classification.md)

## Milestone 3 - Ledger Identity And Persistence

(See ./milestones/m3-ledger-identity-and-persistence.md)

## Milestone 4 - Semantic Candidate Confirmation

(See ./milestones/m4-semantic-candidate-confirmation.md)

## Milestone 5 - Post-Execution CLI Integration

(See ./milestones/m5-post-execution-cli-integration.md)

## Milestone 6 - Free-Form Insight Synthesis

(See ./milestones/m6-free-form-insight-synthesis.md)

## Milestone 7 - HITL Epic Completion Review

(See ./milestones/m7-hitl-epic-completion-review.md)

## Milestone 8 - Architectural Convergence

(See ./milestones/m8-architectural-convergence.md)

## Deferred Work

Do not implement these in this plan:

- structured insight synthesis
- semantic deduplication of extracted knowledge
- repository knowledge projection
- preservation metrics
- repository health analysis
- documentation debt analysis
- semantic garbage collection
- commit gating, publication gating, or repository mutation acceptance
- broad relay/runtime redesign

## Anti-False-Closure Tests

Add tests or explicit review checks for these failure modes:

- Components pass, but no CLI test proves the loop runs after each execution slice.
- Ledger has no unresolved entries, but current changed prose/report files exist at completion.
- A delete decision exists, but the target file hash no longer matches the reviewed ledger entry.
- A deterministic ambiguous route is treated as a final semantic uncertainty and never confirmed.
- Prompt policy appears in prompts, but duplicated hard-coded text diverges from the composer.
- Synthesis is generated and source-linked, but later treated as retention permission.
- A human-requested non-implementation deliverable lacks captured request evidence and blocks completion without explanation.
- A read-only semantic review accidentally uses a mutation-capable execution spec.

## Validation Strategy

Run focused tests after each milestone, then the full solution before completion:

```powershell
dotnet test tests/LoopRelay.Orchestration.Primitives.Tests/LoopRelay.Orchestration.Tests.csproj --no-restore --nologo
dotnet test tests/LoopRelay.Permissions.Tests/LoopRelay.Permissions.Tests.csproj --no-restore --nologo
dotnet test tests/LoopRelay.Cli.Tests/LoopRelay.Cli.Tests.csproj --no-restore --nologo
dotnet test tests/LoopRelay.Completion.Tests/LoopRelay.Completion.Tests.csproj --no-restore --nologo
dotnet test tests/LoopRelay.Roadmap.Cli.Tests/LoopRelay.Roadmap.Cli.Tests.csproj --no-restore --nologo
dotnet test LoopRelay.slnx --no-restore --nologo
```

If restore state is missing, run:

```powershell
dotnet restore LoopRelay.slnx
dotnet test LoopRelay.slnx --no-restore --nologo
```

## Completion Criteria

The plan is complete when:

- every execution slice captures a pre-slice baseline and reviews only execution-produced deltas
- post-execution changed files are discovered, classified, semantically confirmed when needed, and ledgered before `.agents` publication
- implementation and machine-required files reliably avoid semantic review
- confirmed non-implementation, false-positive, and semantic uncertainty states are durably distinct
- repeated unchanged candidates are skipped only by exact path/hash/classifier/prompt identity
- semantic confirmation and synthesis are read-only and mutation-impossible by construction
- compact free-form synthesis is available for confirmed non-implementation files when useful
- epic completion starts with a fresh review refresh
- epic completion blocks for required human decisions and resumes after valid decisions are recorded
- delete decisions require entry ID, path, status, and content hash match before file removal
- human keep/delete/synthesis/uncertain decisions are durable and auditable
- explicit HITL-requested non-implementation deliverables are captured as request evidence when granted
- default prompts steer toward implementation-first repository growth from one centralized composer
- enabled HITL-request mode and explicit HITL-requested documentation are preserved
- no commit gate, publication gate, repository acceptance platform, or structured knowledge system has been introduced
