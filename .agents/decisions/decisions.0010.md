# Decisions

## Newly Authorized

- Proceed with the next M9 slice as continuation event/history persistence.
- Add `WorkflowContinuationEvent` and endpoint-triggered continuation history
  persistence.
- Keep continuation history conceptually separate from workflow timeline.
- Continuation history records what Workflow evaluated, what Workflow decided,
  why it advanced, and why it stopped.
- Workflow timeline remains the record of lifecycle facts: what happened.
- Continuation events should capture input fingerprint, current stage, target
  stage, blocking gate, decision, and reason.
- Continue proving evaluation, explanation, persisted evaluation, and recovery
  of evaluation evidence before allowing preparation behavior.

## Explicitly Deferred

- Do not add preparation service yet.
- Do not add hosted continuation yet.
- Do not invoke domain commands yet.
- Do not create decisions, operational-context proposals, or commit
  preparations yet.
- Re-evaluate the roadmap before permitting preparation services, because
  preparation is the first intentional step from observation into controlled
  domain interaction.
