# Decisions: 2026-06-27 M1.1 Certification Validation Direction

These decisions capture only newly authorized direction from the user response following M1.1 Canonical Contract Examples Slice 0063.

## Authorized Decisions

1. Treat M1.1 as having moved from architecture construction into architecture validation.
   - Do not add new semantic primitives unless certification exposes a concrete model gap.
   - The current M1.1 progression is contract identity, taxonomy, ownership, normalization, boundary semantics, evolution, compatibility, governance, and canonical conformance cases.
   - M1.1 remains independent from generators, TypeScript, Rust, fixtures, and transport implementation.

2. Certify architectural properties rather than document completion.
   - Certification should validate completeness, closure, sufficiency, and determinism of the canonical contract model.
   - Passing documentation review is insufficient if the model leaves ambiguous ownership, classification, compatibility, versioning, boundary, or governance interpretations.

3. Preserve canonical examples as conformance cases.
   - Examples answer whether each contract family can be fully described by the model.
   - Examples must not authorize future implementation details or ossify incidental endpoint, fixture, TypeScript, Rust, shell, or mock behavior.

4. Treat governance traceability as an active certification input.
   - The governance-link failure discovered during Slice 0063 is evidence that Phase 0 governance mechanisms protect later milestone claims.
   - M1.1 certification should preserve reachable evidence links for decisions, capability claims, mechanisms, and slice evidence.

5. Add model determinism as an explicit M1.1 certification criterion.
   - For every existing contract family, the model should yield exactly one valid classification path through identity, category, ownership, normalization, boundary rules, evolution rules, compatibility rules, and governance.
   - If multiple equally valid interpretations remain, M1.1 is not ready for M1.2 because the generator would be forced to encode architectural decisions.

6. Use independent implementability as the sufficiency test for M1.1.
   - M1.1 is ready for M1.2 only if an independent team could implement generated contracts using M1.1 without introducing new architectural concepts.
   - Generation should become an implementation exercise, not a place to invent identity, ownership, compatibility, versioning, stability, or governance rules.

## Evidence Targets

- `.agents/milestones/m1.1-canonical-contract-examples-slice-0063.md`
- `.agents/milestones/m1.1-canonical-contract-model-certification-slice-0064.md`
- `.agents/milestones/m0.4-decision-governance-acceptance-baseline-slice-0058.md`
- `docs/contracts.md`
- `docs/architectural-capabilities.md`
- `.agents/handoffs/handoff.md`

## Next Authorized Sequence

1. Stage the M1.1 canonical examples slice, handoff rotation, decision rotation, and this certification-validation decision checkpoint.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
