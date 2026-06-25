import type { ExecutionRepositorySnapshot } from '../../types'
import { Panel, SectionHeader } from '../../components/design'
import { EvidenceList } from '../../components/explainability'
import { executionRepositorySnapshotToEvidence } from '../../lib/explainability'
import { GitPathBucket } from './GitPathBucket'

type ExecutionRepositorySnapshotPanelProps = {
  repositorySnapshot: ExecutionRepositorySnapshot | null
}

export function ExecutionRepositorySnapshotPanel({
  repositorySnapshot,
}: ExecutionRepositorySnapshotPanelProps) {
  if (!repositorySnapshot) {
    return null
  }

  return (
    <Panel className="dirty-state" aria-label="Repository snapshot">
      <SectionHeader title="Repository Snapshot" headingLevel={5} />
      <div className="context-summary">
        <span>Branch: {repositorySnapshot.branch || '(detached)'}</span>
        <span>State: {repositorySnapshot.dirtyState.isClean ? 'Clean' : 'Dirty'}</span>
        <span>Captured: {new Date(repositorySnapshot.capturedAt).toLocaleString()}</span>
      </div>
      <div className="context-columns">
        <GitPathBucket label="Staged" paths={repositorySnapshot.dirtyState.stagedPaths} />
        <GitPathBucket label="Modified" paths={repositorySnapshot.dirtyState.modifiedPaths} />
        <GitPathBucket label="Added" paths={repositorySnapshot.dirtyState.addedPaths} />
        <GitPathBucket label="Deleted" paths={repositorySnapshot.dirtyState.deletedPaths} />
        <GitPathBucket label="Renamed" paths={repositorySnapshot.dirtyState.renamedPaths} />
        <GitPathBucket label="Untracked" paths={repositorySnapshot.dirtyState.untrackedPaths} />
      </div>
      <EvidenceList
        evidence={executionRepositorySnapshotToEvidence(repositorySnapshot)}
        title="Repository Snapshot Evidence"
      />
    </Panel>
  )
}
