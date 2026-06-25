# Decisions

## Newly Authorized

- Treat `.agents/milestones/m7-continuity-authority-matrix.md` as the constraining implementation artifact for Milestone 7, not merely documentation.
- Every new Milestone 7 continuity transparency feature must follow this pipeline: continuity authority, persisted evidence, backend projection, transport, typed client, presentation-only UI.
- The first implementation slice should add proposal-level assimilation transparency before UI work.
- The backend projection for every candidate decision must answer these independent questions: assigned taxonomy, assimilated or excluded status, exclusion reason, durability, resulting operational statement, and source evidence.
- Omitted-by-limit state must remain separate from exclusion state.
- Excluded means continuity rules determined the decision should not appear in operational context.
- Omitted by limit means continuity rules accepted the decision, but an assimilation limit prevented inclusion.
- Candidate-decision assimilation should model the flow as: decision, qualifies or excluded, then for qualifying decisions included or omitted by assimilation limit.
- Backend tests for the next implementation slice must explicitly cover included decisions, excluded decisions, qualifying decisions omitted due to limit, and silent truncation becoming visible through the projection.
- Preserve the Milestone 5 and Milestone 6 implementation pattern for Milestone 7: establish domain-owned semantic projections first, then defer shared presentation composition to Milestone 8 after the domain model matures.
