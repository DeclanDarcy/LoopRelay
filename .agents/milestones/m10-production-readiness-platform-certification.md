# Phase 10 - Production Readiness and Platform Certification

Goal: harden the complete platform for daily use.

## Implementation

- [ ] Certify the full repository lifecycle:
  - register/select repository
  - plan
  - execute
  - decide
  - review
  - continue
  - transfer/recover
  - complete
  - inspect historical knowledge
- [ ] Harden runtime reliability:
  - process stability
  - resource cleanup
  - per-repository isolation
  - memory pressure behavior
  - cancellation and shutdown
  - long-lived sessions
  - deterministic failure recovery
- [ ] Complete ecosystem integration:
  - Git
  - filesystem
  - Codex
  - Tauri
  - IDE-adjacent workflows
  - repository discovery
  - prompt generation through `Lib.Prompts`
  - generated prompt provenance
- [ ] Add production observability:
  - repository activity
  - conversation health
  - runtime health
  - session health
  - prompt source-hash compatibility
  - execution progress
  - decision progress
  - understanding evolution
  - failure and recovery state
- [ ] Add operational tooling:
  - runtime diagnostics
  - repository diagnostics
  - session diagnostics
  - recovery controls
  - health dashboards
  - export/support bundles
- [ ] Complete documentation:
  - architecture guide
  - runtime guide
  - prompt architecture guide
  - information guide
  - protocol guide
  - user guide
  - operator guide
  - recovery guide
  - extension guide
- [ ] Complete release-path certification, including packaging and shell sidecar lifecycle.
- [ ] Complete prompt release-path certification:
  - `Lib.Prompts` analyzer wiring works from package or governed local project reference
  - every canonical `.prompt` file is discovered during build
  - generated `Template`, `SourceHash`, and `Render(...)` APIs are stable for consumers
  - prompt source-hash drift is surfaced as diagnostics for historical artifacts
  - no runtime path contains duplicated canonical prompt text

## Certification

- [ ] Full backend, frontend, E2E, shell, contract, architecture, stress, recovery, scalability, and governance suites pass.
- [ ] Prompt generation, prompt selection, prompt provenance, source-hash drift, and no-literal-prompt governance suites pass.
- [ ] Failure tests cover restart, session loss, transfer interruption, corrupted/partial context, repository reload, cancellation, and shutdown.
- [ ] Runtime failures never corrupt repository understanding.
- [ ] Prompt generation or prompt-source mismatch failures never corrupt plans, runs, decisions, operational context, or Repository Understanding.
- [ ] Operational documentation matches implemented behavior and recovery paths.
