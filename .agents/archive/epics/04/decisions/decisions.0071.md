# Decisions

## Newly Authorized

- Treat `docs/frontend-modernization-deviations.md` as the primary M8 certification ledger because M8 is a reconciliation milestone.
- Validate the M8 classification model: capability gaps must be explicitly classified rather than implicitly treated as defects.
- Preserve the notification correction: a disabled notification placement is preferable to a synthetic `0` count because the backend notification capability does not exist.
- Confirm abort execution, global navigation surfaces, notifications, cross-repository rollups, milestone progress, and all-repository git summary as backend-owned capability gaps.
- Continue M8 with Workstream 8.7 focused on residue cleanup: temporary migration scaffolding, duplicate abstractions, abandoned DTOs, legacy composition helpers, obsolete CSS, unused adapters, duplicate state, and dead navigation paths.
- Audit `App.tsx` by separating intentional authority from unintentional presentation ownership.
- Keep workflow authority, draft authority, readiness authority, and mutation authority in `App.tsx` when they represent the current architecture boundary.
- Treat presentation helpers, workspace composition, navigation glue, display formatting, and cross-link construction remaining in `App.tsx` as candidates for extraction or explicit deferral.
- Structure the M8 endgame as cleanup, authority-boundary audit, final UX validation, and final certification.
- Treat the remaining modernization work as certification work unless the audit reveals accidental authority leaks, migration artifacts, or synthetic capabilities.
