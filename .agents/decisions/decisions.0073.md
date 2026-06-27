# Decisions: 2026-06-27 M1.2 Production Consumer Candidate Direction

These decisions capture only newly authorized direction from the user response after the M1.2 repository-dashboard schema metadata pilot.

## Authorized Decisions

1. Treat the Slice 0071 schema metadata position as the intended M1.2 dependency direction.
   - The active chain is Accepted Contract Model -> Governed Schema Metadata -> Oracle Fixture -> IR -> Generated Evidence Artifact -> Freshness Verification.
   - Schema metadata now sits above the IR.
   - The IR should become a faithful representation of the contract model instead of recovering semantic information from observed payloads.

2. Preserve the three-phase consumer migration boundary.
   - Evidence generation proves the pipeline can represent the governed model.
   - Consumer candidate generation proves a production-shaped type can be emitted.
   - Consumer migration proves existing consumers can safely adopt the generated artifact.
   - Generated metadata remains governed contract evidence, not authorization for production UI imports.
   - `src/CommandCenter.UI/src/types/repositories.ts` remains the compatibility wrapper until candidate generation and verification are complete.

3. Authorize the next M1.2 artifact as a production-consumer candidate, not a replacement.
   - The next flow is Governed Schema Metadata -> Production Consumer Candidate -> Compatibility Wrapper -> Existing Consumers.
   - The compatibility wrapper acts as the migration oracle.
   - If the compatibility wrapper still needs to strengthen or reinterpret the generated type, the generated consumer is not ready to become authoritative.

4. Add separate structural and semantic equivalence verification for the production-consumer candidate.
   - Structural compatibility must verify fields, nesting, optionality, and collection shape.
   - Semantic compatibility must verify enum domains, identity aliases, nullability, and omission semantics.
   - These checks must report independently so regressions identify whether the issue is missing schema metadata or generator behavior.

5. Preserve generator subordination to the accepted M1.1 model.
   - Deterministic generation, governed schema metadata, production-oriented artifacts, and consumer migration remain separate steps.
   - Consumer migration must be justified by explicit verification rather than confidence in the generator.

6. Publish this decision checkpoint.
   - Rotate `.agents/decisions/decisions.md` to `.agents/decisions/decisions.0072.md`.
   - Create this active decisions checkpoint containing only the newly authorized M1.2 production-consumer candidate direction.
   - Stage the completed Slice 0071 work and this decision rotation, excluding unrelated pre-existing dirty files.
   - Commit and push to `origin/dev`.
   - Stop executing after the push.

## Evidence Targets

- `.agents/decisions/decisions.0072.md`
- `.agents/decisions/decisions.md`
- `.agents/milestones/m0.4-active-governance-artifact-validation-slice-0053.md`
- `.agents/milestones/m1.2-repository-dashboard-schema-metadata-pilot-slice-0071.md`
- `.agents/handoffs/handoff.md`
- `.agents/handoffs/handoff.0067.md`
- `docs/contracts.md`
- `docs/architectural-capabilities.md`
- `docs/architectural-mechanisms.md`
- `tests/CommandCenter.Backend.Tests/ContractVerification/ContractGenerationSupport.cs`
- `tests/CommandCenter.Backend.Tests/ContractGeneratedArtifactPipelineTests.cs`
- `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-dashboard.contract-ir.json`
- `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-dashboard.generated-artifact-freshness.json`
- `src/CommandCenter.UI/src/contracts/generated/repository-dashboard.generated.ts`

## Next Authorized Sequence

1. Stage only the completed Slice 0071 files and this decision rotation.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
