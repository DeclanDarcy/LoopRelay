# Decisions: 2026-06-27 M1.1 Acceptance Boundary Direction

These decisions capture only newly authorized direction from the user response that continued work after M1.1 Certification Validation Direction.

## Authorized Decisions

1. Treat Slice 0064 as the M1.1 certification slice.
   - M1.1 is locally certified as a model-complete canonical contract foundation.
   - Certification is not acceptance, baseline publication, generated ecosystem implementation, fixture expansion, transport migration, or consumer migration.

2. Preserve the M1.1 to M1.2 boundary.
   - M1.2 may start only after M1.1 acceptance and baseline closeout.
   - M1.2 generation should consume the certified M1.1 model rather than inventing contract identity, category, ownership, normalization, boundary, compatibility, versioning, or governance rules.

3. Use acceptance as the next gate.
   - The next slice should record M1.1 acceptance and baseline closeout.
   - Acceptance must confirm downstream compatibility obligations, accepted limitations, rollback readiness, capability matrix final status, durable documentation alignment, and the starting boundary for generated contract ecosystem work.

4. Keep certification evidence separate from implementation evidence.
   - Slice 0064 evidence proves model completeness and determinism.
   - Generated artifacts, schema IR, artifact freshness for generated output, shell transport passivity, TypeScript migration, Rust mirror retirement, and dev mock generation require later milestone evidence.

5. Rotate handoff and decisions artifacts before publication.
   - The active handoff was rotated to `.agents/handoffs/handoff.0062.md` and replaced with the Slice 0064 handoff.
   - The prior active decisions checkpoint was rotated to `.agents/decisions/decisions.0064.md` and replaced with this acceptance-boundary checkpoint.

## Evidence Targets

- `.agents/milestones/m1.1-canonical-contract-model-certification-slice-0064.md`
- `.agents/milestones/m0.4-decision-governance-acceptance-baseline-slice-0058.md`
- `.agents/milestones/m1.1-canonical-contract-model.md`
- `docs/contracts.md`
- `docs/architectural-capabilities.md`
- `.agents/handoffs/handoff.md`
- `.agents/decisions/decisions.0064.md`

## Next Authorized Sequence

1. Stage the M1.1 certification slice, handoff rotation, decision rotation, and this acceptance-boundary decision checkpoint.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
