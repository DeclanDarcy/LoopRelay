# Milestone M1 - Operational Context Consumption

## Objective

Make `.agents/operational_context.md` a first-class optional execution input.

## Backend Changes

- [ ] Add `IArtifactService.GetCurrentOperationalContextAsync`.
- [ ] Extend `ArtifactService` to load current operational context through the same safe relative-path logic used for handoff and decisions.
- [ ] Add `.agents/operational_context.md` as an optional artifact in `ExecutionContextService`.
- [ ] Use artifact role `OperationalContext`.
- [ ] Update `ExecutionPromptBuilder.ArtifactRoleOrder` to:

```text
Plan
Milestone
OperationalContext
CurrentHandoff
CurrentDecisions
```

- [ ] Update prompt text to include an operational-context section when present.
- [ ] Update `ExecutionPromptMetadata.IncludedArtifactPaths` ordering expectations.
- [ ] Extend diagnostics so operational context contributes byte count, character count, warning threshold status, and hard-limit status.
- [ ] Missing operational context must be reported as an optional missing artifact and must not block preview or launch.
- [ ] Empty operational context is allowed.
- [ ] Oversized operational context uses existing context size diagnostics; hard-limit excess blocks launch through the existing `LaunchBlocked` path.

## UI Changes

- [ ] Execution context preview must show operational context presence, size, and content.
- [ ] The context artifact list must display `OperationalContext` between milestone and handoff.
- [ ] Context diagnostics must show operational-context contribution.
- [ ] No generation, review, lifecycle, or promotion controls are added in this milestone.

## Tests

Add or update backend tests:

- [ ] `ExecutionContextServiceTests` verifies operational context is included when present.
- [ ] Missing operational context is optional and reported.
- [ ] Operational-context size contributes to aggregate and per-artifact diagnostics.
- [ ] Hard-limit excess blocks launch.
- [ ] `ExecutionPromptBuilderTests` verifies ordering and prompt inclusion.
- [ ] Endpoint test verifies preview returns operational context artifact data.

## Certification

Operational context is certified as a passive execution input when:

- [ ] It is discovered and loaded when present.
- [ ] It is optional when missing.
- [ ] It appears in prompt output and metadata.
- [ ] It appears in preview and diagnostics.
- [ ] Providers remain unaware of artifact source details.
