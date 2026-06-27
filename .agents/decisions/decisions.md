# Decisions: 2026-06-26 M0.2 Boundary and M0.3 Opening

These decisions capture only newly authorized direction from the user response following Slice 0035.

## Authorized Decisions

1. Accept the M0.2 milestone boundary as coherent and complete for its scoped purpose.
   - M0.2 now has an accepted baseline rather than only accumulated implementation slices.
   - The accepted boundary is the Phase 0 Contract Oracle foundation, not full contract-surface coverage.

2. Do not reopen M0.2 unless there is a named uncovered property.
   - Deferred coverage is not itself a reason to reopen M0.2.
   - Later work should stay in later milestones unless a concrete M0.2 architectural claim is found unsupported.

3. Treat the Contract Oracle as a complete Phase 0 architectural capability.
   - The capability consists of authority definition, boundary taxonomy, field ownership, fixture governance, drift classification, consumer verification, artifact freshness, request-boundary verification, procedural change workflow, local certification, repeatability evidence, and acceptance baseline.

4. Start M0.3 by building framework before scaling regressions.
   - The first M0.3 slice should inventory existing architecture-facing tests and helpers.
   - It should define regression namespaces, organization, and naming.
   - It should introduce one small architectural regression, update `docs/architectural-mechanisms.md`, and verify the narrow test set plus `git diff --check`.

5. Make the first M0.3 regression intentionally meta.
   - The first regression should protect the existence or wiring of an architectural mechanism rather than adding another contract-property check.
   - Candidate targets include Oracle fixture directory existence, Oracle fixture test discoverability, consumer verification infrastructure references, or freshness verifier wiring into the backend test project.

6. Treat architectural mechanisms themselves as regression targets.
   - M0.3 should establish the idea that drift detection protects the mechanisms that protect architecture.
   - Later milestones can add more sophisticated architecture-specific regressions on top of that foundation.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0035 plus this decision checkpoint.
2. Stop executing after the push.
