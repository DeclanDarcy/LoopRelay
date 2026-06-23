# Decisions

## Newly Authorized

- Continue using `DecisionEvolution` plus `EvidenceAdded` for decision archival capture rather than introducing a `DecisionArchived` reasoning event type.
- Treat archival relationships as unnecessary unless they communicate a new explanatory edge not already present in the event references and provenance.
- Preserve the post-authoritative-transition boundary for all inferred capture: source-domain transition succeeds first, reasoning observes afterward.
- Treat the current inferred-capture architecture as proven for Milestone 2 across decision supersession, proposal resolution, and decision archival.
- Proceed next with governance contradiction capture, but model the meaningful reasoning event as contradiction observation rather than governance report generation.
- Use governance reports as provenance, references, and evidence sources for contradiction-related reasoning events.
- Treat event inflation as the main current architecture risk for upcoming Milestone 2 work.
- Before adding any new event type, first test whether an existing event family/type plus richer provenance can represent the capture.
- Prefer reusing existing reasoning vocabulary unless the source-domain observation cannot be represented accurately without a new event type.
