import type {
  DecisionSessionAnalysisDiagnostics,
  DecisionSessionCertificationReport,
  DecisionSessionCoherence,
  DecisionSessionContinuityArtifact,
  DecisionSessionDiagnostics,
  DecisionSessionEconomics,
  DecisionSessionHealthAssessment,
  DecisionSessionInfluenceTrace,
  DecisionSessionLifecycleDiagnostics,
  DecisionSessionLifecycleEvaluation,
  DecisionSessionLifecycleHistory,
  DecisionSessionLifecycleProjection,
  DecisionSessionMetrics,
  DecisionSessionProjection,
  DecisionSessionRecoveryDiagnostics,
  DecisionSessionRecoveryHistory,
  DecisionSessionRecoveryResult,
  DecisionSessionStatistics,
  DecisionSessionTransfer,
  DecisionSessionTransferDiagnostics,
  DecisionSessionTransferEligibility,
  DecisionSessionTransferEligibilityDiagnostics,
  DecisionSessionTransferResult,
  WorkflowDecisionSessionProjection,
  WorkflowGovernanceHealthProjection,
  WorkflowGovernanceInfluenceProjection,
  WorkflowGovernanceSummary,
} from '../types'
import { invokeCommand } from './tauri'

export function listDecisionSessions(repositoryId: string) {
  return invokeCommand<DecisionSessionProjection[]>('list_decision_sessions', { repositoryId })
}

export function getActiveDecisionSession(repositoryId: string) {
  return invokeCommand<DecisionSessionProjection | null>('get_active_decision_session', {
    repositoryId,
  })
}

export function getDecisionSessionDiagnostics(repositoryId: string) {
  return invokeCommand<DecisionSessionDiagnostics>('get_decision_session_diagnostics', {
    repositoryId,
  })
}

export function getDecisionSessionMetrics(repositoryId: string) {
  return invokeCommand<DecisionSessionMetrics>('get_decision_session_metrics', { repositoryId })
}

export function getDecisionSessionStatistics(repositoryId: string) {
  return invokeCommand<DecisionSessionStatistics>('get_decision_session_statistics', {
    repositoryId,
  })
}

export function getDecisionSessionEconomics(repositoryId: string) {
  return invokeCommand<DecisionSessionEconomics>('get_decision_session_economics', {
    repositoryId,
  })
}

export function getDecisionSessionCoherence(repositoryId: string) {
  return invokeCommand<DecisionSessionCoherence>('get_decision_session_coherence', { repositoryId })
}

export function getDecisionSessionAnalysisDiagnostics(repositoryId: string) {
  return invokeCommand<DecisionSessionAnalysisDiagnostics>(
    'get_decision_session_analysis_diagnostics',
    { repositoryId },
  )
}

export function getDecisionSessionLifecyclePolicy(repositoryId: string) {
  return invokeCommand<DecisionSessionLifecycleEvaluation>(
    'get_decision_session_lifecycle_policy',
    { repositoryId },
  )
}

export function getDecisionSessionLifecyclePolicyDiagnostics(repositoryId: string) {
  return invokeCommand<DecisionSessionLifecycleDiagnostics>(
    'get_decision_session_lifecycle_policy_diagnostics',
    { repositoryId },
  )
}

export function getDecisionSessionTransferEligibility(repositoryId: string) {
  return invokeCommand<DecisionSessionTransferEligibility>(
    'get_decision_session_transfer_eligibility',
    { repositoryId },
  )
}

export function getDecisionSessionTransferEligibilityDiagnostics(repositoryId: string) {
  return invokeCommand<DecisionSessionTransferEligibilityDiagnostics>(
    'get_decision_session_transfer_eligibility_diagnostics',
    { repositoryId },
  )
}

export function getDecisionSessionLifecycleProjection(repositoryId: string) {
  return invokeCommand<DecisionSessionLifecycleProjection>(
    'get_decision_session_lifecycle_projection',
    { repositoryId },
  )
}

export function getDecisionSessionLifecycleHistory(repositoryId: string) {
  return invokeCommand<DecisionSessionLifecycleHistory>(
    'get_decision_session_lifecycle_history',
    { repositoryId },
  )
}

export function getDecisionSessionLifecycleInfluence(repositoryId: string) {
  return invokeCommand<DecisionSessionInfluenceTrace>('get_decision_session_lifecycle_influence', {
    repositoryId,
  })
}

export function getDecisionSessionLifecycleHealth(repositoryId: string) {
  return invokeCommand<DecisionSessionHealthAssessment>('get_decision_session_lifecycle_health', {
    repositoryId,
  })
}

export function listDecisionSessionContinuityArtifacts(repositoryId: string) {
  return invokeCommand<DecisionSessionContinuityArtifact[]>(
    'list_decision_session_continuity_artifacts',
    { repositoryId },
  )
}

export function getDecisionSessionContinuityArtifact(repositoryId: string, artifactId: string) {
  return invokeCommand<DecisionSessionContinuityArtifact>(
    'get_decision_session_continuity_artifact',
    { repositoryId, artifactId },
  )
}

export function listDecisionSessionTransfers(repositoryId: string) {
  return invokeCommand<DecisionSessionTransfer[]>('list_decision_session_transfers', {
    repositoryId,
  })
}

export function listDecisionSessionTransferHistory(repositoryId: string) {
  return invokeCommand<DecisionSessionTransfer[]>('list_decision_session_transfer_history', {
    repositoryId,
  })
}

export function getDecisionSessionTransferDiagnostics(repositoryId: string) {
  return invokeCommand<DecisionSessionTransferDiagnostics>(
    'get_decision_session_transfer_diagnostics',
    { repositoryId },
  )
}

export function executeDecisionSessionTransfer(repositoryId: string) {
  return invokeCommand<DecisionSessionTransferResult>('execute_decision_session_transfer', {
    repositoryId,
  })
}

export function getDecisionSessionRecovery(repositoryId: string) {
  return invokeCommand<DecisionSessionRecoveryResult>('get_decision_session_recovery', {
    repositoryId,
  })
}

export function listDecisionSessionRecoveryHistory(repositoryId: string) {
  return invokeCommand<DecisionSessionRecoveryHistory>('list_decision_session_recovery_history', {
    repositoryId,
  })
}

export function getDecisionSessionRecoveryDiagnostics(repositoryId: string) {
  return invokeCommand<DecisionSessionRecoveryDiagnostics>(
    'get_decision_session_recovery_diagnostics',
    { repositoryId },
  )
}

export function recoverDecisionSession(repositoryId: string) {
  return invokeCommand<DecisionSessionRecoveryResult>('recover_decision_session', { repositoryId })
}

export function getDecisionSessionWorkflow(repositoryId: string) {
  return invokeCommand<WorkflowDecisionSessionProjection>('get_decision_session_workflow', {
    repositoryId,
  })
}

export function getDecisionSessionWorkflowSummary(repositoryId: string) {
  return invokeCommand<WorkflowGovernanceSummary>('get_decision_session_workflow_summary', {
    repositoryId,
  })
}

export function getDecisionSessionWorkflowHealth(repositoryId: string) {
  return invokeCommand<WorkflowGovernanceHealthProjection>('get_decision_session_workflow_health', {
    repositoryId,
  })
}

export function getDecisionSessionWorkflowInfluence(repositoryId: string) {
  return invokeCommand<WorkflowGovernanceInfluenceProjection>(
    'get_decision_session_workflow_influence',
    { repositoryId },
  )
}

export function getDecisionSessionCertification(repositoryId: string) {
  return invokeCommand<DecisionSessionCertificationReport | null>(
    'get_decision_session_certification',
    { repositoryId },
  )
}

export function getDecisionSessionCertificationReport(repositoryId: string) {
  return invokeCommand<DecisionSessionCertificationReport>(
    'get_decision_session_certification_report',
    { repositoryId },
  )
}

export function runDecisionSessionCertification(repositoryId: string) {
  return invokeCommand<DecisionSessionCertificationReport>('run_decision_session_certification', {
    repositoryId,
  })
}
