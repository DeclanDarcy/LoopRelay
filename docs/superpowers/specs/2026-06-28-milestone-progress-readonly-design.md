# Milestone Progress — Read-Only Display + Decouple From Execution

**Date:** 2026-06-28
**Status:** Approved direction (backend projection); implementation pending.
**Origin:** Discovery workflow `wf_4ed1e480-f6c` (5 parallel read-only mappers).

## 1. Goal

Two coupled changes:

1. **Rip milestone *selection* out of the execution flow.** Today the UI presents a
   milestone picker (`<select>` in `ExecutionContextPanel`, clickable buttons in
   `WorkspaceMilestonesPanel`) as if it drives execution. It does not — the execution-context
   preview and `Start Execution` are keyed purely on `repositoryId`. The selection is
   decorative and misleading; remove it entirely.
2. **Make milestones a read-only progress display** driven by checked checkboxes in the
   milestone markdown. Agents own milestone agency (they check the boxes); the user only
   *sees* progress. Progress is a **backend projection** (the UI never parses markdown).

## 2. Key discovery findings

- **Execution is already decoupled.** `previewExecutionContext(repositoryId)` and
  `startExecution(repositoryId)` take only the repo id. `executionContextMatchesSelection`
  is `executionContext.id === repository.id` (App.tsx:425-426). `app.smoke.test.tsx:204`
  already asserts that changing the milestone does **not** trigger `preview_execution_context`.
  So ripping selection is **behavior-preserving on execution**: the Build/Start flow survives
  unchanged once the `<select>`'s `disabled={!selectedMilestonePath}` term is dropped.
- **No checkbox parsing exists anywhere** (backend or UI). Milestone content is not read at
  projection time today — only paths. This is net-new.
- **The surface is bigger than "a UI change."** Because the workspace projection is contract-
  pinned, adding a field deliberately moves: the workspace golden, the workspace freshness
  manifest (SHA), the hand-written TS mirror, the Rust mirror, and the dev mock. This is a
  **sanctioned break** of the m8 "byte-untouched / UI 420-by-construction" guarantee —
  justified by the standing "new architecture wins" principle.

## 3. Scope decision — workspace projection ONLY

The display is mounted only in `WorkspaceTab` and reads `/workspace` →
`RepositoryWorkspaceProjection`, whose TS mirror is **hand-written** (`types/repositories.ts`,
pinned by `repository-workspace.artifact-freshness.json`). It does **not** flow through the
generated-dashboard pipeline (IR builder, generated `.ts`, metadata table). Therefore:

- **In scope:** the workspace projection only.
- **Out of scope:** `RepositoryDashboardProjection` and the entire
  `repository-dashboard.*` generated pipeline (IR / generated TS / metadata / generated-freshness).
  The dashboard keeps its flat `milestoneCount`. (YAGNI — the read-only display has no need
  for per-milestone progress in the multi-repo list.)

## 4. Data contract

### New C# DTOs — `src/LoopRelay.Core/Projections/MilestoneProgress.cs` (new file)

```csharp
namespace LoopRelay.Core.Projections;

public sealed class MilestoneProgress
{
    public string RelativePath { get; init; } = "";
    public string Name { get; init; } = "";
    public int CompletedTaskCount { get; init; }
    public int TotalTaskCount { get; init; }
    public bool IsComplete { get; init; }   // TotalTaskCount > 0 && Completed == Total
}

public sealed class MilestoneProgressRollup
{
    public int CompletedMilestoneCount { get; init; }
    public int TotalMilestoneCount { get; init; }
    public IReadOnlyList<MilestoneProgress> Milestones { get; init; } = [];
}
```

### `RepositoryWorkspaceProjection` — add one property after `MilestoneCount`

```csharp
public int MilestoneCount { get; init; }
public MilestoneProgressRollup MilestoneProgress { get; init; } = new();   // NEW
```

`MilestoneCount` is retained (other consumers, e.g. `SelectedRepositorySummary`, still use it).

### TS mirror — `src/LoopRelay.UI/src/types/repositories.ts`

```ts
export type MilestoneProgress = {
  relativePath: string
  name: string
  completedTaskCount: number
  totalTaskCount: number
  isComplete: boolean
}
export type MilestoneProgressRollup = {
  completedMilestoneCount: number
  totalMilestoneCount: number
  milestones: MilestoneProgress[]
}
// on RepositoryWorkspaceProjection:
milestoneProgress: MilestoneProgressRollup
```

### Rust mirror — `src/LoopRelay.Shell/src/main.rs` (`RepositoryWorkspaceProjection`)

Add a `RepositoryMilestoneProgress` struct (snake_case fields) + a
`milestone_progress` field carrying `{ completed_milestone_count, total_milestone_count,
milestones }`. Required so `RepositoryWorkspaceRustMirror…` consumer drift assertions stay
exact (they `Assert.Single`/enumerate known omissions).

## 5. Checkbox parsing rule (fence-aware)

Verified against all 30 milestone files in `LoopRelay` and `Axiom`: the only form is
GFM task items with a hyphen bullet — `- [ ] ` / `- [x] ` (lowercase `x`, 2-space nested
indent, no `*`/`+` bullets, no `[X]`, no tabs). Nested items are counted individually.

**Rule (applied per milestone file):**
1. Split on `\r?\n`. Track fenced-code state: toggle on each line matching `^\s*` + triple-backtick. **Skip lines inside a fence.**
2. A **task** is a non-fenced line matching `^\s*- \[[ xX]\] ` (anchored hyphen, one space, bracket, one space).
3. **Checked** if the bracket holds `x`/`X`.
4. `TotalTaskCount` = tasks; `CompletedTaskCount` = checked tasks.
5. `IsComplete = TotalTaskCount > 0 && CompletedTaskCount == TotalTaskCount`.

Fence-stripping changes no count for today's files (zero checkbox-like lines inside fences)
but is required for robustness — these files do contain fenced blocks.

### C# implementation (in `RepositoryProjectionService`)

Read each milestone via the **already-injected** `IArtifactStore artifactStore`
(`artifactStore.ReadAsync(ArtifactPath.ResolveRepositoryPath(repository, milestone.RelativePath))`
— same pattern the class already uses for operational context). No DI/constructor change.
Add a private `BuildMilestoneProgressAsync(repository, inventory)` + a fence-aware
`CountCheckboxes(content)`; call from `BuildWorkspaceProjectionAsync` and assign
`MilestoneProgress = …` in the initializer.

## 6. Change inventory

### Part A — Backend (projection)
- **NEW** `src/LoopRelay.Core/Projections/MilestoneProgress.cs` (the two DTOs).
- `RepositoryWorkspaceProjection.cs` — add `MilestoneProgress` property.
- `RepositoryProjectionService.cs` — `BuildMilestoneProgressAsync` + fence-aware
  `CountCheckboxes`; call from `BuildWorkspaceProjectionAsync` (~:94-120); assign after the
  `MilestoneCount = …` line (~:110). Uses injected `artifactStore` (:27) — no DI change.

### Part B — Contract fixtures & mirrors (workspace only)
- `tests/.../ContractOracleFixtureTests.cs` — workspace representative builder (~:244-431):
  set deterministic milestone-progress values (e.g. one milestone 7/7 complete, one 3/7).
- `tests/.../ContractFixtures/repository-workspace.golden.json` — regenerate (see §7).
- `tests/.../ContractFixtures/repository-workspace.artifact-freshness.json` — recompute
  `sourceSha256` (golden) + `artifactSha256` (`repositories.ts`).
- `src/LoopRelay.UI/src/types/repositories.ts` — add the new types + `milestoneProgress`.
- `src/LoopRelay.Shell/src/main.rs` — workspace Rust mirror struct + new struct.
- `src/LoopRelay.UI/src/devTauriMock.ts` — workspace mock builder emits `milestoneProgress`.
- `tests/.../RepositoryProjectionServiceTests.cs` — assert the new fields (needs a written
  fixture milestone file with known checkboxes).

### Part C — UI deletions (rip selection from execution)
Delete, with all references (anchors in discovery report 2):
- `selectedMilestonePath`, `selectMilestone`, `reconcileSelectedMilestone` — the whole
  selection slice in `state/shellState.ts` (:49-50, :60-62, :104-117, :123-125, memo entries)
  and every `App.tsx` consumer (:106, :114-115, :640, :1385, :1546-1557, :1579, :1587,
  :1841-1843, :2123).
- The `<select aria-label="Execution milestone">` block in `ExecutionContextPanel.tsx:53-68`
  and its props (`milestoneOptions`, `selectedMilestonePath`, `onSelectMilestone`); drop the
  `!selectedMilestonePath` term from the Build button `disabled` (:73).
- `ExecutionTab.tsx:42,72,83` — re-source the header title (was
  `selectedMilestonePath ?? 'Select a milestone'`) from a repo-scoped value (e.g. repository
  / execution-context name).
- `openWorkspaceExecutionContext` (App.tsx:1656-1663) — drop the `selectMilestone` call; keep
  the tab+section navigation as a zero-arg jump.
- Navigation-target milestone routing (App.tsx:639-641, memo dep :654) — collapse to a
  workspace jump.
- **Preserve** the `setExecutionContext(null)` reset currently in the deleted reconcile effect
  (App.tsx:1556) — verify the workspace-load path still nulls a stale preview, or keep that line.

**Do NOT touch:** the identically-named `onOpenExecutionContext` on
`OperationalContextCurrentPanel`/`OperationalContextTab` — it is a zero-arg nav jump, not
milestone-coupled.

### Part D — Read-only display
- `WorkspaceMilestonesPanel.tsx` — rewrite to a passive `<ul>/<li>` progress readout:
  props `{ rollup: MilestoneProgressRollup }`; per-milestone name + `StatusBadge`
  (Complete / In progress / Not started via a new `milestoneProgressStatus` in `lib/status.ts`)
  + an accessible `role="progressbar"` meter (`aria-valuenow/max`, `aria-valuetext="3/7 tasks"`)
  + visible `N/M tasks`; header `actions` shows the rollup (`N of M milestones complete`).
  No `onClick`, no selection, no `aria-current`.
- `lib/status.ts` — add `milestoneProgressStatus(milestone)`.
- `App.css` — replace the `.workspace-milestone-item` button rules with non-interactive `<li>`
  rules; add `.workspace-milestone-meter*` / `-head` / `-progress` / `-count` (reuse existing
  CSS-var tokens).
- `App.tsx` — mount with `rollup={workspace.milestoneProgress}` (replace `milestones={milestoneOptions}`).

## 7. Golden regeneration & SHA procedure

The workspace golden has **no env-gated writer** (asserted, not written). Procedure:

1. Land Part A + the representative builder (Part B first item).
2. Regenerate `repository-workspace.golden.json`: run
   `dotnet test … --filter …RepositoryWorkspaceGoldenFixtureMatchesBackendSerialization`,
   read the zero-drift diff (it lists each new `$…` path with its serialized value), and
   hand-edit the golden to match — OR temporarily make the fixture test write the actual
   serialization to the golden path, run once, revert the writer.
3. Recompute the freshness manifest SHAs:
   `(Get-FileHash …/repository-workspace.golden.json -Algorithm SHA256).Hash` → `sourceSha256`;
   `(Get-FileHash …/types/repositories.ts -Algorithm SHA256).Hash` → `artifactSha256`;
   paste into `repository-workspace.artifact-freshness.json`.
4. Hand-edit `repositories.ts`, `main.rs`, `devTauriMock.ts` to the agreed shape.
5. Re-run the full backend suite (no env var) + Vitest.

No `RepositoryDashboardGenerationMetadata` rows are needed: the new fields are plain
non-nullable ints/bools/strings (no enum/identity/date), like the existing `milestoneCount`.

## 8. Test plan

- **Backend:** extend `RepositoryProjectionServiceTests` (parser counts from a written
  milestone fixture; rollup math; `isComplete` edge cases incl. zero-checkbox file). Full
  `ContractOracleFixtureTests` / `ContractGeneratedArtifactFreshnessTests` /
  `ContractConsumerVerificationTests` (workspace TS + Rust + mock) go green after §7.
- **UI:** rewrite `workspaceMilestonesPanel.test.tsx` (read-only: names, `N/M tasks`,
  Complete/In-progress badges, `getAllByRole('progressbar')` carries counts, no buttons/links/
  `aria-current`, rollup line, empty state). Invert the old
  `not.toHaveTextContent(/progress|complete/i)` assertion. Edit `shellState.test.tsx` (drop
  milestone-selection assertions/calls). Rewrite the `app.smoke.test.tsx` combobox test to
  drive Build directly (keep the load-on-Build / repo-id guard; remove the combobox helper).
  Keep `projectionHooks.test.tsx` as the repo-scoped guard.
- Target: full backend suite green (was 1133/1-skip/0-fail; expect a few added asserts) and
  Vitest green (was 420/420).

## 9. Certified-surface impact & rollback

This intentionally edits contract-pinned files (workspace golden, freshness manifest,
`repositories.ts`, `main.rs`, `devTauriMock.ts`). The m8 "byte-untouched" guarantee for the
workspace contract is **deliberately retired** for this surface; the consumer-verification
tests still pin every mirror against the regenerated backend golden, so the
mock/contract-divergence risk (a wrong shape identical in mock + types passing while
production crashes) is mitigated **only if the golden is regenerated from real backend
serialization** — which §7 enforces. Rollback = revert the commit (single coherent change).

## 10. Out of scope

- Dashboard projection / `repository-dashboard.*` generated pipeline (kept as flat `milestoneCount`).
- Any change to prompts, the orchestration loop, or how agents check boxes (agents already own
  milestone agency; this only *reads* their checkbox state).
- The `ExecutionContextPanel` Build/Start preview flow (survives unchanged minus the `<select>`).
