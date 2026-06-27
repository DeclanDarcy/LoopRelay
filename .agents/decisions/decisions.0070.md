# Decisions: 2026-06-27 M1.2 Generated Raw Alias Boundary And IR Schema Direction

These decisions capture only newly authorized direction from the user response after M1.2 generated raw TypeScript alias Slice 0069.

## Authorized Decisions

1. Treat Slice 0069 as valuable negative architectural evidence.
   - The raw generated TypeScript aliases are accepted as deterministic generated shape evidence.
   - The rejected direct production wrapper migration is accepted as evidence that the current IR is insufficient for production consumer-safe semantic types.
   - The most important result is the preserved boundary: the generator stopped before overclaiming semantic knowledge.

2. Preserve the current dependency chain.
   - The accepted chain is Canonical Contract Model -> Oracle Observation -> IR -> Generated Raw Shape -> Compatibility Layer -> Production Consumer.
   - Raw generated aliases may represent observed structure.
   - Production consumers must not depend on raw observed aliases until semantic contract information is explicit and governed.

3. Keep semantic information out of observational inference.
   - Enum domains must not be inferred from fixture-observed strings.
   - Contract nullability must not be inferred from one observed fixture value.
   - Future collection semantics, identifier semantics, and arbitrary text semantics must be explicit model concepts before generated consumer types can claim them.

4. Keep `src/CommandCenter.UI/src/types/repositories.ts` as the compatibility wrapper.
   - The manual dashboard type remains the production compatibility layer.
   - The compatibility layer is the visible place for semantic strengthening until the IR can express the semantics directly.
   - The wrapper must not become hidden generator logic or dispersed consumer-side adaptation.

5. Authorize the next slice to define governed IR schema/nullability and semantic alias policy.
   - The next slice should model the distinction between structural properties and semantic properties.
   - Candidate concepts include enum domain, nullable by contract, omitted by contract, array ordering semantics, opaque identity, and arbitrary text.
   - Do not attempt another production consumer migration before this policy exists.

6. Publish the checkpoint.
   - Rotate `.agents/decisions/decisions.md` to the next numbered decisions file.
   - Create a new active decisions checkpoint containing only this newly authorized M1.2 direction.
   - Stage the completed M1.2 Slice 0069 work plus this decision rotation, commit, push to `origin/dev`, and stop executing.

## Evidence Targets

- `.agents/milestones/m1.2-generated-raw-typescript-alias-slice-0069.md`
- `.agents/handoffs/handoff.md`
- `.agents/handoffs/handoff.0065.md`
- `.agents/decisions/decisions.0069.md`
- `docs/contracts.md`
- `docs/architectural-capabilities.md`
- `docs/architectural-mechanisms.md`
- `tests/CommandCenter.Backend.Tests/ContractGeneratedArtifactPipelineTests.cs`
- `tests/CommandCenter.Backend.Tests/ContractConsumerVerificationTests.cs`
- `tests/CommandCenter.Backend.Tests/ContractVerification/ContractGenerationSupport.cs`
- `tests/CommandCenter.Backend.Tests/ContractVerification/ContractConsumerVerificationSupport.cs`
- `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-dashboard.generated-artifact-freshness.json`
- `src/CommandCenter.UI/src/contracts/generated/repository-dashboard.generated.ts`

## Next Authorized Sequence

1. Stage only the completed M1.2 Slice 0069 work and this decision rotation.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
