# Decisions

## Newly Authorized

- Treat Milestone 3D transfer execution as complete.
- Preserve transfer authority ordering: lifecycle policy decides whether transfer should happen, eligibility decides whether transfer can happen now, and transfer execution is the only component that mutates lifecycle state.
- Preserve the invariant that ineligible transfer execution returns a failed result without mutating registry state.
- Preserve boundaries: no manual transfer trigger, no policy override, no eligibility override, and no operational-context ownership by decision sessions.
- Treat `TransferPending` failures as durable recovery input rather than silently rolling back started transfer work.
- Ensure transfer event evidence is sufficient for recovery to distinguish `TransferStarted`, `ContinuityArtifactCreated`, `ContinuityIntegrated`, `SourceRetired`, `ReplacementCreated`, `ReplacementActivated`, `TransferCompleted`, and `TransferFailed`, even if represented by a smaller event-type model.
- Begin Milestone 3E recovery and resilience in the next slice.
- Recovery must reconstruct from `registry.json`, transfer records, continuity artifacts, analysis snapshots, policy snapshots, eligibility snapshots, and continuity evidence.
- Recovery must not silently repair duplicate active sessions or auto-pick a winner; duplicate active sessions produce diagnostic findings.
- Recovery must classify `TransferPending` states including pending before artifact, pending with artifact but no replacement, pending with retired source but no active replacement, and completed transfer with stale diagnostics.
- Current milestone status is Milestone 3A policy complete, Milestone 3B eligibility complete, Milestone 3C continuity artifact complete, Milestone 3D transfer execution complete, and Milestone 3E recovery/resilience ready.
