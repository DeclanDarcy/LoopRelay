# Handoff

## New State

- Continued Milestone 9 by implementing the first authorized preparation invocation path:
  - eligible Decision-stage preparation now calls the existing Decisions discovery command through `IDecisionDiscoveryService.DiscoverAsync`.
  - created review artifacts are recorded as `decision-candidate:<id>` in `WorkflowPreparationEvent.CreatedArtifactIds`.
  - successful invocation records preparation event decision `Created`.
- Preserved authority boundaries:
  - open workflow gates still refuse preparation.
  - duplicate decision candidate/proposal/package evidence still prevents command invocation.
  - preparation does not promote candidates, generate proposals from promoted candidates, resolve decisions, or advance workflow stage.
- Adjusted preparation idempotency so an existing preparation event is matched by fingerprint, stage, command, and duplicate state; this supports successful events whose stored decision differs from the pre-run evaluation outcome.
- Adjusted preparation gate resolution to use the active coordinator stage from the latest workflow timeline when it differs from the domain-derived projection, preventing downstream projected gates from blocking current-stage preparation.
- Updated `.agents/milestones/m9-continuation.md` to mark Decisions discovery preparation and eligible-domain-command artifact creation complete, while keeping proposal generation, Continuity preparation, commit preparation, hosted continuation, influence tracing, and health assessment deferred.
- Rotated previous `.agents/handoffs/handoff.md` to `.agents/handoffs/handoff.0017.md`.

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter WorkflowProjectionServiceTests` passed: 81 tests.
- `dotnet build CommandCenter.slnx` passed with 0 warnings and 0 errors.

## Notes

- The implemented Decisions path creates reviewable candidates only. Proposal generation remains deferred because the existing generation command requires a promoted candidate, and promotion remains a human/decision-domain action.
- A repeated preparation run after candidate creation is blocked by duplicate evidence and does not invoke discovery again.

## Next Slice

- Implement the next Decisions preparation step only after an explicit decision-domain authorization path exists for promoted candidates; otherwise move to Continuity proposal/linkage preparation as the next lowest-risk remaining preparation path.
- Keep hosted continuation/preparation disabled until all endpoint-triggered preparation paths are covered and idempotency is proven.
