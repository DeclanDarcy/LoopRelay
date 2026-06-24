import type {
  HumanGovernanceReport,
  RepositoryWorkflowReport,
  WorkflowCertificationResult,
  WorkflowContinuationEvaluation,
  WorkflowContinuationEvent,
  WorkflowExecutionProjection,
  WorkflowGateCatalogProjection,
  WorkflowGateHistoryProjection,
  WorkflowGitProjection,
  WorkflowHealthAssessment,
  WorkflowHistoryProjection,
  WorkflowHandoffProjection,
  WorkflowInstance,
  WorkflowOperationalContextProjection,
  WorkflowPreparationEvaluation,
  WorkflowPreparationEvent,
  WorkflowProgressionReport,
  WorkflowReadinessReport,
  WorkflowRecoveryDiagnostics,
  WorkflowRecoveryResult,
  WorkflowStateMachineDiagnostics,
  WorkflowTimeline,
} from '../types'
import { invokeCommand } from './tauri'

export function getWorkflowProjection(repositoryId: string) {
  return invokeCommand<WorkflowInstance>('get_workflow_projection', { repositoryId })
}

export function getWorkflowDiagnostics(repositoryId: string) {
  return invokeCommand<WorkflowInstance['diagnostics']>('get_workflow_diagnostics', { repositoryId })
}

export function getWorkflowTimeline(repositoryId: string) {
  return invokeCommand<WorkflowTimeline>('get_workflow_timeline', { repositoryId })
}

export function getWorkflowHistory(repositoryId: string) {
  return invokeCommand<WorkflowHistoryProjection>('get_workflow_history', { repositoryId })
}

export function getWorkflowTransitions(repositoryId: string) {
  return invokeCommand<WorkflowStateMachineDiagnostics>('get_workflow_transitions', { repositoryId })
}

export function getWorkflowGates(repositoryId: string) {
  return invokeCommand<WorkflowGateCatalogProjection>('get_workflow_gates', { repositoryId })
}

export function getWorkflowGateHistory(repositoryId: string) {
  return invokeCommand<WorkflowGateHistoryProjection>('get_workflow_gate_history', { repositoryId })
}

export function getWorkflowRecovery(repositoryId: string) {
  return invokeCommand<WorkflowRecoveryDiagnostics>('get_workflow_recovery', { repositoryId })
}

export function recoverWorkflow(repositoryId: string) {
  return invokeCommand<WorkflowRecoveryResult>('recover_workflow', { repositoryId })
}

export function getWorkflowExecution(repositoryId: string) {
  return invokeCommand<WorkflowExecutionProjection>('get_workflow_execution', { repositoryId })
}

export function getWorkflowHandoff(repositoryId: string) {
  return invokeCommand<WorkflowHandoffProjection>('get_workflow_handoff', { repositoryId })
}

export function getWorkflowDecisions(repositoryId: string) {
  return invokeCommand<WorkflowInstance['currentDecision']>('get_workflow_decisions', { repositoryId })
}

export function getWorkflowOperationalContext(repositoryId: string) {
  return invokeCommand<WorkflowOperationalContextProjection>('get_workflow_operational_context', {
    repositoryId,
  })
}

export function getWorkflowGit(repositoryId: string) {
  return invokeCommand<WorkflowGitProjection>('get_workflow_git', { repositoryId })
}

export function getWorkflowContinuationEvaluation(repositoryId: string) {
  return invokeCommand<WorkflowContinuationEvaluation>('get_workflow_continuation_evaluation', {
    repositoryId,
  })
}

export function runWorkflowContinuation(repositoryId: string) {
  return invokeCommand<WorkflowContinuationEvent>('run_workflow_continuation', { repositoryId })
}

export function getWorkflowContinuationHistory(repositoryId: string) {
  return invokeCommand<WorkflowContinuationEvent[]>('get_workflow_continuation_history', {
    repositoryId,
  })
}

export function getWorkflowPreparationEvaluation(repositoryId: string) {
  return invokeCommand<WorkflowPreparationEvaluation>('get_workflow_preparation_evaluation', {
    repositoryId,
  })
}

export function runWorkflowPreparation(repositoryId: string) {
  return invokeCommand<WorkflowPreparationEvent>('run_workflow_preparation', { repositoryId })
}

export function getWorkflowPreparationHistory(repositoryId: string) {
  return invokeCommand<WorkflowPreparationEvent[]>('get_workflow_preparation_history', {
    repositoryId,
  })
}

export function getWorkflowHealth(repositoryId: string) {
  return invokeCommand<WorkflowHealthAssessment>('get_workflow_health', { repositoryId })
}

export function getRepositoryWorkflowReport(repositoryId: string) {
  return invokeCommand<RepositoryWorkflowReport>('get_repository_workflow_report', { repositoryId })
}

export function getWorkflowProgressionReport(repositoryId: string) {
  return invokeCommand<WorkflowProgressionReport>('get_workflow_progression_report', {
    repositoryId,
  })
}

export function getWorkflowHumanGovernanceReport(repositoryId: string) {
  return invokeCommand<HumanGovernanceReport>('get_workflow_human_governance_report', {
    repositoryId,
  })
}

export function getWorkflowReadinessReport(repositoryId: string) {
  return invokeCommand<WorkflowReadinessReport>('get_workflow_readiness_report', { repositoryId })
}

export function getWorkflowCertification(repositoryId: string) {
  return invokeCommand<WorkflowCertificationResult>('get_workflow_certification', { repositoryId })
}

export function runWorkflowCertification(repositoryId: string) {
  return invokeCommand<WorkflowCertificationResult>('run_workflow_certification', { repositoryId })
}
