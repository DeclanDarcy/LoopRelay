# Decisions: 2026-06-27 M1.2 Entry Strategy Direction

These decisions capture only newly authorized direction from the user response following M1.1 acceptance and baseline.

## Authorized Decisions

1. Treat M1.2 as a consumer of the accepted M1.1 contract model.
   - M1.2 is no longer building the contract model.
   - M1.2 must consume the accepted baseline recorded by M1.1.
   - Generation must not become an upward authority over the canonical contract model.

2. Preserve the one-way dependency chain.
   - The architectural dependency is Domain -> Projection -> Canonical Contract Model -> Generation -> Generated Artifacts -> Consumers.
   - There is no authorized reverse dependency from generation back into the contract model.
   - If generation exposes a model defect, M1.1 must be reopened through governance instead of silently redefining the model in M1.2.

3. Start M1.2 with a pipeline-architecture validation slice.
   - Use a single Oracle pilot family.
   - Establish one IR/schema path.
   - Establish deterministic generation.
   - Establish freshness verification.
   - Do not perform consumer migrations in the first M1.2 slice.

4. Treat the first M1.2 slice as generation-pipeline validation rather than generation breadth.
   - The first slice should prove Accepted Contract Model -> Canonical IR -> Deterministic Generation -> Freshness Verification.
   - TypeScript generation, mock generation, Rust metadata, additional contract families, and consumer migrations should follow only after the pipeline is proven.

5. Preserve IR purity from the outset.
   - The IR may contain only concepts already defined by the accepted M1.1 model.
   - Generator-only metadata, generation identity, consumer compatibility semantics, or serialization ownership flags are not authorized if they introduce concepts absent from M1.1.
   - If such concepts are genuinely needed, reopen M1.1 through governance with evidence instead of treating them as generator implementation details.

6. Rotate active decisions and publish the checkpoint.
   - Rotate `.agents/decisions/decisions.md` to the next numbered decisions file.
   - Create a new active decisions checkpoint containing only this newly authorized M1.2 entry direction.
   - Stage, commit, push, and stop without staging unrelated dirty work.

## Evidence Targets

- `.agents/milestones/m0.4-decision-governance-acceptance-baseline-slice-0058.md`
- `.agents/milestones/m1.1-canonical-contract-model-acceptance-baseline-slice-0065.md`
- `.agents/milestones/m1.1-canonical-contract-model-certification-slice-0064.md`
- `docs/contracts.md`
- `docs/architectural-capabilities.md`
- `.agents/decisions/decisions.0067.md`

## Next Authorized Sequence

1. Stage only the decision rotation and this active decision checkpoint.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
