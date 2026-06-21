# Decisions

## Newly Authorized

- Treat M3 Workstream 3.5 as complete and architecturally correct because `WorkspaceMilestonesPanel` stayed within the artifact-inventory boundary.
- Continue M3 with Workstream 3.6: Inspector Rail.
- Start the Inspector Rail with read-only commit/push summary placement using existing git status and commit-preparation projections.
- The Inspector Rail should consume existing backend/frontend projection answers, not create new workflow answers.
- Safe commit/push summary inputs include current git status, pending changes counts, commit preparation summary, readiness indicators already produced elsewhere, and existing projection outputs.
- Do not add commit orchestration, push orchestration, new readiness calculations, new git state derivation, or new branch inference in the Inspector Rail slice.
- Treat Operational Context inspector placement similarly: show current operational context summary, proposal status summary, compression summary, and existing projection outputs only.
- Do not add proposal interpretation, proposal generation, or proposal readiness ownership to the Workspace Inspector Rail.
- Preserve the emerging M3 split: Workspace main area owns planning, activity, and evidence visibility; Inspector Rail owns review, readiness visibility, and context summary; detailed workflow authority remains in Execution, Operational Context, Continuity, and git workflow surfaces.
