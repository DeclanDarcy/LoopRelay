# Decisions

## Newly Authorized

- Accept the Milestone 4 execution influence verification slice.
- Treat execution influence transparency implementation work as validated and complete.
- Preserve execution influence authority flow as:
  - Decision Services
  - `ExecutionDecisionProjection`
  - `DecisionInfluenceTrace`
  - `ExecutionDecisionInfluencePanel`
- Keep execution influence categories backend-owned; the UI may group those categories but must not derive them.
- Keep Milestone 4 free of shared explainability abstractions; consolidate local explanation components during Milestone 8.
- Proceed next with a regression audit proving the absence of frontend semantic computation for quality scores, recommendation rankings, burden selection, governance findings, lifecycle legality, and influence categorization.
- Do not add proposal recommendation confidence unless the backend owns a semantic confidence model.
- Do not elevate insufficient-evidence or duplicate option categories into standalone UI sections unless the backend distinguishes them as first-class semantic categories.
- Treat validation diagnostics, rejected options, and deduplicated options as the current authoritative explanation for those cases.
- After committing and pushing the accepted execution influence verification slice, stop executing before continuing remaining Milestone 4 closure activities.
