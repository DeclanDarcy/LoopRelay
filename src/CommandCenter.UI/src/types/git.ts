export type RepositoryDirtyState = {
  stagedPaths: string[]
  modifiedPaths: string[]
  addedPaths: string[]
  deletedPaths: string[]
  renamedPaths: string[]
  untrackedPaths: string[]
  isClean: boolean
}

export type RepositoryGitStatus = {
  branch: string
  aheadCount: number
  behindCount: number
  dirtyState: RepositoryDirtyState
  capturedAt: string
}

export type CommitChangeType = 'Staged' | 'Modified' | 'Added' | 'Deleted' | 'Renamed' | 'Untracked'

export type CommitChangeOrigin = 'PreExisting' | 'ExecutionGenerated'

export type CommitScopeItem = {
  path: string
  changeType: CommitChangeType
  origin: CommitChangeOrigin
  originBasis?: string
  isSelected: boolean
}

export type CommitStatusSnapshot = RepositoryGitStatus & {
  id: string
}

export type CommitPreparation = {
  id: string
  sessionId: string
  repositoryId: string
  repositoryPath: string
  proposedMessage: string
  scopeItems: CommitScopeItem[]
  statusSnapshot: CommitStatusSnapshot
  generatedAt: string
  hasPreExistingChanges: boolean
}
