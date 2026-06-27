# Phase 10 - Hardening and Certification

Goal: burn down the concrete risks called out by the design before enabling the flow by default.

## Implementation

- [ ] Validate the exact Codex effort config values for ExtraHigh and Medium.
- [ ] Validate the strictest practical Decision sandbox: read-only, approvals never, and no MCP/tools where supported.
- [ ] Validate persistent process behavior under long output, idle periods, repeated turns, cancellation, failed prompts, and application shutdown.
- [ ] Add feature flags for:
  - [ ] persistent planning process;
  - [ ] persistent Decision process reuse;
  - [ ] transfer-only Decision fallback;
  - [ ] automatic commit/push after Execute Plan.
- [ ] Decide and document whether automatic commit/push needs an explicit user confirmation gate before default enablement.
- [ ] Add recovery tests around multi-write windows:
  - [ ] specs written but plan missing;
  - [ ] plan exists but milestones missing;
  - [ ] operational context copied but commit failed;
  - [ ] handoff exists but rotation failed;
  - [ ] decisions persisted but continuation failed;
  - [ ] operational delta exists but context update failed.
- [ ] Add process leak detection and cleanup on repository deselection, cancellation, failure, and shutdown.
- [ ] Keep deterministic decision services and token estimator as tested fallback behavior.
- [ ] Add stress tests for repeated decision loops and transfer cycles.

## Certification

- [ ] Full backend tests pass.
- [ ] Relevant UI build, lint, unit, and E2E tests pass.
- [ ] Shell tests pass if shell or sidecar behavior changed.
- [ ] Contract and architecture suites pass.
- [ ] No failed or cancelled run leaves orphaned Codex processes.
- [ ] No failed or cancelled run corrupts repository artifacts.
- [ ] Feature flags and fallback paths are documented and tested.
