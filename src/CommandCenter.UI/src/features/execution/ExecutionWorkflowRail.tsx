import type { ExecutionWorkflowStep } from '../../types'

type ExecutionWorkflowRailProps = {
  steps: ExecutionWorkflowStep[]
}

export function ExecutionWorkflowRail({ steps }: ExecutionWorkflowRailProps) {
  return (
    <div className="execution-workflow-rail" aria-label="Execution lifecycle">
      {steps.map((step) => (
        <div
          className={`execution-workflow-step execution-workflow-step-${step.state}`}
          key={step.key}
        >
          <span>{step.label}</span>
          <small>{step.detail}</small>
        </div>
      ))}
    </div>
  )
}
