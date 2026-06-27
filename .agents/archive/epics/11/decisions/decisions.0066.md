# Decisions: 2026-06-27 M1.1 Acceptance Readiness Direction

These decisions capture only newly authorized direction from the user response following M1.1 Canonical Contract Model Certification Slice 0064.

## Authorized Decisions

1. Treat M1.1 as architecturally specified, refined, exemplified, and certified.
   - The completed progression is identity, taxonomy, ownership, normalization, boundary semantics, evolution, compatibility, governance, canonical conformance cases, and certification.
   - The remaining M1.1 work is acceptance and baseline governance, not additional architecture construction.

2. Preserve certification and acceptance as separate gates.
   - Certification answers whether the contract model is coherent and sufficiently evidenced.
   - Acceptance must answer whether the rest of the program can safely depend on the certified model.
   - M1.1 must not move into M1.2 implementation solely because certification passed.

3. Define M1.1 acceptance around operational readiness.
   - Acceptance must demonstrate compatibility obligations are complete for the M1.2 starting boundary.
   - Acceptance must demonstrate rollback boundaries are explicit.
   - Acceptance must demonstrate `docs/contracts.md`, the capability matrix, governance documentation, milestone evidence, and active decision evidence describe the same architectural state.
   - Acceptance must demonstrate the generation boundary is fixed before M1.2 starts.

4. Fix the generation boundary before M1.2.
   - Generators may derive an intermediate representation and generated consumer artifacts from the accepted Canonical Contract Model.
   - Generators must not decide contract identity, serialization ownership, compatibility, version evolution, stability interpretation, governance rules, or semantic authority.
   - Everything above the Canonical Contract Model to generated artifact derivation boundary belongs to M1.1, not M1.2.

5. Scope M1.2 rollback to implementation artifacts and migrations.
   - If M1.2 uncovers an issue, the preferred rollback path is generated artifacts, generator implementation, manifests, freshness evidence, and consumer migrations.
   - Redefining the M1.1 contract model is not the default rollback path; reopening M1.1 requires a named model defect with governance evidence.

6. Keep M1.1 acceptance documentation- and governance-focused unless an acceptance blocker appears.
   - Current verification evidence is proportional: the focused architecture/contract subset passed with 56 tests, and `git diff --check` passed with known line-ending warnings only.
   - Acceptance should not introduce generators, shell changes, TypeScript regeneration, fixture expansion, or transport changes.

## Evidence Targets

- `.agents/milestones/m1.1-canonical-contract-model-certification-slice-0064.md`
- `.agents/milestones/m0.4-decision-governance-acceptance-baseline-slice-0058.md`
- `.agents/milestones/m1.1-canonical-contract-model.md`
- `docs/contracts.md`
- `docs/architectural-capabilities.md`
- `.agents/handoffs/handoff.md`
- `.agents/decisions/decisions.0065.md`

## Next Authorized Sequence

1. Stage the decision rotation and this M1.1 acceptance-readiness decision checkpoint.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
