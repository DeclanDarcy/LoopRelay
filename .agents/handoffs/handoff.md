# Handoff

## New State From This Slice

- Continued Milestone 2 by adding explicit manual reasoning capture support.
- Added `ReasoningManualCaptureKind`, `ManualReasoningCaptureTemplate`, and `ManualReasoningCaptureCommand`.
- Added `IReasoningManualCaptureService` and `ReasoningManualCaptureService`.
- Registered manual capture in `AddReasoning`.
- Added backend endpoints:
  - `GET /api/repositories/{repositoryId}/reasoning/manual-captures/templates`
  - `POST /api/repositories/{repositoryId}/reasoning/manual-captures`
- Manual capture maps approved template kinds to ordinary immutable reasoning events. It does not create first-class hypothesis, alternative, contradiction, direction, assumption, or constraint entities.
- Manual capture accepts `UserSupplied` and legacy `ManualCapture` provenance source kinds, and rejects inferred provenance source kinds.
- Manual capture validates referenced thread IDs before event creation, then appends the created event to each requested thread.
- Added templates for decision evolution, hypothesis, alternative, contradiction, direction, assumption evolution, constraint evolution, and evidence capture.
- Added tests proving:
  - templates expose user-supplied event classifications.
  - alternative introduced/rejected/revisited can be preserved in one event thread.
  - contradiction identified/resolved can be preserved in one event thread.
  - direction shift is recorded as an event without materializing `.agents/reasoning/directions`.
  - manual capture rejects inferred provenance.
- Updated `.agents/milestones/m2-cross-artifact-capture.md` for completed manual-capture backend/test items.
- Rotated previous handoff to `.agents/handoffs/handoff.0010.md`.

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter ReasoningEndpointTests` passes: 7 tests.
- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` passes: 387 tests.
- `dotnet build CommandCenter.slnx` passes with 0 warnings and 0 errors.

## Current Gaps

- Milestone 2 still lacks dedicated reference helper APIs for decisions, proposals, candidates, governance findings, operational-context revisions, handoffs, execution outputs, and artifacts.
- Workspace projection reasoning summary counts remain unimplemented.
- UI event creation forms, nearby "record reasoning" affordances, and family filters remain unimplemented.
- Manual capture currently exposes backend templates only; no Tauri bridge or UI form has been added for this endpoint.

## Next Slice

- Add workspace projection reasoning summary counts by event family and latest activity, then expose them through existing workspace/dashboard projections.
