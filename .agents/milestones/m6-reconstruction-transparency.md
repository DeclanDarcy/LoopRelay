# Milestone 6 Reconstruction Transparency Slice

## Completed

- Extended `ReasoningReconstruction` with structured `ConfidenceRationale` and `Scope` projections.
- `ConfidenceRationale` now reports level, rationale, event evidence presence, relationship evidence presence, trace diagnostic presence, missing evidence, and why confidence was not higher.
- `Scope` now reports direction, target reference, source reference, historical cutoff, reachable evidence, and known unreachable evidence for historical queries.
- Reconstruction report Markdown now includes confidence rationale, scope, missing evidence, confidence blockers, and unreachable evidence.
- TypeScript reasoning types and the dev Tauri mock now expose the new backend response shape.
- `ReasoningReconstructionPanel` now renders backend-owned confidence rationale, missing evidence, confidence blockers, reconstruction scope, reachable evidence, and known unreachable evidence.
- `ReasoningQueryPanel` now summarizes the same backend-owned confidence and scope fields in query results.
- Reasoning trajectory characterization coverage now includes high-confidence reconstruction, limited-confidence reconstruction, missing evidence, confidence blockers, forward/backward direction, historical cutoff, and known unreachable evidence.

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter "FullyQualifiedName~ReasoningReconstructionServiceTests"` passed: 8 tests.
- `npm test -- reasoningTrajectory` passed: 1 file, 10 tests.
- `npm test -- reasoningTrajectory` passed after UI transparency work: 1 file, 11 tests.
- `npm run build` passed. Vite still reports the existing large chunk warning.
- `npm run build` passed after UI transparency work. Vite still reports the existing large chunk warning.
- `dotnet build CommandCenter.slnx` passed.
- Full backend test run currently shows an order-dependent failure in `ExecutionSessionServiceTests.AcceptAndRejectEndpointsReturnTransitionedSessionMetadata`; the same test passed in isolation.
- Full UI test run currently shows an order-dependent failure in `app.smoke.test.tsx` for commit preparation; the same test passed in isolation.

## Residual Risk

- Known unreachable evidence is currently populated for historical event cutoffs where the service can prove future events. Other unreachable categories remain empty until the graph exposes stronger negative evidence.
- Milestone 6 still needs a broader pass over reasoning transparency surfaces outside reconstruction/query, including report history and any product-cohesion affordances planned for later milestones.
