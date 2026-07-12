# ADR-0004: Compose Templates and Policy Profiles Before Prompt Hashing

- Status: Accepted
- Date: 2026-07-11
- Owners: Prompt Authority, Policy Authority, Runtime Authority

## Context

One branch appends policy text at prompt send sites. The convergence branch moves implementation-first policy text into individual templates. Both approaches conflate prompt templates and policy or make their relationship difficult to evolve explicitly.

## Decision

Prompt templates and policy profiles are distinct versioned inputs owned by their respective authorities.

The canonical flow is:

```text
Versioned template + resolved policy profile + consumed input facts
  -> Canonical Prompt Renderer
  -> Rendered Prompt
  -> Content hash and provenance
  -> Durable rendered-prompt fact
  -> Runtime send
```

Everything that can influence provider-visible prompt content must be composed before hashing and persistence. Send sites and provider adapters may not append, remove, or reorder prompt instructions.

The rendered-prompt fact records template identity and source hash, policy identity and profile version, consumed-input identities, final rendered text and hash, and attempt/session/turn correlation. Production execution may not send a prompt until this fact is durably recorded.

Invariant template instructions remain in templates. Rules selected or evolved as policy remain in typed policy profiles. Shared policy prose is not duplicated into templates merely to make hashing convenient.

## Consequences

- Dynamic post-render policy appending is retired.
- Template-owned copies of policy prose must migrate to explicit policy profiles unless the text is truly an invariant part of that template's contract.
- Test-only prompt overrides use declared policy profiles and participate in the final hash.
- Prompt rendering and evidence persistence become required runtime capabilities, not best-effort helpers.
