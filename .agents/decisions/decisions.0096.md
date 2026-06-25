# Decisions

## Newly Authorized

- Treat execution recovery interaction normalization as accepted Milestone 9 work.
- Keep execution recovery observational through backend recovery state and `ExecutionSessionTransparency`.
- Do not introduce a UI-owned execution recovery workflow or React-owned recovery semantics.
- Preserve the authority chain from execution service to recovery projection to `ExecutionRecoveryInteractionSummary` to `InteractionPatternView` to React presentation.
- Keep `InteractionPatternView` stable around subject, expected result, eligibility, evidence, and diagnostics.
- Continue using thin domain-specific wrappers to add execution or governance context without expanding the shared interaction component.
- Treat the execution interaction family as complete for Milestone 9 interaction normalization, covering commit, push, push retry, and recovery.
- Continue Milestone 9 interaction normalization with decision-session transfer actions next.
- Compose transfer-specific context such as readiness, continuity artifacts, ownership context, blocked transfer reasons, and recovery guidance around `InteractionPatternView`.
- After governance transfer normalization, shift Milestone 9 toward interaction consistency audit, information-density refinement, retirement of obsolete presentation, and final product cohesion verification.
