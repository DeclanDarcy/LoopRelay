import type { RepositoryExecutionState } from '../../types'

export type CertificationExecutionStateFixture = {
  repositoryId: string
  repositoryName: string
  executionState: RepositoryExecutionState
}

export const certificationExecutionStates: readonly CertificationExecutionStateFixture[] = [
  { repositoryId: 'repo-alpha', repositoryName: 'AlphaRepo', executionState: 'Ready' },
  {
    repositoryId: 'repo-cert-executing',
    repositoryName: 'CertificationExecuting',
    executionState: 'Executing',
  },
  {
    repositoryId: 'repo-cert-awaiting-acceptance',
    repositoryName: 'CertificationAwaitingAcceptance',
    executionState: 'AwaitingAcceptance',
  },
  {
    repositoryId: 'repo-cert-awaiting-commit',
    repositoryName: 'CertificationAwaitingCommit',
    executionState: 'AwaitingCommit',
  },
  {
    repositoryId: 'repo-cert-awaiting-push',
    repositoryName: 'CertificationAwaitingPush',
    executionState: 'AwaitingPush',
  },
  {
    repositoryId: 'repo-cert-failed',
    repositoryName: 'CertificationFailed',
    executionState: 'Failed',
  },
  {
    repositoryId: 'repo-cert-cancelled',
    repositoryName: 'CertificationCancelled',
    executionState: 'Cancelled',
  },
] as const
