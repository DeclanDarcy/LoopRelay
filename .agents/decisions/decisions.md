# Decisions

## Newly Authorized

- Confirm the second Milestone 6 slice as correct.
- Treat package validation before immutable persistence as the correct governance sequence.
- Keep package validation at the `DecisionPackageService` boundary.
- Preserve the responsibility chain: generation builds proposal content, package construction assembles the snapshot, package validation verifies package semantics, immutable persistence stores only valid package versions.
- Require a recommendation or an explicit no-recommendation explanation before package persistence.
- Require preferred recommendations to point to an option inside the package.
- Require recommendation evidence before persisting a package with a selected recommendation.
- Keep fallback generation context in `DecisionGenerationService` narrow and compatibility-focused.
- Do not allow fallback generation context to become a second context construction system.
- Continue Milestone 6 with package comparison next.
- Implement package comparison in this order:
  - recommendation change detection
  - option added/removed/modified detection
  - evidence and risk deltas
  - context fingerprint deltas

## Not Authorized

- Do not move package validation into `DecisionGenerationService`, repository persistence, UI, or client-side logic.
- Do not treat packages as decision authority.
- Do not add quality dashboards, certification, throughput reporting, package authority, or workflow mutation before package infrastructure is complete.
- Do not expand fallback generation context beyond compatibility/test support.
