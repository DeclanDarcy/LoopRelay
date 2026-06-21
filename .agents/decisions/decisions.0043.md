# Decisions

## Newly Authorized

- Treat the operational-context semantic-change extraction as architecturally significant because it proved the remaining proposal-review surface can be audited for presentation-only evidence rather than frontend interpretation.
- Classify extracted operational-context proposal panels as evidence displays only when they remain `props -> render` with no workflow ownership.
- Keep proposal loading, proposal generation, draft ownership, review notes, accept, reject, promote, and comparison coordination centralized in `App.tsx` as authority-bearing responsibilities for M0.
- Treat decision-continuity review as the pivotal next audit target.
- Audit decision-continuity review by first asking what responsibility the block owns, not whether it can become a component.
- Authorize extraction of decision-continuity review only if it is read-only evidence display with no interpretation, score, recommendation, risk assessment, decision-quality assessment, acceptance guidance, or workflow support.
- If decision-continuity review participates in review reasoning or acceptance-oriented interpretation, leave it in `App.tsx` until a future authority owner exists.
- Treat a deliberate non-extraction after audit as a successful M0.5 outcome when it shows the remaining code is coordination or authority logic rather than overlooked presentation.
- Continue using the repeated verification pattern of extraction, characterization, full frontend suite, e2e suite, and backend suite as evidence that authority boundaries are functioning.
