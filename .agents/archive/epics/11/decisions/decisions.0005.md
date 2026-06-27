# Decisions: 2026-06-26 Slice 0005 Acceptance and Milestone 0.2 Transition

These decisions capture only newly authorized direction from the response accepting slice 0005.

## Authorized Decisions

1. Slice 0005 is accepted as the natural transition point from verification mechanisms to Oracle definition.
   - Accepted invariant: the shell preserves backend-owned error semantics without reinterpretation for boundary-violation error envelopes.
   - Accepted coverage remains partial, not certified passive transport.

2. No additional shell-preparation slices should be added before beginning Milestone 0.2.
   - Rationale: remaining transport work belongs naturally to Milestone 1.3 after contract authority exists.
   - Scope boundary: POST relay, command-family classification, mirror retirement, and broader transport migration are deferred.

3. Milestone 0.2 should begin with a contract relationship matrix, not with golden fixtures.
   - Required matrix fields: contract identity, owning projection or command, serialization authority, producer, consumers, parallel representations, compatibility obligations, planned Oracle fixture, and migration priority.

4. Milestone 0.2 sequencing is authorized as:
   - contract surface inventory,
   - boundary taxonomy,
   - consumer taxonomy,
   - parallel truth matrix,
   - serialization architecture,
   - first Oracle fixture.

5. Oracle fixtures must observe authoritative contracts after ownership is established.
   - Rationale: fixtures must not accidentally certify whichever duplicated representation currently exists.
   - Oracle role: observation point for authoritative contracts, not a new source of authority.

## Explicit Non-Decisions

- No production passive transport migration is authorized by this acceptance.
- No additional pre-Milestone 0.2 shell passivity preparation is authorized.
- No golden contract fixture should be created before inventory, ownership, consumer mapping, parallel-truth identification, and serialization boundary definition.
