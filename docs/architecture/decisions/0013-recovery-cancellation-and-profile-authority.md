# ADR 0013: Recovery cancellation and exact-profile authority

Status: Accepted  
Date: 2026-07-12

## Decision

Accept D5. Cancellation before outward dispatch records `Cancelled` with no provider work. Cancellation after outward acceptance creates a recovery case and requires reconciliation before any resend. Validated output is retained and may be reused only after freshness validation. Started effects and partially completed closure remain on their original durable plans and are reconciled; cancellation does not erase their facts or convert them to ordinary failure.

Accept D10. Resume, provider-read, and native-fork recovery actions require evidence for the exact observed provider profile. Unsupported profiles fail closed and may select only certified reconstruction or a durable human-decision request. Capability is never inferred from an interface alone.

## Consequences

Recovery classification is a pure projection of durable facts. An immutable plan naming its evidence, policy, profile, action, and idempotency key is persisted before execution. A retry preserves root run, workflow instance, and transition-run identity while minting a new attempt identity. Prior attempts remain immutable.

## Verification

M9 classification, planning, persistence, restart, cancellation, and exact-profile fixtures are the executable acceptance evidence for this ruling.
