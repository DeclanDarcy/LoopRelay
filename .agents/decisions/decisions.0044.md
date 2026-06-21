# Decisions

## Newly Authorized

- Treat negative extraction decisions as first-class M0.5 outcomes when an audit identifies an authority boundary.
- Record `DecisionContinuityReview` staying in `App.tsx` as the more important result of the previous slice because it confirmed acceptance-oriented guidance and proposal review coordination should not be extracted as neutral presentation.
- Preserve the distinction between operational-context evidence displays and review or acceptance authority.
- Classify `OperationalContextProposalSummaryPanel`, `OperationalContextCompressionSummaryPanel`, `OperationalContextProposalStatusPanel`, `OperationalContextSemanticChangeList`, and `OperationalContextProposalComparison` as evidence-display components while they remain `props -> render`.
- Keep proposal loading, proposal generation, draft ownership, review notes, comparison coordination, accept, reject, promote, and decision-continuity review centralized unless a future audit proves a narrower neutral display boundary.
- Continue with the generated-handoff review audit next, split into neutral content display versus workflow decision support.
- Authorize extraction from generated-handoff review only for neutral content display such as generated markdown, handoff body, metadata, timestamps, author information, or summary display.
- Keep generated-handoff accept readiness, confirmation, generation status, approval state, and backend commands in `App.tsx`.
- Use the late-M0.5 heuristic: would the component still be useful if every button disappeared?
- Treat M0.5 as effectively complete once audits mostly prove the remaining code owns authority, even if isolated JSX fragments remain in `App.tsx`.
