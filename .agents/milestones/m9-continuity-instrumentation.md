# Milestone M9 - Continuity Instrumentation

## Objective

Measure continuity quality without making metrics authoritative or automatic.

## Backend Changes

- [ ] Add `IContinuityDiagnosticsService`.
- [ ] Add `IContinuityReportService`.
- [ ] Add `UnderstandingEvolutionLedger`.
- [ ] Compute read-only diagnostics:
  - [ ] Revision count.
  - [ ] Revision frequency.
  - [ ] Current context bytes and characters.
  - [ ] Growth rate.
  - [ ] Architecture preservation changes.
  - [ ] Constraint retention changes.
  - [ ] Decision retention changes.
  - [ ] Rationale retention warnings.
  - [ ] Open question added/resolved/lost counts.
  - [ ] Active risk added/resolved/lost counts.
  - [ ] Compression summary trends.
  - [ ] Repeated investigation indicators.
  - [ ] Repeated question indicators.
  - [ ] Decision rework indicators.
- [ ] Continuity reports can be generated on demand under:

```text
.agents/operational_context/reports/
```

- [ ] Reports are diagnostic artifacts, not workflow gates.

## Explicit Non-Metrics

Do not add productivity or session-routing metrics:

- [ ] Session reuse.
- [ ] Session lifetime as a continuity quality signal.
- [ ] Session routing.
- [ ] Token consumption unless directly tied to context size diagnostics.
- [ ] Commit count.
- [ ] Lines changed.
- [ ] Execution count as quality.
- [ ] Ranking repositories by productivity.
- [ ] Scoring users or providers.

## UI Changes

- [ ] Add read-only `ContinuityDiagnosticsPanel`.
- [ ] Show:
  - [ ] Revision count.
  - [ ] Context growth.
  - [ ] Open question trends.
  - [ ] Risk trends.
  - [ ] Decision preservation trends.
  - [ ] Compression observations.
  - [ ] Continuity warnings.
- [ ] Avoid single numeric quality scores.
- [ ] Avoid auto-correction, auto-rejection, or auto-promotion.

## Tests

Add backend tests:

- [ ] Revision tracking reads current and historical operational contexts.
- [ ] Constraint loss is detected.
- [ ] Decision retention is measured.
- [ ] Rationale loss is warned.
- [ ] Open question resolution is distinguished from disappearance.
- [ ] Compression metrics are calculated from proposal summaries.
- [ ] Repeated investigation indicators can be observed.
- [ ] Report generation writes a diagnostic artifact without mutating current context.

## Certification

Instrumentation is certified when Command Center can answer, from observable evidence:

- [ ] Is understanding improving?
- [ ] Is understanding degrading?
- [ ] Are decisions surviving?
- [ ] Are questions being resolved?
- [ ] Is compression working?
- [ ] Is continuity succeeding?

while preserving explicit user control and review-before-mutation.
