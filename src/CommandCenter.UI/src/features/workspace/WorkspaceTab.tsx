import type { ReactNode } from 'react'
import type { ExecutionWorkflowStep } from '../../types'
import { WorkflowRail } from './WorkflowRail'

type WorkspaceTabProps = {
  summary: ReactNode
  workflowSteps: ExecutionWorkflowStep[]
  executionContext: ReactNode
  liveActivity: ReactNode
  artifactWorkspace: ReactNode
  inspector: ReactNode
  hidden?: boolean
}

export function WorkspaceTab({
  summary,
  workflowSteps,
  executionContext,
  liveActivity,
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
        <WorkflowRail steps={workflowSteps} />
        {executionContext}
        {liveActivity}
        {artifactWorkspace}
      </div>
      <aside className="workspace-inspector-rail" aria-label="Workspace inspector">
        {inspector}
      </aside>
    </section>
  )
}
