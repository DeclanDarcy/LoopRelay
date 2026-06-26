# Decisions: 2026-06-26 Slice 0016 Artifact Freshness Checkpoint

These decisions capture only newly authorized direction from the response accepting the Slice 0016 artifact freshness mechanism and authorizing the checkpoint workflow.

## Authorized Decisions

1. Treat Slice 0016 as the architectural decomposition point for the Oracle ecosystem.
   - The Oracle mechanisms now answer distinct questions rather than accumulating responsibility in one verifier.
   - Fixture comparison, consumer verification, and artifact freshness verification are accepted as separate Contract Oracle branches.

2. Preserve the three-branch Oracle mechanism model.
   - Fixture comparison answers whether the authoritative serialized contract changed.
   - Consumer verification answers whether downstream consumers still conform.
   - Artifact freshness verification answers whether maintained artifacts are synchronized with the Oracle.

3. Keep `repositories.ts` classified as a Phase 0 verified contract artifact.
   - It must not be treated as generated Milestone 1.2 output.
   - Milestone 0.2 may verify synchronization of today's maintained artifact without implementing generation, regeneration, or lifecycle automation.

4. Preserve distinct artifact freshness failure classes.
   - Stale artifact, unexpected manual artifact modification, and missing expected artifact remain separate because they imply different remediation paths.
   - Do not collapse freshness failures into a generic freshness error.

5. Make fixture update workflow the next Milestone 0.2 slice.
   - The next mechanism should govern how detected contract, consumer, and freshness changes become accepted.
   - Add workflow evidence before expanding the Oracle to another contract family.

6. Keep the fixture update workflow procedural before automation.
   - Document the canonical operational sequence before implementing generator or lifecycle automation.
   - The process should cover drift detection, classification, review, fixture update, artifact refresh, consumer verification, freshness verification, and evidence update.

7. Treat remaining Milestone 0.2 work as governance and certification rather than new Oracle mechanism invention.
   - New architectural concepts should be avoided unless certification exposes a real gap.
   - The current posture is to consolidate, govern, and certify the existing Oracle ecosystem.

## Next Authorized Sequence

1. Commit and push Slice 0016 as an architectural checkpoint.
2. Stop executing after the checkpoint.
3. In the next work slice, add procedural fixture update workflow evidence for Oracle-managed contracts.
