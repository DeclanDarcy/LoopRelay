# Handoff

## Slice Summary

- Completed Epic 2 M4A.2 historical handoff preservation.
- `HandoffService` now reads the generated current handoff after successful provider completion instead of only checking existence.
- If a launch-time previous handoff snapshot exists and differs from the generated current handoff, the previous snapshot is archived to the next highest `.agents/handoffs/handoff.NNNN.md`.
- Historical sequence allocation now uses highest existing four-digit handoff suffix plus one.
- If no previous snapshot exists, no archive is created.
- If the previous snapshot content matches the generated current handoff, no archive is created.
- If archive creation fails, the generated current handoff remains in place and the session/repository transition to `Failed`.
- Added stable archive failure reason: `Execution completed but the previous handoff could not be archived.`
- Updated M4 checklist for completed archive preservation items only.
- Rotated prior `.agents/handoffs/handoff.md` to `.agents/handoffs/handoff.0014.md`.

## Files Changed

- `.agents/milestones/m4-handoff-lifecycle.md`
- `.agents/handoffs/handoff.0014.md`
- `.agents/handoffs/handoff.md`
- `src/CommandCenter.Backend/Execution/HandoffService.cs`
- `tests/CommandCenter.Backend.Tests/ExecutionHandoffServiceTests.cs`

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` passed: 109 tests.

## New State

- M4A.2 backend archive preservation is implemented and covered.
- Completion validation now certifies current handoff existence, previous-current archive creation when required, no-op archive cases, and archive-failure behavior.
- M4 still has remaining work for generated handoff review visibility, projection refresh after completion processing, and completed duration metadata.

## Recommended Next Slice

- Continue with M4A.3: expose the generated handoff for review and ensure `AwaitingAcceptance` projection/workspace visibility is complete without adding accept/reject controls before M5.
