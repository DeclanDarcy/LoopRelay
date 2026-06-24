import { useCallback, useEffect, useState } from 'react'
import { formatError, getWorkflowRecovery, recoverWorkflow } from '../api'
import type { WorkflowRecoveryDiagnostics, WorkflowRecoveryResult } from '../types'

export function useWorkflowRecovery(repositoryId: string | null) {
  const [diagnostics, setDiagnostics] = useState<WorkflowRecoveryDiagnostics | null>(null)
  const [lastRecovery, setLastRecovery] = useState<WorkflowRecoveryResult | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [isRecovering, setIsRecovering] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setDiagnostics(null)
      setIsLoading(false)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const nextDiagnostics = await getWorkflowRecovery(repositoryId)
      setDiagnostics(nextDiagnostics)
      return nextDiagnostics
    } catch (recoveryError) {
      const message = formatError(recoveryError)
      setDiagnostics(null)
      setError(message)
      return null
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  const recover = useCallback(async () => {
    if (!repositoryId) {
      return null
    }

    setIsRecovering(true)
    setError(null)
    try {
      const result = await recoverWorkflow(repositoryId)
      setLastRecovery(result)
      setDiagnostics(result.diagnostics)
      return result
    } catch (recoveryError) {
      const message = formatError(recoveryError)
      setError(message)
      return null
    } finally {
      setIsRecovering(false)
    }
  }, [repositoryId])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [refresh])

  return { diagnostics, lastRecovery, isLoading, isRecovering, error, refresh, load: refresh, recover }
}
