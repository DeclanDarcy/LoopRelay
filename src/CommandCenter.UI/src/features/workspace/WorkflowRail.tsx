import type { ExecutionWorkflowStep } from '../../types'

type WorkflowRailProps = {
  steps: ExecutionWorkflowStep[]
}

export function WorkflowRail({ steps }: WorkflowRailProps) {
  return (
    <div className="workspace-workflow-rail" aria-label="Workspace workflow state">
      {steps.map((step) => (
        <div
          className={`workspace-workflow-step workspace-workflow-step-${step.state}`}
          key={step.key}
        >
          <span>{step.label}</span>
          <small>{step.detail}</small>
        </div>
      ))}
    </div>
  )
}
