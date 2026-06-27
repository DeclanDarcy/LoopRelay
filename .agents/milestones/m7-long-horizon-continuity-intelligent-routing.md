# Phase 7 - Router Reuse and Transfer

Goal: wire the decision-session lifecycle router to the live Decision process so the loop can reuse or transfer as designed.

## Implementation

- [ ] Call `IDecisionSessionLifecyclePolicy.EvaluateAsync(repositoryId)` after every operational continuation and handoff rotation.
- [ ] Feed the active Decision session token count or deterministic fallback estimate into the router.
- [ ] Implement `Continue` as warm reuse:
  - [ ] keep the active Decision process open;
  - [ ] submit `GetNextDecisions.Render(handoff)`;
  - [ ] stream and capture decisions;
  - [ ] return to editable decision review.
- [ ] Implement `Transfer` as the explicit transfer sequence:
  - [ ] call `IDecisionSessionTransferEligibilityService.CheckAsync`;
  - [ ] submit `ProduceOperationalDelta.Text` to the active Decision process;
  - [ ] capture stdout and write `.agents/operational_delta.md`;
  - [ ] close the active Decision process;
  - [ ] run `UpdateOperationalContext.Text` as an Operational, ExtraHigh, one-shot turn;
  - [ ] verify rewritten `.agents/operational_context.md`;
  - [ ] start a fresh Decision, ExtraHigh, held-open process;
  - [ ] submit `StartDecisionSessionFromTransfer.Render(File.ReadAllText(.agents/operational_context.md))`;
  - [ ] submit `GetNextDecisions.Render(handoff)`;
  - [ ] stream and capture decisions;
  - [ ] return to editable decision review.
- [ ] Keep observed token accounting as the target implementation and deterministic token estimates as fallback until live accounting is certified.
- [ ] Record prompt provenance for `ProduceOperationalDelta`, `UpdateOperationalContext`, `StartDecisionSessionFromTransfer`, and transfer-triggered `GetNextDecisions`.

## Certification

- [ ] Router `Continue` reuses the same warm Decision process.
- [ ] Router `Transfer` produces an operational delta, rewrites operational context, closes the old Decision process, and starts a new one.
- [ ] Transfer eligibility blocks unsafe transfer.
- [ ] The loop continues after both reuse and transfer paths.
- [ ] Tests cover token-threshold routing, fallback estimates, transfer failure windows, and process cleanup.
