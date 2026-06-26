# Decisions: 2026-06-26 Slice 0010 Oracle Governance Checkpoint

These decisions capture only newly authorized direction from the response accepting the Oracle drift policy classification slice and authorizing the checkpoint workflow.

## Authorized Decisions

1. Treat Slice 0010 as the point where the Contract Oracle basic governance model becomes executable.
   - Accepted capability: the Oracle can now distinguish structural drift from compatibility-review drift.
   - Accepted status: the repository dashboard pilot remains partial and uncertified, but it now supports governed contract evolution rather than simple fixture equality.

2. Treat additive backend fields as compatibility-review drift, not silent acceptance.
   - Additive backend fields must require human review before allowlisting or fixture adoption.
   - The backend serialized contract remains authoritative.
   - Contract evolution must not bypass review by appearing as harmless unknown JSON.

3. Keep the current drift classes separate by owner and response.
   - Structural drift is a hard Oracle failure.
   - Compatibility-review drift requires explicit review before allowlisting.
   - Consumer drift is evidence for consumer verification and must not redefine Oracle authority.

4. Preserve exact JSON-path allowlisting for reviewed compatibility additions.
   - Exact paths are accepted because they are deterministic, auditable, and retireable.
   - Wildcards or broad pattern matching are not authorized.
   - Introducing wildcard allowlisting would require a separate architectural decision because it would weaken the mechanism.

5. Make the next Milestone 0.2 capability the first consumer verification mechanism.
   - Consumer verification should be separate from the Oracle.
   - The known Rust `RepositoryDashboardProjection` omission of `decisionSessionSummary` is the preferred first verification target.
   - Consumer verification reports downstream drift; it must not redefine backend Oracle truth.

6. Classify consumer verification drift symmetrically when implemented.
   - Missing downstream field: consumer omitted a backend field.
   - Extra downstream field: consumer invented a field.
   - Shape mismatch: consumer shape differs from backend serialized shape.
   - Semantic reinterpretation should eventually be classified separately because it indicates downstream semantic authority, not just structural consumer drift.

7. Commit and push Slice 0010 as an architectural checkpoint before beginning consumer verification.
   - Rationale: Milestone 0.2 advanced from fixture comparison into governed contract evolution, with implementation, documentation, evidence, and backend verification aligned.

## Current M0.2 Certification Posture

| Capability | Status |
| --- | --- |
| Oracle definition | Complete |
| Boundary taxonomy | Complete |
| Contract inventory | Complete |
| Consumer inventory | Complete |
| Endpoint catalog | Complete |
| Field catalog | Complete for repository dashboard pilot |
| First executable fixture | Complete |
| Recursive comparison | Complete |
| Drift classification | Complete for repository dashboard pilot |
| Consumer verification | Pending |
| Oracle certification | Pending |

## Next Authorized Sequence

1. Add downstream consumer verification for the repository dashboard pilot.
2. Start with the Rust shell dashboard mirror drift around `decisionSessionSummary`.
3. Keep TypeScript/manual mock verification visible but non-authoritative.
