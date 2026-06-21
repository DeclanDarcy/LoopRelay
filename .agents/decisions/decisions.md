# Decisions

## Newly Authorized

- Reassess M0 projection extraction based on architectural authority, not hook count or `App.tsx` size reduction.
- Treat the question for further extraction as: does extracting another hook improve architectural authority?
- Create `.agents/audits/m0-projection-authority-certification.md` before any additional hook extraction.
- The projection-authority certification should document Projection, Authority Hook, Consumer, Duplicate Load Paths, and Certified status for every extracted projection.
- Use the certification to prove one projection has one frontend authority.
- Evaluate `useOperationalContextProposal(repositoryId, proposalId)` only after the projection-authority certification artifact exists.
- If `useOperationalContextProposal(repositoryId, proposalId)` is extracted, constrain it to load proposal, refresh proposal, proposal loading state, proposal error state, and proposal projection data.
- `useOperationalContextProposal(repositoryId, proposalId)` must not own proposal generation, proposal editing, accept, reject, promote, review readiness, promotion readiness, or current-understanding comparison.
- Use this litmus test for proposal hook extraction: if proposal generation disappeared tomorrow, the hook should still make sense.
- After certification and possible proposal-loading extraction, reassess whether M0 is effectively complete.
- Treat remaining responsibilities after read-oriented projections as increasingly workflow actions, workflow gating, navigation, draft state, and view composition rather than M0 projection-hook targets.

## Validation Expected For Next Slice

- Create the projection authority certification artifact first.
- Do not extract operational-context proposal loading until the certification justifies it.
- Preserve the backend-to-API-to-projection-hook-to-`App.tsx` authority flow.
