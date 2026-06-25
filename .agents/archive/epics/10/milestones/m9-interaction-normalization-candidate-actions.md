# Milestone 9 Evidence: Candidate Action Interaction Normalization

## Scope

- Normalized candidate promote, dismiss, expire, duplicate, and proposal generation action presentation through `InteractionPatternView`.
- Preserved backend-owned lifecycle eligibility and existing command handlers.
- Removed the local candidate lifecycle eligibility renderer from `DecisionCandidateBrowser`.
- Kept candidate selection and duplicate target selection as local presentation/input state only.

## Implementation

- `DecisionCandidateBrowser` now renders selected candidate action subject, command result, action eligibility, interaction evidence, and diagnostics with the shared explainability interaction pattern.
- Candidate interaction evidence includes current state, priority, classification, allowed next states, blocked next states, selected duplicate target, last transition reason, candidate evidence, and source references.
- Existing action buttons still use backend-projected eligibility for disabled state and title text.

## Verification

- `npm test -- decisionCandidateBrowser.test.tsx`
- `npm test -- decisionCandidateBrowser.test.tsx decisionLifecycleNavigation.test.tsx decisionProposalViewer.test.tsx`

## Residual Risk

- The generation result panel remains a proposal-output-specific summary; it was not folded into `InteractionPatternView` because it renders generated proposal diagnostics, option counts, and validation results after command execution.
- Refinement, resolution, execution commit/push, recovery, and transfer action families still need normalization review.
