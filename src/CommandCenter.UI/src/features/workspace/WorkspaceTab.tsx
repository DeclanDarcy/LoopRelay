import type { ReactNode } from 'react'
import type { ExecutionWorkflowStep } from '../../types'
import { WorkflowRail } from './WorkflowRail'

type WorkspaceTabProps = {
  summary: ReactNode
  workflowSteps: ExecutionWorkflowStep[]
  artifactWorkspace: ReactNode
  inspector: ReactNode
}

export function WorkspaceTab({
  summary,
  workflowSteps,
  artifactWorkspace,
  inspector,
}: WorkspaceTabProps) {
  return (
    <section className="workspace-tab tab-panel tab-workspace" aria-label="Workspace overview">
      <div className="workspace-tab-main">
        {summary}
        <WorkflowRail steps={workflowSteps} />
        {artifactWorkspace}
      </div>
      <aside className="workspace-inspector-rail" aria-label="Workspace inspector">
        {inspector}
      </aside>
    </section>
  )
}
