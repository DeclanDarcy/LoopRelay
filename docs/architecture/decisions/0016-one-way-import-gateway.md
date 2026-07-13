# ADR-0016: Use a One-Way, Verified Import Gateway

## Status

Accepted.

## Decision

Owned pre-canonical workspace formats enter the canonical runtime only through the Import Gateway. Detection and preview open source material read-only, bind a durable preview to a complete source fingerprint, and expose ambiguity, unsupported facts, unknown fields, identity correspondence, and semantic delta without guessing.

An import requires authenticated approval and an immutable operation plan. Canonical facts are staged separately, logically verified, and promoted by a durable M8 effect. The import receipt and monotonic canonical-only marker are appended only after that effect settles. An uncertain promotion is M9 recovery work, not permission to repeat an outward mutation.

Canonical schema convergence is owned by Storage Authority and is never modeled as domain import. Compatibility operation facts are reused as the import operation journal; no parallel import ledger is permitted.

After canonical-only authority is marked, invoking a legacy runtime reader is a defect. Adapters are ingress-only. Retirement requires a portfolio snapshot that links every owned fixture to a successful receipt, canonical-only execution, and an adapter-disabled parity result; discovering another owned format supersedes that exhaustion fact.

## Consequences

- Repeated identical imports return the original receipt.
- Source identity is preserved only when type, scope, and meaning agree; otherwise correspondence is immutable and explicit.
- Legacy source bytes remain non-authoritative evidence and are never dual-written.
- Unknown, mixed, malformed, stale, or fidelity-mismatched inputs fail closed with a typed result.
