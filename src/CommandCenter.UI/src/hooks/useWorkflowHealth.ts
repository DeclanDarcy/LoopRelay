import { useCallback, useEffect, useState } from 'react'
import {
  formatError,
  getRepositoryWorkflowReport,
  getWorkflowHealth,
  getWorkflowHumanGovernanceReport,
  getWorkflowProgressionReport,
  getWorkflowReadinessReport,
} from '../api'
import type {
  HumanGovernanceReport,
  RepositoryWorkflowReport,
  WorkflowHealthAssessment,
  WorkflowProgressionReport,
  WorkflowReadinessReport,
} from '../types'

export function useWorkflowHealth(repositoryId: string | null) {
  const [health, setHealth] = useState<WorkflowHealthAssessment | null>(null)
  const [repositoryReport, setRepositoryReport] = useState<RepositoryWorkflowReport | null>(null)
  const [progressionReport, setProgressionReport] = useState<WorkflowProgressionReport | null>(null)
  const [humanGovernanceReport, setHumanGovernanceReport] = useState<HumanGovernanceReport | null>(null)
  const [readinessReport, setReadinessReport] = useState<WorkflowReadinessReport | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setHealth(null)
      setRepositoryReport(null)
      setProgressionReport(null)
      setHumanGovernanceReport(null)
      setReadinessReport(null)
      setIsLoading(false)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const [nextHealth, nextRepository, nextProgression, nextGovernance, nextReadiness] =
        await Promise.all([
          getWorkflowHealth(repositoryId),
          getRepositoryWorkflowReport(repositoryId),
          getWorkflowProgressionReport(repositoryId),
          getWorkflowHumanGovernanceReport(repositoryId),
          getWorkflowReadinessReport(repositoryId),
        ])
      setHealth(nextHealth)
      setRepositoryReport(nextRepository)
      setProgressionReport(nextProgression)
      setHumanGovernanceReport(nextGovernance)
      setReadinessReport(nextReadiness)
      return nextHealth
    } catch (healthError) {
      const message = formatError(healthError)
      setHealth(null)
      setRepositoryReport(null)
      setProgressionReport(null)
      setHumanGovernanceReport(null)
      setReadinessReport(null)
      setError(message)
      return null
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [refresh])

  return {
    health,
    repositoryReport,
    progressionReport,
    humanGovernanceReport,
    readinessReport,
    isLoading,
    error,
    refresh,
    load: refresh,
  }
}
