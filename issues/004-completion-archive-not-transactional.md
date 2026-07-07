# Completion archive mutates live workspace before close succeeds

## Severity

High

## Finding

Completion certification moves live execution artifacts before all close steps have succeeded.

Affected code:

- `src/LoopRelay.Completion/CompletedEpicArchiveService.cs`
- `src/LoopRelay.Completion/CompletionCertificationService.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`

`CompletedEpicArchiveService.ArchiveAndSynthesizeAsync` copies or moves the live epic workspace into `.agents/archive/epics/{index}/` before running `SynthesizeCompletedEpic`. The caller then runs `UpdateRoadmapCompletionContext` after archive synthesis returns.

If synthesis, roadmap completion update, quota handling, agent startup, or artifact writes fail after the move, `.agents/milestones`, `.agents/plan.md`, `.agents/details.md`, `.agents/operational_context.md`, and handoff/decision history may already be removed from the live workspace. The service returns a failed or blocked result, but the original certification inputs are no longer present for a clean retry.

## Impact

A transient prompt or runtime failure can strand an otherwise completed epic in a partially archived state. Main CLI retries may no longer see milestone evidence, and roadmap recovery may point at transition failure evidence without enough state to resume the same archive attempt safely.

## Proposal

Make completed-epic archiving transactional or explicitly resumable.

The robust shape is:

- Copy live artifacts into a pending archive directory first.
- Run synthesis and roadmap completion context update against copied inputs.
- Delete or mark live artifacts only after all close steps succeed.
- Or persist an archive recovery manifest that lets retries resume the same archive index and same copied inputs.
- Add failure-path tests for synthesis failure and update failure after archive materialization begins.

## Acceptance Criteria

- A failed `SynthesizeCompletedEpic` run leaves the live completion inputs available for retry, or can resume from a documented pending archive.
- A failed `UpdateRoadmapCompletionContext` run does not make the main CLI fall back to ordinary execution.
- Retrying after transient prompt failure does not create duplicate archive indexes or lose milestone evidence.
