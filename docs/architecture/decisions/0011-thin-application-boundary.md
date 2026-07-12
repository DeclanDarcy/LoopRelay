# ADR-0011: Establish a Thin Application Boundary

- Status: Accepted
- Date: 2026-07-11
- Owners: Application Authority, Composition Root, CLI

## Decision

The CLI depends on an explicit application boundary. CLI invocations are translated into typed
commands and queries, including workflow execution, status, storage, and recovery use cases. The
application service invokes the canonical kernel and returns a typed `ApplicationCommandResult`.
The result carries outcome, evidence, warnings, pending effects, required actions, and suggested
exit semantics.

The application boundary coordinates use cases, authorization, and result projection. It does not
implement workflow transitions, recovery decisions, prompt composition, runtime dispatch, or
effect execution. Those mechanics remain owned by the kernel and their established authorities.

`Program` is a composition and invocation entry point. Production configuration and policy are
resolved once, required capabilities are validated, the application is constructed, and the parsed
invocation is forwarded. Missing required production capabilities fail composition.

Status is returned as an immutable `CanonicalCliStatusSnapshot` containing canonical workflow,
policy, storage, recovery, interaction, effects, compatibility, warning, and required-action facts.
The CLI formatter is a pure snapshot renderer and performs no repository, persistence, or recovery
queries.

Resume behavior is a resolved `ResumePolicy` with a stable `ResumePolicyIdentity`. Environment and
CLI inputs may contribute invocation-layer policy values, but runtime consumes only the resolved
policy. The retired `DecisionResumeComposition` shim is not an authority.

## Consequences

- `UnifiedCliRunner` translates input, renders typed results, and maps suggested exit semantics.
- Application services may query canonical read models; CLI adapters may not.
- Composition constructs, validates, and wires dependencies without selecting workflow mechanics.
- The same application contract can support future non-CLI adapters without duplicating kernel logic.
- Clean-input integration tests commit repository inputs before invoking canonical gates.
