# Decisions: 2026-06-26 Slice Response

These decisions capture only the newly authorized direction from the response to Milestone 0.1 Slice 0002.

## Authorized Decisions

1. Milestone 0.1 should certify the local verification baseline now rather than adding CI first.
   - Rationale: M0.1's immediate purpose is to make the existing verification substrate trustworthy; adding CI can become a separate infrastructure slice and expand Phase 0 indefinitely.

2. CI absence is quarantined for Milestone 0.1 certification.
   - Owner: Milestone 0.1 / verification governance.
   - Risk: local verification can be healthy while remote verification remains absent.
   - Retirement condition: CI quarantine retires when a minimal workflow runs the same canonical local verification set or a documented supported subset.

3. The first Rust shell behavioral invariant is passive response relay.
   - Invariant: opaque backend JSON responses are relayed without shell-owned domain interpretation.
   - Minimum first regression: arbitrary backend JSON with unknown fields, nested objects, arrays, nulls, and enum-like strings is returned as the same JSON body without deserializing into domain-shaped structs.
   - Rationale: this directly protects later Milestone 1.3 passive transport work while remaining small.

4. `.NET` build/test remain serialized as a formal verifier execution rule.
   - Retirement condition: output path isolation is implemented and proven by verification evidence.

5. The next slice is Milestone 0.1 certification/governance.
   - Required content: structural verification certification package, CI absence quarantine, Rust behavioral coverage quarantine, serialized `.NET` execution rule, and first shell passivity regression specification as a bridge into M0.3 / M1.3.

## Certification Posture

Milestone 0.1 can certify after the next slice if the package explicitly states:

- local verification is healthy enough to support architectural refactoring,
- CI is not yet a verified path,
- Rust shell behavior is not yet behaviorally protected,
- and the CI/Rust gaps are quarantined rather than hidden.

## Explicit Non-Decisions

- No CI implementation is authorized yet.
- No production architecture migration is authorized.
- No shell startup/process lifecycle regression is selected as the first Rust behavioral invariant.
