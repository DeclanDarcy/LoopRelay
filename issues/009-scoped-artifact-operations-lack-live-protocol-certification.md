# Scoped artifact operations lacked live protocol certification

## Status

Resolved

## Severity

High

## Original Finding

The sandbox-copy fallback was removed before the Codex app-server protocol behavior that replaced it was certified.

Affected code:

- `src/LoopRelay.Plan.Cli/PlanPipeline.cs`
- `src/LoopRelay.Plan.Cli/PermissionedArtifactOperationStep.cs`
- `src/LoopRelay.Cli/DecisionSession.cs`
- `tests/LoopRelay.Agents.Tests/Services/Codex/CodexAppServerSessionTests.cs`

`PlanPipeline` unconditionally routed scoped artifact steps through `PermissionedArtifactOperationStep`, and `DecisionSession` transfer operations used scoped app-server sessions in the real repository. The old temp-workspace isolation path had been removed from these flows.

At the time of the finding, the live checks that proved the replacement safety boundary existed only as skipped, empty xUnit facts. They described the exact protocol assumptions the new path depended on: read-only app-server sessions requesting approval before edits, file-change approvals exposing exact target paths, declined scoped approvals not hanging, and accepted scoped approvals applying only requested writes.

The migration plan explicitly required keeping the old sandbox-copy path if Codex did not expose precise enough approval requests. The default changed while those checks remained manual-only.

## Impact

A Codex protocol mismatch can either break all scoped artifact operations or weaken the old isolation boundary. If read-only app-server sessions can mutate without approval, or if approvals expose only broad roots, scoped operations no longer provide the same constraint as the temp sandbox that only contained declared inputs and copied back declared outputs.

## Resolution

`src/LoopRelay.Certification/MilestoneThreeRunner.cs` now owns this live-provider certification. It launches the configured Codex app-server against a disposable repository and records executable checks for:

- the exact Codex version and schema profile;
- live `xhigh` plus read-only posture acceptance;
- `item/fileChange/requestApproval` arriving before mutation;
- an exact file-change target path;
- an accepted in-profile write;
- a declined out-of-profile write completing without a hang; and
- the accepted write remaining exactly scoped.

The campaign writes `milestone-3.latest.json` evidence under the configured certification authority root and participates in continuous-certification profile gating. Live milestone-3 campaigns have been completed multiple times. Component tests continue to pin frame construction, approval enrichment, and permission evaluation without pretending to certify provider behavior. The redundant skipped facts have been removed.

Future supported Codex version or schema changes must rerun milestone 3; a component-suite pass is not a substitute for that live gate.

## Acceptance Criteria

- [x] The supported Codex app-server profile is recorded with live evidence for the required approval behavior.
- [x] A precise file-change target path is required before an operation-scoped approval can be accepted.
- [x] The live release gate covers read-only edit approval, exact target paths, declined approvals, and accepted scoped writes.
- [x] Provider-dependent certification has one executable owner rather than empty skipped test placeholders.
