# Decisions

## Newly Authorized

- Proceed next with Milestone 8 decision-domain migration.
- Keep the decision migration order:
  - Decision certification
  - Governance explanation
  - Evidence
  - Diagnostics
  - Constraints
  - Eligible actions
- Continue treating shared explainability adapters as projection reshapers only.
- Decision adapters must preserve authoritative evidence, diagnostics, constraints, uncertainty, certification findings, and eligible actions.
- Decision adapters must not compute recommendation ranking, recommendation scores, quality, burden, governance outcome, execution influence, lifecycle legality, or certification result.
- If backend certification findings later gain explicit per-finding result states, adapters should forward those authoritative values rather than inventing presentation semantics.
