# Decisions: 2026-06-27 M1.2 Dev Mock Bridge Publication And Freshness Direction

These decisions capture only newly authorized direction from the user response after the M1.2 repository-dashboard dev Tauri mock bridge slice.

## Authorized Decisions

1. Accept the Slice 0075 architectural classification of `devTauriMock` as a development/test consumer of the generated repository-dashboard candidate.
   - `devTauriMock` must not become contract authority.
   - The generated candidate may be consumed by the mock bridge only as a verified downstream consumer shape.
   - Production direct generated imports remain unauthorized outside the existing compatibility-wrapper boundary.

2. Prioritize a freshness manifest for the generated-candidate-typed mock bridge before generated repository-dashboard mock output.
   - The next M1.2 slice should first make silent drift between the generated candidate, mock implementation, and production compatibility wrapper executable.
   - Generated mock artifact output should wait until the bridge freshness mechanism exists or is explicitly superseded by later evidence.

3. Keep freshness verification independently scoped by boundary.
   - Generated TypeScript consumer candidates, production compatibility wrappers, dev mock bridges, and future generated mock artifacts should have localized freshness verification.
   - A stale mock bridge should not invalidate the production generated-candidate pipeline unless the shared source artifact itself is stale.

4. Publish the completed M1.2 repository-dashboard dev Tauri mock bridge slice.
   - Stage the Slice 0075 mock bridge, verifier, evidence, docs, handoff rotation, and this decision rotation.
   - Exclude unrelated pre-existing dirty files, including `src/CommandCenter.DecisionSessions/CommandCenter.DecisionSessions.csproj`, `design.md`, and `refactor-readiness.md`.
   - Commit and push to `origin/dev`.
   - Stop executing after the push.

## Evidence Targets

- `.agents/decisions/decisions.0075.md`
- `.agents/decisions/decisions.md`
- `.agents/handoffs/handoff.md`
- `.agents/handoffs/handoff.0070.md`
- `.agents/milestones/m1.2-generated-contracts.md`
- `.agents/milestones/m1.2-repository-dashboard-dev-tauri-mock-bridge-slice-0075.md`
- `.agents/milestones/m0.4-referential-governance-validation-slice-0054.md`
- `docs/contracts.md`
- `docs/architectural-capabilities.md`
- `docs/architectural-mechanisms.md`
- `src/CommandCenter.UI/src/devTauriMock.ts`
- `tests/CommandCenter.Backend.Tests/ContractConsumerVerificationTests.cs`

## Next Authorized Sequence

1. Re-run the architectural decision governance guard.
2. Stage only the completed Slice 0075 files, handoff rotation, and this decision rotation.
3. Commit and push to `origin/dev`.
4. Stop executing after the push.
