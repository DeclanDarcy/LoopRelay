# Decisions

## Newly Authorized

- Treat the M9 projection-diagnostics prerequisite as complete.
- Proceed next to Milestone 9 influence trace persistence.
- Persist influence traces under `.agents/decisions/influence/`.
- Key influence traces by execution session id and projection fingerprint.
- Record, at minimum, execution session id, projection fingerprint, decision id, statement type, statement text, prompt section, and timestamp.
- Map projected authority through this chain: decision id, projected statement, prompt section, execution session.
- Tie influence tracing to projected authority and projection fingerprints, not mutable current decision state.
- Add richer adherence observations only after the core influence trace exists.

## Not Authorized

- Do not expand execution UI influence surfaces before influence trace persistence and backend retrieval are in place.
- Do not infer historical execution influence from current repository decisions.
- Do not proceed to higher-level execution analytics before core M9 traceability exists.
