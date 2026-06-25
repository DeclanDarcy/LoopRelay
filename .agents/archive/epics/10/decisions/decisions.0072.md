# Decisions

## Newly Authorized

- Proceed next with Milestone 8 governance migration before decision migration.
- Governance migration order:
  - Certification
  - Recovery
  - Health/findings
  - Eligible actions
- Decision migration order:
  - Certification
  - Governance explanation
  - Evidence
  - Diagnostics
  - Constraints
  - Eligible actions
- Continue the established Milestone 8 discipline: adapters reorganize only, shared components render only, and backend projections remain authoritative.
- Add adapter preservation tests for migrated domains that verify evidence, diagnostics, uncertainty, constraints, findings, and eligible actions are preserved.
- Add adapter tests proving migrated adapters do not derive lifecycle state, certification result, recommendation score, health, governance outcome, or eligibility.
