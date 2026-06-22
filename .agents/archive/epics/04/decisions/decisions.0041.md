# Decisions

## Newly Authorized

- Treat `OperationalContextProposalSummaryPanel` and `OperationalContextCompressionSummaryPanel` as safe M0.5 extractions because they sit on the observation side of the proposal workflow: backend proposal/compression projections flow directly into summary display.
- Use the same extraction test that justified `SelectedRepositorySummary`, `ArtifactMetadata`, and `ArtifactMarkdownPreview`: the component remains meaningful if generate, accept, reject, promote, edit, and note-saving actions disappear.
- Classify remaining operational-context proposal surfaces into three groups: read-only projection, review support, and workflow authority.
- Treat proposal summary, compression summary, proposal metadata, status display, timestamps, archive indicators, and failure notices as read-only projection surfaces and good extraction candidates when they remain `props -> render`.
- Treat semantic changes, continuity deltas, and comparison summaries as review-support surfaces that require focused audits before extraction because they may be coupled to acceptance reasoning.
- Keep proposal generation, proposal loading, draft ownership, review notes, accept, reject, promote, and comparison coordination centralized in `App.tsx` as workflow authority.
- Prefer the next M0.5 slice to target proposal metadata plus stale/archive/write-failure notice surfaces rather than semantic review, decision-continuity review, or comparison rendering.
- Apply the stricter late-M0.5 rule for proposal review extraction: if accept, reject, and promote disappear, the section must still have standalone value to be an extraction candidate.
- Expect future late-M0.5 slices to move fewer lines while requiring more audit effort; this is acceptable evidence that remaining `App.tsx` code is increasingly legitimate coordination logic.
