# Decisions

## Newly Authorized

- M2 is complete.
- M2 closure is based on terminal candidate hygiene, context-driven discovery, explicit promotion, persisted `CAND-*` artifacts, active rediscovery suppression for dismissed/expired/duplicate candidates, endpoint success-path coverage, and passing backend verification.
- The `Promote Candidate != Generate Proposal` boundary remains authoritative going into M3.
- M3 should begin with an explicit proposal-generation action from a promoted candidate rather than implicit proposal creation during discovery or promotion.
- The recommended M3 opening slice is the minimal backend vertical slice for `IDecisionGenerationService`, proposal persistence under `.agents/decisions/proposals/PROP-*`, proposal ID allocation, promoted-candidate-only generation, source attribution, deterministic `proposal.md` rendering, `decisions.md` refresh, and focused tests for explicit generation boundaries and non-mutation of unrelated lifecycle surfaces.
