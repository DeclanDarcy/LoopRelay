# Decisions

## Newly Authorized Decisions

- M6.1 is accepted as well-scoped and architecturally clean.
- Preserve the Git inspection ownership chain: Git, `IGitService`, `RepositoryGitStatus`, backend API, Tauri bridge, React projection.
- Do not allow React or Tauri to execute or own raw Git commands.
- M6.1 is certified as observational Git inspection only.
- M6.2 is authorized as commit preparation, not commit mutation.
- Introduce a distinct `CommitPreparation` aggregate for M6.2.
- Keep `RepositoryGitStatus` and `CommitPreparation` separate; `RepositoryGitStatus` answers what exists, while `CommitPreparation` answers what is proposed for commit.
- `CommitPreparation` should include preparation identity, repository identity, proposed message, scope items, status snapshot, generation timestamp, and whether pre-existing changes are present.
- Commit preparation must use snapshot identity so later commit execution can detect drift between reviewed status and current repository state.
- M6.2 must compare the pre-execution dirty snapshot with current repository state to classify change origin.
- Commit scope items should include path, change type, and origin.
- Initial origin values are `PreExisting` and `ExecutionGenerated`.
- Proposed commit messages must be deterministic and derived from milestone context and execution metadata, not AI-generated narrative.
- M6.2 exit criteria are prepare commit, generate preparation, display scope, display origins, display proposed message, and persist snapshot identity.

## Explicitly Deferred

- No staging in M6.2.
- No commit execution in M6.2.
- No push execution in M6.2.
- Richer AI-written commit message generation remains deferred.
