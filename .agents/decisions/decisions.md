# Decisions: 2026-06-27 M1.1 Canonical Examples and Certification Direction

These decisions capture only newly authorized direction from the user response following M1.1 Contract Evolution, Compatibility, and Governance Slice 0062.

## Authorized Decisions

1. Treat Slice 0062 as a valid semantic completion layer for M1.1 before canonical examples.
   - The current M1.1 progression is contract identity, taxonomy, ownership, normalization, boundary semantics, evolution, compatibility, and governance.
   - The milestone remains an architectural model rather than an implementation mechanism.
   - M1.2 generation should remain downstream until M1.1 examples and certification validate that the model is closed.

2. Preserve compatibility bridges as projections of backend authority.
   - Compatibility bridges must derive from backend authority and must not invent meaning downstream.
   - This invariant protects M1.2 generators, M1.3 shell passivity, M2.x semantic authority restoration, and M7 compatibility-layer retirement.
   - A compatibility bridge cannot continue once the backend no longer emits the authoritative source needed to derive it.

3. Treat canonical examples as conformance cases.
   - Examples must instantiate the full model rather than serve as informal snippets.
   - Each example family should demonstrate contract identity, category, authoritative source, ownership dimensions, normalization rules, stability classification, compatibility policy, version identity, consumer classes, and applicable governance constraints.
   - If a family requires special rules outside the shared vocabulary, M1.1 is not yet complete.

4. Focus M1.1 certification on architectural closure rather than implementation progress.
   - Certification should ask whether every existing contract family can be classified, every contract aspect has a single owner, all boundary responsibilities are accounted for, permitted evolution fits the evolution model, and compatibility obligations derive from the model.
   - M1.2 is ready only if a generator would not need to invent architectural rules not already specified by M1.1.

## Evidence Targets

- `.agents/milestones/m1.1-contract-evolution-compatibility-governance-slice-0062.md`
- `docs/contracts.md`
- `docs/architectural-capabilities.md`
- `.agents/handoffs/handoff.md`

## Next Authorized Sequence

1. Stage the M1.1 evolution/compatibility/governance slice, handoff rotation, decision rotation, and this decision checkpoint.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
