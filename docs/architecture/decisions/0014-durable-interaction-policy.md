# ADR 0014: Durable interaction policy

Status: Accepted  
Date: 2026-07-12

## Decision

Accept D6. Dirty-input offers, import conflicts, recovery ambiguity, and completion ambiguity have no hidden timeout or default. Headless callers receive the category-specific typed outcome and do not create a request. Requests and their resolved category policy are durable before presentation. Responses are isolated with compare-and-set, immutable IDs, schema hashes, semantic idempotency, and one active resolution per request.

A response that authorizes mutation must carry durable responder trust and authorization evidence. Identical duplicate responses return the accepted response and resolution; conflicting, late, expired, or schema-invalid responses append a rejection event without replacing accepted state. Only a durable `ResumeAuthorized` fact may resume kernel or recovery work.

## Consequences

The broker never reads console input and never performs the requested mutation. Dirty-input acceptance creates a scoped M8 effect intent; rejection remains `DirtyInputSurface`. Other reserved categories stay inactive until their owning milestones integrate them.
