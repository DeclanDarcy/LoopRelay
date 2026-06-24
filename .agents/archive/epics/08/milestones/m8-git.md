# Milestone 8: Git Workflow Integration

Objective: make workflow aware of commit, push, and repository lifecycle completion.

Implementation note: `WorkflowGitStatus.PushSkipped` and the push-skipped
completion path are modeled, but current Execution/Git evidence does not expose
an explicit push-skip artifact. Workflow therefore never infers push skipped
from absence; the status remains available for future domain-owned evidence.

Deliver:

- [x] `WorkflowGitProjection` with repository id, commit status, push status, commit id, branch, commit timestamp, push timestamp, pending changes flag, and unpushed changes flag.
- [x] `WorkflowGitStatus` with not ready, awaiting commit, committed, awaiting push, pushed, and failed.
- [x] `IWorkflowGitService`.
- [x] commit rules for awaiting commit, committed, and commit not required.
- [x] push rules for awaiting push, pushed, and push skipped.
- [x] gate integration for commit approval and push approval.
- [x] `WorkflowGitDiagnostics`.
- [x] timeline events: commit prepared, commit approved, commit executed, push approved, push executed, and push skipped.
- [x] recovery integration for commit status, push status, gate status, and timeline events.
- [x] `WorkflowCompletionEvaluation`.

Rules:

- [x] Git remains authoritative.
- [x] Workflow never commits.
- [x] Workflow never pushes.
- [x] Existing commit and push commands remain canonical.
- [x] No changes produced can complete the workflow without commit or push, with diagnostics.
- [x] Push skipped can complete the workflow only when there is explicit domain evidence that push was intentionally skipped.

Tests:

- [x] awaiting commit, committed, awaiting push, pushed, failed, no-change, and skip states project correctly.
- [x] commit gate opens correctly.
- [x] push gate opens correctly.
- [x] completion evaluation closes workflow only for push completed, legitimate skip, or no changes.
- [x] pending push does not complete workflow.
- [x] recovery rebuilds git workflow state.
- [x] workflow never mutates repository state.

Exit criteria:

- [x] git projection exists.
- [x] git service exists.
- [x] commit and push integration works.
- [x] completion evaluation works.
- [x] timeline integration exists.
- [x] recovery integration exists.
- [x] diagnostics exist.
