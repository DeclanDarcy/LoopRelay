# Decisions

## Newly Authorized

- Treat Milestone 5 Workflow and Repository Consumption as complete.
- Proceed to Milestone 6 Certification next.
- Certification should prove the architecture rather than implement new lifecycle behavior.
- Prioritize certification checks for authority preservation:
  - Workflow cannot mutate lifecycle.
  - Middle cannot mutate lifecycle.
  - Observability cannot mutate lifecycle.
- Prioritize certification checks for the single-active-session invariant:
  - Zero active sessions may be allowed for pre-initialization or diagnostic failure states.
  - One active session is valid normal operation.
  - Two or more active sessions must fail certification.
- Prioritize determinism certification for metrics, economics, coherence, and policy from identical evidence.
- Prioritize eligibility certification proving transfer execution is allowed only when policy recommends transfer and eligibility is eligible; blocked and deferred eligibility must prevent transfer while preserving the policy decision.
- Prioritize transfer certification proving continuity artifact creation, source retirement, replacement activation, single-active invariant preservation, and lineage preservation.
- Prioritize recovery certification proving missing snapshot rebuild, active-session reconstruction, duplicate-active detection, and interrupted-transfer diagnostics without silent repair.
- Certification should consume externally visible public surfaces where practical, especially observability, workflow, and repository projections, instead of certifying only through internal implementation details.
