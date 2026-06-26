# Decisions: 2026-06-26 Slice 0022 Workspace Artifact Freshness Repeatability

These decisions capture only newly authorized direction from the response accepting Slice 0022 as a freshness-mechanism repeatability slice.

## Authorized Decisions

1. Treat Slice 0022 primarily as mechanism stability evidence.
   - The important architectural result is that repository workspace artifact freshness reused the existing manifest and verifier model unchanged.
   - Repository workspace did not authorize a new freshness mechanism, drift taxonomy, manifest model, artifact classification, or generated-artifact assumption.

2. Keep freshness scoped to Phase 0 verified manual artifacts.
   - `src/CommandCenter.UI/src/types/repositories.ts` remains a verified manual TypeScript contract artifact during Milestone 0.2.
   - Milestone 0.2 proves the Oracle can observe and govern artifacts.
   - Generated artifacts remain Milestone 1.2 work.

3. Track mechanism stability explicitly for repeated Oracle mechanisms.
   - Evidence should classify reuse as unchanged, implementation refinement, or architectural refinement.
   - Unchanged reuse indicates mechanism stability.
   - Implementation refinements, such as parser fidelity improvements, should remain distinct from architectural refinements.
   - Architectural refinements required by repeated contract families should be treated as maturity signals before local or global certification.

4. Preserve repository workspace maturity posture.
   - Repository workspace now has fixture comparison, consumer verification, and artifact freshness verification.
   - Repository workspace still lacks request-boundary verification and local certification.
   - Milestone 0.2 remains active and uncertified globally.

5. Continue repository workspace through the same dashboard lifecycle.
   - Next repository workspace step is request-boundary verification.
   - If request-boundary verification completes without new Oracle mechanism types, proceed to local repository workspace Oracle certification.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0022 and this decision checkpoint.
2. Stop executing after the checkpoint.
3. In the next work slice, begin repository workspace request-boundary verification.
