# Decisions

## Newly Authorized

- Preserve the recovery behavior that duplicate-active or invalid registry evidence produces diagnostic findings only.
- Recovery must not silently select a winner for duplicate active sessions.
- Recovery must not perform hidden repair of authoritative registry corruption.
- Repair authority can be introduced only when durable evidence is sufficient to prove the correct state.
- Treat `GET /recovery` as a fresh current assessment.
- Treat hosted recovery history as durable startup recovery evidence, not current truth.
- Complete the remaining Milestone 3E work by rebuilding missing, stale, or corrupt derived snapshots during recovery.
- Recovery should rebuild disposable metrics, economics, coherence, lifecycle policy, and transfer eligibility snapshots from stronger evidence.
- Derived snapshot recovery must not change the rule that authoritative registry corruption remains finding-only.
