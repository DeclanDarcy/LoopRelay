# Decisions: 2026-06-26 Slice 0020 Repository Workspace Repeatability

These decisions capture only newly authorized direction from the response accepting Slice 0020 as a fixture-repeatability slice.

## Authorized Decisions

1. Treat Slice 0020 as evidence of Oracle repeatability, not merely as another fixture.
   - The key architectural result is that repository workspace reused the same Oracle design as repository dashboard.
   - No second Oracle mechanism or contract-family-specific design is authorized.

2. Preserve the repository workspace maturity posture.
   - Repository workspace currently has fixture comparison only.
   - Repository workspace is not locally certified.
   - Milestone 0.2 remains active and uncertified globally.

3. Keep backend serialization as contract authority.
   - Rust, TypeScript, and dev mock representations remain downstream consumers.
   - The Rust `RepositoryWorkspaceProjection` omission of `decisionSessionSummary` remains consumer drift, not Oracle authority.

4. Complete repository workspace through the same lifecycle order as repository dashboard.
   - Rust consumer verification.
   - TypeScript consumer verification.
   - Dev Tauri mock verification.
   - Artifact freshness.
   - Request-boundary verification where applicable.
   - Local certification.

5. Track mechanism reuse explicitly while completing repository workspace.
   - Future workspace evidence should record which dashboard mechanisms were reused unchanged.
   - The mechanism reuse table should distinguish completed reuse from pending reuse across fixture comparison, recursive comparison, drift classification, consumer verification, artifact freshness, and request-boundary verification.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0020 and this decision checkpoint.
2. Stop executing after the checkpoint.
3. In the next work slice, begin repository workspace consumer verification, starting with Rust consumer verification.
