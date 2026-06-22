# Decisions

## Newly Authorized

- Treat Milestone 1 as complete, certified, and closed.
- Do not reopen Milestone 1 for more `Button` conversions, primitive usage counts, `App.tsx` size reduction, workflow extraction, or navigation restructuring.
- Treat the preserved M0 authority map as the core reason M1 can close: DTO authority in `types`, transport in `api`, projection loading in `hooks`, navigation in `shellState`, presentation in `features`, and remaining workflow/draft/readiness/mutation authority in `App.tsx`.
- Move next to Milestone 2: Application Shell.
- Before visible M2 shell implementation, review the M2 milestone document and define shell authority boundaries, `shellState` relationship, layout composition model, sidebar/header/tab responsibilities, and workflow-ownership exclusions.
- In M2, classify global layout, sidebar, workspace switching, repository navigation, tab navigation, and command-palette entry points as likely shell concerns.
- Keep execution workflow, git workflow, proposal workflow, continuity workflow, readiness logic, and mutation logic outside shell ownership.
