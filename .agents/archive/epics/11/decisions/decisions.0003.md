# Decisions: 2026-06-26 Milestone 0.1 Acceptance

These decisions capture only the newly authorized direction from the response accepting Milestone 0.1.

## Authorized Decisions

1. Milestone 0.1 is accepted against the implementation plan intent.
   - Accepted scope: local compiler trust, local verifier trust, verification inventories, verification governance, and quarantine model.
   - Explicit non-scope: CI reproducibility, shell behavioral correctness, transport passivity, and contract correctness.

2. Durable architecture documentation should continue growing from certified capabilities upward.
   - Rationale: avoid defining broad architecture ahead of certified implementation evidence.
   - Current application: `docs/architectural-capabilities.md` and `docs/architectural-mechanisms.md` are accepted as narrow M0.1 documentation seeds.

3. The next shell work should be framed as the first executable architectural invariant, not merely the first Rust test.
   - Invariant: the shell is transport, not semantic authority.
   - First mechanism: a JSON relay regression proving opaque backend JSON payloads are preserved without shell-owned domain interpretation.

4. Before implementing the relay regression, record the reusable transport invariant shape in an architectural artifact.
   - Suggested home: `docs/architectural-mechanisms.md` or colocated test documentation.
   - Minimum matrix seed:
     - transport preserves payload semantics,
     - transport preserves unknown fields,
     - transport preserves null/empty,
     - transport preserves backend errors.

5. Milestone 0.2 is ready to begin after the preparatory shell passivity invariant slice is recorded and protected.

## Explicit Non-Decisions

- No CI implementation is authorized yet.
- No contract Oracle implementation is authorized in this response.
- No production passive-transport migration is authorized before the first invariant artifact/regression slice.
