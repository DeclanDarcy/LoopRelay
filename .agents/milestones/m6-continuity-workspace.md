# Milestone 6: Continuity Workspace

## Tracking

- [ ] Milestone complete
- [ ] Workstream 6.1: Understanding Evolution
- [ ] Workstream 6.2: Continuity Warnings
- [ ] Workstream 6.3: Compression Trends
- [ ] Workstream 6.4: Decision, Question, and Risk Lifecycle
- [ ] Workstream 6.5: Reports
- [ ] Workstream 6.6: Continuity Cross-Links
- [ ] Certification complete

Goal: make understanding evolution observable without creating continuity governance.

## Workstream 6.1: Understanding Evolution

Display a table from `ContinuityDiagnostics`:

Columns:

- [ ] Section.
- [ ] Added.
- [ ] Removed.
- [ ] Resolved.
- [ ] Lost.

Rows:

- [ ] Architecture.
- [ ] Constraints.
- [ ] Stable decisions.
- [ ] Rationale.
- [ ] Open questions.
- [ ] Active risks.

Rules:

- [ ] Display observed diagnostic counts.
- [ ] Do not compute a score.
- [ ] Do not add gates.

## Workstream 6.2: Continuity Warnings

Display:

- [ ] Continuity warnings.
- [ ] Compression warnings.
- [ ] Decision/rationale warnings.
- [ ] Repeated investigation indicators.
- [ ] Repeated question indicators.
- [ ] Decision rework indicators.

Warnings are observations. They must not block actions.

## Workstream 6.3: Compression Trends

Display from `compressionTrend`:

- [ ] Proposal count.
- [ ] Compressed item count.
- [ ] Removed item count.
- [ ] Resolved question count.
- [ ] Retired risk count.
- [ ] Warning count.
- [ ] Warnings.
- [ ] Noise removed indicators.

## Workstream 6.4: Decision, Question, and Risk Lifecycle

Display:

- [ ] Stable decisions retained/removed where diagnostics expose it.
- [ ] Rationale preservation and warnings.
- [ ] Questions added/resolved/lost.
- [ ] Risks added/retired/lost.

Use existing trend fields and reports. If a specific lifecycle value is not projected, omit it or mark it unavailable.

## Workstream 6.5: Reports

Display continuity report visibility:

- [ ] Latest report.
- [ ] Report history.
- [ ] Report generated time.
- [ ] Relative path.
- [ ] Diagnostics summary.

Rules:

- [ ] Reports are supporting artifacts.
- [ ] Corrupt or unreadable reports should remain safely ignored by backend behavior.

## Workstream 6.6: Continuity Cross-Links

Add links introduced by the Continuity workspace:

- [ ] Understanding evolution rows navigate to corresponding Operational Context sections.
- [ ] Warning rows navigate to the relevant Continuity subsection when available.
- [ ] Decision warnings navigate to Operational Context stable decisions or decision rationale sections when available.
- [ ] Question lifecycle rows navigate to Operational Context open questions.
- [ ] Risk lifecycle rows navigate to Operational Context active risks.
- [ ] Report paths navigate to artifact/report surfaces when the backend projection exposes a valid relative path.

Rules:

- [ ] Links navigate only.
- [ ] Continuity diagnostics remain observational and never become workflow gates.

### Certification

- [ ] Continuity tab gives complete observable state available from projections.
- [ ] It does not create scores, gates, auto-correction, auto-promotion, or auto-rejection.
- [ ] Continuity links do not mutate lifecycle state.
