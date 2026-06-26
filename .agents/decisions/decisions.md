# Decisions: 2026-06-26 Shell Passivity Preparation Acceptance

These decisions capture only newly authorized direction from the response accepting slice 0004.

## Authorized Decisions

1. The shell passivity preparation slice is accepted as Phase 0 mechanism work, not as Milestone 1.3 passive transport implementation.
   - Rationale: the slice moves an architectural invariant into executable verification while avoiding production transport migration.
   - Accepted invariant: the shell preserves opaque successful backend JSON without becoming semantic authority.

2. The revised sequence is accepted.
   - Milestone 0.1: trust the verification substrate.
   - Shell passivity preparation: prove the substrate can enforce an architectural invariant.
   - Milestone 0.2: establish the Contract Oracle.
   - Milestone 0.3: generalize the architectural regression framework.

3. Remaining initial transport protection should expand in this order:
   - backend error-envelope preservation,
   - POST relay request and response preservation,
   - command-family coverage,
   - mirror retirement only after regressions make removal safe.

4. Transport regressions should be organized around architectural invariants rather than HTTP verbs.
   - Initial invariant categories: success payload preservation, error-envelope preservation, unknown-field preservation, null/absence preservation, no shell semantic ownership, and no domain response mirrors.

5. Milestone 0.2 is ready to begin after the backend error-envelope preservation regression completes the initial passivity mechanism family.

6. Milestone 0.2 should begin with contract surface inventory and boundary taxonomy before golden fixtures.
   - Inventory questions: all cross-boundary contracts, owner for each contract, consumer set for each contract, and locations of parallel truths.
   - Fixture timing: introduce the first fixture only after ownership and lifecycle are understood.

## Explicit Non-Decisions

- No production passive transport migration is authorized by this acceptance.
- No mirror retirement is authorized before protective regressions exist.
- No golden contract fixture should be introduced before contract inventory and boundary taxonomy.
