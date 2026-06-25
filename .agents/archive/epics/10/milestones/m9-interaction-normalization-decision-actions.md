# Milestone 9 Evidence: Decision Action Interaction Normalization

## Scope

- Continued Milestone 9 interaction normalization with resolved decision supersede and archive actions.
- Reused `InteractionPatternView` for the resolved decision action panel.
- Replaced the local decision eligibility block with a normalized interaction summary covering action subject, result, eligibility, evidence, and diagnostics.
- Preserved backend-owned lifecycle eligibility and the existing supersede/archive command handlers.

## Authoritative Inputs

- Decision lifecycle eligibility remains sourced from `DecisionLifecycleEligibilityProjection`.
- Action availability remains adapted through `decisionLifecycleEligibilityToActions`.
- Diagnostics remain adapted through `decisionDiagnosticsToExplanation`.
- Current decision state, allowed/blocked next states, and selected replacement decision evidence are rendered from lifecycle eligibility and user-selected command input.

## Verification

- `npm test -- decisionLifecycleNavigation.test.tsx`

## Residual Risk

- Candidate generation/promote/dismiss/expire/duplicate actions still need the same interaction summary pattern.
- Decision refinement, resolution, execution commit/push, recovery, and transfer action families remain to be normalized.
