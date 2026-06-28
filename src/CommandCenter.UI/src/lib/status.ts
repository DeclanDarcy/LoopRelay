import type { ContinuityDiagnostics } from '../types/continuity'
import type { ExecutionReadiness, ExecutionSessionState, RepositoryExecutionState } from '../types/execution'
import type { OperationalContextProposalStatus, OperationalContextReviewState } from '../types/operationalContext'
import type { MilestoneProgress, RepositoryAvailability } from '../types/repositories'
import type { WorkflowInstance, WorkflowProgressState } from '../types/workflow'

export type StatusTone = 'neutral' | 'success' | 'warning' | 'danger' | 'info' | 'done'

export type StatusPresentation = {
  label: string
  tone: StatusTone
  className: string
}

const status = (label: string, tone: StatusTone): StatusPresentation => ({
  label,
  tone,
  className: `status-${tone}`,
})

export const repositoryAvailabilityStatus: Record<RepositoryAvailability, StatusPresentation> = {
  Available: status('Available', 'success'),
  Missing: status('Missing', 'warning'),
  AccessDenied: status('Access denied', 'danger'),
}

export const executionReadinessStatus: Record<ExecutionReadiness, StatusPresentation> = {
  Ready: status('Ready', 'success'),
  MissingPlan: status('Missing plan', 'warning'),
  MissingMilestones: status('Missing milestones', 'warning'),
}

export const repositoryExecutionStatus: Record<RepositoryExecutionState, StatusPresentation> = {
  Ready: status('Ready', 'success'),
  Executing: status('Executing', 'warning'),
  AwaitingAcceptance: status('Awaiting acceptance', 'warning'),
  Accepted: status('Accepted', 'done'),
  AwaitingCommit: status('Awaiting commit', 'warning'),
  AwaitingPush: status('Awaiting push', 'warning'),
  Failed: status('Failed', 'danger'),
  Cancelled: status('Cancelled', 'danger'),
}

// A new execution can be started only from a settled state. Ready is the clean baseline; Failed and
// Cancelled are terminal outcomes whose correct next action is to start again (the backend's start
// guard only rejects an already-Executing repository, so it accepts a restart from these). The
// in-progress states — Executing, AwaitingAcceptance, Accepted, AwaitingCommit, AwaitingPush — must
// be resolved first and therefore block a new start.
export function isStartableExecutionState(state: RepositoryExecutionState): boolean {
  return state === 'Ready' || state === 'Failed' || state === 'Cancelled'
}

export const executionSessionStatus: Record<ExecutionSessionState, StatusPresentation> = {
  Created: status('Created', 'info'),
  Executing: status('Executing', 'warning'),
  Completed: status('Completed', 'done'),
  Failed: status('Failed', 'danger'),
  Cancelled: status('Cancelled', 'danger'),
}

export const operationalContextProposalStatus: Record<OperationalContextProposalStatus, StatusPresentation> = {
  Pending: status('Pending', 'warning'),
  Edited: status('Edited', 'info'),
  Superseded: status('Superseded', 'neutral'),
  Accepted: status('Accepted', 'done'),
  Rejected: status('Rejected', 'danger'),
  Promoted: status('Promoted', 'success'),
}

export const operationalContextReviewStatus: Record<OperationalContextReviewState, StatusPresentation> = {
  PendingReview: status('Pending review', 'warning'),
  Edited: status('Edited', 'info'),
  Accepted: status('Accepted', 'done'),
  Rejected: status('Rejected', 'danger'),
  Stale: status('Stale', 'warning'),
}

export function continuityWarningStatus(diagnostics: Pick<ContinuityDiagnostics, 'continuityWarnings'> | null) {
  if (!diagnostics || diagnostics.continuityWarnings.length === 0) {
    return status('No warnings', 'success')
  }

  return status(`${diagnostics.continuityWarnings.length} warning${diagnostics.continuityWarnings.length === 1 ? '' : 's'}`, 'warning')
}

export function milestoneProgressStatus(
  milestone: Pick<MilestoneProgress, 'isComplete' | 'completedTaskCount'>,
) {
  if (milestone.isComplete) {
    return status('Complete', 'done')
  }

  if (milestone.completedTaskCount > 0) {
    return status('In progress', 'warning')
  }

  return status('Not started', 'neutral')
}

const workflowProgressTone: Record<WorkflowProgressState, StatusTone> = {
  Ready: 'success',
  Active: 'warning',
  AwaitingGate: 'warning',
  Blocked: 'danger',
  Completed: 'done',
  Failed: 'danger',
  Recovering: 'info',
  WaitingForHuman: 'warning',
}

export function workflowProjectionStatus(workflow: Pick<WorkflowInstance, 'currentStage' | 'progressState'> | null) {
  if (!workflow) {
    return status('Workflow not loaded', 'neutral')
  }

  return status(`${workflow.currentStage} / ${workflow.progressState}`, workflowProgressTone[workflow.progressState])
}
