# Roadmap Unblock Review

The Roadmap CLI separates three operator intents:

- `status` reports persisted workflow state without preflight or mutation.
- `run` executes active workflow transitions and preserves blocked, failed, completed, and terminal paused states as report-only states.
- `unblock` performs explicit recovery review for persisted blocked recovery states.

Normal `run` does not retry blocked states. A blocked state means the runtime stopped because a safety contract failed or required operator repair. Recovery is only allowed through `unblock`, which revalidates persisted evidence before writing a new transition.

Preflight, resume-planning, and generic runtime blockers discovered by `run` are ephemeral. They are reported to the operator, but `run` does not convert them into `EvidenceBlocked`, append blocker rows, or write generic blocker evidence. The last durable workflow state remains authoritative until the operator fixes the condition and reruns, or manually updates state to record a durable blocker.

## Review Model

`RoadmapUnblockPlanner` is the recovery boundary. It treats `TransitionIntent` as recovery evidence, not dispatch authority. The planner combines:

- persisted current state
- transition intent and dispatch state
- prior transition output and evidence paths
- Project Context health
- repaired artifact existence and hashes
- execution preparation freshness
- intent-specific recovery rules

Successful unblock writes durable unblock-review evidence, appends a journal record, and transitions to the safe next workflow state. Failed unblock preserves the original current state, last transition, transition intent, and blockers, then appends review evidence and a non-duplicated review blocker.

## Supported Intents

- `ResolveBlocker`: supported only for fresh `Preflight` blockers. It reruns Project Context preflight, verifies roadmap source readiness, records inspected source hashes, and recovers to `CoreReady`.
- `ResolveMalformedExecutionOutput`: requires exactly one execution evidence path, reparses the repaired `Execution Disposition`, validates the protocol pair, and routes to the validated execution outcome.
- `ResolveInvalidCompletionCertification`: requires completion evaluation evidence, reparses it, validates completion certification policy, and routes through the completion router. Completion-context updates still use the existing explicit prompt route when the selected completion route requires it.
- `RepairExecutionRuntimeFailure`: verifies active epic, milestone specs, operational context, execution prompt, compatibility artifacts, and execution-preparation provenance before recovering to `ExecutionPromptReady`.

## Report-Only Intents

The planner deliberately does not guess for blockers without deterministic recovery contracts. These remain report-only and produce unsupported-review evidence under explicit `unblock`:

- `ResolveArtifactPromotionBlocker`
- `ResolveSplitEpicBlocker`
- `ResolveTransitionFailure`
- `ResolveExecutionBlocker`
- unregistered future intents

This keeps blocked states safe by default while making repaired supported states recoverable through an explicit, evidence-driven domain transition.
