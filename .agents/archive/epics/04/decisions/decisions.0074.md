# Decisions

## Newly Authorized

- Treat the Git workflow and generated handoff review extraction as proof that presentation residue still existed in `App.tsx`.
- Shift the remaining M8 audit from file size reduction to authority-boundary validation.
- Do not extract hooks, effects, or coordinators merely because `App.tsx` is still large.
- Use this test for remaining `App.tsx` code: if moving it makes authority harder to locate, keep it in `App.tsx`.
- Keep draft ownership, readiness ownership, mutation orchestration, backend dispatch, and workflow lifecycle coordination in `App.tsx` when those blocks own decisions.
- Extract only remaining code that translates or shapes data for rendering without owning decisions.
- Consider M8 ready to close when every remaining `App.tsx` responsibility is authority-oriented, and every non-authority responsibility has either been extracted or documented as an intentional deviation.
- The next slice should try to falsify the hypothesis that `App.tsx` is now primarily an authority container.
- If that audit cannot falsify the hypothesis, record `Natural Authority Boundary Reached` in the deviation/certification ledger and move to final validation and milestone certification.
