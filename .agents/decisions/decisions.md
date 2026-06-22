# Decisions

## Newly Authorized

- Future services must call `DecisionLifecycleRules` rather than reimplementing lifecycle state checks.
- M0B must keep domain entities persistence-agnostic; filesystem, JSON, repository layout, and artifact path concerns belong in persistence adapters.
- Authoritative structured artifacts introduced in M0B must include schema versioning immediately, with repository ownership and timestamps on records.
- Proposal lifecycle, review state, and decision state must remain separate and must not collapse into a generic status field.
- M0B should focus exclusively on authoritative structured persistence; markdown projection generation is deferred to M0C.
- M0B implementation order is: `IDecisionRepository`, in-memory repository, file-system repository, repository ownership metadata, schema versioning, ID allocation, filesystem safety validation, round-trip persistence tests, repository isolation tests.
