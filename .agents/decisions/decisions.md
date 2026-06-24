# Decisions

## Newly Authorized

- Preserve the `CommandCenter.DecisionSessions` project boundary and continue excluding `CommandCenter.Execution`; this boundary is architecturally significant and should be protected as lifecycle work expands.
- Keep Milestone 1 recovery diagnostic-only for now. Recovery should identify registry problems before any repair behavior is introduced in later lifecycle stages.
- Keep the decision-session endpoint surface read-only: list, active, and diagnostics only. Do not add force activation, retirement, transfer, policy override, or eligibility override endpoints.
- Do not move into Stage 2 analysis yet. First harden Stage 1 with schema-version enforcement, timestamp validation, transfer-state transition coverage, registry corruption diagnostics, and repository-isolation certification.
- Treat the decision-session invariant as one active session per repository, not one active session globally.
