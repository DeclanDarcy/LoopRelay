# Milestone 6 Reconstruction Transparency Slice

## Completed

- Extended `ReasoningReconstruction` with structured `ConfidenceRationale` and `Scope` projections.
- `ConfidenceRationale` now reports level, rationale, event evidence presence, relationship evidence presence, trace diagnostic presence, missing evidence, and why confidence was not higher.
- `Scope` now reports direction, target reference, source reference, historical cutoff, reachable evidence, and known unreachable evidence for historical queries.
- Reconstruction report Markdown now includes confidence rationale, scope, missing evidence, confidence blockers, and unreachable evidence.
- TypeScript reasoning types and the dev Tauri mock now expose the new backend response shape.

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter "FullyQualifiedName~ReasoningReconstructionServiceTests"` passed: 8 tests.
- `npm test -- reasoningTrajectory` passed: 1 file, 10 tests.
- `npm run build` passed. Vite still reports the existing large chunk warning.
- `dotnet build CommandCenter.slnx` passed.
- Full backend test run currently shows an order-dependent failure in `ExecutionSessionServiceTests.AcceptAndRejectEndpointsReturnTransitionedSessionMetadata`; the same test passed in isolation.
- Full UI test run currently shows an order-dependent failure in `app.smoke.test.tsx` for commit preparation; the same test passed in isolation.

## Residual Risk

- UI panels still render the old compact confidence surface. The new fields are typed and available, but Milestone 6 UI presentation remains open.
- Known unreachable evidence is currently populated for historical event cutoffs where the service can prove future events. Other unreachable categories remain empty until the graph exposes stronger negative evidence.
