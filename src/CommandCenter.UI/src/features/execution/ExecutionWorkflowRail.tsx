import type { WorkflowInstance } from '../../types'
import { Button, EmptyState, Panel, SectionHeader, StatusBadge } from '../../components/design'

type ExecutionWorkflowRailProps = {
  workflow: WorkflowInstance | null
  isLoading?: boolean
  error?: string | null
  onOpenWorkflow?: () => void
}

export function ExecutionWorkflowRail({
  workflow,
  isLoading = false,
  error = null,
  onOpenWorkflow,
}: ExecutionWorkflowRailProps) {
  const title = workflow ? workflow.currentStage : 'Projection'
  const blockingGate = workflow?.openGates[0] ?? null
  const requiredAction =
    workflow?.requiredHumanAction || blockingGate?.requiredAction || 'No human action required.'

  return (
    <Panel className="execution-workflow-summary-panel" aria-label="Execution workflow summary">
      <SectionHeader
        eyebrow="Workflow"
        title={title}
        headingLevel={4}
        actions={
          <Button type="button" variant="secondary" className="secondary-action" onClick={onOpenWorkflow}>
            Workflow
          </Button>
        }
      />
      {workflow ? (
        <div className="execution-rail-summary">
          <span>
            Progress: <StatusBadge status={{ label: workflow.progressState, tone: 'info', className: 'status-info' }} />
          </span>
          <span>Blocking gate: {workflow.blockingGate}</span>
          <span>Required action: {requiredAction}</span>
          <span>Open gates: {workflow.openGates.length}</span>
          <span>Next stages: {workflow.nextPossibleStages.join(', ') || 'None'}</span>
        </div>
      ) : (
        <EmptyState className="empty-state">
          {error ?? (isLoading ? 'Loading workflow projection...' : 'Workflow projection is not loaded.')}
        </EmptyState>
      )}
    </Panel>
  )
}
