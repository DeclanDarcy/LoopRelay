# Handoff: 2026-06-26 After M0.2 Acceptance Baseline Slice 0035

Current milestone state: Milestone 0.2 is accepted and baselined as a scoped Phase 0 Contract Oracle foundation with explicit deferrals. It is not accepted as full contract-surface coverage.

New state from this slice:

- Added `.agents/milestones/m0.2-oracle-acceptance-baseline-slice-0035.md`.
- Updated `.agents/milestones/m0.2-contract-oracle.md` to record scoped acceptance status and mark the accepted foundation outputs/exit criteria complete with limitations.
- Updated `docs/contracts.md`, `docs/architectural-mechanisms.md`, and `docs/architectural-capabilities.md` from certification-review wording to accepted-baseline wording.
- Rotated prior `.agents/handoffs/handoff.md` to `.agents/handoffs/handoff.0035.md`.

Accepted M0.2 baseline:

- Contract authority is backend-owned projection and command-result shape after backend JSON serialization.
- Fixture gating, drift classification, consumer verification, artifact freshness, request-boundary checks, and Oracle change governance are accepted Phase 0 protections.
- Repository dashboard, repository workspace, and primary workflow projection are the locally certified pilots.
- Full endpoint coverage, generated contracts, mechanical versioning, automatic regeneration, full dependency graph coverage, stream/error/body/query coverage, passive transport certification, and broad semantic reinterpretation detection remain deferred.

Verification:

- No production code, fixtures, generated artifacts, verifier behavior, or consumer behavior changed.
- `git diff --check` passed with line-ending normalization warnings only.

High-leverage decisions currently relevant:

- Move to Milestone 0.3 next; do not reopen M0.2 unless a named uncovered property is discovered.
- Decision lifecycle eligibility remains the preferred fourth Oracle family only if a future review requires deeper backend-owned eligibility semantics.
- Generated contracts, mechanical versioning, and automatic regeneration stay in Milestone 1.2.
- Passive transport and error-envelope preservation stay aligned with Milestone 1.3 and runtime isolation unless an earlier regression-framework need makes them necessary.

Recommended next slice:

- Start Milestone 0.3 with a regression framework inventory/skeleton slice. The first slice should identify existing architecture-facing tests and reusable assertion helpers, define where architectural regression tests live, add one small drift check that reuses current Oracle evidence, update `docs/architectural-mechanisms.md`, and run the narrow verifier for the new regression plus `git diff --check`.
