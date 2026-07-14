# Scoped artifact operations lack live protocol certification

## Severity

High

## Finding

The sandbox-copy fallback was removed before the Codex app-server protocol behavior that replaces it was certified.

Affected code:

- `src/LoopRelay.Plan.Cli/PlanPipeline.cs`
- `src/LoopRelay.Plan.Cli/PermissionedArtifactOperationStep.cs`
- `src/LoopRelay.Cli/DecisionSession.cs`
- `tests/LoopRelay.Agents.Tests/CodexAppServerSessionTests.cs`

`PlanPipeline` now unconditionally routes scoped artifact steps through `PermissionedArtifactOperationStep`, and `DecisionSession` transfer operations use scoped app-server sessions in the real repository. The old temp-workspace isolation path has been removed from these flows.

The live tests that prove the replacement safety boundary are still skipped. They cover the exact protocol assumptions the new path depends on: read-only app-server sessions requesting approval before edits, file-change approvals exposing exact target paths, declined scoped approvals not hanging, and accepted scoped approvals applying only requested writes.

The migration plan explicitly required keeping the old sandbox-copy path if Codex did not expose precise enough approval requests. This commit flips the default while those checks remain manual-only.

## Impact

A Codex protocol mismatch can either break all scoped artifact operations or weaken the old isolation boundary. If read-only app-server sessions can mutate without approval, or if approvals expose only broad roots, scoped operations no longer provide the same constraint as the temp sandbox that only contained declared inputs and copied back declared outputs.

## Proposal

Do not remove the sandbox-copy fallback until the target Codex version is certified.

The robust shape is:

- Keep scoped app-server operations behind a feature flag or settings switch.
- Fall back to the previous sandbox-copy one-shot path when live protocol certification has not passed.
- Promote the skipped live-certification checks into a documented release gate for the supported Codex version.
- Pin the accepted file-change request shape in tests or fixtures captured from a real app-server session.

## Acceptance Criteria

- Scoped artifact operations are not the only available execution path until live protocol certification passes.
- The supported Codex app-server version is documented with evidence for the required approval behavior.
- A precise file-change target path is required before an operation-scoped approval can be accepted.
- Tests or release checks cover read-only edit approval, exact target paths, declined approvals, and accepted scoped writes.
