# Milestone 6: Continuity Workspace

## Tracking

- [x] Milestone complete
- [x] Workstream 6.1: Understanding Evolution
- [x] Workstream 6.2: Continuity Warnings
- [x] Workstream 6.3: Compression Trends
- [x] Workstream 6.4: Decision, Question, and Risk Lifecycle
- [x] Workstream 6.5: Reports
- [x] Workstream 6.6: Continuity Cross-Links
- [x] Certification complete

Goal: make understanding evolution observable without creating continuity governance.

## Workstream 6.1: Understanding Evolution

Display a table from `ContinuityDiagnostics`:

Columns:

- [x] Section.
- [x] Added.
- [x] Removed.
- [x] Resolved.
- [x] Lost.

Rows:

- [x] Architecture.
- [x] Constraints.
- [x] Stable decisions.
- [x] Rationale.
- [x] Open questions.
- [x] Active risks.

Rules:

- [x] Display observed diagnostic counts.
- [x] Do not compute a score.
- [x] Do not add gates.

## Workstream 6.2: Continuity Warnings

Display:

- [x] Continuity warnings.
- [x] Compression warnings.
- [x] Decision/rationale warnings.
- [x] Repeated investigation indicators.
- [x] Repeated question indicators.
- [x] Decision rework indicators.

Warnings are observations. They must not block actions.

## Workstream 6.3: Compression Trends

Display from `compressionTrend`:

- [x] Proposal count.
- [x] Compressed item count.
- [x] Removed item count.
- [x] Resolved question count.
- [x] Retired risk count.
- [x] Warning count.
- [x] Warnings.
- [x] Noise removed indicators.

## Workstream 6.4: Decision, Question, and Risk Lifecycle

Display:

- [x] Stable decisions retained/removed where diagnostics expose it.
- [x] Rationale preservation and warnings.
- [x] Questions added/resolved/lost.
- [x] Risks added/retired/lost.

Use existing trend fields and reports. If a specific lifecycle value is not projected, omit it or mark it unavailable.

## Workstream 6.5: Reports

Display continuity report visibility:

- [x] Latest report.
- [x] Report history.
- [x] Report generated time.
- [x] Relative path.
- [x] Diagnostics summary.

Rules:

- [x] Reports are supporting artifacts.
- [x] Corrupt or unreadable reports should remain safely ignored by backend behavior.

## Workstream 6.6: Continuity Cross-Links

Add links introduced by the Continuity workspace:

- [x] Understanding evolution rows navigate to corresponding Operational Context sections.
- [x] Warning rows navigate to the relevant Continuity subsection when available.
- [x] Decision warnings navigate to Operational Context stable decisions or decision rationale sections when available.
- [x] Question lifecycle rows navigate to Operational Context open questions.
- [x] Risk lifecycle rows navigate to Operational Context active risks.
- [x] Report paths navigate to artifact/report surfaces when the backend projection exposes a valid relative path.

Rules:

- [x] Links navigate only.
- [x] Continuity diagnostics remain observational and never become workflow gates.

### Certification

- [x] Continuity tab gives complete observable state available from projections.
- [x] It does not create scores, gates, auto-correction, auto-promotion, or auto-rejection.
- [x] Continuity links do not mutate lifecycle state.
