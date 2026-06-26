# Handoff: 2026-06-26 After Workflow Request Boundary Slice 0030

Current milestone state: Milestone 0.2 remains active and uncertified. The primary workflow projection contract now has backend fixture comparison, TypeScript response consumer verification, and request-boundary verification for the represented fixture/endpoint variant.

New state from this slice:

- Added `.agents/milestones/m0.2-workflow-request-boundary-slice-0030.md`.
- Extended `tests/CommandCenter.Backend.Tests/ContractRequestBoundaryTests.cs` with workflow request-boundary coverage.
- The new backend test verifies `GET /api/repositories/{repositoryId:guid}/workflow` has one required GUID route parameter and no body metadata.
- The new Rust test verifies `get_workflow_projection(repository_id: String) -> Result<Value, String>` forwards through `backend_get_value` to `/api/repositories/{repository_id}/workflow` without constructing a request body.
- The new TypeScript test verifies `getWorkflowProjection(repositoryId)` invokes `get_workflow_projection` with only `{ repositoryId }`.
- Updated `docs/contracts.md`, `docs/architectural-mechanisms.md`, and `docs/architectural-capabilities.md` to record workflow request-boundary verification status.
- Rotated prior `.agents/handoffs/handoff.md` to `.agents/handoffs/handoff.0030.md`.

Verification:

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter FullyQualifiedName~ContractRequestBoundaryTests`
- Result: passed, 9 tests.

Current limits:

- No workflow artifact freshness manifest exists.
- No populated `decisionSession` fixture variant exists.
- No sibling workflow endpoint fixtures or request-boundary verifiers exist.
- No dev mock workflow handler was implemented or verified; `devTauriMock.ts` still lacks `get_workflow_projection` / `get_workflow_*` handler coverage.
- No local workflow Oracle certification exists.
- Existing untracked `docs/audits/` content existed before this slice and was left untouched.

High-leverage decisions currently relevant:

- Workflow request-boundary verification protects request shape only; it does not certify workflow response shape beyond the existing fixture and TypeScript consumer checks.
- Rust workflow projection remains passive transport via `serde_json::Value` and `backend_get_value`; do not introduce a Rust `WorkflowInstance` mirror for this path.
- The backend serialized workflow fixture remains Oracle truth; TypeScript and Rust checks remain downstream verifiers.
- The workflow family is now closer to the repository pilot lifecycle, but local workflow Oracle certification should wait for artifact freshness or an explicit decision to certify without it.

Recommended next slice:

- Add a workflow artifact freshness manifest for `tests/CommandCenter.Backend.Tests/ContractFixtures/workflow-instance.golden.json` and `src/CommandCenter.UI/src/types/workflow.ts`, then extend `ContractGeneratedArtifactFreshnessTests` through the existing manifest-driven mechanism. After that, run a local workflow Oracle certification slice or explicitly decide whether dev mock/populated `decisionSession` coverage must precede certification.
