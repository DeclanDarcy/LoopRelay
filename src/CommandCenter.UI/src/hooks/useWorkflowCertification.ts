import { useCallback, useEffect, useState } from 'react'
import { formatError, getWorkflowCertification, runWorkflowCertification } from '../api'
import type { WorkflowCertificationResult } from '../types'

export function useWorkflowCertification(repositoryId: string | null) {
  const [certification, setCertification] = useState<WorkflowCertificationResult | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [isRunning, setIsRunning] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setCertification(null)
      setIsLoading(false)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const result = await getWorkflowCertification(repositoryId)
      setCertification(result)
      return result
    } catch (certificationError) {
      const message = formatError(certificationError)
      setCertification(null)
      setError(message)
      return null
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  const run = useCallback(async () => {
    if (!repositoryId) {
      return null
    }

    setIsRunning(true)
    setError(null)
    try {
      const result = await runWorkflowCertification(repositoryId)
      setCertification(result)
      return result
    } catch (certificationError) {
      const message = formatError(certificationError)
      setError(message)
      return null
    } finally {
      setIsRunning(false)
    }
  }, [repositoryId])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [refresh])

  return { certification, isLoading, isRunning, error, refresh, load: refresh, run }
}
