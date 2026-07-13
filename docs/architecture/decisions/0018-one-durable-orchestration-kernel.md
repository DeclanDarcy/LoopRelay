# ADR-0018: Use One Durable Orchestration Kernel

## Status

Accepted.

## Decision

The production composition root constructs exactly one `OrchestrationKernel`. It is the only
coordinator authorized to spend an observation budget, select fresh or recovery attempt
authorization, invoke the catalog-selected chain, reobserve owner projections, choose continued
work, and persist an immutable decision fact for every observation cycle.

Invocation locates the single nonterminal root for the workspace, chain, and invocation mode.
Zero matching roots creates one; one exact-catalog root is re-entered; multiple roots are
ambiguous; and a catalog mismatch is recovery-required. Successor workflows receive distinct
workflow-instance identities beneath the same root. Budget exhaustion is passive waiting and
does not create a durable stall.

`TransitionRuntime` remains one authorized dispatch attempt. It may register candidates and
atomically promote validated products/state/effect intents, but it does not retry, present
interactions, execute recovery, settle effects, select successors, or loop. Provider capability
authorization uses the compatibility evidence observed for the composed runtime; handlers may
not synthesize a broader capability profile or read ambient provider configuration.

All causally required facts fail closed. If a required write fails before outward work, no
canonical advance is recorded. If outward work may have started and its result is unknown, the
kernel returns recovery-required with the last durable causal identity.

## Consequences

- CLI parsing and rendering do not advance workflow state.
- Every kernel boundary is restart-auditable by catalog, snapshot, lineage, alternatives,
  selected action, outcome, and evidence.
- Feature handlers are candidate/evidence transformations; progression and continuation are
  catalog-driven kernel responsibilities.
- Runtime, telemetry, prerequisite, usage-limit, and input-wait services remain policy-composed
  wrappers around the exact provider runtime.
