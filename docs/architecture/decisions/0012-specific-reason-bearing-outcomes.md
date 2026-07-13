# ADR-0012: Use Specific Reason-Bearing Outcomes

- Status: Accepted
- Date: 2026-07-12
- Owners: Evaluation Authority, Recovery Authority, Completion Authority, Application Boundary

## Context

The merged implementation contains both the accepted M2 specific-obstacle model and older
feature-local `Blocked` / `OperatorUnblock` values. A generic latch does not explain which fact
prevents progress, whether the condition is still present, or which authority may act next.

## Decision

Canonical outcomes are reason-bearing and derived from current durable evidence. They distinguish
completion, passive waiting, failure, cancellation, stall, ambiguity/conflict, invalidated input,
pending effects, required recovery, required human decision, unsupported provider capability,
required compatibility import, missing runtime prerequisites, and specific cannot-proceed reasons
such as missing, dirty, or unversioned input and unusable, corrupt, or unsupported storage.

`Blocked`, `Unblock`, and `OperatorUnblock` are not canonical outcome values. During convergence
they may appear only at an explicit compatibility translation boundary for persisted historical
labels. The translation must immediately produce a specific canonical reason and may not create a
manually cleared latch. M9 removes the transition-recovery value and M15 removes the remaining
completion/review values before those authorities close.

The typed application result remains more specific than its process exit code. Sharing exit code
4 never permits collapsing effect-pending, recovery-required, human-decision-required,
import-required, unsupported-capability, or a specific cannot-proceed reason.

## Consequences

- New canonical enums and persisted facts cannot add generic blocker or unblock members.
- Compatibility readers may recognize historical labels, but production writers never emit them.
- Read models derive current cannot-proceed state from evidence rather than reading a mutable latch.
- M9 and M15 own deletion of the explicitly identified pre-canonical values; they are not accepted
  as alternate runtime semantics by this ADR.
