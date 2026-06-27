# Decisions: 2026-06-27 M1.2 Governance Validation And Schema Metadata Direction

These decisions capture only newly authorized direction from the user response after the M1.2 generated TypeScript consumer policy governance repair.

## Authorized Decisions

1. Treat the governance repair as an architectural outcome, not merely a test fix.
   - The important chain is Architectural Claim -> Decision Record -> Reachable Evidence -> Governance Verification.
   - That chain is now intact for the M1.2 generated TypeScript consumer policy work.
   - The passing governance suite is evidence that the M1.2 policy claim again satisfies the M0.4 reachability invariant.

2. Preserve M0.4 governance dependency direction.
   - Later milestones adapt to governance.
   - Governance does not adapt to later milestones unless explicitly changed through governance itself.
   - The accepted repair path is to fix the decision artifact so the mechanism succeeds, not to weaken or bypass the governance mechanism.

3. Treat M1.2 as back on track after governance validation.
   - The accepted progression is Accepted Contract Model (M1.1) -> Generation Pipeline -> Raw Generated Aliases -> Governed Consumer Policy -> Governance Validation.
   - The next architectural dependency is explicit schema metadata, not production consumer migration.

4. Authorize the next schema metadata pilot scope.
   - Keep the first `repository-dashboard` schema extension deliberately narrow.
   - Model facts that cannot be inferred from fixtures: contractual nullability, omission versus present-but-null, semantic enum domains, and identity semantics.
   - Do not collapse the compatibility alias toward a passthrough until those schema facts are represented.

5. Preserve the generator-as-implementation boundary.
   - When generation needs information absent from the accepted model, stop and introduce a governed concept.
   - Continue treating consumer policy, semantic enum handling, nullability, omission, governance traceability, and identity semantics as governed architecture concepts rather than generator folklore.
   - The generator remains an implementation of the accepted contract model; the contract model and governed schema define architectural truth.

6. Publish this decision checkpoint.
   - Rotate `.agents/decisions/decisions.md` to the next numbered decisions file.
   - Create this active decisions checkpoint containing only the newly authorized M1.2 governance/schema direction.
   - Stage only the decision rotation.
   - Commit and push to `origin/dev`.
   - Stop executing after the push.

## Evidence Targets

- `.agents/decisions/decisions.0071.md`
- `.agents/decisions/decisions.md`
- `.agents/milestones/m1.2-generated-typescript-consumer-policy-slice-0070.md`
- `.agents/milestones/m0.4-active-governance-artifact-validation-slice-0053.md`
- `docs/contracts.md`
- `docs/architectural-capabilities.md`
- `docs/architectural-mechanisms.md`

## Next Authorized Sequence

1. Stage only this decision rotation.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
