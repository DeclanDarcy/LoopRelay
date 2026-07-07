# Milestone 2 - Slice Baseline And Changed File Classification

## Objective

deterministically route only files changed by the current execution slice.

## Work
- [ ] Implement `RepositorySliceBaselineStore`.
  - [ ] Capture a pre-slice snapshot immediately before the execution agent runs.
  - [ ] Capture a post-slice snapshot immediately after the execution agent completes.
  - [ ] Compare path status, existence, and content hash to compute execution-produced changes.
  - [ ] Exclude pre-existing dirty files that did not change during the slice.
  - [ ] Include pre-existing dirty files that the execution slice further changed, with `PreExisted = true`.
  - [ ] Include new untracked files created during the slice.
  - [ ] Assign an `ExecutionSliceId` and persist baseline metadata where necessary for crash-safe review.
- [ ] Implement `RepositoryChangeSetDetector` using `IProcessRunner` and `Repository`.
  - [ ] Parse `git status --porcelain` for changed path discovery.
  - [ ] Record `git diff --name-status` metadata where available for tracked files.
  - [ ] Include untracked, modified, added, deleted, renamed, and staged paths.
  - [ ] Capture path, status, baseline status, post status, deletion flag, existence, extension, size if available, baseline hash if readable, post hash if readable, and tracked diff metadata when available.
  - [ ] Filter out `.agents` only after recording sanctioned operational evidence.
- [ ] Implement `NonImplementationArtifactClassifier`.
  - [ ] Exclude implementation artifacts by source/test directory, recognized code extensions, UI asset paths, migrations, scripts, prompt resources, and generated source conventions.
  - [ ] Exclude machine-required artifacts such as `.slnx`, `.csproj`, `.props`, `.targets`, lockfiles, package manifests, CI files, settings templates, JSON schema/config files used by runtime or tests, and source-generator inputs.
  - [ ] Exclude sanctioned operational artifacts under `.agents`, including the review ledger and evidence created by this capability.
  - [ ] Route likely prose/design/audit/roadmap/report files as `SemanticReviewCandidate`.
  - [ ] Route unknown ambiguous files as `AmbiguousForSemanticReview`.
  - [ ] Emit classification evidence with rule ID, path facts, and rationale.
- [ ] Run classification with `Task.WhenAll` over the slice delta records.
- [ ] Add tests:
  - [ ] code files under `src` and `tests` are excluded
  - [ ] `.csproj`, `.slnx`, package/config files, prompt resources, and lockfiles are excluded
  - [ ] `.agents` operational files are excluded as sanctioned
  - [ ] root/docs/issues Markdown files route as candidates when changed by the slice
  - [ ] unknown ambiguous files route as ambiguous for semantic review
  - [ ] pre-existing dirty files unchanged by execution are excluded from the slice delta
  - [ ] pre-existing dirty files modified by execution are included with baseline facts
  - [ ] untracked files created during execution are discovered
  - [ ] renames use the destination path

## Acceptance
- [ ] Changed-file discovery includes untracked files without treating the whole dirty tree as current execution output.
- [ ] Classification is deterministic for the same baseline and post-slice state.
- [ ] Code and machine-required files reliably avoid semantic review.
- [ ] Candidate output includes enough evidence for semantic review, ledger identity, and debugging.
