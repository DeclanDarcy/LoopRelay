# Milestone 9: Workflow Continuation Engine

Objective: automate mechanical progression between non-authority stages, introduce separately governed artifact preparation, and stop at every human gate.

Deliver:

- [ ] `IWorkflowContinuationService`.
- [ ] `IWorkflowPreparationService`.
- [ ] `WorkflowContinuationEvaluation`.
- [ ] `WorkflowPreparationEvaluation`.
- [ ] continuation rules for execution complete, handoff accepted, decision resolved, context promoted/rejected/not required, commit executed, push executed, no changes, and completed workflow.
- [ ] preparation rules for decision discovery/generation, operational-context proposal generation/linkage, and commit preparation.
- [ ] gate halting for work selection, execution acceptance, decision resolution, operational context review, operational context promotion, commit approval, and push approval.
- [ ] `WorkflowContinuationDiagnostics`.
- [ ] `WorkflowPreparationDiagnostics`.
- [ ] `WorkflowContinuationEvent`.
- [ ] `WorkflowPreparationEvent`.
- [ ] recovery integration that reevaluates continuation after restart without duplicate progression.
- [ ] `WorkflowContinuationHostedService`.
- [ ] `WorkflowInfluenceTrace`.
- [ ] `WorkflowHealthAssessment`.

Progression rules:

- [ ] execution complete and no open execution gate projects to handoff.
- [ ] handoff accepted and no open execution acceptance gate projects to decision.
- [ ] decision resolved and no decision governance block projects to operational context.
- [ ] context promoted, rejected, or not required projects to commit.
- [ ] commit executed projects to push.
- [ ] push executed, legitimate push skip, or no repository changes projects to completed.
- [ ] after push completed or legitimate completion condition exists, persist completion evidence and open work selection gate.

Preparation rules:

- [ ] accepted handoff may trigger decision discovery and reviewable proposal generation through existing Decisions commands, but this does not resolve the decision gate and does not itself move the stage beyond decision.
- [ ] resolved decision may trigger operational-context proposal generation or linkage through existing Continuity commands, but this does not review or promote context and does not itself move the stage beyond operational context.
- [ ] context complete may trigger commit preparation through the existing Execution command, but this does not approve or execute commit and does not itself move the stage beyond commit.
- [ ] preparation must record command name, source stage, input fingerprint, created artifact identifiers, skipped reason, and diagnostics.
- [ ] preparation must skip when equivalent reviewable artifacts already exist for the same fingerprint.

Gate halting:

- [ ] Any open authority gate produces `WaitingForHuman` and stops continuation.
- [ ] Work selection gate must never be auto-satisfied.
- [ ] Commit and push gates must never be crossed by continuation.
- [ ] Decision resolution and context promotion must never be crossed by continuation.
- [ ] Preparation may not run when it would create artifacts on the far side of an unsatisfied gate.

Idempotency rules:

- [ ] continuation events carry fingerprints.
- [ ] preparation events carry fingerprints.
- [ ] before invoking an allowed preparation command, preparation checks whether equivalent domain evidence already exists.
- [ ] restart reevaluation must not duplicate proposals, commit preparations, timeline entries, or continuation events.
- [ ] restart reevaluation must not duplicate preparation events or reviewable artifacts.

Tests:

- [ ] eligible workflow advances mechanically.
- [ ] ineligible workflow does not advance.
- [ ] eligible preparation creates reviewable artifacts only through existing domain commands.
- [ ] ineligible preparation does not create artifacts.
- [ ] every open gate stops progression.
- [ ] open gates block preparation when the requested artifact would bypass authority.
- [ ] restart does not duplicate progression.
- [ ] restart does not duplicate preparation.
- [ ] identical workflow state produces identical continuation outcome.
- [ ] identical preparation inputs produce identical preparation outcome.
- [ ] continuation never selects work, resolves decisions, promotes context, commits, pushes, or accepts handoffs.
- [ ] preparation never creates parallel commands, satisfies gates, moves workflow stage, or performs authority actions.
- [ ] every continuation decision explains why it advanced or stopped.
- [ ] every preparation decision explains why it created, skipped, or refused an artifact.

Exit criteria:

- [ ] continuation service exists.
- [ ] preparation service exists.
- [ ] continuation rules exist.
- [ ] preparation rules exist.
- [ ] gate halting works.
- [ ] continuation history exists.
- [ ] preparation history exists.
- [ ] hosted runner exists.
- [ ] recovery integration works.
- [ ] health assessment exists.
