import type { WorkflowInstance } from '../../types'
import { WorkflowRail } from '../workspace/WorkflowRail'

type ExecutionWorkflowRailProps = {
  workflow: WorkflowInstance | null
  isLoading?: boolean
  error?: string | null
}

export function ExecutionWorkflowRail({
  workflow,
  isLoading = false,
  error = null,
}: ExecutionWorkflowRailProps) {
  return <WorkflowRail workflow={workflow} isLoading={isLoading} error={error} variant="execution" />
}
