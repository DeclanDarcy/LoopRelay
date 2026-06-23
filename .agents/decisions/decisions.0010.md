# Decisions

## Newly Authorized

- Treat Milestone 2 as architecturally complete even if some checklist items remain, because inferred capture has now been exercised across Decision Lifecycle, Governance, Operational Context, and Execution without authority drift.
- Treat remaining Milestone 2 work as expanding capture coverage and enabling manual capture rather than proving the core architecture.
- Implement manual capture as creation of reasoning events, not materialized reasoning entities.
- Manual capture may create event classifications such as `HypothesisRaised`, `AlternativeIntroduced`, and `ContradictionIdentified`.
- Manual capture must not create first-class `Hypothesis`, `Alternative`, `Contradiction`, or `Direction` entities before materialization review approval.
- Manual captures require provenance.
- Provenance should support `UserSupplied` as a valid source kind so users can record directly observed reasoning without fabricating artifact references.
- Continue treating semantic dilution from low-value captures as the primary current risk.
- Continue treating authority drift, hidden lifecycle state machines, thread authority creep, materialization gate violations, workflow mirroring, and reasoning-owned execution/governance/operational-context state as risks to actively avoid.
