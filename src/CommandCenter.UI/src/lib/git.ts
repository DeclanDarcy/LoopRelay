import type { RepositoryDirtyState } from '../types'

export function countDirtyPaths(dirtyState: RepositoryDirtyState | null) {
  if (!dirtyState) {
    return 0
  }

  return (
    dirtyState.stagedPaths.length +
    dirtyState.modifiedPaths.length +
    dirtyState.addedPaths.length +
    dirtyState.deletedPaths.length +
    dirtyState.renamedPaths.length +
    dirtyState.untrackedPaths.length
  )
}
