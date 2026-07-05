# Roadmap Execution Interpretation Boundary

## Current Boundary Failure

The roadmap CLI currently treats a completed execution-agent turn as epic completion. That conflates transport completion with roadmap-domain meaning. A completed agent turn only proves the process returned successfully; it does not prove the epic is complete, blocked, malformed, or ready for certification.

## Boundary Decision

Execution transport is responsible for invoking the execution agent and returning transport evidence: agent state, output, and diagnostics.

Execution interpretation is a separate domain boundary. It consumes transport evidence, parses the required execution disposition contract when transport completed, and returns a typed execution outcome.

Workflow routing consumes only the typed execution outcome. It persists execution evidence before routing, and it never derives roadmap transitions directly from transport success.

Completion certification remains independent. It runs only after an explicit execution outcome of `EpicComplete`, and it receives the persisted execution evidence as causal input.

## Outcome Model

Execution interpretation distinguishes:

- `EpicComplete`: the execution agent explicitly claims implementation reached epic completion.
- `ContinueRequired`: execution completed a valid turn but more execution is required.
- `ExecutionBlocked`: execution encountered a roadmap-domain blocker.
- `RuntimeFailure`: the execution transport failed or ended in a non-completed process state.
- `MalformedOutput`: transport completed, but the execution output did not satisfy the disposition contract.

These are represented as an enum rather than booleans so future outcomes can be added without collapsing domain semantics again.

## Evidence Flow

Every execution turn produces durable execution evidence under `.agents/evidence/execution`. The evidence includes transport status, interpreted outcome, parser/diagnostic details, disposition fields when present, and the raw execution output.

The workflow state transition references that evidence path. Certification context and transition input snapshots include the execution evidence path when and only when execution explicitly claims completion.

## Lifecycle Policy

Continuation preserves `ExecutionLoop` and keeps the active epic lifecycle as `Executing`.

Execution blockers route to `ExecutionBlocked` and keep the active epic lifecycle as `Executing`; the workflow state, blocker rows, and evidence classify the pause.

Malformed output blocks in `EvidenceBlocked` because it is an execution-contract or parser failure, not a domain execution blocker.

Runtime failure routes to `Failed` because it is infrastructure/transport failure, not a roadmap-domain blocker.
