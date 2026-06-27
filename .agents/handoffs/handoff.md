# Handoff: After M1.2 Repository Dashboard Dev Tauri Mock Bridge Slice 0075

Current milestone state: M1.2 now has a repository-dashboard generated production-consumer candidate consumed through the production compatibility wrapper and used as the `devTauriMock` dashboard entry return type. Direct generated imports by production UI remain unauthorized outside the existing wrapper boundary.

New state from this slice:

- Changed `src/CommandCenter.UI/src/devTauriMock.ts` so `dashboardEntry(workspace)` returns `RepositoryDashboardConsumerCandidateProjection` imported from the generated repository-dashboard artifact.
- Preserved the existing manual mock payload construction and `list_repositories` behavior.
- Added `ContractConsumerVerificationTests.RepositoryDashboardDevTauriMockUsesGeneratedConsumerCandidateType`.
- Added `.agents/milestones/m1.2-repository-dashboard-dev-tauri-mock-bridge-slice-0075.md`.
- Updated `.agents/milestones/m1.2-generated-contracts.md`, `docs/contracts.md`, `docs/architectural-capabilities.md`, and `docs/architectural-mechanisms.md`.
- Rotated prior `.agents/handoffs/handoff.md` to `.agents/handoffs/handoff.0070.md`.

Verification:

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter FullyQualifiedName~ContractConsumerVerificationTests`: passed, 15 tests.
- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter "FullyQualifiedName~ArchitecturalDecisionGovernanceTests|FullyQualifiedName~ArchitecturalRegressionFrameworkTests"`: passed, 24 tests.
- `npm run build` in `src/CommandCenter.UI`: passed, with existing Vite chunk-size warning.

High-leverage decisions currently relevant:

- The mock bridge is generated-candidate-typed, not generated mock replacement.
- `devTauriMock` remains a development/test consumer and must not become contract authority.
- Production UI direct generated imports remain unauthorized; the production consumption point is still the compatibility wrapper in `src/CommandCenter.UI/src/types/repositories.ts`.
- The next generated-mock slice should keep payload behavior stable and add mechanism strength before retiring manual mock construction.

Recommended next slice:

- Continue M1.2 with a narrow generated mock fixture artifact for repository dashboard, or a mechanical freshness manifest for the current generated-candidate-typed mock bridge if generated output is still too large. The slice should preserve current mock behavior, add stale-artifact detection for the mock boundary, and keep direct production generated imports out of scope.
