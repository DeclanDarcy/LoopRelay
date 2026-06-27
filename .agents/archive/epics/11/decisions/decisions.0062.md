# Decisions: 2026-06-27 M1.1 Evolution and Examples Direction

These decisions capture only newly authorized direction from the user response following M1.1 Contract Normalization and Boundaries Slice 0061.

## Authorized Decisions

1. Treat M1.1 Slice 0061 as a valid continuation of the canonical contract model.
   - The slice correctly adds the final foundational semantic layer before evolution, compatibility, governance, and examples.
   - The current M1.1 strata are contract identity, contract taxonomy, ownership model, and normalization plus boundary semantics.
   - The sequence remains model-first and does not depend on generation.

2. Preserve the separation of stability, compatibility, and versioning as a high-leverage contract-model rule.
   - Stability answers whether the observable contract remains fundamentally the same.
   - Compatibility answers whether existing consumers remain valid.
   - Versioning records intentional evolution.
   - Later Oracle mechanisms and generators should reason about these dimensions independently.

3. Continue M1.1 with evolution, compatibility, and governance before canonical examples.
   - The next model sequence is evolution model, compatibility model, governance model, canonical examples, then M1.1 completion.
   - Canonical examples should illustrate the completed model rather than accidentally define missing rules.

4. Add contract evolution operations before closing M1.1.
   - The model should classify operations such as additive field, deprecated field, removed field, renamed field, semantic reinterpretation, representation normalization, contract split, contract merge, projection split, projection aggregation, and compatibility-only alias.
   - Each operation should specify whether identity changes, compatibility changes, version changes, consumer action is required, and what governance or evidence is expected.
   - This vocabulary should guide M1.2 generators and compatibility tooling instead of embedding evolution rules directly in implementation logic.

## Evidence Targets

- `.agents/milestones/m1.1-contract-normalization-boundaries-slice-0061.md`
- `docs/contracts.md`
- `docs/architectural-capabilities.md`
- `.agents/handoffs/handoff.md`

## Next Authorized Sequence

1. Stage the M1.1 normalization/boundary slice, handoff rotation, decision rotation, and this decision checkpoint.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
