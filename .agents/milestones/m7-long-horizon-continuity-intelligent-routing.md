# Phase 7 - Router Reuse and Transfer

Goal: route the continuation loop's next decision turn to either reuse the warm Decision process or transfer
(recycle) it, preserving continuity through operational context.

## Design note (m7 course-correction)

The original plan wired the router to the `DecisionSessions` lifecycle policy
(`IDecisionSessionLifecyclePolicy.EvaluateAsync`) and transfer-eligibility service
(`IDecisionSessionTransferEligibilityService.CheckAsync`). Those services are part of the **registry-based**
DecisionSessions subsystem: they require a persisted, *active* `DecisionSession` aggregate and throw / block
without one. The Plan-Authoring loop (m5-m7) is deliberately **registry-free** — it drives its own warm
`IAgentSession` and never creates registry aggregates — so wiring those services made `Transfer` structurally
unreachable in production (the policy always threw `KeyNotFoundException`, which degraded to `Continue`).

Per the architecture principle (the new loop owns its flow), m7 routes on the loop's **own** decision-session
token pressure instead: a registry-free `IDecisionSessionRouter` thresholds `RouterInputs`, and a loop-owned
eligibility gate (a *primed* Decision process must exist) replaces the registry eligibility service. The
inline transfer *mechanics* (delta → context rewrite → recycle) are unchanged. The registry-based
DecisionSessions services remain for their own HTTP/diagnostics consumers; they are simply not the loop's router.

## Implementation

- [x] Evaluate the router after every operational continuation and handoff rotation (`RouteNextDecisionRunAsync`).
- [x] Route on the active Decision session's observed token count, with a deterministic content estimate as the
      fallback before any turn is observed (`ComputeRouterInputs` -> `RouterInputs` -> `DecisionSessionRouter`).
- [x] Implement `Continue` as warm reuse:
  - [x] keep the active Decision process open;
  - [x] submit `GetNextDecisions.Render(handoff)`;
  - [x] stream and capture decisions;
  - [x] return to editable decision review.
- [x] Implement `Transfer` as the explicit transfer sequence (gated by eligibility + the execution gate):
  - [x] require a primed Decision process (`decisionSeeded`); an ineligible Transfer degrades to reuse;
  - [x] claim the execution gate for the rewrite (mutual exclusion with a concurrent continuation);
  - [x] submit `ProduceOperationalDelta.Text` to the active Decision process;
  - [x] capture output and write `.agents/operational_delta.md`;
  - [x] close the active Decision process;
  - [x] run `UpdateOperationalContext.Text` as an Operational, ExtraHigh, one-shot turn;
  - [x] verify rewritten `.agents/operational_context.md`;
  - [x] start a fresh Decision, ExtraHigh, held-open process;
  - [x] submit `StartDecisionSessionFromTransfer.Render(<rewritten operational context>)`;
  - [x] submit `GetNextDecisions.Render(handoff)`;
  - [x] stream and capture decisions;
  - [x] return to editable decision review.
- [x] Keep observed token accounting as the routing signal and deterministic token estimates as the fallback.
- [x] Record prompt provenance for `ProduceOperationalDelta`, `UpdateOperationalContext`,
      `StartDecisionSessionFromTransfer`, and transfer-triggered `GetNextDecisions`.

## Certification

- [x] Router `Continue` reuses the same warm Decision process.
- [x] Router `Transfer` produces an operational delta, rewrites operational context, closes the old Decision
      process, and starts a new one (verified through the REAL router, not only a fake).
- [x] Transfer eligibility blocks unsafe transfer (an unseeded process degrades to warm reuse).
- [x] The loop continues after both reuse and transfer paths.
- [x] Tests cover token-threshold routing, fallback estimates, transfer failure windows (delta / rewrite /
      reseed), and process cleanup.
