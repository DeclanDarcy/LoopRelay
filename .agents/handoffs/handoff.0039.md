# Handoff: 2026-06-26 After M0.3 Regression Taxonomy Slice 0038

Current milestone state: Milestone 0.3 is in progress. Slice 0038 completed the regression taxonomy output and added an executable taxonomy metadata guard.

New state from this slice:

- Replaced the informal initial taxonomy in `docs/architectural-mechanisms.md` with `### Regression Taxonomy`.
- Taxonomy rows now require category, preferred mechanism, minimum acceptable mechanism, preferred execution phase, owner, severity, evidence, drift model, and remediation.
- Added severity rules for advisory warning, compatibility warning, local build failure, CI failure, and release blocker.
- Extended `tests/CommandCenter.Backend.Tests/Architecture/ArchitecturalRegressionFrameworkTests.cs` with `RegressionTaxonomyDefinesMechanismSelectionMetadata`.
- Added `.agents/milestones/m0.3-regression-taxonomy-slice-0038.md`.
- Updated `.agents/milestones/m0.3-regression-framework.md` to mark regression taxonomy complete.
- Updated `docs/architectural-capabilities.md` to record the taxonomy guard as active M0.3 protection.
- Rotated prior `.agents/handoffs/handoff.md` to `.agents/handoffs/handoff.0038.md`.

Verification:

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter ArchitecturalRegressionFrameworkTests` passed: 5 passed, 0 failed, 0 skipped.
- `git diff --check` passed with line-ending normalization warnings only.

High-leverage decisions currently relevant:

- Regression category selection is now governed by the taxonomy rather than chosen ad hoc per future invariant.
- `Minimum acceptable mechanism` is intentionally distinct from `Preferred mechanism` so temporary documentation or inventory protection remains explicit and cannot masquerade as mature enforcement.
- `Preferred execution phase` is now part of the mechanism-selection metadata, keeping expensive checks out of fast verifier layers unless a later decision justifies escalation.
- M0.3 still is not certified; the taxonomy guard protects category metadata, not the full regression framework.

Recommended next slice:

- Continue M0.3 with the regression ownership and severity model slice. Extract explicit owner surfaces for backend, frontend, shell, cross-layer, Oracle, generated artifacts, build, and CI regressions, formalize severity escalation rules, and add a guard that prevents owner/severity rows from losing remediation and evidence metadata.
