# ADR-0008: Separate Single-Attempt Execution from Recovery and Effects

- Status: Accepted
- Date: 2026-07-11
- Owners: Orchestration Kernel, Recovery Authority, Runtime Authority, Effect Coordinator

## Context

The merged branches offered two incomplete transition runtimes. One preserved interruption-boundary classification and in-process recovery, while the other added causal attempts, read receipts, warnings, and workflow-instance lineage. Combining both inside `TransitionRuntime` would make it the owner of execution, retry policy, recovery, effects, and progression.

## Decision

`TransitionRuntime` executes exactly one typed, already-authorized attempt. It resolves and gates inputs, freezes causal inputs and policy, mints or consumes an authorized transition-run identity, mints a distinct attempt identity, persists intent and read receipts, persists the exact rendered prompt fact and dispatch intent, dispatches once, persists the normalized raw outcome, registers and validates candidates, revalidates input freshness, and commits promotion, state, and effect intents atomically.

Provider dispatch is an external-effect boundary. Once dispatch intent is durable, a thrown or missing normalized result is `RecoveryRequired`; it is never proof that submission did not occur.

Recovery Authority classifies durable evidence and persists a typed `TransitionRecoveryPlan`. A retry retains the logical transition-run identity and receives a new attempt identity. Raw-output reuse leaves its source attempt immutable. Recovery planning does not execute provider work, effects, or workflow progression.

External effects are enqueued during the authoritative state transaction. Effect Coordinator executes and reconciles those intents after the attempt. `EffectsPending`, attempt completion, transition completion, and permission to advance are distinct states.

Workflow Controller re-observes canonical state after every attempt/effect cycle. Workflow chaining uses promoted product identities, preserves the root run, creates a new workflow-instance identity for each successor, and emits typed stop reasons.

Required causal persistence fails closed. Optional formatting and diagnostic enrichment may be best effort, but cannot substitute for gates, outcomes, or authoritative evidence.

## Consequences

- `TransitionRuntime` has no retry, resume, fork, reconciliation, chaining, or human-escalation policy.
- Nullable caller-supplied run IDs are prohibited; fresh and recovery attempts use typed authorization.
- Prompt dispatch cannot occur before attempt intent, read receipt, policy identity, rendered prompt fact, and dispatch intent are durable.
- Candidate evidence survives invalidation, validation failure, and concurrent-state rejection.
- Product promotion, lifecycle evidence, current-state projection, attempt completion, and required effect intents share one SQLite transaction.
- Generic blocker storage is not a runtime progression authority; specific outcomes and remediation evidence remain observable.
