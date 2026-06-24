# Milestone 2: Workflow Persistence and Recovery

Objective: make workflow evidence durable and recoverable without making it authoritative.

Deliver:

- [x] `WorkflowTimeline` with repository id, current stage, previous stage, entries, generated timestamp, and fingerprint.
- [x] `WorkflowTimelineEntry` with timestamp, stage, event type, reason, source domain, source artifact, and fingerprint.
- [x] `IWorkflowRepository` with save/load/list/latest timeline operations plus report persistence.
- [x] `IWorkflowRecoveryService` with rebuild timeline, recover current workflow, and validate recovered workflow operations.
- [x] `WorkflowFingerprint` that includes current stage, timeline count, last timeline entry, blocking conditions, and gate state.
- [x] `WorkflowRecoveryDiagnostics`.
- [x] `WorkflowHistoryProjection` with timeline, gate history, progress summary, and recovery summary.
- [x] `WorkflowRecoveryHostedService` that runs on application startup only.

Recovery rules:

- [x] If workflow artifacts exist, load and validate them against domain evidence.
- [x] If workflow artifacts are missing, rebuild from domain evidence.
- [x] If workflow artifacts conflict with domain evidence, discard workflow view and rebuild.
- [x] If workflow artifacts are partially corrupt, recover only what can be proven and record diagnostics.
- [x] Domain artifacts always win.

Tests:

- [x] timeline saves, loads, and lists.
- [x] latest timeline lookup works.
- [x] missing workflow artifacts rebuild from domains.
- [x] corrupt workflow artifacts rebuild from domains.
- [x] fingerprints are stable for identical evidence.
- [x] fingerprints detect divergence.
- [x] restart recovery restores workflow evidence.
- [x] recovery never mutates execution, decisions, continuity, or git.

Exit criteria:

- [x] workflow timeline persists.
- [x] recovery works.
- [x] rebuild works.
- [x] fingerprinting works.
- [x] recovery diagnostics exist.
- [x] hosted startup recovery works.
