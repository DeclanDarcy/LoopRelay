# Decisions

## Newly Authorized

- Treat commit preparation as review-artifact creation, not commit approval.
- The intended commit lifecycle is:
  - operational context complete.
  - commit stage.
  - prepare commit snapshot.
  - `CommitApproval` gate.
  - human commit decision.
  - commit executed.
- Prepared commit snapshots are analogous to decision candidates and operational-context proposals:
  - they are reviewable artifacts.
  - they are not authority outcomes.
- The commit-preparation implementation is semantically acceptable if these invariants remain true:
  - `PrepareCommitAsync` creates evidence.
  - `PrepareCommitAsync` does not execute commit.
  - `PrepareCommitAsync` does not close `CommitApproval`.
  - `PrepareCommitAsync` does not advance workflow stage.
  - `PrepareCommitAsync` may be rerun idempotently.
  - prepared commit snapshot evidence is discoverable by duplicate detection.
- Before expanding Decision preparation, review whether proposal generation is artifact creation or authority exercise.
- Decision proposal generation may be authorized only if the existing Decisions command:
  - consumes existing promoted candidate evidence.
  - creates a reviewable proposal.
  - does not resolve decisions.
  - does not approve decisions.
  - does not promote candidates.
  - does not supersede decisions or proposals.
  - leaves `DecisionResolution` open.
  - leaves governance state unchanged.
  - leaves workflow stage unchanged.
  - is duplicate-detectable.
  - is restart-idempotent.
- Perform a full Milestone 9 architectural review after the remaining preparation-path review and coverage are complete.

## Explicitly Deferred

- Do not implement Decision proposal-generation invocation until the Decisions command boundary is reviewed against the artifact-creation criteria above.
- Do not enable hosted continuation.
- Do not enable hosted preparation.
- Do not perform background invocation.
- Do not perform autonomous work selection.
- Do not resolve, archive, supersede, approve, or reject decisions.
- Do not review, accept, reject, edit, or promote operational context.
- Do not approve commits.
- Do not execute commits.
- Do not approve pushes.
- Do not execute pushes.
