# Milestone M9 - Continuity Instrumentation

## Objective

Measure continuity quality without making metrics authoritative or automatic.

## Backend Changes

- [x] Add `IContinuityDiagnosticsService`.
- [x] Add `IContinuityReportService`.
- [x] Add `UnderstandingEvolutionLedger`.
- [x] Compute read-only diagnostics:
  - [x] Revision count.
  - [x] Revision frequency.
  - [x] Current context bytes and characters.
  - [x] Growth rate.
  - [x] Architecture preservation changes.
  - [x] Constraint retention changes.
  - [x] Decision retention changes.
  - [x] Rationale retention warnings.
  - [x] Open question added/resolved/lost counts.
  - [x] Active risk added/resolved/lost counts.
  - [x] Compression summary trends.
  - [x] Repeated investigation indicators.
  - [x] Repeated question indicators.
  - [x] Decision rework indicators.
- [x] Continuity reports can be generated on demand under:

```text
.agents/operational_context/reports/
```

- [x] Reports are diagnostic artifacts, not workflow gates.

## Explicit Non-Metrics

Do not add productivity or session-routing metrics:

- [x] Session reuse.
- [x] Session lifetime as a continuity quality signal.
- [x] Session routing.
- [x] Token consumption unless directly tied to context size diagnostics.
- [x] Commit count.
- [x] Lines changed.
- [x] Execution count as quality.
- [x] Ranking repositories by productivity.
- [x] Scoring users or providers.

## UI Changes

- [x] Add read-only `ContinuityDiagnosticsPanel`.
- [x] Show:
  - [x] Revision count.
  - [x] Context growth.
  - [x] Open question trends.
  - [x] Risk trends.
  - [x] Decision preservation trends.
  - [x] Compression observations.
  - [x] Continuity warnings.
- [x] Avoid single numeric quality scores.
- [x] Avoid auto-correction, auto-rejection, or auto-promotion.

## Tests

Add backend tests:

- [x] Revision tracking reads current and historical operational contexts.
- [x] Constraint loss is detected.
- [x] Decision retention is measured.
- [x] Rationale loss is warned.
- [x] Open question resolution is distinguished from disappearance.
- [x] Compression metrics are calculated from proposal summaries.
- [x] Repeated investigation indicators can be observed.
- [x] Report generation writes a diagnostic artifact without mutating current context.

## Certification

Instrumentation is certified when Command Center can answer, from observable evidence:

- [x] Is understanding improving?
- [x] Is understanding degrading?
- [x] Are decisions surviving?
- [x] Are questions being resolved?
- [x] Is compression working?
- [x] Is continuity succeeding?

while preserving explicit user control and review-before-mutation.
