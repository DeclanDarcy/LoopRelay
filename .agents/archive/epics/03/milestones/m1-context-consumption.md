# Milestone M1 - Operational Context Consumption

## Objective

Make `.agents/operational_context.md` a first-class optional execution input.

## Backend Changes

- [x] Add `IArtifactService.GetCurrentOperationalContextAsync`.
- [x] Extend `ArtifactService` to load current operational context through the same safe relative-path logic used for handoff and decisions.
- [x] Add `.agents/operational_context.md` as an optional artifact in `ExecutionContextService`.
- [x] Use artifact role `OperationalContext`.
- [x] Update `ExecutionPromptBuilder.ArtifactRoleOrder` to:

```text
Plan
Milestone
OperationalContext
CurrentHandoff
CurrentDecisions
```

- [x] Update prompt text to include an operational-context section when present.
- [x] Update `ExecutionPromptMetadata.IncludedArtifactPaths` ordering expectations.
- [x] Extend diagnostics so operational context contributes byte count, character count, warning threshold status, and hard-limit status.
- [x] Missing operational context must be reported as an optional missing artifact and must not block preview or launch.
- [x] Empty operational context is allowed.
- [x] Oversized operational context uses existing context size diagnostics; hard-limit excess blocks launch through the existing `LaunchBlocked` path.

## UI Changes

- [x] Execution context preview must show operational context presence, size, and content.
- [x] The context artifact list must display `OperationalContext` between milestone and handoff.
- [x] Context diagnostics must show operational-context contribution.
- [x] No generation, review, lifecycle, or promotion controls are added in this milestone.

## Tests

Add or update backend tests:

- [x] `ExecutionContextServiceTests` verifies operational context is included when present.
- [x] Missing operational context is optional and reported.
- [x] Operational-context size contributes to aggregate and per-artifact diagnostics.
- [x] Hard-limit excess blocks launch.
- [x] `ExecutionPromptBuilderTests` verifies ordering and prompt inclusion.
- [x] Endpoint test verifies preview returns operational context artifact data.

## Certification

Operational context is certified as a passive execution input when:

- [x] It is discovered and loaded when present.
- [x] It is optional when missing.
- [x] It appears in prompt output and metadata.
- [x] It appears in preview and diagnostics.
- [x] Providers remain unaware of artifact source details.
