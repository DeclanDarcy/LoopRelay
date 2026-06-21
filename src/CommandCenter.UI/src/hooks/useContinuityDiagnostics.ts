import { useCallback, useEffect, useState } from 'react'
import { formatError, getContinuityDiagnostics } from '../api'
import type { ContinuityDiagnostics } from '../types'

export function useContinuityDiagnostics(repositoryId: string | null) {
  const [data, setData] = useState<ContinuityDiagnostics | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setData(null)
      setIsLoading(false)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const diagnostics = await getContinuityDiagnostics(repositoryId)
      setData(diagnostics)
      return diagnostics
    } catch (diagnosticsError) {
      const message = formatError(diagnosticsError)
      setData(null)
      setError(message)
      return null
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  useEffect(() => {
    if (!repositoryId) {
      const timeoutId = window.setTimeout(() => {
        setData(null)
        setIsLoading(false)
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [refresh, repositoryId])

  return { data, setData, isLoading, error, refresh, load: refresh }
}
