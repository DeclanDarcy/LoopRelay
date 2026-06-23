# Milestone 8: Git Workflow Integration

Objective: make workflow aware of commit, push, and repository lifecycle completion.

Deliver:

- [ ] `WorkflowGitProjection` with repository id, commit status, push status, commit id, branch, commit timestamp, push timestamp, pending changes flag, and unpushed changes flag.
- [ ] `WorkflowGitStatus` with not ready, awaiting commit, committed, awaiting push, pushed, and failed.
- [ ] `IWorkflowGitService`.
- [ ] commit rules for awaiting commit, committed, and commit not required.
- [ ] push rules for awaiting push, pushed, and push skipped.
- [ ] gate integration for commit approval and push approval.
- [ ] `WorkflowGitDiagnostics`.
- [ ] timeline events: commit prepared, commit approved, commit executed, push approved, push executed, and push skipped.
- [ ] recovery integration for commit status, push status, gate status, and timeline events.
- [ ] `WorkflowCompletionEvaluation`.

Rules:

- [ ] Git remains authoritative.
- [ ] Workflow never commits.
- [ ] Workflow never pushes.
- [ ] Existing commit and push commands remain canonical.
- [ ] No changes produced can complete the workflow without commit or push, with diagnostics.
- [ ] Push skipped can complete the workflow only when there is explicit domain evidence that push was intentionally skipped.

Tests:

- [ ] awaiting commit, committed, awaiting push, pushed, failed, no-change, and skip states project correctly.
- [ ] commit gate opens correctly.
- [ ] push gate opens correctly.
- [ ] completion evaluation closes workflow only for push completed, legitimate skip, or no changes.
- [ ] pending push does not complete workflow.
- [ ] recovery rebuilds git workflow state.
- [ ] workflow never mutates repository state.

Exit criteria:

- [ ] git projection exists.
- [ ] git service exists.
- [ ] commit and push integration works.
- [ ] completion evaluation works.
- [ ] timeline integration exists.
- [ ] recovery integration exists.
- [ ] diagnostics exist.
