# Decisions: 2026-06-27 M1.1 Normalization and Stability Direction

These decisions capture only newly authorized direction from the user response following M1.1 Contract Taxonomy and Ownership Slice 0060.

## Authorized Decisions

1. Treat M1.1 Slice 0060 as a valid continuation of the canonical contract model.
   - The slice correctly progresses from identity into taxonomy and ownership.
   - The slice preserves the M1.1 boundary by avoiding generation, fixture expansion, shell migration, TypeScript migration, and dev mock generation.
   - Taxonomy and ownership should constrain later generation rather than being inferred from it.

2. Continue M1.1 with normalization and boundary semantics before M1.2 generation.
   - The next slice should define canonical normalization rather than implementation-specific serialization mechanics.
   - Each normalization topic should answer: canonical representation, allowed producer variation, consumer guarantees, and compatibility evolution.
   - The slice should cover identifiers, enums, dates, null versus omitted values, collections, metadata, ordering, diagnostics, compatibility fields, request and response boundaries, error envelopes, and streams.

3. Add contract stability as a first-class M1.1 concept.
   - Stability is distinct from versioning and compatibility.
   - The model should define which properties participate in contract identity, which are observational metadata, which are additive, which are intentionally unstable, and which may change without changing contract identity.
   - The model should define which changes require a new contract identity.
   - This distinction should prevent later Oracle, fixture, generator, compatibility, and regression mechanisms from conflating any JSON change with a contract identity change.

4. Preserve the current M1.1 sequence.
   - The intended sequence remains identity, taxonomy, ownership, normalization, boundary semantics, canonical examples, M1.1 completion, then M1.2 generation architecture.
   - Canonical examples should come after normalization and boundary semantics so they instantiate the model rather than inventing new rules.

## Evidence Targets

- `.agents/milestones/m1.1-contract-taxonomy-ownership-slice-0060.md`
- `docs/contracts.md`
- `docs/architectural-capabilities.md`
- `.agents/handoffs/handoff.md`

## Next Authorized Sequence

1. Stage the M1.1 taxonomy/ownership slice, handoff rotation, decision rotation, and this decision checkpoint.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
