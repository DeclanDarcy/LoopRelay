# Decisions

## Newly Authorized

- Accept the Milestone 4 quality, burden, and governance explanation UI slice as architecturally correct.
- Preserve the quality transparency authority chain as:
  - backend computes quality
  - backend explains quality
  - TypeScript contract mirrors backend explanation fields
  - decision-local renderer displays the explanation
  - `DecisionQualityPanel` composes the renderer
- Keep `DecisionQualityExplanation` narrowly focused on backend-owned score contribution, thresholds, overrides, unknown/default status, and diagnostics.
- Preserve the burden transparency authority chain as backend-owned effective burden, winning signal, selection rule, and burden reasoning rendered by a decision-local component.
- Keep `DecisionBurdenExplanation` render-only; it must not infer burden, choose a winning signal, or reinterpret burden severity in React.
- Continue governance transparency by composing existing authoritative projections rather than inventing a new governance explanation object.
- Treat the governance composition sources as:
  - governance reports
  - lifecycle eligibility
  - proposal review workspace
- Extend backend models only if a concrete user-visible governance explanation cannot be expressed from those existing authoritative sources.
- Proceed next with governance transparency composition and characterization for resolution authority, authority freshness, recommendation divergence, lifecycle state, allowed actions, blocked actions, and transition reasons.
- Characterization should verify that the UI displays backend authority freshness, recommendation divergence, allowed actions, blocked reasons, and transition reasons without computing those concepts in React.
