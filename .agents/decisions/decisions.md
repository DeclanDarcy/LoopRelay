# Decisions: 2026-06-26 M0.4 Governance Artifact Validation Direction

These decisions capture only newly authorized direction from the user response following M0.4 Shell Mirror Governance Slice 0052.

## Authorized Decisions

1. Accept Slice 0052 as a correct M0.4 governance-strengthening step.
   - Bidirectional validation between `src/CommandCenter.Shell/src/main.rs` and `docs/shell-transport-classification.md` is an appropriate governance increase.
   - The guard should protect both new unclassified Rust shell structs and stale inventory entries.

2. Preserve the limited certification claim for Slice 0052.
   - The guard proves inventory and implementation cannot silently diverge.
   - The guard does not certify passive transport, correct classification, correct target state, or migration readiness.

3. Continue M0.4 with governance artifact validation.
   - The next layer should validate `.agents/decisions/` and `.agents/milestones/`.
   - This work should protect governance artifacts themselves rather than only implementation metadata.

4. Separate governance artifact validation into structural and referential classes.
   - Structural validation should mechanically check required sections, metadata, and identifiers.
   - Referential validation should check links among decisions, evidence, milestones, capabilities, and mechanisms.
   - These validation classes should evolve independently.

5. Add reachability as a future governance invariant.
   - Governance artifacts should not exist in isolation.
   - Future checks should detect orphaned decisions, evidence, or certification artifacts.

## Next Authorized Sequence

1. Stage the current M0.4 Slice 0052 changes, handoff rotation, and this decision checkpoint.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
