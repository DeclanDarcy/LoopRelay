import { useCallback, useEffect, useState } from 'react'
import {
  executeDecisionSessionTransfer,
  formatError,
  getActiveDecisionSession,
  getDecisionSessionAnalysisDiagnostics,
  getDecisionSessionCertification,
  getDecisionSessionCertificationReport,
  getDecisionSessionCoherence,
  getDecisionSessionDiagnostics,
  getDecisionSessionEconomics,
  getDecisionSessionLifecycleHealth,
  getDecisionSessionLifecycleHistory,
  getDecisionSessionLifecycleInfluence,
  getDecisionSessionLifecyclePolicy,
  getDecisionSessionLifecyclePolicyDiagnostics,
  getDecisionSessionLifecycleProjection,
  getDecisionSessionMetrics,
  getDecisionSessionRecovery,
  getDecisionSessionRecoveryDiagnostics,
  getDecisionSessionStatistics,
  getDecisionSessionTransferDiagnostics,
  getDecisionSessionTransferEligibility,
  getDecisionSessionTransferEligibilityDiagnostics,
  getDecisionSessionWorkflow,
  getDecisionSessionWorkflowHealth,
  getDecisionSessionWorkflowInfluence,
  getDecisionSessionWorkflowSummary,
  listDecisionSessionContinuityArtifacts,
  listDecisionSessionRecoveryHistory,
  listDecisionSessions,
  listDecisionSessionTransferHistory,
  listDecisionSessionTransfers,
  recoverDecisionSession,
  runDecisionSessionCertification,
} from '../api'
import type { DecisionSessionGovernanceSnapshot } from '../types'

const emptySnapshot: DecisionSessionGovernanceSnapshot = {
  sessions: [],
  activeSession: null,
  diagnostics: null,
  metrics: null,
  statistics: null,
  economics: null,
  coherence: null,
  analysisDiagnostics: null,
  lifecyclePolicy: null,
  lifecyclePolicyDiagnostics: null,
  transferEligibility: null,
  transferEligibilityDiagnostics: null,
  lifecycleProjection: null,
  lifecycleHistory: null,
  lifecycleInfluence: null,
  health: null,
  continuityArtifacts: [],
  transfers: [],
  transferHistory: [],
  transferDiagnostics: null,
  recovery: null,
  recoveryHistory: null,
  recoveryDiagnostics: null,
  workflow: null,
  workflowSummary: null,
  workflowHealth: null,
  workflowInfluence: null,
  certification: null,
  certificationReport: null,
}

export function useDecisionSessions(repositoryId: string | null) {
  const [snapshot, setSnapshot] = useState<DecisionSessionGovernanceSnapshot>(emptySnapshot)
  const [isLoading, setIsLoading] = useState(false)
  const [isTransferring, setIsTransferring] = useState(false)
  const [isRecovering, setIsRecovering] = useState(false)
  const [isCertifying, setIsCertifying] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    if (!repositoryId) {
      setSnapshot(emptySnapshot)
      setIsLoading(false)
      return emptySnapshot
    }

    setIsLoading(true)
    setError(null)
    try {
      const [
        sessions,
        activeSession,
        diagnostics,
        metrics,
        statistics,
        economics,
        coherence,
        analysisDiagnostics,
        lifecyclePolicy,
        lifecyclePolicyDiagnostics,
        transferEligibility,
        transferEligibilityDiagnostics,
        lifecycleProjection,
        lifecycleHistory,
        lifecycleInfluence,
        health,
        continuityArtifacts,
        transfers,
        transferHistory,
        transferDiagnostics,
        recovery,
        recoveryHistory,
        recoveryDiagnostics,
        workflow,
        workflowSummary,
        workflowHealth,
        workflowInfluence,
        certification,
        certificationReport,
      ] = await Promise.all([
        listDecisionSessions(repositoryId),
        getActiveDecisionSession(repositoryId),
        getDecisionSessionDiagnostics(repositoryId),
        getDecisionSessionMetrics(repositoryId),
        getDecisionSessionStatistics(repositoryId),
        getDecisionSessionEconomics(repositoryId),
        getDecisionSessionCoherence(repositoryId),
        getDecisionSessionAnalysisDiagnostics(repositoryId),
        getDecisionSessionLifecyclePolicy(repositoryId),
        getDecisionSessionLifecyclePolicyDiagnostics(repositoryId),
        getDecisionSessionTransferEligibility(repositoryId),
        getDecisionSessionTransferEligibilityDiagnostics(repositoryId),
        getDecisionSessionLifecycleProjection(repositoryId),
        getDecisionSessionLifecycleHistory(repositoryId),
        getDecisionSessionLifecycleInfluence(repositoryId),
        getDecisionSessionLifecycleHealth(repositoryId),
        listDecisionSessionContinuityArtifacts(repositoryId),
        listDecisionSessionTransfers(repositoryId),
        listDecisionSessionTransferHistory(repositoryId),
        getDecisionSessionTransferDiagnostics(repositoryId),
        getDecisionSessionRecovery(repositoryId),
        listDecisionSessionRecoveryHistory(repositoryId),
        getDecisionSessionRecoveryDiagnostics(repositoryId),
        getDecisionSessionWorkflow(repositoryId),
        getDecisionSessionWorkflowSummary(repositoryId),
        getDecisionSessionWorkflowHealth(repositoryId),
        getDecisionSessionWorkflowInfluence(repositoryId),
        getDecisionSessionCertification(repositoryId),
        getDecisionSessionCertificationReport(repositoryId),
      ])
      const nextSnapshot: DecisionSessionGovernanceSnapshot = {
        sessions,
        activeSession,
        diagnostics,
        metrics,
        statistics,
        economics,
        coherence,
        analysisDiagnostics,
        lifecyclePolicy,
        lifecyclePolicyDiagnostics,
        transferEligibility,
        transferEligibilityDiagnostics,
        lifecycleProjection,
        lifecycleHistory,
        lifecycleInfluence,
        health,
        continuityArtifacts,
        transfers,
        transferHistory,
        transferDiagnostics,
        recovery,
        recoveryHistory,
        recoveryDiagnostics,
        workflow,
        workflowSummary,
        workflowHealth,
        workflowInfluence,
        certification,
        certificationReport,
      }
      setSnapshot(nextSnapshot)
      return nextSnapshot
    } catch (loadError) {
      const message = formatError(loadError)
      setSnapshot(emptySnapshot)
      setError(message)
      return emptySnapshot
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  const executeTransfer = useCallback(async () => {
    if (!repositoryId) {
      return null
    }

    setIsTransferring(true)
    setError(null)
    try {
      const result = await executeDecisionSessionTransfer(repositoryId)
      await load()
      return result
    } catch (transferError) {
      const message = formatError(transferError)
      setError(message)
      return null
    } finally {
      setIsTransferring(false)
    }
  }, [load, repositoryId])

  const recover = useCallback(async () => {
    if (!repositoryId) {
      return null
    }

    setIsRecovering(true)
    setError(null)
    try {
      const result = await recoverDecisionSession(repositoryId)
      await load()
      return result
    } catch (recoveryError) {
      const message = formatError(recoveryError)
      setError(message)
      return null
    } finally {
      setIsRecovering(false)
    }
  }, [load, repositoryId])

  const runCertification = useCallback(async () => {
    if (!repositoryId) {
      return null
    }

    setIsCertifying(true)
    setError(null)
    try {
      const result = await runDecisionSessionCertification(repositoryId)
      await load()
      return result
    } catch (certificationError) {
      const message = formatError(certificationError)
      setError(message)
      return null
    } finally {
      setIsCertifying(false)
    }
  }, [load, repositoryId])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void load().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [load])

  return {
    snapshot,
    setSnapshot,
    isLoading,
    isTransferring,
    isRecovering,
    isCertifying,
    error,
    load,
    refresh: load,
    executeTransfer,
    recover,
    runCertification,
  }
}
