# Archive operational_delta.md on successful context update

**Date:** 2026-07-01
**Status:** Approved (design)
**Scope:** `CommandCenter.Orchestration` **and** `CommandCenter.CLI` — the two independent
Transfer implementations. Discovery found the CLI loop (`DecisionSession.TransferAsync`) is a
genuine parallel transfer site with identical behavior (writes the live delta, evolves and
writes the context back, records health), not merely a consumer. The user asked for consistent
behavior everywhere, so both sites archive.

## Problem

During a Transfer, `RepositoryOrchestrator.PrepareTransferAsync` writes
`.agents/operational_delta.md`, folds it into `.agents/operational_context.md`, then
reseeds a fresh Decision process. The delta is never archived — it simply lingers in place
until the *next* transfer overwrites it, so at any moment the live `operational_delta.md`
reads as a "pending" delta that has, in fact, already been consumed.

## Goal

After `operational_context.md` is successfully updated, archive the delta into a numbered
historical copy under a dedicated directory and remove the live file. Archiving is a hard
step in the transfer sequence: if it fails, the transfer fails.

## Design

### Trigger / placement

In `PrepareTransferAsync`, immediately after the evolved context is persisted and
health-checked — right after `RecordOperationalContextHealth(evolvedContext.Length)` (today
~line 1663), and **before** the fresh Decision process reseeds (step 4). That is the point
where `operational_context.md` is definitively updated. Archiving becomes step "3.5" in the
sequence.

For UI consistency with the other steps, publish a phase marker before running:
`DecisionStream.Publish("phase", Serialize(new { phase = "ArchiveOperationalDelta" }))`.

### Archive semantics (rotate-then-delete)

1. Read `.agents/operational_delta.md`.
2. Compute the next 4-digit sequence: list `.agents/deltas/` for `operational_delta.*.md`,
   parse the `NNNN` sequence from each name, take `max + 1` (default 0 → `0001`). Same
   sequencing logic `ArtifactRotationService` already uses for handoffs/decisions. The
   counter is monotonic per repo and survives across runs.
3. Write the content to `.agents/deltas/operational_delta.{seq:0000}.md`.
4. Delete the live `.agents/operational_delta.md` so no stale "pending" delta lingers.

### New paths (`OrchestrationArtifactPaths`)

Mirroring the existing `HistoricalHandoff` / `HistoricalDecision` members:

- `DeltasDirectory = ".agents/deltas"`
- `HistoricalDeltaSearchPattern = "operational_delta.*.md"`
- `HistoricalDelta(int sequence) => $".agents/deltas/operational_delta.{sequence:0000}.md"`

### Failure handling (strict)

The archive step is a hard gate. If the read/sequence/write/delete work throws, publish a
`failed` frame and return `false`, consistent with every other step in
`PrepareTransferAsync`:

```
DecisionStream.Publish("failed", Serialize(new {
    phase = "ArchiveOperationalDelta",
    reason = "<describe the archive failure>",
    detail = "<exception message>",
}));
return false;
```

Because this runs after the context write (step 3) and before the reseed (step 4), a failure
means: context updated, old process closed, fresh process not yet opened, transfer returns
`false`. On a retry, step 1 (`ProduceOperationalDelta`) regenerates a fresh delta on a warm
process, so a leftover/partial delta from the failed attempt is harmless.

### Scope boundary

Shared `OrchestrationArtifactPaths` members (`DeltasDirectory`, `HistoricalDelta`,
`HistoricalDeltaSearchPattern`) serve both sites. Deliberately **not** extending
`ArtifactRotationService` or the Core artifact taxonomy (no new `ArtifactFamily`/`ArtifactType`,
no `GetCurrentOperationalDeltaAsync`), because that ripples into contract goldens and UI
artifact typing for no benefit here.

- **Backend (`RepositoryOrchestrator`):** a private `ArchiveOperationalDeltaAsync(repository)`
  helper plus `NextDeltaSequenceAsync`, driven by the existing `IArtifactStore`
  (`ReadAsync`/`ListAsync`/`WriteAsync`/`DeleteAsync`), mirroring the existing
  `NextHandoffSequenceAsync`/`HighestSequence` rotation. Strict failure = a published `failed`
  frame + `return false` from `PrepareTransferAsync`.
- **CLI (`DecisionSession` / `LoopArtifacts`):** reuse the existing generic
  `LoopArtifacts.RotateAsync` (already read→write-numbered→delete-live move semantics for
  handoffs/decisions) via a one-line `RotateOperationalDeltaAsync()`. `TransferAsync` calls it
  right after the context is written back and before the fresh process opens. Strict failure =
  a thrown `LoopStepException` (a missing delta) or a propagated store exception, both of which
  fail the loop step.

## Testing

Extend the transfer tests (`RepositoryOrchestratorTransferTests`):

- After a successful transfer, `.agents/deltas/operational_delta.0001.md` holds the delta
  content and `.agents/operational_delta.md` no longer exists.
- A second successful transfer yields `.agents/deltas/operational_delta.0002.md`
  (monotonic sequencing).
- When the archive step fails (e.g. an `IArtifactStore` that throws on the delta write/delete),
  a `failed` frame with `phase = "ArchiveOperationalDelta"` is published and the transfer
  returns `false` (fresh process not seeded).

## Verification during implementation

Before making deletion unconditional, confirm no live consumer treats a missing
`operational_delta.md` as an error state — check the UI `operationalContext.ts` API surface and
the backend proposal/generation services (`OperationalContextGenerationService`,
`FileSystemOperationalContextProposalStore`). If any consumer reads the live delta as
"current," reconcile before shipping.

## Out of scope

- Retention/pruning of `.agents/deltas/` (unbounded history, same as handoffs/decisions today).
- Surfacing archived deltas in the UI.
- Changing the delta's content or the `ProduceOperationalDelta` / `UpdateOperationalContext`
  prompts.
