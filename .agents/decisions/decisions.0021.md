# Decisions

## Newly Authorized

- Treat the final-certification direction as accepted.
- Preserve the boundary: workflow proof uses public read-only surfaces.
- Preserve the boundary: the end-to-end lifecycle fixture is test evidence only.
- Treat diagnostics coverage as the final Milestone 6 closure item.
- Add explicit diagnostic proof for:
  - Continue decision.
  - Transfer decision.
  - Eligibility blocked.
  - Eligibility deferred.
  - Recovery findings.
  - Transfer failure.
  - Duplicate active sessions.
  - Missing or corrupt derived snapshots.
- Diagnostic proof must show the system explains failures in the correct layer, not only that it fails safely.
- Do not add markdown reports unless they clearly serve certification evidence.
- Treat the persisted JSON certification report as sufficient unless human-facing audit output is required.
