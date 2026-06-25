# Decisions

## Newly Authorized

- Treat execution git interaction normalization as accepted Milestone 9 work because it demonstrates the shared interaction pattern applies beyond decision lifecycles and into operational workflows.
- Keep `ExecutionGitInteractionSummary` as a thin execution-specific wrapper around `InteractionPatternView`.
- Keep `InteractionPatternView` focused on subject, expected result, eligibility, evidence, and diagnostics.
- Preserve execution-specific concepts such as commit scope editing, push metadata, and retry information outside the shared interaction component.
- Preserve the authority boundary where backend `ExecutionGitActionEligibility` owns commit legality, push legality, retry eligibility, diagnostics, and evidence.
- Keep React limited to presenting execution git projections through the shared interaction language.
- Continue using characterization tests scoped to normalized presentation and application integration, without altering execution behavior.
- Continue Milestone 9 interaction normalization with execution recovery actions next.
- Normalize remaining action families in this order: recovery actions, retry actions if distinct from recovery, then decision-session transfer actions.
- Use thin wrappers for execution recovery and decision-session transfer when domain-specific context is needed, without expanding the shared interaction component.
- Complete the major operational action families before moving Milestone 9 into final cleanup and cohesion verification.
