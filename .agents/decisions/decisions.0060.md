# Decisions: 2026-06-27 M1.1 Canonical Model Continuation

These decisions capture only newly authorized direction from the user response following M1.1 Contract Identity Inventory Slice 0059.

## Authorized Decisions

1. Treat M1.1 Slice 0059 as a clean opening slice.
   - The slice correctly establishes the architectural identity model without introducing generation, migration, or consumer mechanics.
   - The slice preserves the roadmap sequence by keeping M1.1 as the semantic foundation for M1.2.

2. Continue M1.1 by finishing the canonical contract model before introducing generation mechanics.
   - The next work should answer what kinds of contracts exist.
   - The next work should define ownership dimensions for semantic ownership, shape ownership, serialization ownership, compatibility ownership, version ownership, and evolution or deprecation ownership.
   - The next work should define how contract identity relates to category, authoritative projection or command result, shape owner, serialization rules, compatibility policy, version identity, and consumer classes.

3. Keep M1.2 generation downstream of a fully specified contract model.
   - Generated artifacts should be treated later as deterministic projections from the canonical contract model.
   - The generator must not implicitly encode architectural decisions that belong in M1.1.

## Evidence Targets

- `.agents/milestones/m1.1-contract-identity-inventory-slice-0059.md`
- `docs/contracts.md`
- `docs/architectural-capabilities.md`
- `.agents/handoffs/handoff.md`

## Next Authorized Sequence

1. Stage the M1.1 identity slice, handoff rotation, decision rotation, and this decision checkpoint.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
