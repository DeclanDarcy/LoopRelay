# Milestone 2: Workflow Persistence and Recovery

Objective: make workflow evidence durable and recoverable without making it authoritative.

Deliver:

- [ ] `WorkflowTimeline` with repository id, current stage, previous stage, entries, generated timestamp, and fingerprint.
- [ ] `WorkflowTimelineEntry` with timestamp, stage, event type, reason, source domain, source artifact, and fingerprint.
- [ ] `IWorkflowRepository` with save/load/list/latest timeline operations plus report persistence.
- [ ] `IWorkflowRecoveryService` with rebuild timeline, recover current workflow, and validate recovered workflow operations.
- [ ] `WorkflowFingerprint` that includes current stage, timeline count, last timeline entry, blocking conditions, and gate state.
- [ ] `WorkflowRecoveryDiagnostics`.
- [ ] `WorkflowHistoryProjection` with timeline, gate history, progress summary, and recovery summary.
- [ ] `WorkflowRecoveryHostedService` that runs on application startup only.

Recovery rules:

- [ ] If workflow artifacts exist, load and validate them against domain evidence.
- [ ] If workflow artifacts are missing, rebuild from domain evidence.
- [ ] If workflow artifacts conflict with domain evidence, discard workflow view and rebuild.
- [ ] If workflow artifacts are partially corrupt, recover only what can be proven and record diagnostics.
- [ ] Domain artifacts always win.

Tests:

- [ ] timeline saves, loads, and lists.
- [ ] latest timeline lookup works.
- [ ] missing workflow artifacts rebuild from domains.
- [ ] corrupt workflow artifacts rebuild from domains.
- [ ] fingerprints are stable for identical evidence.
- [ ] fingerprints detect divergence.
- [ ] restart recovery restores workflow evidence.
- [ ] recovery never mutates execution, decisions, continuity, or git.

Exit criteria:

- [ ] workflow timeline persists.
- [ ] recovery works.
- [ ] rebuild works.
- [ ] fingerprinting works.
- [ ] recovery diagnostics exist.
- [ ] hosted startup recovery works.
