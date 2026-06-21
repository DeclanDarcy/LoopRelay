import type { ExecutionWorkflowStep, RepositoryExecutionState } from '../types'

export function getExecutionWorkflowSteps(
  repositoryState: RepositoryExecutionState,
  hasContextForSelection: boolean,
  hasExecutionSession: boolean,
): ExecutionWorkflowStep[] {
  const contextComplete = hasContextForSelection || hasExecutionSession || repositoryState !== 'Ready'
  const executionComplete =
    hasExecutionSession &&
    repositoryState !== 'Executing' &&
    repositoryState !== 'Failed' &&
    repositoryState !== 'Cancelled'
  const handoffComplete =
    repositoryState === 'AwaitingCommit' ||
    repositoryState === 'AwaitingPush' ||
    (repositoryState === 'Ready' && hasExecutionSession)
  const commitComplete =
    repositoryState === 'AwaitingPush' || (repositoryState === 'Ready' && hasExecutionSession)
  const pushComplete = repositoryState === 'Ready' && hasExecutionSession
  const isFailed = repositoryState === 'Failed' || repositoryState === 'Cancelled'

  return [
    {
      key: 'context',
      label: 'Context',
      detail: contextComplete ? 'Prepared' : 'Needs preview',
      state: contextComplete ? 'complete' : repositoryState === 'Ready' ? 'current' : 'pending',
    },
    {
      key: 'execution',
      label: 'Execution',
      detail:
        repositoryState === 'Executing'
          ? 'Running'
          : executionComplete
            ? 'Completed'
            : isFailed
              ? repositoryState === 'Failed'
                ? 'Failed'
                : 'Cancelled'
              : 'Not started',
      state:
        repositoryState === 'Executing'
          ? 'current'
          : executionComplete
            ? 'complete'
            : isFailed
              ? 'blocked'
              : contextComplete
                ? 'current'
                : 'pending',
    },
    {
      key: 'handoff',
      label: 'Handoff',
      detail:
        repositoryState === 'AwaitingAcceptance'
          ? 'Awaiting review'
          : handoffComplete
            ? 'Accepted or closed'
            : isFailed
              ? 'Unavailable'
              : 'Pending execution',
      state:
        repositoryState === 'AwaitingAcceptance'
          ? 'current'
          : handoffComplete
            ? 'complete'
            : isFailed
              ? 'blocked'
              : 'pending',
    },
    {
      key: 'commit',
      label: 'Commit',
      detail:
        repositoryState === 'AwaitingCommit'
          ? 'Awaiting review'
          : commitComplete
            ? 'Committed'
            : 'Pending acceptance',
      state:
        repositoryState === 'AwaitingCommit'
          ? 'current'
          : commitComplete
            ? 'complete'
            : isFailed
              ? 'blocked'
              : 'pending',
    },
    {
      key: 'push',
      label: 'Push',
      detail:
        repositoryState === 'AwaitingPush'
          ? 'Awaiting push'
          : pushComplete
            ? 'Published'
            : 'Pending commit',
      state:
        repositoryState === 'AwaitingPush'
          ? 'current'
          : pushComplete
            ? 'complete'
            : isFailed
              ? 'blocked'
              : 'pending',
    },
  ]
}
