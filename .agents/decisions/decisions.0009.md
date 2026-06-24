# Decisions

## Newly Authorized

- Accept the Milestone 3C continuity artifact implementation as the correct result.
- Treat Milestone 3C continuity artifacts as complete.
- Treat Milestone 3D transfer execution as ready to begin in a future slice.
- Preserve the boundary that continuity artifacts are canonical transfer payloads and Decision Sessions are not operational context owners.
- Keep artifact creation non-mutating; preparing durable continuity must not imply transfer has happened.
- Transfer execution must enforce strict ordering: active source session, policy `Transfer`, eligibility `Eligible`, source `TransferPending`, continuity artifact creation, `TransferStarted` event, continuity artifact integration without ownership, source retirement, replacement creation, replacement activation, artifact target-session attachment, and `TransferCompleted` event.
- Transfer execution tests must prove blocked eligibility does not mutate registry state, artifact creation happens before source retirement, source is retired, replacement becomes active, two active sessions never exist, transfer events are durable, and failed transfer leaves diagnostics.
