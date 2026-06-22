# Decisions

## Newly Authorized

- Treat the execution-context preview decomposition effort as complete unless a new audit produces unexpectedly clean `props -> render` candidates.
- Preserve the current burden of proof: remaining execution-context code in `App.tsx` is presumed intentional until demonstrated otherwise.
- Do not continue extracting execution-context preview surfaces automatically.
- Treat M0.5 execution-context preview work as complete because the extracted components reduced presentation density without moving workflow, readiness, commit, promotion, or execution authority.
- Make the next high-value activity authority inventory and characterization rather than further decomposition.
- Audit remaining large `App.tsx` regions by responsibility category:
  - Category A: pure presentation, candidate for extraction.
  - Category B: presentation plus interpretation, likely retain.
  - Category C: workflow coordination, must retain.
  - Category D: authority decisions, must retain.
- Prefer Workstream 0.6 characterization around milestone selection, proposal gating, commit gating, and review gating.
- Shift the key M0 question from `Can this move?` to `What must never change?`.

## Next Authorized Slice

Start with an authority inventory of remaining large `App.tsx` regions, then add focused M0.6 characterization for the highest-risk authority boundaries instead of continuing extraction-first work.
