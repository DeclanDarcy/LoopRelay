# Milestone 9: Workflow Continuation Engine

Objective: automate mechanical progression between non-authority stages, introduce separately governed artifact preparation, and stop at every human gate.

Deliver:

- [x] `IWorkflowContinuationService`.
- [x] `IWorkflowPreparationService`.
- [x] `WorkflowContinuationEvaluation`.
- [x] `WorkflowPreparationEvaluation`.
- [x] continuation rules for execution complete, handoff accepted, decision resolved, context promoted/rejected/not required, commit executed, push executed, no changes, and completed workflow.
- [ ] preparation rules for decision discovery/generation, operational-context proposal generation/linkage, and commit preparation.
- [x] gate halting for work selection, execution acceptance, decision resolution, operational context review, operational context promotion, commit approval, and push approval.
- [x] `WorkflowContinuationDiagnostics`.
- [x] `WorkflowPreparationDiagnostics`.
- [x] `WorkflowContinuationEvent`.
- [x] `WorkflowPreparationEvent`.
- [x] recovery integration that reevaluates continuation after restart without duplicate progression.
- [ ] `WorkflowContinuationHostedService`.
- [ ] `WorkflowInfluenceTrace`.
- [ ] `WorkflowHealthAssessment`.

Slice progress:

- Added evaluation-only continuation service and fingerprinting.
- Added `GET /api/repositories/{repositoryId}/workflow/continuation/evaluation`.
- Added endpoint-triggered continuation event persistence with
  `POST /api/repositories/{repositoryId}/workflow/continuation/run`.
- Added continuation history read endpoint:
  `GET /api/repositories/{repositoryId}/workflow/continuation/history`.
- Added continuation event JSON and markdown evidence under
  `.agents/workflow/continuation`.
- Continuation events capture trigger, input fingerprint, current stage,
  optional target stage, blocking gate, decision, stop/advance reason, required
  human action, completion state, and diagnostics.
- Re-running continuation with an identical evaluation fingerprint returns the
  existing event instead of duplicating history.
- Continuation evaluation now uses the latest persisted workflow timeline as
  the coordinator stage when one exists, then compares it to the current
  domain-derived projection.
- Endpoint-triggered continuation can persist a one-step workflow timeline
  progression when domain evidence has reached or passed the next canonical
  stage and the coordinator stage has no open gate.
- A repeated run after a progression stops at the newly exposed authority gate
  instead of duplicating the same timeline progression.
- Added persisted one-step progression coverage for accepted handoff to
  decision, resolved decision to operational context, and completed operational
  context to commit.
- Added persisted one-step progression coverage for committed evidence to push,
  pushed evidence to completed, no-change completion, and work-selection gate
  halting after completed progression.
- Added recovery/idempotency coverage for completed restart reconstruction,
  domain-over-stale-timeline recovery, no-change completion recovery, and
  restart continuation re-evaluation without duplicate completed stop events.
- Added preparation evaluation and event persistence without invoking domain
  commands.
- Added preparation endpoints:
  `GET /api/repositories/{repositoryId}/workflow/preparation/evaluation`,
  `POST /api/repositories/{repositoryId}/workflow/preparation/run`, and
  `GET /api/repositories/{repositoryId}/workflow/preparation/history`.
- Added preparation event JSON and markdown evidence under
  `.agents/workflow/preparation`.
- Preparation events capture trigger, input fingerprint, stage, blocking gate,
  candidate command, decision, reason, created artifact identifiers, and
  diagnostics.
- Re-running preparation with an identical evaluation fingerprint returns the
  existing event instead of duplicating history.
- Added duplicate-domain-evidence detection before any preparation command
  invocation:
  - decision candidate, proposal, and package evidence.
  - operational-context proposal, assimilation, and decision/execution linkage
    evidence.
  - commit-preparation snapshot and prepared commit evidence.
- Preparation evaluation now reports `Allowed`, `Refused`, `Skipped`, or
  `Duplicate` outcomes; duplicate evidence is included in evaluation
  diagnostics and persisted preparation events.
- Preparation refuses open authority gates that would be bypassed by the requested
  artifact and reports the future command name only when a stage would be
  eligible; Decisions discovery is now invoked for eligible Decision-stage
  preparation, Continuity generation is now invoked for eligible
  OperationalContext-stage preparation, and Execution commit preparation is now
  invoked for eligible Commit-stage preparation while leaving CommitApproval
  open.
- Decision-stage preparation can now call the existing Decisions discovery
  command to create reviewable decision candidates, records created candidate
  artifact identifiers in preparation events, and leaves workflow stage
  progression and decision resolution untouched.
- OperationalContext-stage preparation can now call the existing Continuity
  generation command to create a reviewable operational-context proposal,
  records the created proposal artifact identifier in preparation events, and
  leaves context review, promotion, and workflow progression untouched.
- Commit-stage preparation can now call the existing Execution commit
  preparation command to create reviewable commit-preparation evidence, records
  the created preparation snapshot identifier in preparation events, and leaves
  commit approval, commit execution, push approval, push execution, and workflow
  progression untouched.
- Continuation evaluation consumes the aggregate workflow projection, latest
  persisted workflow timeline evidence, and state-machine, gate, and completion
  evidence.
- Continuation evaluation reports current stage, optional mechanical target stage, open gate, required human action, stop reason, deterministic fingerprint, and diagnostics.
- Open authority gates halt evaluation with `WaitingForHuman`; no domain commands are invoked.
- Hosted continuation, recovery integration, influence tracing, and
  health assessment remain deferred.

Progression rules:

- [x] execution complete and no open source-stage gate projects to handoff.
- [x] handoff accepted and no open execution acceptance gate projects to decision.
- [x] decision resolved and no decision governance block projects to operational context.
- [x] context promoted, rejected, or not required projects to commit.
- [x] commit executed projects to push.
- [x] push executed or no repository changes projects to completed.
- [ ] legitimate push skip projects to completed if domain evidence supports it.
- [x] after push completed or legitimate completion condition exists, persist completion evidence and open work selection gate.

Preparation rules:

- [x] accepted handoff may trigger decision discovery through the existing Decisions command, but this does not resolve the decision gate and does not itself move the stage beyond decision.
- [ ] promoted decision candidates may trigger reviewable proposal generation through existing Decisions commands, but this does not resolve the decision gate and does not itself move the stage beyond decision.
- [x] resolved decision may trigger operational-context proposal generation or linkage through existing Continuity commands, but this does not review or promote context and does not itself move the stage beyond operational context.
- [x] context complete may trigger commit preparation through the existing Execution command, but this does not approve or execute commit and does not itself move the stage beyond commit.
- [x] preparation must record command name, source stage, input fingerprint, created artifact identifiers, skipped reason, and diagnostics.
- [x] preparation must skip when equivalent reviewable artifacts already exist for the same fingerprint.

Gate halting:

- [x] Any open authority gate produces `WaitingForHuman` and stops continuation.
- [x] Work selection gate must never be auto-satisfied.
- [x] Commit and push gates must never be crossed by continuation.
- [x] Decision resolution and context promotion must never be crossed by continuation.
- [x] Preparation may not run when it would create artifacts on the far side of an unsatisfied gate.

Idempotency rules:

- [x] continuation events carry fingerprints.
- [x] preparation events carry fingerprints.
- [x] before invoking an allowed preparation command, preparation checks whether equivalent domain evidence already exists.
- [x] identical endpoint-triggered continuation reevaluation does not duplicate continuation events.
- [x] restart reevaluation must not duplicate continuation events or timeline progression.
- [x] restart reevaluation must not duplicate proposals, commit preparations, or preparation events.
- [x] restart reevaluation must not duplicate preparation events or reviewable artifacts.

Tests:

- [x] eligible workflow advances mechanically.
- [ ] ineligible workflow does not advance.
- [x] eligible preparation creates reviewable artifacts only through existing domain commands.
- [x] ineligible preparation does not create artifacts.
- [x] every open gate stops progression.
- [x] open gates block preparation when the requested artifact would bypass authority.
- [x] restart does not duplicate progression.
- [x] restart does not duplicate preparation.
- [x] identical workflow state produces identical continuation outcome.
- [x] identical continuation run input does not duplicate continuation history.
- [x] identical preparation inputs produce identical preparation outcome.
- [x] continuation never selects work, resolves decisions, promotes context, commits, pushes, or accepts handoffs.
- [x] preparation never creates parallel commands, satisfies gates, moves workflow stage, or performs authority actions.
- [x] every continuation decision explains why it advanced or stopped.
- [x] every preparation decision explains why it created, skipped, or refused an artifact.

Exit criteria:

- [x] continuation service exists.
- [x] preparation service exists.
- [ ] continuation rules exist.
- [ ] preparation rules exist.
- [x] gate halting works.
- [x] continuation history exists.
- [x] preparation history exists.
- [ ] hosted runner exists.
- [ ] recovery integration works.
- [ ] health assessment exists.
