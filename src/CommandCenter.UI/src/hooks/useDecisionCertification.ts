import { useCallback, useEffect, useState } from 'react'
import {
  formatError,
  getDecisionCertification,
  listDecisionCertificationReports,
  runDecisionCertification,
} from '../api'
import type { DecisionCertificationReport } from '../types'

export function useDecisionCertification(repositoryId: string | null) {
  const [currentReport, setCurrentReport] = useState<DecisionCertificationReport | null>(null)
  const [reports, setReports] = useState<DecisionCertificationReport[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [isRunning, setIsRunning] = useState(false)
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
        getDecisionCertification(repositoryId),
        listDecisionCertificationReports(repositoryId),
      ])
      setCurrentReport(current)
      setReports(history)
      return current
    } catch (certificationError) {
      const message = formatError(certificationError)
      setCurrentReport(null)
      setReports([])
      setError(message)
      return null
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  const runCertification = useCallback(async () => {
    if (!repositoryId) {
      return null
    }

    setIsRunning(true)
    setError(null)
    try {
      const report = await runDecisionCertification(repositoryId)
      setCurrentReport(report)
      const history = await listDecisionCertificationReports(repositoryId)
      setReports(history)
      return report
    } catch (certificationError) {
      const message = formatError(certificationError)
      setError(message)
      return null
    } finally {
      setIsRunning(false)
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
    isRunning,
    error,
    refresh,
    runCertification,
    load: refresh,
  }
}
