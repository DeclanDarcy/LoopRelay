# Handoff: 2026-06-26 After M0.4 Regression Weakening Guard Slice 0051

Current milestone state: M0.4 is started but not certified.

New state from this slice:

- Added `ArchitecturalDecisionGovernanceTests.ArchitectureRegressionTestsAreNotDisabledOrFocused`.
- The new guard scans backend architecture xUnit tests for `Fact`/`Theory` `Skip` usage.
- The new guard scans frontend architecture Vitest tests for `.skip` and `.only` usage.
- Added `.agents/milestones/m0.4-regression-weakening-guard-slice-0051.md`.
- Updated `.agents/milestones/m0.4-decision-governance.md` to mark the disabled/focused regression bypass guard as complete under the broader ungoverned-change task.
- Updated `docs/architectural-capabilities.md` and `docs/architectural-mechanisms.md` to record the new guard, scope, and remaining gaps.
- Rotated prior `.agents/handoffs/handoff.md` to `.agents/handoffs/handoff.0049.md`.

Verification:

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter ArchitecturalDecisionGovernanceTests` passed: 5 passed, 0 failed, 0 skipped.
- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter "ArchitecturalRegressionFrameworkTests|ArchitecturalDecisionGovernanceTests"` passed: 19 passed, 0 failed, 0 skipped.
- `git diff --check` passed with line-ending normalization warnings only.

High-leverage decisions currently relevant:

- M0.4 now has initial executable regression-weakening enforcement, but only for disabled or focused architecture tests.
- Disabled/focused architecture regressions are treated as mechanism weakening and require governance rather than hidden test-runner behavior.
- This slice does not authorize M0.4 certification; active decision/evidence validation and broader ungoverned-change detection remain open.

Recommended next slice:

- Continue M0.4 by adding new shell response mirror detection against `src/CommandCenter.Shell/src/main.rs` and `docs/shell-transport-classification.md`, so new Rust backend-shaped mirrors cannot appear without governance.
