# Handoff

## New State

- Completed Milestone 2 workflow persistence and recovery slice.
- Added fingerprinted derived workflow evidence:
  `WorkflowFingerprint`, `WorkflowTimeline`, `WorkflowTimelineEntry` source-domain/source-artifact/fingerprint fields, `WorkflowRecoveryDiagnostics`, `WorkflowHistoryProjection`, and `WorkflowRecoveryResult`.
- Added workflow persistence:
  `IWorkflowRepository`, `FileSystemWorkflowRepository`, workflow artifact document/path/json helpers, deterministic timeline JSON/Markdown persistence under `.agents/workflow/timelines`, and report JSON/Markdown persistence under `.agents/workflow/reports`.
- Added workflow recovery:
  `IWorkflowRecoveryService`, `WorkflowRecoveryService`, and `WorkflowRecoveryHostedService`.
- Registered workflow repository, recovery service, and startup recovery through `AddWorkflow()`.
- Added workflow recovery/history endpoints:
  `GET /api/repositories/{repositoryId}/workflow/history`,
  `GET /api/repositories/{repositoryId}/workflow/recovery`,
  `POST /api/repositories/{repositoryId}/workflow/recover`.
- Startup recovery is best-effort per repository. It does not block backend startup if a repository has incomplete local evidence, such as a test fixture path that is not a git repository.
- Marked `.agents/milestones/m2-persistence-recovery.md` complete.
- Rotated previous `.agents/handoffs/handoff.md` to `.agents/handoffs/handoff.0002.md`.

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter WorkflowProjectionServiceTests` passed: 19 tests.
- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` passed on rerun: 531 tests.
- `dotnet build CommandCenter.slnx` passed with 0 warnings and 0 errors.

## Notes

- Workflow persistence remains derived evidence. Current projection still comes from domain services, and the guardrail test proves deleting workflow timeline artifacts does not change projected stage, blocking gate, or valid transitions.
- Recovery compares the latest persisted timeline fingerprint with a domain-derived timeline fingerprint. Missing, corrupt, or conflicting workflow evidence is rebuilt from domain evidence.
- Recovery currently records diagnostics in returned models; persisted recovery-record files under `.agents/workflow/recovery` are still a later hardening point if stricter audit history is needed.

## Next Slice

- Start Milestone 3 by implementing the workflow gate catalog: gate model/evidence/diagnostics, gate discovery from current projection and transitions, gate history endpoint scaffolding, and tests proving each human authority boundary maps to existing domain commands without satisfying the gate.
