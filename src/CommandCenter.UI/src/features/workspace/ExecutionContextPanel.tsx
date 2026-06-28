import { EmptyState, Panel, SectionHeader } from '../../components/design'
import {
  ExecutionContextArtifactContentPreviews,
} from '../execution/ExecutionContextArtifactContentPreviews'
import { ExecutionContextArtifactDiagnosticsList } from '../execution/ExecutionContextArtifactDiagnosticsList'
import { ExecutionContextArtifactList } from '../execution/ExecutionContextArtifactList'
import { ExecutionContextMissingOptionalList } from '../execution/ExecutionContextMissingOptionalList'
import { ExecutionContextSummaryRows } from '../execution/ExecutionContextSummaryRows'
import { ExecutionContextValidationList } from '../execution/ExecutionContextValidationList'
import { ExecutionRepositorySnapshotPanel } from '../execution/ExecutionRepositorySnapshotPanel'
import type { ExecutionContextPreview } from '../../types'

type ExecutionContextPanelProps = {
  id?: string
  executionContext: ExecutionContextPreview | null
  isContextLoading: boolean
  canStartExecution: boolean
  isStartingExecution: boolean
  startExecutionBlockedReason: string
  operationalContextExecutionStatus: string
  executionContextSizeStatus: string
  onBuildExecutionContext: () => void
  onStartExecution: () => void
}

export function ExecutionContextPanel({
  id,
  executionContext,
  isContextLoading,
  canStartExecution,
  isStartingExecution,
  startExecutionBlockedReason,
  operationalContextExecutionStatus,
  executionContextSizeStatus,
  onBuildExecutionContext,
  onStartExecution,
}: ExecutionContextPanelProps) {
  return (
    <Panel id={id} className="execution-context-panel" aria-label="Execution context preview">
      <SectionHeader
        className="context-toolbar"
        eyebrow="Execution Context"
        title="Preview Package"
        headingLevel={4}
        actions={
          <div className="context-controls">
            <button
              type="button"
              className="secondary-action"
              onClick={onBuildExecutionContext}
              disabled={isContextLoading}
            >
              {isContextLoading ? 'Building...' : 'Build Execution Context'}
            </button>
            <button
              type="button"
              className="primary-action"
              onClick={onStartExecution}
              disabled={!canStartExecution}
              title={startExecutionBlockedReason}
            >
              {isStartingExecution ? 'Starting...' : 'Start Execution'}
            </button>
          </div>
        }
      />

      {executionContext ? (
        <div className="context-preview">
          <ExecutionContextSummaryRows
            executionContext={executionContext}
            operationalContextStatus={operationalContextExecutionStatus}
            launchStatus={canStartExecution ? 'Ready' : startExecutionBlockedReason}
            sizeStatus={executionContextSizeStatus}
          />

          <div className="context-columns">
            <div>
              <h5>Artifacts</h5>
              <ExecutionContextArtifactList artifacts={executionContext.artifacts} />
            </div>
            <div>
              <h5>Missing Optional</h5>
              <ExecutionContextMissingOptionalList
                paths={executionContext.diagnostics.missingOptionalArtifacts}
              />
            </div>
            <div>
              <h5>Validation</h5>
              <ExecutionContextValidationList
                validationErrors={executionContext.diagnostics.validationErrors}
              />
            </div>
          </div>

          <ExecutionRepositorySnapshotPanel
            repositorySnapshot={executionContext.snapshot}
          />

          <div className="artifact-diagnostics">
            <h5>Artifact Sizes</h5>
            <ExecutionContextArtifactDiagnosticsList
              diagnostics={executionContext.diagnostics.artifactDiagnostics}
            />
          </div>

          <ExecutionContextArtifactContentPreviews artifacts={executionContext.artifacts} />
        </div>
      ) : (
        <EmptyState className="empty-state">
          Build a context preview for this repository.
        </EmptyState>
      )}
    </Panel>
  )
}
