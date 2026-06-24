import type { ReactNode } from 'react'
import type { WorkflowInstance } from '../../types'
import { WorkflowRail } from './WorkflowRail'

type WorkspaceTabProps = {
  summary: ReactNode
  workflow: WorkflowInstance | null
  isWorkflowLoading?: boolean
  workflowError?: string | null
  executionContext: ReactNode
  liveActivity: ReactNode
  milestones: ReactNode
  artifactWorkspace: ReactNode
  inspector: ReactNode
  hidden?: boolean
}

export function WorkspaceTab({
  summary,
  workflow,
  isWorkflowLoading = false,
  workflowError = null,
  executionContext,
  liveActivity,
  milestones,
  artifactWorkspace,
  inspector,
  hidden = false,
}: WorkspaceTabProps) {
  return (
    <section
      className="workspace-tab tab-panel tab-workspace"
      aria-label="Workspace overview"
      hidden={hidden}
    >
      <div className="workspace-tab-main">
        {summary}
        <WorkflowRail workflow={workflow} isLoading={isWorkflowLoading} error={workflowError} />
        {executionContext}
        {liveActivity}
        {milestones}
        {artifactWorkspace}
      </div>
      <aside id="workspace-inspector" className="workspace-inspector-rail" aria-label="Workspace inspector">
        {inspector}
      </aside>
    </section>
  )
}
