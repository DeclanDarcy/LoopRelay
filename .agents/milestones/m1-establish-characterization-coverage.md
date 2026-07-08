# Milestone 1: Establish Characterization Coverage

## Work Items

- [ ] Keep existing state-machine integration tests.
- [ ] Add focused tests only where the current behavior is not directly pinned.

## Characterization Tests

- [ ] Add or confirm selection parse failure after `.agents/selection.md`, selection evidence, provenance, and lifecycle are already written.
- [ ] Add or confirm `Insufficient Evidence` audit output persisting audit evidence and audit decision before throwing.
- [ ] Add or confirm bootstrap runtime failure preserving output path `.agents/core/roadmap-completion-context.md`.
- [ ] Add or confirm `CreateNewEpic` prompt failure using status `Failed`, decision `Runtime Failure`, and intent `ResolveTransitionFailure`.
- [ ] Add or confirm `RealignEpic` fallback to current selection only when `.agents/epic.md` is missing.
- [ ] Add or confirm milestone post-prompt bundle failure writing `milestone-spec-generation-failed.NNNN.md`.
- [ ] Add or confirm completion evaluation parse failure after evaluation evidence is written, without converting it to invalid-certification blocker state.
- [ ] Add or confirm close-route completion-context update superseding active selection after the context rewrite.

## Verification

- [ ] Run Roadmap CLI tests.

```powershell
dotnet test tests/LoopRelay.Roadmap.Cli.Tests/LoopRelay.Roadmap.Cli.Tests.csproj --no-restore
```

- [ ] Run full solution tests.

```powershell
dotnet test LoopRelay.slnx --no-restore
```
