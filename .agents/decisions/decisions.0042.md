# Decisions

## Newly Authorized

- Treat the current late-M0.5 operational-context extraction sequence as valid because `OperationalContextCurrentPanel`, `OperationalContextProposalSummaryPanel`, `OperationalContextCompressionSummaryPanel`, and `OperationalContextProposalStatusPanel` all preserve the invariant `backend projection -> props -> render`.
- Continue excluding loading, generation, drafts, review notes, acceptance, rejection, promotion, and comparison orchestration from extracted operational-context presentation components.
- Classify the remaining operational-context proposal-review surface into read-only status/metadata, review evidence, and workflow authority.
- Treat read-only status/metadata as mostly harvested for this area.
- Treat review evidence as potentially harvestable only after explicit audit.
- Use semantic-change rendering as the highest-probability next M0.5 audit target.
- Apply the stricter semantic-change audit question: does the candidate merely render an already-derived list of changes, or does it perform grouping, classification, severity assignment, readiness interpretation, or change-significance determination?
- Extract semantic-change rendering only if it remains pure `props -> render`; leave it in `App.tsx` or a future authority owner if it participates in acceptance interpretation, readiness determination, or decision-support logic.
- Treat decision-continuity review as higher risk than semantic-change rendering because it directly supports workflow decisions.
- Apply the standalone-report test to decision-continuity review: if accept/reject/promote disappear and the section remains useful as evidence display, it may be an extraction candidate; otherwise keep it centralized.
- Consider clean `npm run test:e2e` and `dotnet test CommandCenter.slnx` after small presentation-only extractions as evidence that M0.6 characterization is doing its job.
- Recognize that late M0.5 is approaching completion when remaining extraction candidates require authority-boundary audits rather than straightforward component movement.
