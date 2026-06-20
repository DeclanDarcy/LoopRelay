# Decisions

## Newly Authorized Decisions

- Epic 3 is functionally complete after M9 unless a repository-wide review uncovers major architectural defects.
- The central Epic 3 hypothesis is validated: current understanding can be generated, reviewed, promoted, compressed, preserved, observed, and certified while remaining repository-owned, artifact-mediated, deterministic, and review-governed.
- Continuity must continue to mean artifact-mediated understanding, not long-lived sessions, provider memory, decision sessions, project journals, or historical replay.
- `.agents/operational_context.md` remains the authority for current understanding; proposals, reports, UI projections, and diagnostics remain supporting artifacts or observations.
- Backend projections remain authoritative for workspace continuity state, and the UI remains a projection surface rather than a governance, decision-resolution, or operational-context authority surface.
- The generate-review-accept-promote boundary remains important; accepting a proposal must not collapse into promotion.
- M9 diagnostics remain observational only and must not introduce continuity scores, automatic promotion, automatic rejection, automatic correction, or workflow gates.
- The strongest Epic 3 proof is the combination of compression, decision continuity, and long-horizon certification, because it demonstrates understanding preservation across repeated evolution rather than only storage.
- Do not add more roadmap work until a repository-wide review audits continuity authority boundaries, stale artifact behavior, documentation alignment, and UI density.

## Recommended Next Slice

Run a repository-wide review focused on:

- Ensuring proposals and reports cannot become authoritative through promotion, reload, startup recovery, or projection construction paths.
- Pressure-testing accepted-then-stale, promoted-then-replaced, missing historical revision, corrupt report, orphan proposal, and interrupted promotion cases.
- Aligning `architecture.md`, `operational-context-schema.md`, milestone docs, and certification docs around the same continuity model.
- Checking that the UI remains an execution-supporting understanding surface rather than drifting into a continuity dashboard.
