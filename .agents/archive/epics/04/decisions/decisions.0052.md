# Decisions

## Newly Authorized

- Treat Workstream 1.5 as nearing diminishing returns now that `App.tsx` render-only surfaces have adopted shared primitives while workflow actions, workflow buttons, backend transitions, and readiness logic remain untouched.
- Evaluate any further primitive adoption against a stricter standard: it should reduce real presentation duplication, not merely increase primitive usage counts.
- Recognize the current target architecture as `Design System -> Render Primitives`, `Features -> Presentation Composition`, and `App.tsx -> Workflow Coordination / Mutation Authority / Readiness Authority`.
- Reject design-system primitive changes that make primitives workflow-aware or domain-aware.
- Treat `Button` conversion as safe only when it is a literal JSX wrapper replacement preserving `type`, `className`, `disabled`, `title`, `onClick`, and children exactly.
- Reject `Button` conversions that introduce workflow/domain props such as `variant="promotion"`, `proposal`, `workflowState`, or `readiness`.
- Prioritize the next and likely final Workstream 1.5 implementation pass in this order: extracted feature components, low-risk trivially equivalent button usage, and `App.tsx` workflow controls last.
- Stop button conversion immediately if any conversion requires workflow interpretation.
- After one more focused Workstream 1.5 pass, seriously evaluate whether Milestone 1 should transition from additional foundation work to certification review.
