# Milestone 1: Establish Characterization Coverage

## Work Items

- [ ] Keep existing state-machine integration tests.
- [ ] Add focused tests only where the current behavior is not directly pinned.

## Characterization Tests

- [x] Add or confirm projection validation or stale-projection failure writes
  projection blocker evidence before any prompt-start state for that prompt.
- [x] Add or confirm bootstrap runtime failure preserving output path
  `.agents/core/roadmap-completion-context.md`.
- [x] Add or confirm selection parse failure after `.agents/selection.md`,
  numbered selection evidence, provenance, and lifecycle are already written,
  and before decision-ledger append.
- [ ] Add or confirm stale active selection is rejected before create, split,
  audit, or rewrite fallback prompts run.
- [ ] Add or confirm `Insufficient Evidence` audit output persisting audit
  evidence and audit decision before throwing, with no durable blocker branch
  state.
- [ ] Add or confirm `CreateNewEpic` prompt failure using status `Failed`,
  decision `Runtime Failure`, and intent `ResolveTransitionFailure`.
- [ ] Add or confirm `RealignEpic` and `ReimagineEpic` fallback to current
  selection only when `.agents/epic.md` is missing.
- [ ] Add or confirm promotion prompt completion is not artifact completion.
- [ ] Add or confirm invalid split bundle writes no child files and no split
  family.
- [ ] Add or confirm split promotion uses selected child content and reuses the
  original prompt correlation id, timing, and input snapshot.
- [ ] Add or confirm milestone success uses `PromptCompleted` plus
  `MilestoneSpecsMaterialized`, not `TransitionCompleted`.
- [ ] Add or confirm milestone post-prompt bundle failure writing
  `milestone-spec-generation-failed.NNNN.md` and not rolling back already
  written artifacts.
- [ ] Add or confirm completion evaluation parse failure after evaluation
  evidence is written, without converting it to invalid-certification blocker
  state.
- [ ] Add or confirm invalid parsed certification writes
  `CompletionCertificationRejected` and intent
  `ResolveInvalidCompletionCertification`.
- [ ] Add or confirm close-route completion-context update superseding active
  selection after the context rewrite and excluding numbered update evidence
  from final route outputs.
- [ ] Add or confirm archive and synthesis failures are not converted into
  invalid-certification blockers.

## Verification

- [x] Run Roadmap CLI tests.

```powershell
dotnet test tests/LoopRelay.Roadmap.Cli.Tests/LoopRelay.Roadmap.Cli.Tests.csproj --no-restore
```

- [x] Run full solution tests.

```powershell
dotnet test LoopRelay.slnx --no-restore
```
