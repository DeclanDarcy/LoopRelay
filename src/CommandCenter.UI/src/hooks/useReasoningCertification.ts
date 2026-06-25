import { useCallback, useEffect, useState } from 'react'
import {
  formatError,
  getBoundaryViolation,
  getReasoningCertification,
  listReasoningCertificationReports,
  runReasoningCertification,
} from '../api'
import type { BoundaryViolationProjection, ReasoningCertificationReport } from '../types'

export function useReasoningCertification(repositoryId: string | null) {
  const [currentReport, setCurrentReport] = useState<ReasoningCertificationReport | null>(null)
  const [reports, setReports] = useState<ReasoningCertificationReport[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [isRunning, setIsRunning] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [boundaryViolation, setBoundaryViolation] = useState<BoundaryViolationProjection | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setCurrentReport(null)
      setReports([])
      setIsLoading(false)
      setError(null)
      return null
    }

    setIsLoading(true)
    setError(null)
    setBoundaryViolation(null)
    try {
      const [current, history] = await Promise.all([
        getReasoningCertification(repositoryId),
        listReasoningCertificationReports(repositoryId),
      ])
      setCurrentReport(current)
      setReports(history)
      return current
    } catch (certificationError) {
      const message = formatError(certificationError)
      setCurrentReport(null)
      setReports([])
      setError(message)
      setBoundaryViolation(getBoundaryViolation(certificationError))
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
    setBoundaryViolation(null)
    try {
      const report = await runReasoningCertification(repositoryId)
      const history = await listReasoningCertificationReports(repositoryId)
      setCurrentReport(report)
      setReports(history)
      return report
    } catch (certificationError) {
      const message = formatError(certificationError)
      setError(message)
      setBoundaryViolation(getBoundaryViolation(certificationError))
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
        setIsRunning(false)
        setError(null)
        setBoundaryViolation(null)
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
    boundaryViolation,
    refresh,
    runCertification,
  }
}
