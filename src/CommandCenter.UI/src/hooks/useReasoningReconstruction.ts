import { useCallback, useEffect, useState } from 'react'
import { formatError, reconstructReasoning } from '../api'
import type { ReasoningQuery, ReasoningReconstruction } from '../types'

export function useReasoningReconstruction(repositoryId: string | null) {
  const [data, setData] = useState<ReasoningReconstruction | null>(null)
  const [isRunning, setIsRunning] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const run = useCallback(
    async (query: ReasoningQuery) => {
      if (!repositoryId) {
        setData(null)
        return null
      }

      setIsRunning(true)
      setError(null)
      try {
        const reconstruction = await reconstructReasoning(repositoryId, query)
        setData(reconstruction)
        return reconstruction
      } catch (reconstructionError) {
        const message = formatError(reconstructionError)
        setData(null)
        setError(message)
        return null
      } finally {
        setIsRunning(false)
      }
    },
    [repositoryId],
  )

  useEffect(() => {
    if (!repositoryId) {
      const timeoutId = window.setTimeout(() => {
        setData(null)
        setIsRunning(false)
        setError(null)
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    return undefined
  }, [repositoryId])

  return {
    data,
    isRunning,
    error,
    run,
  }
}
