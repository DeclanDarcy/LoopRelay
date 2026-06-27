# Decisions: 2026-06-27 M1.1 Acceptance Baseline Direction

These decisions capture only newly authorized direction from the latest user request for this slice.

## Authorized Decisions

1. Continue the current milestone by closing M1.1 acceptance and baseline governance.
   - The slice may update evidence, durable contract documentation, capability status, active handoff, and active decisions.
   - The slice must not start M1.2 generated artifact implementation.

2. Treat M1.1 acceptance as model-boundary acceptance.
   - M1.1 defines contract identity, taxonomy, ownership, normalization, boundary semantics, compatibility, versioning direction, and governance.
   - M1.2 may implement generation mechanics from that model, but it may not invent those architectural rules.

3. Preserve existing compatibility consumers as transitional obligations.
   - Rust mirrors, manual TypeScript types, and dev mocks remain compatibility consumers where they exist.
   - Existing Oracle/freshness/request-boundary pilots remain the only certified contract mechanism coverage.
   - No compatibility artifact is retired by this slice.

4. Rotate active handoff and decisions files for this execution slice.
   - Rotate `.agents/handoffs/handoff.md` to the next numbered handoff file and create a compact new active handoff with only new slice state.
   - Rotate `.agents/decisions/decisions.md` to the next numbered decisions file and create a new active decisions checkpoint containing only newly authorized decisions.

5. Stage, commit, push, and stop after the slice.
   - Stage only files belonging to this slice.
   - Do not stage unrelated existing dirty work such as `src/CommandCenter.DecisionSessions/CommandCenter.DecisionSessions.csproj` or `refactor-plan.md`.

## Evidence Targets

- `.agents/milestones/m0.4-decision-governance-acceptance-baseline-slice-0058.md`
- `.agents/milestones/m1.1-canonical-contract-model-acceptance-baseline-slice-0065.md`
- `.agents/milestones/m1.1-canonical-contract-model-certification-slice-0064.md`
- `.agents/milestones/m1.1-canonical-contract-model.md`
- `docs/contracts.md`
- `docs/architectural-capabilities.md`
- `.agents/handoffs/handoff.md`
- `.agents/decisions/decisions.0066.md`

## Next Authorized Sequence

1. Verify the focused architecture/contract test subset and whitespace check.
2. Stage only this slice's files.
3. Commit and push to `origin/dev`.
4. Stop executing after the push.
