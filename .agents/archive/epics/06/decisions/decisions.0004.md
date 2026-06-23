# Decisions

## Newly Authorized

- Treat Milestone 1 as complete: event substrate, thread grouping, relationship persistence, services, endpoints, shell bridge commands, basic UI, and characterization coverage now satisfy the milestone intent.
- Start Milestone 2 with assisted/inferred capture from authoritative decision lifecycle transitions, but prefer decision supersession before broader proposal-resolution capture.
- For the first Milestone 2 slice, reasoning capture must observe an already-authoritative decision transition; it must not independently decide that the transition happened.
- The first Milestone 2 capture implementation must preserve idempotency so re-running capture does not create duplicate reasoning events.
- Keep the Reasoning UI read-only for explanation, navigation, and history; do not add decision authoring, status mutation, or workflow ownership through the Reasoning workspace.
- Keep Tauri as a bridge only. Reasoning behavior and capture logic remain backend-owned.
