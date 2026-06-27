# Decisions: 2026-06-27 M0.4 Compatibility Structure Governance Direction

These decisions capture only newly authorized direction from the user response following M0.4 Authority/Projection Watchlist Slice 0055.

## Authorized Decisions

1. Accept Slice 0055 as a correct M0.4 governance-strengthening step.
   - The authority/projection detector is a governance watchlist, not an architectural correctness checker.
   - File-name heuristics may surface potential governance events but must not claim to prove semantic correctness.
   - The watchlist raises attention for governance review; it does not render an architectural verdict.

2. Preserve the limited certification claim for authority/projection watchlist governance.
   - The guard may require explicit inventory for authority/projection-like source file names.
   - The guard must not certify semantic authority correctness, projection purity, or architectural role.
   - Later authority and projection mechanisms remain responsible for stronger semantic enforcement.

3. Continue M0.4 with compatibility-structure governance.
   - Compatibility structures should have explicit lifecycle and supporting evidence.
   - The guard should ensure every compatibility structure has owner, consumers, replacement path, retirement condition, and reachable evidence.
   - The guard should not judge whether the compatibility mechanism is intrinsically good or bad.

4. Classify compatibility objects by kind.
   - Compatibility field: transitional serialized property.
   - Compatibility route: legacy endpoint.
   - Compatibility command: legacy transport command.
   - Compatibility mirror: transitional Rust or TypeScript model.
   - Each kind should satisfy the same governance metadata while remaining reportable for retirement planning.

5. Align compatibility governance failure language with M0.4.
   - Prefer "Ungoverned compatibility structure detected."
   - The failure model should emphasize missing governance metadata rather than implying compatibility itself is undesirable.

## Next Authorized Sequence

1. Stage Slice 0055 changes, handoff rotation, decision rotation, and this decision checkpoint.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
