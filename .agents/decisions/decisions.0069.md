# Decisions: 2026-06-27 M1.2 Generated Alias And Generator Transparency Direction

These decisions capture only newly authorized direction from the user response after M1.2 generation-pipeline validation Slice 0068.

## Authorized Decisions

1. Treat Slice 0068 as valid generation-architecture evidence.
   - The demonstrated pipeline is accepted as deterministic transformation evidence, not as a new source of contract truth.
   - The accepted dependency chain is Backend Serialized Contract -> Oracle Fixture -> Canonical IR -> Generated Artifact -> Freshness Verification.
   - Every stage remains downstream of the accepted M1.1 contract model.

2. Keep generation validation separate from consumer migration.
   - The metadata-only generated TypeScript artifact must remain non-production-facing until consumer migration is explicitly sliced.
   - Future failures must remain attributable to pipeline, generator, generated output, consumer, or migration strategy rather than combining those concerns in one slice.

3. Preserve the IR as the M1.2 architectural choke point.
   - The IR may represent only the accepted M1.1 model.
   - The IR must not answer compatibility policy, identity semantics, ownership, version evolution, or normalization-rule questions independently.
   - If generator work requires concepts absent from M1.1, reopen M1.1 through governance instead of extending the IR ad hoc.

4. Authorize the next slice to introduce generated repository dashboard TypeScript consumer aliases.
   - Keep `src/CommandCenter.UI/src/types/repositories.ts` as a compatibility wrapper during the transition.
   - The intended transition path is Generated Type -> Compatibility Alias -> Existing Consumers.
   - Do not remove the wrapper until generation correctness, freshness, and consumer compatibility are independently stable.

5. Add or plan generator transparency protection as M1.2 expands.
   - Generated artifacts must remain pure projections of the IR.
   - Generation must be reproducible without hidden generator state, handwritten exceptions, or repository-specific logic.
   - Special-case generator branches for individual contract families are architectural risk and should fail or be governed before acceptance.

6. Rotate active decisions and publish the checkpoint.
   - Rotate `.agents/decisions/decisions.md` to the next numbered decisions file.
   - Create a new active decisions checkpoint containing only this newly authorized M1.2 direction.
   - Stage the completed M1.2 Slice 0068 work plus this decision rotation, commit, push to `origin/dev`, and stop executing.

## Evidence Targets

- `.agents/milestones/m1.2-generated-contract-pipeline-validation-slice-0068.md`
- `.agents/handoffs/handoff.md`
- `.agents/handoffs/handoff.0064.md`
- `.agents/decisions/decisions.0068.md`
- `docs/contracts.md`
- `docs/architectural-capabilities.md`
- `docs/architectural-mechanisms.md`
- `tests/CommandCenter.Backend.Tests/ContractGeneratedArtifactPipelineTests.cs`
- `tests/CommandCenter.Backend.Tests/ContractVerification/ContractGenerationSupport.cs`
- `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-dashboard.contract-ir.json`
- `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-dashboard.generated-artifact-freshness.json`
- `src/CommandCenter.UI/src/contracts/generated/repository-dashboard.generated.ts`

## Next Authorized Sequence

1. Stage only the completed M1.2 Slice 0068 work and this decision rotation.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
