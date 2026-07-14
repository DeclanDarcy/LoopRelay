# ADR-0009: Canonical Prompt Dispatch Gateway

- Status: Accepted
- Date: 2026-07-11
- Owners: Prompt Authority, Runtime Authority, Persistence Authority, Recovery Authority

## Context

Prompt sends were split between direct provider calls and optional, best-effort rendered-prompt
capture. Some runners also selected runtime settings or materialized provider output into the
repository. A crash or evidence-write failure could therefore leave an untraceable provider
effect, and multi-turn sends had no immutable prompt-fact identity.

## Decision

All provider-visible prompts follow one canonical boundary:

```text
Invariant template + resolved prompt-policy profile + consumed inputs
  -> PromptComposition
  -> RenderedPromptFact (hash, bytes, encoding, causality, persistence receipt)
  -> Dispatch authorization (policy, profile, runtime profile, attempt, session/turn)
  -> Dispatch lifecycle intent
  -> identity-only Runtime dispatch
  -> provider evidence
```

Prompt Authority composes template and profile before hashing. It persists the immutable prompt
fact and `Planned`/`Authorized` lifecycle events before Runtime Authority may dispatch. Runtime
receives only `RenderedPromptFactIdentity` plus authorization identities and lazily loads the
persisted bytes. It cannot replace provider-visible text.

The dispatch lifecycle is `Planned`, `Authorized`, `Started`, optional `Accepted`, and
`Observed`; `Failed` and `Cancelled` are terminal non-success states. Any exception after
`Started` that prevents a trustworthy terminal observation records `Unknown` and transfers
ownership to Recovery Authority.

Every multi-turn send binds a canonical agent-session identity, turn identity, and rendered
prompt-fact identity. Streaming remains a Runtime concern. Prompt runners do not choose model,
effort, sandbox, permissions, or retry behavior, and never materialize repository state.

## Invariants

- Failure to persist the prompt fact or pre-dispatch lifecycle intent prevents provider dispatch.
- No send site appends, removes, or reorders instructions after canonical composition.
- Every provider turn references exactly one persisted rendered-prompt fact.
- Provider-evidence persistence failure after dispatch is a recovery condition, never permission
  to redispatch blindly.
- Completion and Effect authorities own synthesis validation and repository mutation.

## Consequences

- Optional rendered-prompt capture is retired.
- Policy prose is independently versioned and is not duplicated across invariant templates.
- Runtime profile identity is auditable without copying model-specific configuration into prompt
  contracts.
- Tests are separated by authority: composition/persistence/ordering, provider transport,
  recovery reconciliation, and completion effects.
