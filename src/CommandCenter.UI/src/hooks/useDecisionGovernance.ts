import { useCallback, useEffect, useState } from 'react'
import {
  formatError,
  generateDecisionGovernanceReport,
  getDecisionGovernance,
  listDecisionGovernanceReports,
} from '../api'
import type { DecisionGovernanceReport } from '../types'

export function useDecisionGovernance(repositoryId: string | null) {
  const [currentReport, setCurrentReport] = useState<DecisionGovernanceReport | null>(null)
  const [reports, setReports] = useState<DecisionGovernanceReport[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [isGenerating, setIsGenerating] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setCurrentReport(null)
      setReports([])
      setIsLoading(false)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const [current, history] = await Promise.all([
        getDecisionGovernance(repositoryId),
        listDecisionGovernanceReports(repositoryId),
      ])
      setCurrentReport(current)
      setReports(history)
      return current
    } catch (governanceError) {
      const message = formatError(governanceError)
      setCurrentReport(null)
      setReports([])
      setError(message)
      return null
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  const generateReport = useCallback(async () => {
    if (!repositoryId) {
      return null
    }

    setIsGenerating(true)
    setError(null)
    try {
      const report = await generateDecisionGovernanceReport(repositoryId)
      setCurrentReport(report)
      const history = await listDecisionGovernanceReports(repositoryId)
      setReports(history)
      return report
    } catch (governanceError) {
      const message = formatError(governanceError)
      setError(message)
      return null
    } finally {
      setIsGenerating(false)
    }
  }, [repositoryId])

  useEffect(() => {
    if (!repositoryId) {
      const timeoutId = window.setTimeout(() => {
        setCurrentReport(null)
        setReports([])
        setIsLoading(false)
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [refresh, repositoryId])

  return {
    currentReport,
    reports,
    isLoading,
    isGenerating,
    error,
    refresh,
    generateReport,
    load: refresh,
  }
}
