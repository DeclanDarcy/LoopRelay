# Decisions

## Newly Authorized

- Treat the `Git Workflow` to `Git Evidence` terminology cleanup as architecturally consistent and behavior-preserving.
- Continue reserving visible `Workflow` terminology for authoritative operational lifecycle surfaces.
- Keep compatibility anchors such as `git-workflow` when they preserve deep links while visible product language evolves.
- Preserve the regression boundary that workflow stage, gate, and progress must come from Workflow projection, not `RepositoryExecutionState`.
- Allow `RepositoryExecutionState` only for execution-owned display surfaces.
- Continue Milestone 9 with a health and certification presentation audit.
- For each remaining health or certification surface, identify whether it locally renders generic health entries, diagnostics, findings, or evidence.
- Migrate generic health, diagnostics, findings, and evidence renderers to shared presentation components where applicable: `HealthView`, `DiagnosticList`, `EvidenceList`, and `CertificationFindingsView`.
- Retain thin domain wrappers when they provide domain summaries, timelines, navigation, status rollups, or lifecycle framing instead of duplicating generic rendering.
- Classify any local renderer that remains after the audit as intentional, compatibility, technical debt, or retire candidate.
- Treat the rest of Milestone 9 primarily as validation: audit remaining generic renderers, document intentional exceptions, remove obsolete compatibility presentation, and prepare final cohesion verification before Milestone 10.
